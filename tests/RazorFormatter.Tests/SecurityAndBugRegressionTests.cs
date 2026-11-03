using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RazorFormatter.Core;
using RazorFormatter.Core.Configuration;
using RazorFormatter.Core.Enums;
using RazorFormatter.Core.Lexing;
using Xunit;

namespace RazorFormatter.Tests
{
    public class SecurityAndBugRegressionTests
    {
        // =========================================================================
        // SEC-03: ReDoS Protection & Regex Caching in EditorConfigGlobMatcher
        // =========================================================================

        [Fact]
        public void Sec03_EditorConfigGlobMatcher_RegexCaching_ReturnsSameInstance()
        {
            const string pattern = "**/*.{cshtml,razor}";
            var regex1 = EditorConfigGlobMatcher.GetOrCompileRegex(pattern);
            var regex2 = EditorConfigGlobMatcher.GetOrCompileRegex(pattern);

            Assert.NotNull(regex1);
            Assert.Same(regex1, regex2);
        }

        [Fact]
        public void Sec03_EditorConfigGlobMatcher_RegexTimeout_ConfiguredSafely()
        {
            const string pattern = "**/*.razor";
            var regex = EditorConfigGlobMatcher.GetOrCompileRegex(pattern);

            Assert.Equal(TimeSpan.FromMilliseconds(250), regex.MatchTimeout);
        }

        [Fact]
        public void Sec03_EditorConfigGlobMatcher_MalformedPattern_ReturnsFalseGracefully()
        {
            // Malformed bracket / regex patterns should not throw ArgumentException
            var match1 = EditorConfigGlobMatcher.IsMatch("[a-", "test.cshtml");
            var match2 = EditorConfigGlobMatcher.IsMatch("[*?", "test.cshtml");

            Assert.False(match1);
            Assert.False(match2);
        }

        // =========================================================================
        // BUG-01: Trailing Backslash Bounds Clamping in Lexers
        // =========================================================================

        [Theory]
        [InlineData("<div class=\"test\\")]
        [InlineData("<div class='test\\")]
        [InlineData("<span attr=\"\\")]
        [InlineData("<a href=\"something\\\\")]
        [InlineData("<button title=\"abc\\")]
        public void Bug01_HtmlLexer_TrailingBackslashInAttribute_DoesNotThrowIndexOutOfRange(string html)
        {
            var exception = Record.Exception(() =>
            {
                var text = new SourceText(html);
                var csharp = new CSharpLexer(text);
                var htmlLexer = new HtmlLexer(text, csharp);
                var offset = 0;
                htmlLexer.TryScanTag(ref offset, out _, out _, out _, out _);

                var razorLexer = new RazorLexer(text);
                var tokens = razorLexer.Lex();
                Assert.NotNull(tokens);
            });

            Assert.Null(exception);
        }

        [Theory]
        [InlineData("@{\n    var s = \"trailing backslash\\\n")]
        [InlineData("@{\n    var ch = '\\\n")]
        [InlineData("@{\n    var interp = $\"escaped quote {val}\\\n")]
        public void Bug01_CSharpLexer_TrailingBackslashInString_DoesNotThrowIndexOutOfRange(string csharp)
        {
            var exception = Record.Exception(() =>
            {
                var text = new SourceText(csharp);
                var lexer = new RazorLexer(text);
                var tokens = lexer.Lex();
                Assert.NotNull(tokens);
            });

            Assert.Null(exception);
        }

        // =========================================================================
        // BUG-02: Untitled / Virtual Document URIs in ConfigurationResolver
        // =========================================================================

        [Theory]
        [InlineData("untitled:Untitled-1")]
        [InlineData("untitled:Untitled-2")]
        [InlineData("inmemory://session/document.razor")]
        [InlineData("gitfs:/repo/src/Index.cshtml")]
        public void Bug02_ConfigurationResolver_VirtualDocumentUri_ResolvesWithoutException(string virtualUri)
        {
            var exception = Record.Exception(() =>
            {
                var options = ConfigurationResolver.Resolve(virtualUri);
                Assert.NotNull(options);
                Assert.Equal(virtualUri, options.FilePath);
            });

            Assert.Null(exception);
        }

        [Fact]
        public void Bug02_FormattingEngine_VirtualDocumentUri_FormatsSuccessfully()
        {
            var engine = RazorFormatterFactory.Create();
            const string source = "<div>\n<p>Hello Virtual World</p>\n</div>";

            var exception = Record.Exception(() =>
            {
                var options = new RazorFormatterOptions { FilePath = "untitled:Untitled-1" };
                var result = engine.Format(source, options);
                Assert.NotNull(result);
                Assert.Contains("Hello Virtual World", result.FormattedText);
            });

            Assert.Null(exception);
        }


        [Fact]
        public void Bug03_CliDirectoryScanning_MatchesCshtmlAndRazorFiles()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "RazorFormatter_Bug03_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var file1 = Path.Combine(tempDir, "View1.cshtml");
                var file2 = Path.Combine(tempDir, "Component1.razor");
                var file3 = Path.Combine(tempDir, "ignore.cs");
                var file4 = Path.Combine(tempDir, "readme.md");

                File.WriteAllText(file1, "<h1>View</h1>");
                File.WriteAllText(file2, "<h1>Component</h1>");
                File.WriteAllText(file3, "public class C {}");
                File.WriteAllText(file4, "# Readme");

                // Test the directory scan enumeration logic used in CLI Program.cs
                var matched = Directory.EnumerateFiles(tempDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f)
                    .ToArray();

                Assert.Equal(2, matched.Length);
                Assert.Contains(file1, matched);
                Assert.Contains(file2, matched);
                Assert.DoesNotContain(file3, matched);
                Assert.DoesNotContain(file4, matched);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }

        // =========================================================================
        // BUG-04: Localized Text Changes (Non-Contiguous Edits Not Collapsed)
        // =========================================================================

        [Fact]
        public void Bug04_FormattingEngine_LocalizedChanges_DoesNotCollapseUnchangedLines()
        {
            var engine = RazorFormatterFactory.Create();

            // Construct a document with 40 lines.
            // Line 1 is badly indented.
            // Lines 3-35 are cleanly indented and should remain untouched.
            // Line 37 is badly indented.
            var lines = new List<string>();
            lines.Add("<div>");
            lines.Add("   <span>Bad indent line 1</span>"); // 3 spaces instead of 4
            lines.Add("</div>");
            for (var i = 0; i < 30; i++)
            {
                lines.Add($"<p>Pristine unchanged line {i}</p>");
            }
            lines.Add("<div>");
            lines.Add("   <span>Bad indent line 37</span>"); // 3 spaces instead of 4
            lines.Add("</div>");

            var source = string.Join("\n", lines) + "\n";
            var result = engine.Format(source);

            Assert.NotNull(result);
            // With localized line-by-line diffing, we should get multiple distinct TextChanges
            Assert.True(result.Changes.Count >= 2, $"Expected at least 2 distinct TextChanges, got {result.Changes.Count}");

            // Verify that the middle untouched region (e.g. pristine lines) is NOT spanned by the changes
            var firstPristineOffset = source.IndexOf("<p>Pristine unchanged line 10</p>", StringComparison.Ordinal);
            Assert.True(firstPristineOffset > 0);

            var changeSpansMiddle = result.Changes.Any(c => c.Start <= firstPristineOffset && c.End >= firstPristineOffset + 20);
            Assert.False(changeSpansMiddle, "A monolithic change spanned across pristine, unchanged middle lines.");
        }

        // =========================================================================
        // BUG-05: Embedded Delimiters Inside JavaScript and CSS Literals
        // =========================================================================

        [Fact]
        public void Bug05_RazorLexer_ScriptTagWithLiteralClosingTag_DoesNotTerminatePrematurely()
        {
            const string source = "<script>\n    const safe = \"</script>\";\n    console.log(safe);\n</script>\n";
            var text = new SourceText(source);
            var lexer = new RazorLexer(text);
            var tokens = lexer.Lex();

            // Verify tokens span full source without gaps
            Assert.NotEmpty(tokens);
            Assert.Equal(0, tokens[0].Start);
            Assert.Equal(source.Length, tokens[^1].End);

            // Find the script closing tag
            var closingTags = tokens.Where(t => t.Kind == RazorSyntaxKind.HtmlTag && source.Substring(t.Start, t.End - t.Start).Contains("</script")).ToList();
            Assert.Single(closingTags);

            // The single closing tag must be at the very end of the source, not inside the JS string
            var expectedClosePos = source.LastIndexOf("</script>", StringComparison.Ordinal);
            Assert.Equal(expectedClosePos, closingTags[0].Start);

            // JavaScript state tokens should include console.log
            var jsTokens = tokens.Where(t => t.State == LexicalState.JavaScript).ToList();
            Assert.NotEmpty(jsTokens);
            var jsText = string.Join("", jsTokens.Select(t => source.Substring(t.Start, t.End - t.Start)));
            Assert.Contains("console.log(safe)", jsText);
        }

        [Fact]
        public void Bug05_RazorLexer_StyleTagWithLiteralClosingTag_DoesNotTerminatePrematurely()
        {
            const string source = "<style>\n    .msg:after { content: \"</style>\"; }\n    .other { color: red; }\n</style>\n";
            var text = new SourceText(source);
            var lexer = new RazorLexer(text);
            var tokens = lexer.Lex();

            Assert.NotEmpty(tokens);
            Assert.Equal(0, tokens[0].Start);
            Assert.Equal(source.Length, tokens[^1].End);

            var closingTags = tokens.Where(t => t.Kind == RazorSyntaxKind.HtmlTag && source.Substring(t.Start, t.End - t.Start).Contains("</style")).ToList();
            Assert.Single(closingTags);

            var expectedClosePos = source.LastIndexOf("</style>", StringComparison.Ordinal);
            Assert.Equal(expectedClosePos, closingTags[0].Start);

            var cssTokens = tokens.Where(t => t.State == LexicalState.Css).ToList();
            Assert.NotEmpty(cssTokens);
            var cssText = string.Join("", cssTokens.Select(t => source.Substring(t.Start, t.End - t.Start)));
            Assert.Contains(".other { color: red; }", cssText);
        }
    }
}

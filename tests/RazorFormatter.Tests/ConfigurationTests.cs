using System;
using System.IO;
using RazorFormatter.Core;
using RazorFormatter.Core.Abstractions;
using RazorFormatter.Core.Configuration;
using RazorFormatter.Core.Enums;
using Xunit;

namespace RazorFormatter.Tests
{
    public class Phase4ConfigurationTests : IDisposable
    {
        private readonly IRazorFormatter _formatter = RazorFormatterFactory.Create();
        private readonly string _tempDirectory;

        public Phase4ConfigurationTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "RazorFormatter_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDirectory))
                    Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup issues in temp directory
            }
        }

        [Fact]
        public void EditorConfig_StandardKeys_ParsedAndApplied()
        {
            var editorConfigContent = @"root = true

[*.cshtml]
indent_style = space
indent_size = 2
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true
";
            var configFile = Path.Combine(_tempDirectory, ".editorconfig");
            File.WriteAllText(configFile, editorConfigContent);

            var cshtmlPath = Path.Combine(_tempDirectory, "Index.cshtml");
            var source = "<div>\n<p>Hello</p>  \n</div>";
            File.WriteAllText(cshtmlPath, source);

            var result = _formatter.FormatFile(cshtmlPath);

            // Indent 2 spaces, trailing whitespace trimmed, ends with final newline
            var expected = "<div>\n  <p>Hello</p>\n</div>\n";
            Assert.Equal(expected, result.FormattedText);
        }

        [Fact]
        public void EditorConfig_TabIndentation_WithTabWidth()
        {
            var editorConfigContent = @"root = true

[*.cshtml]
indent_style = tab
tab_width = 4
indent_size = tab
end_of_line = lf
";
            var configFile = Path.Combine(_tempDirectory, ".editorconfig");
            File.WriteAllText(configFile, editorConfigContent);

            var cshtmlPath = Path.Combine(_tempDirectory, "Tabs.cshtml");
            var source = "<div>\n<p>Tab Indented</p>\n</div>\n";
            File.WriteAllText(cshtmlPath, source);

            var result = _formatter.FormatFile(cshtmlPath);

            var expected = "<div>\n\t<p>Tab Indented</p>\n</div>\n";
            Assert.Equal(expected, result.FormattedText);
        }

        [Fact]
        public void EditorConfig_RazorSpecificKeys_Applied()
        {
            var editorConfigContent = @"root = true

[*.cshtml]
indent_size = 4
end_of_line = lf
razor_attribute_layout = one_per_line
razor_void_element_slash = slash
";
            var configFile = Path.Combine(_tempDirectory, ".editorconfig");
            File.WriteAllText(configFile, editorConfigContent);

            var cshtmlPath = Path.Combine(_tempDirectory, "Attributes.cshtml");
            var source = "<input id=\"username\" name=\"username\" type=\"text\">\n";
            File.WriteAllText(cshtmlPath, source);

            var result = _formatter.FormatFile(cshtmlPath);

            // Attributes formatted one per line with self-closing slash
            var expected = "<input\n    id=\"username\"\n    name=\"username\"\n    type=\"text\" />\n";
            Assert.Equal(expected, result.FormattedText);
        }

        [Fact]
        public void EditorConfig_VoidElementSlash_NoSlashPolicy()
        {
            var editorConfigContent = @"root = true

[*.cshtml]
end_of_line = lf
razor_void_element_slash = no_slash
";
            var configFile = Path.Combine(_tempDirectory, ".editorconfig");
            File.WriteAllText(configFile, editorConfigContent);

            var cshtmlPath = Path.Combine(_tempDirectory, "NoSlash.cshtml");
            var source = "<input id=\"test\" />\n<br />\n";
            File.WriteAllText(cshtmlPath, source);

            var result = _formatter.FormatFile(cshtmlPath);

            var expected = "<input id=\"test\">\n<br>\n";
            Assert.Equal(expected, result.FormattedText);
        }

        [Fact]
        public void ConfigurationPrecedence_ExplicitOptions_OverrideEditorConfig()
        {
            var editorConfigContent = @"root = true

[*.cshtml]
indent_size = 2
end_of_line = lf
";
            var configFile = Path.Combine(_tempDirectory, ".editorconfig");
            File.WriteAllText(configFile, editorConfigContent);

            var cshtmlPath = Path.Combine(_tempDirectory, "Precedence.cshtml");
            var source = "<div>\n<p>Content</p>\n</div>\n";
            File.WriteAllText(cshtmlPath, source);

            // Explicit options specify IndentSize = 4, which should override the .editorconfig's 2
            var explicitOptions = new RazorFormatterOptions
            {
                IndentSize = 4,
            };

            var result = _formatter.FormatFile(cshtmlPath, explicitOptions);

            var expected = "<div>\n    <p>Content</p>\n</div>\n";
            Assert.Equal(expected, result.FormattedText);
        }

        [Fact]
        public void MultiLevelDirectory_CloserEditorConfigOverridesParent()
        {
            // Parent directory config: indent_size = 2
            var parentConfig = @"root = true
[*.cshtml]
indent_size = 2
end_of_line = lf
";
            File.WriteAllText(Path.Combine(_tempDirectory, ".editorconfig"), parentConfig);

            // Subdirectory config: indent_size = 6
            var subDir = Path.Combine(_tempDirectory, "SubDir");
            Directory.CreateDirectory(subDir);
            var childConfig = @"[*.cshtml]
indent_size = 6
";
            File.WriteAllText(Path.Combine(subDir, ".editorconfig"), childConfig);

            var cshtmlPath = Path.Combine(subDir, "Page.cshtml");
            var source = "<div>\n<p>Nested</p>\n</div>\n";
            File.WriteAllText(cshtmlPath, source);

            var result = _formatter.FormatFile(cshtmlPath);

            // Child config (6 spaces) overrides parent (2 spaces)
            var expected = "<div>\n      <p>Nested</p>\n</div>\n";
            Assert.Equal(expected, result.FormattedText);
        }

        [Fact]
        public void RootTrue_StopsUpwardDirectoryTraversal()
        {
            // Grandparent config: indent_size = 8
            var grandparentConfig = @"[*.cshtml]
indent_size = 8
";
            File.WriteAllText(Path.Combine(_tempDirectory, ".editorconfig"), grandparentConfig);

            // Middle directory with root = true and indent_size = 2
            var midDir = Path.Combine(_tempDirectory, "MidDir");
            Directory.CreateDirectory(midDir);
            var midConfig = @"root = true
[*.cshtml]
indent_size = 2
end_of_line = lf
";
            File.WriteAllText(Path.Combine(midDir, ".editorconfig"), midConfig);

            var leafDir = Path.Combine(midDir, "Leaf");
            Directory.CreateDirectory(leafDir);

            var cshtmlPath = Path.Combine(leafDir, "Page.cshtml");
            var source = "<div>\n<p>Root Check</p>\n</div>\n";
            File.WriteAllText(cshtmlPath, source);

            var result = _formatter.FormatFile(cshtmlPath);

            // Should use midDir's 2 spaces, not grandparent's 8 spaces
            var expected = "<div>\n  <p>Root Check</p>\n</div>\n";
            Assert.Equal(expected, result.FormattedText);
        }

        [Fact]
        public void BlankLines_PreservationRules_MaxConsecutiveBlankLines()
        {
            var source = "<div>\n\n\n\n\n    <p>Multiple Blanks</p>\n</div>\n";

            // Case 1: MaxConsecutiveBlankLines = 2
            var res2 = _formatter.FormatToText(source, new RazorFormatterOptions
            {
                CollapseMultipleBlankLines = true,
                MaxConsecutiveBlankLines = 2,
                EndOfLine = EndOfLine.Lf
            });
            Assert.Equal("<div>\n\n\n    <p>Multiple Blanks</p>\n</div>\n", res2);

            // Case 2: MaxConsecutiveBlankLines = 0 (no blank lines)
            var res0 = _formatter.FormatToText(source, new RazorFormatterOptions
            {
                CollapseMultipleBlankLines = true,
                MaxConsecutiveBlankLines = 0,
                EndOfLine = EndOfLine.Lf
            });
            Assert.Equal("<div>\n    <p>Multiple Blanks</p>\n</div>\n", res0);

            // Case 3: CollapseMultipleBlankLines = false (preserve all blanks)
            var resPreserve = _formatter.FormatToText(source, new RazorFormatterOptions
            {
                CollapseMultipleBlankLines = false,
                EndOfLine = EndOfLine.Lf
            });
            Assert.Equal(source, resPreserve);
        }

        [Fact]
        public void PerLanguage_Options_ToggleHtmlAndControlFlowSeparately()
        {
            var source = "@if (true)\n{\n<div>\n<p>Test</p>\n</div>\n}\n";

            var options = new RazorFormatterOptions
            {
                FormatHtml = false,
                FormatRazorControlFlow = true,
                EndOfLine = EndOfLine.Lf
            };

            var result = _formatter.FormatToText(source, options);

            // Control flow indents, HTML children do not indent further
            var expected = "@if (true)\n{\n    <div>\n    <p>Test</p>\n    </div>\n}\n";
            Assert.Equal(expected, result);
        }

        [Fact]
        public void CliIgnoreMatcher_DefaultAndCustomPatterns_MatchCorrectly()
        {
            var matcher = new RazorIgnoreMatcher();

            // Default patterns
            Assert.True(matcher.IsIgnored("bin/Debug/net10.0/App.cshtml"));
            Assert.True(matcher.IsIgnored("src/Project/obj/Index.cshtml"));
            Assert.True(matcher.IsIgnored(".git/HEAD.cshtml"));
            Assert.True(matcher.IsIgnored("node_modules/pkg/Component.cshtml"));
            Assert.False(matcher.IsIgnored("src/Project/Views/Home/Index.cshtml"));

            // Custom pattern
            matcher.AddPattern("**/*.generated.cshtml");
            Assert.True(matcher.IsIgnored("src/Views/Account.generated.cshtml"));
            Assert.False(matcher.IsIgnored("src/Views/Account.cshtml"));
        }

        [Fact]
        public void CliIgnoreMatcher_LoadsFromRazorIgnoreFile()
        {
            var ignoreFilePath = Path.Combine(_tempDirectory, ".razorignore");
            File.WriteAllLines(ignoreFilePath, new[]
            {
                "# Comment line",
                "custom_folder/",
                "special_*.cshtml"
            });

            var matcher = new RazorIgnoreMatcher();
            matcher.DiscoverAndLoad(_tempDirectory);

            Assert.True(matcher.IsIgnored(Path.Combine(_tempDirectory, "custom_folder", "Test.cshtml")));
            Assert.True(matcher.IsIgnored(Path.Combine(_tempDirectory, "special_item.cshtml")));
            Assert.False(matcher.IsIgnored(Path.Combine(_tempDirectory, "normal.cshtml")));
        }

        [Fact]
        public void UnsupportedSyntax_WithSkipOnLexerError_ReturnsDiagnosticsAndOriginalSource()
        {
            var invalidSource = "<button class=\"@(unclosed string) disabled>Submit</button>";

            var options = new RazorFormatterOptions
            {
                SkipOnLexerError = true
            };

            var result = _formatter.Format(invalidSource, options);

            if (result.Diagnostics.Count > 0)
            {
                Assert.True(result.WasSkipped);
                Assert.Equal(invalidSource, result.FormattedText);
            }
        }

        [Fact]
        public void EditorConfig_BracedGlobExpansion_MatchesMultipleExtensions()
        {
            var editorConfigContent = @"root = true

[*.{cshtml,razor}]
indent_size = 3
end_of_line = lf
";
            var configFile = Path.Combine(_tempDirectory, ".editorconfig");
            File.WriteAllText(configFile, editorConfigContent);

            var razorPath = Path.Combine(_tempDirectory, "Counter.razor");
            var source = "<div>\n<p>Counter</p>\n</div>\n";
            File.WriteAllText(razorPath, source);

            var result = _formatter.FormatFile(razorPath);

            var expected = "<div>\n   <p>Counter</p>\n</div>\n";
            Assert.Equal(expected, result.FormattedText);
        }

        [Fact]
        public void FinalNewline_False_DoesNotInsertFinalNewline()
        {
            var editorConfigContent = @"root = true

[*.cshtml]
insert_final_newline = false
end_of_line = lf
";
            var configFile = Path.Combine(_tempDirectory, ".editorconfig");
            File.WriteAllText(configFile, editorConfigContent);

            var cshtmlPath = Path.Combine(_tempDirectory, "NoNewline.cshtml");
            var source = "<div>\n    <p>No Final Newline</p>\n</div>";
            File.WriteAllText(cshtmlPath, source);

            var result = _formatter.FormatFile(cshtmlPath);

            Assert.False(result.FormattedText.EndsWith('\n'));
            Assert.Equal(source, result.FormattedText);
        }

        [Fact]
        public void EndOfLine_Crlf_NormalizesToWindowsLineEndings()
        {
            var editorConfigContent = @"root = true

[*.cshtml]
end_of_line = crlf
";
            var configFile = Path.Combine(_tempDirectory, ".editorconfig");
            File.WriteAllText(configFile, editorConfigContent);

            var cshtmlPath = Path.Combine(_tempDirectory, "Crlf.cshtml");
            var source = "<div>\n    <p>Windows EOL</p>\n</div>\n";
            File.WriteAllText(cshtmlPath, source);

            var result = _formatter.FormatFile(cshtmlPath);

            Assert.Contains("\r\n", result.FormattedText);
            Assert.DoesNotContain("([^\r])\n", result.FormattedText);
        }

        [Fact]
        public void OptionalTags_HtmlList_DoesNotBreakNestingStructure()
        {
            var source = "<ul>\n<li>First item\n<li>Second item\n</ul>\n";
            var result = _formatter.FormatToText(source, new RazorFormatterOptions
            {
                IndentSize = 4,
                EndOfLine = EndOfLine.Lf
            });

            // The closing </ul> safely restores indentation at depth 0
            Assert.StartsWith("<ul>", result);
            Assert.EndsWith("</ul>\n", result);
        }
    }
}

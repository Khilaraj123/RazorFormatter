using System;
using System.IO;
using System.Linq;
using RazorFormatter.Core;
using RazorFormatter.Core.Enums;
using RazorFormatter.Core.Lexing;
using Xunit;

namespace RazorFormatter.Tests
{
    public class Phase1LexingTests
    {
        private static void AssertZeroGapsOrOverlaps(string source, IReadOnlyList<RazorToken> tokens)
        {
            if (string.IsNullOrEmpty(source))
            {
                Assert.Empty(tokens);
                return;
            }

            Assert.NotEmpty(tokens);
            Assert.Equal(0, tokens[0].Start);

            for (var i = 0; i < tokens.Count - 1; i++)
            {
                var current = tokens[i];
                var next = tokens[i + 1];

                Assert.True(current.End <= next.Start,
                    $"Overlap detected at token {i} ({current}) and {i + 1} ({next}). Current End: {current.End}, Next Start: {next.Start}");

                Assert.True(current.End == next.Start,
                    $"Gap detected between token {i} ({current}) and {i + 1} ({next}). Missing span [{current.End}..{next.Start}): '{source.Substring(current.End, next.Start - current.End)}'");
            }

            Assert.Equal(source.Length, tokens[^1].End);
        }

        // -------------------------------------------------------------
        // 1. Completion criteria: Zero gaps, Zero overlaps
        // -------------------------------------------------------------

        [Theory]
        [InlineData("<div>Hello</div>")]
        [InlineData("<p>Price: @Model.Price</p>")]
        [InlineData("<p>Result: @(Model.Price > 100 ? \"High\" : \"Low\")</p>")]
        [InlineData("@{\n    var message = \"A } character inside a string\";\n    var value = new { Name = \"Test\" };\n}\n")]
        [InlineData("<script>\n    const template = `Hello ${user.name}`;\n</script>\n")]
        [InlineData("<style>\n    body { color: red; }\n</style>\n")]
        [InlineData("@if (Model.IsAvailable)\n{\n    <div>\n        @foreach (var item in Model.Items)\n        {\n            <p>@item.Name</p>\n        }\n    </div>\n}\n")]
        [InlineData("<!-- comment -->@* razor comment *@\n<div>Plain @@ escaped</div>")]
        [InlineData("@:\n@: <span class=\"text\">Single line markup</span>\n")]
        public void Lex_ZeroGapsOrOverlaps_PreservesEntireSource(string source)
        {
            var text = new SourceText(source);
            var lexer = new RazorLexer(text);
            var tokens = lexer.Lex();

            AssertZeroGapsOrOverlaps(source, tokens);
        }

        [Fact]
        public void Lex_AllSampleFiles_HaveZeroGapsOrOverlaps()
        {
            var samplesDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples"));
            if (!Directory.Exists(samplesDir)) return;

            foreach (var file in Directory.GetFiles(samplesDir, "*.cshtml"))
            {
                var content = File.ReadAllText(file);
                var text = new SourceText(content);
                var lexer = new RazorLexer(text);
                var tokens = lexer.Lex();

                AssertZeroGapsOrOverlaps(content, tokens);
            }
        }

        // -------------------------------------------------------------
        // 2. Explicit lexical states
        // -------------------------------------------------------------

        [Fact]
        public void Lex_IdentifiesAllExplicitLexicalStates()
        {
            var source = @"<!DOCTYPE html>
<!-- HTML comment -->
@* Razor comment *@
<style>
    .test { margin: 0; }
</style>
<script>
    console.log('hi');
</script>
@if (true)
{
    <span>@DateTime.Now</span>
}
@{
    var x = 42;
}";
            var text = new SourceText(source);
            var lexer = new RazorLexer(text);
            var tokens = lexer.Lex();

            AssertZeroGapsOrOverlaps(source, tokens);

            Assert.Contains(tokens, t => t.State == LexicalState.Html);
            Assert.Contains(tokens, t => t.State == LexicalState.HtmlComment);
            Assert.Contains(tokens, t => t.State == LexicalState.RazorComment);
            Assert.Contains(tokens, t => t.State == LexicalState.Css);
            Assert.Contains(tokens, t => t.State == LexicalState.JavaScript);
            Assert.Contains(tokens, t => t.State == LexicalState.Razor);
            Assert.Contains(tokens, t => t.State == LexicalState.CSharp);
        }

        // -------------------------------------------------------------
        // 3. C# string handling
        // -------------------------------------------------------------

        [Fact]
        public void Lex_CSharpStrings_NormalVerbatimInterpolatedRawAndChars()
        {
            var source = @"@{
    var normal = ""hello \""world\""\n"";
    var verbatim = @""first line
second """"quoted"""" line"";
    var interpolated = $""value: {normal.Length}"";
    var raw = """"""
        line 1
        line 2
        """""" ;
    char ch = '}';
}";
            var text = new SourceText(source);
            var lexer = new RazorLexer(text);
            var tokens = lexer.Lex();

            AssertZeroGapsOrOverlaps(source, tokens);

            // None of the internal braces or quotes inside strings closed the C# code block
            var blockCloses = tokens.Where(t => t.Kind == RazorSyntaxKind.RazorBlockClose).ToList();
            Assert.Single(blockCloses);
            Assert.Equal(source.LastIndexOf('}'), blockCloses[0].Start);
        }

        // -------------------------------------------------------------
        // 4. C# line and block comments
        // -------------------------------------------------------------

        [Fact]
        public void Lex_CSharpComments_LineAndBlockHandledCorrectly()
        {
            var source = @"@{
    // Single line with { and } braces
    /* Multi-line
       with { and } braces */
    var count = 10;
}";
            var text = new SourceText(source);
            var lexer = new RazorLexer(text);
            var tokens = lexer.Lex();

            AssertZeroGapsOrOverlaps(source, tokens);

            // Braces inside comments must not create block open/close tokens
            var blockOpens = tokens.Where(t => t.Kind == RazorSyntaxKind.RazorBlockOpen).ToList();
            var blockCloses = tokens.Where(t => t.Kind == RazorSyntaxKind.RazorBlockClose).ToList();
            Assert.Single(blockOpens);
            Assert.Single(blockCloses);
        }

        // -------------------------------------------------------------
        // 5. Nested braces and parentheses
        // -------------------------------------------------------------

        [Fact]
        public void Lex_NestedBracesAndParentheses()
        {
            var source = @"@{
    var obj = new {
        Level1 = new {
            Level2 = new { Value = ((10 + 20) * 30) }
        }
    };
}";
            var text = new SourceText(source);
            var lexer = new RazorLexer(text);
            var tokens = lexer.Lex();

            AssertZeroGapsOrOverlaps(source, tokens);

            var csharpOpens = tokens.Where(t => t.Kind == RazorSyntaxKind.CSharpBlockOpen).Count();
            var csharpCloses = tokens.Where(t => t.Kind == RazorSyntaxKind.CSharpBlockClose).Count();
            Assert.Equal(3, csharpOpens);
            Assert.Equal(3, csharpCloses);

            var razorCloses = tokens.Count(t => t.Kind == RazorSyntaxKind.RazorBlockClose);
            Assert.Equal(1, razorCloses);
        }

        // -------------------------------------------------------------
        // 6. Razor transitions (@, @@, @:, @<)
        // -------------------------------------------------------------

        [Fact]
        public void Lex_RazorTransitions_EscapedAtSign()
        {
            var source = "<p>support@@example.com</p>";
            var text = new SourceText(source);
            var lexer = new RazorLexer(text);
            var tokens = lexer.Lex();

            AssertZeroGapsOrOverlaps(source, tokens);
            Assert.DoesNotContain(tokens, t => t.Kind == RazorSyntaxKind.RazorImplicitExpression);
            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.Html && text.ToString(t.Start, t.Length).Contains("@@"));
        }

        [Fact]
        public void Lex_RazorTransitions_SingleLineMarkupTransition()
        {
            var source = "@{\n    @: Line with <b>bold</b> text\n    var x = 1;\n}";
            var text = new SourceText(source);
            var lexer = new RazorLexer(text);
            var tokens = lexer.Lex();

            AssertZeroGapsOrOverlaps(source, tokens);
            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.RazorBlockTransition);
        }

        [Fact]
        public void Lex_RazorTransitions_TemplatedDelegateTransition()
        {
            var source = "@{\n    Func<dynamic, object> t = @<span>Item @item</span>;\n}";
            var text = new SourceText(source);
            var lexer = new RazorLexer(text);
            var tokens = lexer.Lex();

            AssertZeroGapsOrOverlaps(source, tokens);
            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.RazorBlockTransition);
            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.HtmlTag && text.ToString(t.Start, t.Length) == "<span>");
            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.HtmlTag && text.ToString(t.Start, t.Length) == "</span>");
        }

        // -------------------------------------------------------------
        // 7. Multiline & nested Razor constructs
        // -------------------------------------------------------------

        [Fact]
        public void Lex_MultilineNestedRazorConstructs()
        {
            var source = @"<div class=""container"">
    @if (Model.IsAdmin)
    {
        <section data-user=""@Model.UserId"">
            @for (int i = 0; i < Model.Count; i++)
            {
                <span class=""@(i % 2 == 0 ? ""even"" : ""odd"")"">
                    Item @i
                </span>
            }
        </section>
    }
</div>";
            var text = new SourceText(source);
            var lexer = new RazorLexer(text);
            var tokens = lexer.Lex();

            AssertZeroGapsOrOverlaps(source, tokens);

            // 2 control flow statements: @if, @for
            var controlFlowTokens = tokens.Where(t => t.Kind == RazorSyntaxKind.RazorControlFlow).ToList();
            Assert.Equal(2, controlFlowTokens.Count);

            // 2 block opens, 2 block closes
            Assert.Equal(2, tokens.Count(t => t.Kind == RazorSyntaxKind.RazorBlockOpen));
            Assert.Equal(2, tokens.Count(t => t.Kind == RazorSyntaxKind.RazorBlockClose));
        }

        // -------------------------------------------------------------
        // 8. Better handling of malformed and incomplete documents
        // -------------------------------------------------------------

        [Fact]
        public void Lex_MalformedAndIncompleteDocuments_DoesNotThrow()
        {
            var malformedCases = new[]
            {
                "<div class=\"unclosed",
                "<button disabled=",
                "@{ var s = \"unclosed string",
                "@{ var s = @\"unclosed verbatim",
                "@{ var s = $\"unclosed interpolated {x",
                "@( unclosed explicit expression",
                "@* unclosed razor comment",
                "<!-- unclosed html comment",
                "<script>\n unclosed script",
                "<style>\n unclosed style",
                "@if (a < b",
                "@{\n if (x) {",
            };

            foreach (var malformed in malformedCases)
            {
                var text = new SourceText(malformed);
                var lexer = new RazorLexer(text);
                var tokens = lexer.Lex();

                // Should not crash and should maintain zero gaps
                AssertZeroGapsOrOverlaps(malformed, tokens);
            }
        }
    }
}

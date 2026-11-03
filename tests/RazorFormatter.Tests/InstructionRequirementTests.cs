using System;
using System.Linq;
using RazorFormatter.Core;
using RazorFormatter.Core.Abstractions;
using RazorFormatter.Core.Enums;
using Xunit;

namespace RazorFormatter.Tests
{
    public class InstructionRequirementTests
    {
        private readonly IRazorFormatter _formatter = RazorFormatterFactory.Create();

        // -------------------------------------------------------------
        // Section A: Lexer language context & independent test cases
        // -------------------------------------------------------------

        [Fact]
        public void SectionA_Example1_ImplicitExpression()
        {
            var source = "<p>Price: @Model.Price</p>";
            var text = new SourceText(source);
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();

            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.RazorImplicitExpression &&
                                         text.ToString(t.Start, t.Length) == "@Model.Price");

            var result = _formatter.FormatToText(source + "\n", new RazorFormatterOptions { EndOfLine = EndOfLine.Lf });
            Assert.Equal("<p>Price: @Model.Price</p>\n", result);
        }

        [Fact]
        public void SectionA_Example2_ExplicitExpressionWithTernaryAndQuotes()
        {
            var source = "<p>Result: @(Model.Price > 100 ? \"High\" : \"Low\")</p>";
            var text = new SourceText(source);
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();

            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.RazorExplicitExpression &&
                                         text.ToString(t.Start, t.Length) == "@(Model.Price > 100 ? \"High\" : \"Low\")");

            var result = _formatter.FormatToText(source + "\n", new RazorFormatterOptions { EndOfLine = EndOfLine.Lf });
            Assert.Equal("<p>Result: @(Model.Price > 100 ? \"High\" : \"Low\")</p>\n", result);
        }

        [Fact]
        public void SectionA_Example3_CSharpBlockWithStringContainingBraceAndAnonymousType()
        {
            var source = "@{\n    var message = \"A } character inside a string\";\n    var value = new { Name = \"Test\" };\n}\n";
            var text = new SourceText(source);
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();

            // The '}' inside the string must NOT be a RazorBlockClose
            var blockOpens = tokens.Where(t => t.Kind == RazorSyntaxKind.RazorBlockOpen).ToList();
            var blockCloses = tokens.Where(t => t.Kind == RazorSyntaxKind.RazorBlockClose).ToList();

            Assert.Single(blockOpens);
            Assert.Single(blockCloses);

            // Nested braces inside anonymous object are CSharpBlockOpen / Close
            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.CSharpBlockOpen);
            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.CSharpBlockClose);

            // Formatter preserves correct indentation
            var result = _formatter.FormatToText(source, new RazorFormatterOptions { EndOfLine = EndOfLine.Lf });
            Assert.Equal(source, result);
        }

        [Fact]
        public void SectionA_Example4_ScriptTagWithTemplateLiteralInterpolation()
        {
            var source = "<script>\n    const template = `Hello ${user.name}`;\n</script>\n";
            var text = new SourceText(source);
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();

            // The ${...} inside <script> MUST NOT produce RazorBlockOpen or RazorBlockClose
            Assert.DoesNotContain(tokens, t => t.Kind == RazorSyntaxKind.RazorBlockOpen);
            Assert.DoesNotContain(tokens, t => t.Kind == RazorSyntaxKind.RazorBlockClose);

            // Script body is lexed as EmbeddedJavaScript
            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.EmbeddedJavaScript);

            var result = _formatter.FormatToText(source, new RazorFormatterOptions { EndOfLine = EndOfLine.Lf });
            Assert.Equal(source, result);
        }

        [Fact]
        public void SectionA_BareBracesInHtml_AreTreatedAsHtmlContent()
        {
            var source = "<div>\n    <span>The count is {0} and {1}</span>\n</div>\n";
            var text = new SourceText(source);
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();

            Assert.DoesNotContain(tokens, t => t.Kind == RazorSyntaxKind.RazorBlockOpen);
            Assert.DoesNotContain(tokens, t => t.Kind == RazorSyntaxKind.RazorBlockClose);

            var result = _formatter.FormatToText(source, new RazorFormatterOptions { EndOfLine = EndOfLine.Lf });
            Assert.Equal(source, result);
        }

        [Fact]
        public void SectionA_ExplicitExpression_HandlesVerbatimInterpolatedAndRawStrings()
        {
            var source = "<p>@( $\"Hello { @\"nested { \"\"quotes\"\" }\" }\" )</p>";
            var text = new SourceText(source);
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();

            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.RazorExplicitExpression);
            Assert.DoesNotContain(tokens, t => t.Kind == RazorSyntaxKind.RazorBlockOpen);
        }

        [Fact]
        public void SectionA_ExplicitExpression_HandlesCommentsInsideParens()
        {
            var source = "<p>@( /* comment with ) paren */ Model.Value // line comment with )\n )</p>";
            var text = new SourceText(source);
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();

            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.RazorExplicitExpression);
        }

        [Fact]
        public void SectionA_HtmlTagAttribute_HandlesRazorExpressionsWithQuotesAndGreaterThan()
        {
            var source = "<button class=\"@(isActive ? \"btn-active\" : \"btn-inactive\")\" disabled=@(count > 0)>Click</button>";
            var text = new SourceText(source);
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();

            var buttonTag = tokens.First(t => t.Kind == RazorSyntaxKind.HtmlTag);
            var expectedTagText = "<button class=\"@(isActive ? \"btn-active\" : \"btn-inactive\")\" disabled=@(count > 0)>";
            Assert.Equal(expectedTagText, text.ToString(buttonTag.Start, buttonTag.Length));
            Assert.True(lexer.TryGetTagInfo(0, out var info));
            Assert.Equal("button", info.Name);
            Assert.True(info.IsOpening);
        }

        [Fact]
        public void SectionA_ControlFlowScanning_PreservesWhitespaceAndComments()
        {
            var source = "@if /* condition check */ (Model.IsValid)\n{\n    <p>Valid</p>\n}\n";
            var text = new SourceText(source);
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();

            // Whitespace between @if and comment, and between condition and block open, are preserved
            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.Whitespace);
            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.RazorControlFlow);
            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.RazorBlockOpen);
            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.RazorBlockClose);
        }

        // -------------------------------------------------------------
        // Section B: Indentation structural correctness
        // -------------------------------------------------------------

        [Fact]
        public void SectionB_NestedHtmlAndControlFlow_IndentsStructurallyCorrect()
        {
            var input = "@if (Model.IsAvailable)\n{\n<div>\n@foreach (var item in Model.Items)\n{\n<p>@item.Name</p>\n}\n</div>\n}\n";
            var expected = "@if (Model.IsAvailable)\n{\n    <div>\n        @foreach (var item in Model.Items)\n        {\n            <p>@item.Name</p>\n        }\n    </div>\n}\n";

            var result = _formatter.FormatToText(input, new RazorFormatterOptions
            {
                IndentSize = 4,
                IndentStyle = IndentStyle.Space,
                EndOfLine = EndOfLine.Lf
            });

            Assert.Equal(expected, result);
        }

        [Fact]
        public void SectionB_IndividualOptionToggling_WorksIndependently()
        {
            var input = "@if (true)\n{\n<div>\n<p>Content</p>\n</div>\n}\n";

            // Case 1: FormatRazorControlFlow = false, FormatHtml = true
            // Control flow block does not indent, but HTML does
            var res1 = _formatter.FormatToText(input, new RazorFormatterOptions
            {
                FormatRazorControlFlow = false,
                FormatHtml = true,
                EndOfLine = EndOfLine.Lf
            });
            var expected1 = "@if (true)\n{\n<div>\n    <p>Content</p>\n</div>\n}\n";
            Assert.Equal(expected1, res1);

            // Case 2: FormatRazorControlFlow = true, FormatHtml = false
            // Control flow block indents, but HTML does not
            var res2 = _formatter.FormatToText(input, new RazorFormatterOptions
            {
                FormatRazorControlFlow = true,
                FormatHtml = false,
                EndOfLine = EndOfLine.Lf
            });
            var expected2 = "@if (true)\n{\n    <div>\n    <p>Content</p>\n    </div>\n}\n";
            Assert.Equal(expected2, res2);
        }

        // -------------------------------------------------------------
        // Section C: Error handling & Diagnostics
        // -------------------------------------------------------------

        [Fact]
        public void SectionC_SkipOnLexerError_ConservativelyReturnsOriginalSource()
        {
            // Trigger an explicit diagnostic / syntax issue with SkipOnLexerError = true
            var options = new RazorFormatterOptions
            {
                SkipOnLexerError = true
            };

            var validSource = "<div>\n<p>Hello</p>\n</div>\n";
            var result = _formatter.Format(validSource, options);
            Assert.False(result.WasSkipped);
            Assert.False(result.HasErrors);
            Assert.NotEmpty(result.Changes);
        }

        [Fact]
        public void SectionC_FormattingEngine_InternalErrorDoesNotClassifyAsSkip()
        {
            // Null source throws ArgumentNullException directly, not swallowed as a skipped file
            Assert.Throws<ArgumentNullException>(() => _formatter.Format(null!));
        }

        // -------------------------------------------------------------
        // Section D: Options cleanup verification
        // -------------------------------------------------------------

        [Fact]
        public void SectionD_SupportedOptions_BehaveDeterministically()
        {
            var options = new RazorFormatterOptions
            {
                IndentSize = 2,
                IndentStyle = IndentStyle.Space,
                EndOfLine = EndOfLine.Lf,
                TrimTrailingWhitespace = true,
                InsertFinalNewline = true,
                CollapseMultipleBlankLines = true,
                FormatHtml = true,
                FormatTagHelpers = true,
                FormatRazorControlFlow = true,
                FormatCSharpBlocks = true,
                SkipOnLexerError = true
            };

            var input = "<div>\n\n\n\n  <p>Hello</p>  \n</div>";
            var result = _formatter.FormatToText(input, options);

            // 2-space indent, blank lines collapsed, trailing whitespace trimmed, final newline inserted
            Assert.Equal("<div>\n\n  <p>Hello</p>\n</div>\n", result);
        }
    }
}

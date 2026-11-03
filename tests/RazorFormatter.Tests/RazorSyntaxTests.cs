using RazorFormatter.Core;
using RazorFormatter.Core.Enums;
using Xunit;

namespace RazorFormatter.Tests
{
    public class RazorSyntaxTests
    {
        [Fact]
        public void Lex_HtmlStartAndEndTags_IdentifiedWithSpans()
        {
            var text = new SourceText("<div>Hello</div>");
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();

            Assert.Equal(3, tokens.Count);

            Assert.Equal(RazorSyntaxKind.HtmlTag, tokens[0].Kind);
            Assert.Equal(0, tokens[0].Start);
            Assert.Equal(5, tokens[0].End);
            Assert.True(lexer.TryGetTagInfo(0, out var divOpen));
            Assert.True(divOpen.IsOpening);
            Assert.False(divOpen.IsClosing);
            Assert.False(divOpen.IsVoidElement);
            Assert.Equal("div", divOpen.Name);

            Assert.Equal(RazorSyntaxKind.Html, tokens[1].Kind);
            Assert.Equal(5, tokens[1].Start);
            Assert.Equal(10, tokens[1].End);
            Assert.Equal("Hello", text.ToString(tokens[1].Start, tokens[1].Length));

            Assert.Equal(RazorSyntaxKind.HtmlTag, tokens[2].Kind);
            Assert.Equal(10, tokens[2].Start);
            Assert.Equal(16, tokens[2].End);
            Assert.True(lexer.TryGetTagInfo(2, out var divClose));
            Assert.False(divClose.IsOpening);
            Assert.True(divClose.IsClosing);
            Assert.Equal("div", divClose.Name);
        }

        [Fact]
        public void Lex_VoidAndSelfClosingTags_DoNotIndentChildren()
        {
            var text = new SourceText("<img src=\"test.png\"><br/><input type=\"text\" />");
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();

            Assert.Equal(3, tokens.Count);

            Assert.True(lexer.TryGetTagInfo(0, out var imgInfo));
            Assert.True(imgInfo.IsVoidElement);
            Assert.False(imgInfo.IndentsChildren);

            Assert.True(lexer.TryGetTagInfo(1, out var brInfo));
            Assert.True(brInfo.IsSelfClosing);
            Assert.False(brInfo.IndentsChildren);

            Assert.True(lexer.TryGetTagInfo(2, out var inputInfo));
            Assert.True(inputInfo.IsVoidElement);
            Assert.False(inputInfo.IndentsChildren);
        }

        [Fact]
        public void Lex_TagHelperComponent_ClassifiedAsTagHelper()
        {
            var text = new SourceText("<MyComponent Title=\"Test\" />");
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();

            Assert.Single(tokens);
            Assert.Equal(RazorSyntaxKind.TagHelper, tokens[0].Kind);
            Assert.True(lexer.TryGetTagInfo(0, out var info));
            Assert.Equal("MyComponent", info.Name);
            Assert.True(info.IsSelfClosing);
        }

        [Fact]
        public void Lex_QuotedAttributesWithGreaterSign_DoesNotEndTagEarly()
        {
            var text = new SourceText("<div data-comparison=\"a > b\" class=\"btn\">Content</div>");
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();

            Assert.Equal(3, tokens.Count);
            Assert.Equal(RazorSyntaxKind.HtmlTag, tokens[0].Kind);
            Assert.Equal("<div data-comparison=\"a > b\" class=\"btn\">", text.ToString(tokens[0].Start, tokens[0].Length));
            Assert.True(lexer.TryGetTagInfo(0, out var tagInfo));
            Assert.True(tagInfo.IsOpening);
        }

        [Fact]
        public void Lex_MultilineQuotedAttribute_PreservesTagSpan()
        {
            var raw = "<button\n  class=\"btn\n         btn-primary\">Click</button>";
            var text = new SourceText(raw);
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();

            var buttonTok = tokens[0];
            Assert.Equal(RazorSyntaxKind.HtmlTag, buttonTok.Kind);
            Assert.True(lexer.TryGetTagInfo(0, out var tagInfo));
            Assert.Equal("button", tagInfo.Name);
            Assert.True(tagInfo.IsOpening);
        }

        [Fact]
        public void Lex_HtmlAndRazorComments_Distinguished()
        {
            var text = new SourceText("<!-- html comment -->@* razor comment *@");
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();

            Assert.Equal(2, tokens.Count);
            Assert.Equal(RazorSyntaxKind.HtmlComment, tokens[0].Kind);
            Assert.Equal("<!-- html comment -->", text.ToString(tokens[0].Start, tokens[0].Length));

            Assert.Equal(RazorSyntaxKind.RazorComment, tokens[1].Kind);
            Assert.Equal("@* razor comment *@", text.ToString(tokens[1].Start, tokens[1].Length));
        }

        [Fact]
        public void Lex_RazorExpressions_ExplicitAndImplicit()
        {
            var text = new SourceText("Hello @user.Name! Result: @(10 > 5 ? 1 : 0)");
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();

            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.RazorImplicitExpression && text.ToString(t.Start, t.Length) == "@user.Name");
            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.RazorExplicitExpression && text.ToString(t.Start, t.Length) == "@(10 > 5 ? 1 : 0)");
        }

        [Fact]
        public void Lex_RazorControlFlowAndDirectives()
        {
            var text = new SourceText("@page \"/test\"\n@model MyModel\n@if (condition)\n{\n}");
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();

            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.RazorDirective && text.ToString(t.Start, t.Length) == "@page \"/test\"");
            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.RazorDirective && text.ToString(t.Start, t.Length) == "@model MyModel");
            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.RazorControlFlow && text.ToString(t.Start, t.Length) == "@if");
            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.RazorBlockOpen);
            Assert.Contains(tokens, t => t.Kind == RazorSyntaxKind.RazorBlockClose);
        }

        [Fact]
        public void Lex_MalformedInput_DoesNotThrow()
        {
            // Less than operator in text, not a tag
            var text = new SourceText("if (a < b && c > d) { < 5 }");
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();
            Assert.NotEmpty(tokens);

            // Incomplete tag at EOF
            var unclosed = new SourceText("<div class=\"unfinished");
            var unclosedLexer = new RazorSyntaxLexer(unclosed);
            var unclosedTokens = unclosedLexer.Lex();
            Assert.NotEmpty(unclosedTokens);
        }
    }
}


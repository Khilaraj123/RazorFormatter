using RazorFormatter.Core;
using RazorFormatter.Core.Enums;
using RazorFormatter.Core.internals;
using Xunit;

namespace RazorFormatter.Tests
{
    public class FormattingTests
    {
        [Fact]
        public void IndentationEngine_ComputesLineIndents_Directly()
        {
            var raw = "<div>\n<p>\nHello\n</p>\n</div>";
            var text = new SourceText(raw);
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();
            var options = new RazorFormatterOptions();

            var indents = IndentationEngine.ComputeLineIndents(text, tokens, lexer, options);

            Assert.Equal(new[] { 0, 1, 2, 1, 0 }, indents);
        }

        [Fact]
        public void IndentationEngine_VoidElements_DoNotIncreaseChildDepth()
        {
            var raw = "<div>\n<input type=\"text\">\n<p>\nContent\n</p>\n</div>";
            var text = new SourceText(raw);
            var lexer = new RazorSyntaxLexer(text);
            var tokens = lexer.Lex();
            var options = new RazorFormatterOptions();

            var indents = IndentationEngine.ComputeLineIndents(text, tokens, lexer, options);

            Assert.Equal(new[] { 0, 1, 1, 2, 1, 0 }, indents);
        }

        [Fact]
        public void Format_BasicNestedHtml_IndentedCorrectly()
        {
            var input = "<div>\n<section>\n<p>\nHello World\n</p>\n</section>\n</div>\n";
            var expected = "<div>\n    <section>\n        <p>\n            Hello World\n        </p>\n    </section>\n</div>\n";

            var engine = RazorFormatterFactory.Create();
            var result = engine.FormatToText(input, new RazorFormatterOptions
            {
                IndentSize = 4,
                IndentStyle = IndentStyle.Space,
                EndOfLine = EndOfLine.Lf
            });

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Format_TwoSpaceIndentation()
        {
            var input = "<div>\n<p>\nHello\n</p>\n</div>\n";
            var expected = "<div>\n  <p>\n    Hello\n  </p>\n</div>\n";

            var engine = RazorFormatterFactory.Create();
            var result = engine.FormatToText(input, new RazorFormatterOptions
            {
                IndentSize = 2,
                IndentStyle = IndentStyle.Space,
                EndOfLine = EndOfLine.Lf
            });

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Format_TabIndentation()
        {
            var input = "<div>\n<p>\nHello\n</p>\n</div>\n";
            var expected = "<div>\n\t<p>\n\t\tHello\n\t</p>\n</div>\n";

            var engine = RazorFormatterFactory.Create();
            var result = engine.FormatToText(input, new RazorFormatterOptions
            {
                IndentStyle = IndentStyle.Tab,
                EndOfLine = EndOfLine.Lf
            });

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Format_TrimsTrailingWhitespace()
        {
            var input = "<div>   \n    <p>Hello</p>  \n</div>\n";
            var expected = "<div>\n    <p>Hello</p>\n</div>\n";

            var engine = RazorFormatterFactory.Create();
            var result = engine.FormatToText(input, new RazorFormatterOptions
            {
                TrimTrailingWhitespace = true,
                EndOfLine = EndOfLine.Lf
            });

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Format_CollapsesMultipleBlankLines()
        {
            var input = "<div>\n\n\n\n    <p>Hello</p>\n</div>\n";
            var expected = "<div>\n\n    <p>Hello</p>\n</div>\n";

            var engine = RazorFormatterFactory.Create();
            var result = engine.FormatToText(input, new RazorFormatterOptions
            {
                CollapseMultipleBlankLines = true,
                EndOfLine = EndOfLine.Lf
            });

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Format_InsertsFinalNewline()
        {
            var input = "<div>\n    <p>Hello</p>\n</div>";
            var expected = "<div>\n    <p>Hello</p>\n</div>\n";

            var engine = RazorFormatterFactory.Create();
            var result = engine.FormatToText(input, new RazorFormatterOptions
            {
                InsertFinalNewline = true,
                EndOfLine = EndOfLine.Lf
            });

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Format_RazorControlFlow_IndentsBlockContents()
        {
            var input = "@if (true)\n{\n<div>\n<span>Active</span>\n</div>\n}\n";
            var expected = "@if (true)\n{\n    <div>\n        <span>Active</span>\n    </div>\n}\n";

            var engine = RazorFormatterFactory.Create();
            var result = engine.FormatToText(input, new RazorFormatterOptions
            {
                IndentSize = 4,
                IndentStyle = IndentStyle.Space,
                EndOfLine = EndOfLine.Lf
            });

            Assert.Equal(expected, result);
        }
    }
}


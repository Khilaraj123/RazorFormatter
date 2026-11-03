using System;
using RazorFormatter.Core;
using RazorFormatter.Core.Abstractions;
using RazorFormatter.Core.Enums;
using Xunit;

namespace RazorFormatter.Tests
{
    public class Phase3AttributeAndEmbeddedTests
    {
        private readonly IRazorFormatter _formatter = RazorFormatterFactory.Create();

        [Fact]
        public void OnePerLine_FormatsFormAndInputAttributes()
        {
            var input = "<form asp-controller=\"Users\" asp-action=\"Create\" method=\"post\" class=\"form-container\">\n" +
                        "    <input asp-for=\"Email\" class=\"form-control\" placeholder=\"Email\" />\n" +
                        "</form>\n";

            var expected = "<form\n" +
                           "    asp-controller=\"Users\"\n" +
                           "    asp-action=\"Create\"\n" +
                           "    method=\"post\"\n" +
                           "    class=\"form-container\">\n" +
                           "    <input\n" +
                           "        asp-for=\"Email\"\n" +
                           "        class=\"form-control\"\n" +
                           "        placeholder=\"Email\" />\n" +
                           "</form>\n";

            var result = _formatter.FormatToText(input, new RazorFormatterOptions
            {
                AttributeLayout = AttributeLayout.OnePerLine,
                IndentSize = 4,
                IndentStyle = IndentStyle.Space,
                EndOfLine = EndOfLine.Lf
            });

            Assert.Equal(expected, result);
        }

        [Fact]
        public void SingleLine_CollapsesMultilineAttributesIntoSingleLine()
        {
            var input = "<button\n" +
                        "    type=\"button\"\n" +
                        "    class=\"btn btn-primary\"\n" +
                        "    data-toggle=\"modal\">\n" +
                        "    Submit\n" +
                        "</button>\n";

            var expected = "<button type=\"button\" class=\"btn btn-primary\" data-toggle=\"modal\">\n" +
                           "    Submit\n" +
                           "</button>\n";

            var result = _formatter.FormatToText(input, new RazorFormatterOptions
            {
                AttributeLayout = AttributeLayout.SingleLine,
                IndentSize = 4,
                IndentStyle = IndentStyle.Space,
                EndOfLine = EndOfLine.Lf
            });

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Preserve_PreservesSingleLineTagUnchanged()
        {
            var input = "<div id=\"main\" class=\"container text-center\">\n    <p>Content</p>\n</div>\n";

            var result = _formatter.FormatToText(input, new RazorFormatterOptions
            {
                AttributeLayout = AttributeLayout.Preserve,
                EndOfLine = EndOfLine.Lf
            });

            Assert.Equal(input, result);
        }

        [Fact]
        public void Preserve_AlignsMultilineAttributesToIndentLevel()
        {
            var input = "<div\n" +
                        "  id=\"main\"\n" +
                        "  class=\"container\">\n" +
                        "    <p>Content</p>\n" +
                        "</div>\n";

            var expected = "<div\n" +
                           "    id=\"main\"\n" +
                           "    class=\"container\">\n" +
                           "    <p>Content</p>\n" +
                           "</div>\n";

            var result = _formatter.FormatToText(input, new RazorFormatterOptions
            {
                AttributeLayout = AttributeLayout.Preserve,
                IndentSize = 4,
                IndentStyle = IndentStyle.Space,
                EndOfLine = EndOfLine.Lf
            });

            Assert.Equal(expected, result);
        }

        [Fact]
        public void RazorExpressionsInAttributes_PreservedAccurately()
        {
            var input = "<button class=\"@(isActive ? \"btn-success\" : \"btn-danger\")\" disabled=@(items.Count == 0)>\n" +
                        "    Click Me\n" +
                        "</button>\n";

            var result = _formatter.FormatToText(input, new RazorFormatterOptions
            {
                AttributeLayout = AttributeLayout.OnePerLine,
                IndentSize = 4,
                IndentStyle = IndentStyle.Space,
                EndOfLine = EndOfLine.Lf
            });

            var expected = "<button\n" +
                           "    class=\"@(isActive ? \"btn-success\" : \"btn-danger\")\"\n" +
                           "    disabled=@(items.Count == 0)>\n" +
                           "    Click Me\n" +
                           "</button>\n";

            Assert.Equal(expected, result);
        }

        [Fact]
        public void TagHelperAttributes_HandledCorrectly()
        {
            var input = "<input asp-for=\"User.FirstName\" asp-validation-for=\"User.FirstName\" class=\"form-input\" />\n";

            var result = _formatter.FormatToText(input, new RazorFormatterOptions
            {
                AttributeLayout = AttributeLayout.OnePerLine,
                IndentSize = 4,
                IndentStyle = IndentStyle.Space,
                EndOfLine = EndOfLine.Lf
            });

            var expected = "<input\n" +
                           "    asp-for=\"User.FirstName\"\n" +
                           "    asp-validation-for=\"User.FirstName\"\n" +
                           "    class=\"form-input\" />\n";

            Assert.Equal(expected, result);
        }

        [Fact]
        public void EmbeddedCss_FormatsNestedRulesAndPreservesRazorExpressions()
        {
            var input = "<style>\n" +
                        "body {\n" +
                        "color: @Model.ThemeColor;\n" +
                        "font-size: 14px;\n" +
                        "}\n" +
                        ".container {\n" +
                        "padding: 10px;\n" +
                        "}\n" +
                        "</style>\n";

            var expected = "<style>\n" +
                           "    body {\n" +
                           "        color: @Model.ThemeColor;\n" +
                           "        font-size: 14px;\n" +
                           "    }\n" +
                           "    .container {\n" +
                           "        padding: 10px;\n" +
                           "    }\n" +
                           "</style>\n";

            var result = _formatter.FormatToText(input, new RazorFormatterOptions
            {
                FormatEmbeddedLanguages = true,
                IndentSize = 4,
                IndentStyle = IndentStyle.Space,
                EndOfLine = EndOfLine.Lf
            });

            Assert.Equal(expected, result);
        }

        [Fact]
        public void EmbeddedJavaScript_FormatsBlocksAndPreservesTemplateLiterals()
        {
            var input = "<script>\n" +
                        "function greet(name) {\n" +
                        "const msg = `Hello, ${name}!`;\n" +
                        "console.log(msg);\n" +
                        "}\n" +
                        "</script>\n";

            var expected = "<script>\n" +
                           "    function greet(name) {\n" +
                           "        const msg = `Hello, ${name}!`;\n" +
                           "        console.log(msg);\n" +
                           "    }\n" +
                           "</script>\n";

            var result = _formatter.FormatToText(input, new RazorFormatterOptions
            {
                FormatEmbeddedLanguages = true,
                IndentSize = 4,
                IndentStyle = IndentStyle.Space,
                EndOfLine = EndOfLine.Lf
            });

            Assert.Equal(expected, result);
        }

        [Fact]
        public void EmbeddedLanguages_Disabled_LeavesContentUntouched()
        {
            var input = "<style>\n" +
                        "body { color: red; }\n" +
                        "</style>\n";

            var result = _formatter.FormatToText(input, new RazorFormatterOptions
            {
                FormatEmbeddedLanguages = false,
                EndOfLine = EndOfLine.Lf
            });

            Assert.Equal(input, result);
        }
    }
}


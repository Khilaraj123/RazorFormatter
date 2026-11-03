using RazorFormatter.Core;
using RazorFormatter.Core.Enums;
using Xunit;

namespace RazorFormatter.Tests
{
    public class IdempotencyTests
    {
        [Theory]
        [InlineData("<div>\n    <p>\n        Hello World\n    </p>\n</div>\n")]
        [InlineData("<!DOCTYPE html>\n<html>\n    <head>\n        <meta charset=\"utf-8\" />\n        <title>Test</title>\n    </head>\n    <body>\n        <!-- Main Container -->\n        <div class=\"container\">\n            <input type=\"text\" />\n            <br />\n            <p>\n                Paragraph\n            </p>\n        </div>\n    </body>\n</html>\n")]
        [InlineData("@page \"/demo\"\n@model TestModel\n\n@if (Model.IsActive)\n{\n    <div class=\"alert\">\n        <p>\n            Welcome @Model.UserName\n        </p>\n    </div>\n}\n")]
        public void Format_IsIdempotent_SecondPassMakesZeroChanges(string sample)
        {
            var engine = RazorFormatterFactory.Create();
            var options = new RazorFormatterOptions
            {
                IndentSize = 4,
                IndentStyle = IndentStyle.Space,
                EndOfLine = EndOfLine.Lf,
                InsertFinalNewline = true,
                TrimTrailingWhitespace = true
            };

            // First pass
            var firstResult = engine.Format(sample, options);

            // Second pass on the output of the first pass
            var secondResult = engine.Format(firstResult.FormattedText, options);

            Assert.Equal(firstResult.FormattedText, secondResult.FormattedText);
            Assert.Empty(secondResult.Changes);
        }

        [Fact]
        public void Format_AllSampleFiles_AreIdempotent()
        {
            var samplesDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples"));
            if (!Directory.Exists(samplesDir)) return;

            var engine = RazorFormatterFactory.Create();
            var options = new RazorFormatterOptions
            {
                IndentSize = 4,
                IndentStyle = IndentStyle.Space,
                EndOfLine = EndOfLine.Lf,
                InsertFinalNewline = true,
                TrimTrailingWhitespace = true
            };

            foreach (var file in Directory.GetFiles(samplesDir, "*.cshtml"))
            {
                var content = File.ReadAllText(file);
                var first = engine.Format(content, options);
                var second = engine.Format(first.FormattedText, options);

                Assert.Equal(first.FormattedText, second.FormattedText);
                Assert.Empty(second.Changes);
            }
        }
    }
}

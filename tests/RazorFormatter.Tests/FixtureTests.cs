using RazorFormatter.Core;
using RazorFormatter.Core.Enums;
using Xunit;

namespace RazorFormatter.Tests
{
    public class FixtureTests
    {
        [Theory]
        [InlineData("NestedHtml")]
        [InlineData("RazorControlFlow")]
        [InlineData("TagHelpers")]
        public void GoldenFileFixtures_FormatMatchesExpected(string fixtureName)
        {
            var fixturesDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Fixtures"));
            var inputPath = Path.Combine(fixturesDir, $"{fixtureName}.input.cshtml");
            var expectedPath = Path.Combine(fixturesDir, $"{fixtureName}.expected.cshtml");

            Assert.True(File.Exists(inputPath), $"Missing input fixture: {inputPath}");
            Assert.True(File.Exists(expectedPath), $"Missing expected fixture: {expectedPath}");

            var input = File.ReadAllText(inputPath);
            var expected = File.ReadAllText(expectedPath);

            var engine = RazorFormatterFactory.Create();
            var options = new RazorFormatterOptions
            {
                IndentSize = 4,
                IndentStyle = IndentStyle.Space,
                EndOfLine = EndOfLine.Lf,
                InsertFinalNewline = true,
                TrimTrailingWhitespace = true
            };

            var actual = engine.FormatToText(input, options);

            // Normalize CRLF for cross-platform comparison
            Assert.Equal(expected.Replace("\r\n", "\n"), actual.Replace("\r\n", "\n"));
        }
    }
}


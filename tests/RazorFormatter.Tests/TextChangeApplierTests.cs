using RazorFormatter.Core;
using Xunit;

namespace RazorFormatter.Tests
{
    public class TextChangeApplierTests
    {
        [Fact]
        public void Apply_Insertions_BeginningMiddleAndEnd()
        {
            var source = "World";
            var changes = new[]
            {
                TextChange.Insert(0, "Hello "),
                TextChange.Insert(5, "!")
            };

            var result = TextChangeApplier.Apply(source, changes);
            Assert.Equal("Hello World!", result);
        }

        [Fact]
        public void Apply_Deletions_BeginningMiddleAndEnd()
        {
            var source = "[START]Content[MID]Text[END]";
            var changes = new[]
            {
                TextChange.Delete(0, 7),     // Delete [START]
                TextChange.Delete(14, 19),   // Delete [MID]
                TextChange.Delete(23, 28)    // Delete [END]
            };

            var result = TextChangeApplier.Apply(source, changes);
            Assert.Equal("ContentText", result);
        }

        [Fact]
        public void Apply_Replacements_AndMultipleEditsOnSameLine()
        {
            var source = "var foo = bar + baz;";
            var changes = new[]
            {
                TextChange.Replace(4, 7, "x"),
                TextChange.Replace(10, 13, "y"),
                TextChange.Replace(16, 19, "z")
            };

            var result = TextChangeApplier.Apply(source, changes);
            Assert.Equal("var x = y + z;", result);
        }

        [Fact]
        public void Apply_ThrowsOnOutOfBoundsEditRange()
        {
            var source = "hello";
            var changes = new[]
            {
                TextChange.Replace(0, 10, "world")
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => TextChangeApplier.Apply(source, changes));
        }

        [Fact]
        public void Apply_ThrowsOnOverlappingEdits()
        {
            var source = "0123456789";
            var changes = new[]
            {
                TextChange.Replace(2, 6, "A"),
                TextChange.Replace(4, 8, "B")
            };

            Assert.Throws<InvalidOperationException>(() => TextChangeApplier.Apply(source, changes));
        }

        [Fact]
        public void Apply_FormattedTextAgreesWithApplyingChanges()
        {
            var input = "<div>\n<p>\nHello\n</p>\n</div>\n";
            var engine = RazorFormatterFactory.Create();
            var result = engine.Format(input);

            var manualApplied = TextChangeApplier.Apply(input, result.Changes);
            Assert.Equal(result.FormattedText, manualApplied);
        }
    }
}


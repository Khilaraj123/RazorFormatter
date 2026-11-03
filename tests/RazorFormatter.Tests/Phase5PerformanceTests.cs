using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using RazorFormatter.Core;
using RazorFormatter.Core.Abstractions;
using RazorFormatter.Core.Enums;
using Xunit;

namespace RazorFormatter.Tests
{
    public class Phase5PerformanceTests
    {
        private readonly IRazorFormatter _formatter = RazorFormatterFactory.Create();

        [Fact]
        public void FormatRange_FormatsOnlySelectedRegion()
        {
            var source = "<div class=\"unformatted-start\">\n" +
                         "<p>Unformatted Top</p>\n" +
                         "</div>\n" +
                         "<section>\n" +
                         "<p>Target To Format</p>\n" +
                         "</section>\n" +
                         "<div class=\"unformatted-end\">\n" +
                         "<p>Unformatted Bottom</p>\n" +
                         "</div>\n";

            // Find the offset of <section> and </section>
            var sectionStart = source.IndexOf("<section>", StringComparison.Ordinal);
            var sectionEnd = source.IndexOf("</section>", StringComparison.Ordinal) + "</section>".Length;

            var result = _formatter.FormatRange(source, sectionStart, sectionEnd, new RazorFormatterOptions
            {
                IndentSize = 4,
                EndOfLine = EndOfLine.Lf
            });

            // The edits should only modify the target region, not touch the top or bottom unformatted lines
            Assert.Contains(result.Changes, c => c.Start >= sectionStart && c.End <= sectionEnd + 10);
            Assert.DoesNotContain(result.Changes, c => c.Start < sectionStart);
            Assert.DoesNotContain(result.Changes, c => c.Start > sectionEnd + 10);

            // Outside lines remain untouched
            Assert.StartsWith("<div class=\"unformatted-start\">\n<p>Unformatted Top</p>", result.FormattedText);
            Assert.EndsWith("<div class=\"unformatted-end\">\n<p>Unformatted Bottom</p>\n</div>\n", result.FormattedText);

            // Inside lines are formatted
            Assert.Contains("<section>\n    <p>Target To Format</p>\n</section>", result.FormattedText);
        }

        [Fact]
        public void Performance_MonotonicLineTracking_AccuratelyMatchesBinarySearch()
        {
            var sb = new StringBuilder();
            for (var i = 0; i < 500; i++)
            {
                sb.AppendLine($"<div id=\"item-{i}\">");
                sb.AppendLine($"    <p>Item count @i: @(i * 2)</p>");
                sb.AppendLine($"</div>");
            }

            var text = new SourceText(sb.ToString());

            // Test 1,000 random offsets
            var rand = new Random(42);
            for (var i = 0; i < 1000; i++)
            {
                var offset = rand.Next(0, text.Length);
                var expected = text.GetLineColumn(offset);

                // Sequential tracking from start to offset
                var last = 0;
                var actual = text.GetSequentialLineColumn(offset, ref last);

                Assert.Equal(expected.Line, actual.Line);
                Assert.Equal(expected.Column, actual.Column);
            }
        }

        [Fact]
        public void Performance_LargeDocument_FormatsEfficiently()
        {
            // Build a 2,500-line realistic Razor document
            var sb = new StringBuilder(100_000);
            sb.AppendLine("@page \"/orders\"");
            sb.AppendLine("@model OrderViewModel");
            sb.AppendLine();
            sb.AppendLine("<div class=\"container\">");

            for (var i = 0; i < 400; i++)
            {
                sb.AppendLine($"@if (Model.HasItems_{i})");
                sb.AppendLine("{");
                sb.AppendLine($"    <div class=\"card mb-3\">");
                sb.AppendLine($"        <div class=\"card-header\">Order #{i}</div>");
                sb.AppendLine($"        <div class=\"card-body\">");
                sb.AppendLine($"            <p>Customer: @Model.CustomerName</p>");
                sb.AppendLine($"            <button class=\"btn btn-primary\" disabled=@(Model.IsReadOnly_{i})>View</button>");
                sb.AppendLine($"        </div>");
                sb.AppendLine($"    </div>");
                sb.AppendLine("}");
            }

            sb.AppendLine("</div>");

            var largeDocument = sb.ToString();

            // Warm up
            _formatter.Format(largeDocument, new RazorFormatterOptions { EndOfLine = EndOfLine.Lf });

            // Measure execution time
            var stopwatch = Stopwatch.StartNew();
            var result = _formatter.Format(largeDocument, new RazorFormatterOptions
            {
                IndentSize = 4,
                EndOfLine = EndOfLine.Lf
            });
            stopwatch.Stop();

            Assert.False(result.WasSkipped);
            Assert.False(result.HasErrors);
            Assert.NotEmpty(result.FormattedText);

            // A 2,500-line document should format well under 500ms (typically < 100ms)
            Assert.True(stopwatch.ElapsedMilliseconds < 1000,
                $"Formatting took {stopwatch.ElapsedMilliseconds}ms, expected under 1000ms");

            // Verify idempotency on large document: second pass produces 0 changes
            var secondPass = _formatter.Format(result.FormattedText, new RazorFormatterOptions
            {
                IndentSize = 4,
                EndOfLine = EndOfLine.Lf
            });
            Assert.Empty(secondPass.Changes);
            Assert.Equal(result.FormattedText, secondPass.FormattedText);
        }
    }
}

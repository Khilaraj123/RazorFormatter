using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RazorFormatter.Cli
{
    public static class DiffHelper
    {
        public static string CreateUnifiedDiff(string filePath, string original, string formatted)
        {
            var originalLines = SplitLines(original);
            var formattedLines = SplitLines(formatted);

            var sb = new StringBuilder();
            sb.AppendLine($"--- {filePath} (original)");
            sb.AppendLine($"+++ {filePath} (formatted)");

            // Simple line-by-line diff for blocks with changes
            var origIdx = 0;
            var fmtIdx = 0;

            while (origIdx < originalLines.Count || fmtIdx < formattedLines.Count)
            {
                if (origIdx < originalLines.Count && fmtIdx < formattedLines.Count &&
                    string.Equals(originalLines[origIdx], formattedLines[fmtIdx], StringComparison.Ordinal))
                {
                    origIdx++;
                    fmtIdx++;
                    continue;
                }

                // Found a difference, find next synchronization point
                var chunkStartOrig = Math.Max(0, origIdx - 2);
                var chunkStartFmt = Math.Max(0, fmtIdx - 2);

                var changedOrig = new List<string>();
                var changedFmt = new List<string>();

                while (origIdx < originalLines.Count &&
                       (fmtIdx >= formattedLines.Count || !string.Equals(originalLines[origIdx], formattedLines[fmtIdx], StringComparison.Ordinal)))
                {
                    changedOrig.Add(originalLines[origIdx++]);
                    if (fmtIdx < formattedLines.Count && !originalLines.Contains(formattedLines[fmtIdx]))
                    {
                        changedFmt.Add(formattedLines[fmtIdx++]);
                    }
                }

                while (fmtIdx < formattedLines.Count &&
                       (origIdx >= originalLines.Count || !string.Equals(formattedLines[fmtIdx], originalLines[origIdx], StringComparison.Ordinal)))
                {
                    changedFmt.Add(formattedLines[fmtIdx++]);
                }

                sb.AppendLine($"@@ -{chunkStartOrig + 1} +{chunkStartFmt + 1} @@");
                foreach (var line in changedOrig)
                {
                    sb.AppendLine($"- {line}");
                }
                foreach (var line in changedFmt)
                {
                    sb.AppendLine($"+ {line}");
                }
            }

            return sb.ToString();
        }

        private static List<string> SplitLines(string text)
        {
            var lines = new List<string>();
            using var reader = new StringReader(text);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                lines.Add(line);
            }
            return lines;
        }
    }
}

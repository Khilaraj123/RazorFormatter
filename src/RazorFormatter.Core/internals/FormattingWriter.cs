using System.Collections.Generic;

namespace RazorFormatter.Core.internals
{
    /// <summary>
    /// Backward-compatible internal facade delegating to <see cref="Formatting.FormattingWriter"/>.
    /// </summary>
    internal static class FormattingWriter
    {
        public static List<TextChange> GenerateChanges(
            SourceText source,
            int[] lineIndents,
            RazorFormatterOptions options,
            string eol)
        {
            return Formatting.FormattingWriter.GenerateChanges(source, lineIndents, options, eol);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace RazorFormatter.Core
{
    public sealed class FormatResult
    {
        public string FormattedText { get; init; }
        public IReadOnlyList<TextChange> Changes { get; init; }
        public RazorFormatterOptions OptionsUsed { get; init; }
        public bool WasSkipped { get; init; }
        public IReadOnlyList<RazorDiagnostic> Diagnostics { get; init; }
        public Exception? InternalError { get; init; }

        public bool HasErrors => InternalError != null || Diagnostics.Any(d => d.Severity == RazorDiagnosticSeverity.Error);

        public FormatResult(
            string formattedText,
            IReadOnlyList<TextChange> changes,
            RazorFormatterOptions optionsUsed,
            bool wasSkipped,
            IReadOnlyList<RazorDiagnostic>? diagnostics = null,
            Exception? internalError = null)
        {
            FormattedText = formattedText;
            Changes = changes;
            OptionsUsed = optionsUsed;
            WasSkipped = wasSkipped;
            Diagnostics = diagnostics ?? Array.Empty<RazorDiagnostic>();
            InternalError = internalError;
        }
    }
}

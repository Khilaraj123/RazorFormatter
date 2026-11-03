using System;

namespace RazorFormatter.Core
{
    public enum RazorDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class RazorDiagnostic
    {
        public RazorDiagnosticSeverity Severity { get; }
        public string Message { get; }
        public int Offset { get; }
        public int Line { get; }
        public int Column { get; }

        public RazorDiagnostic(RazorDiagnosticSeverity severity, string message, int offset = 0, int line = 0, int column = 0)
        {
            Severity = severity;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Offset = offset;
            Line = line;
            Column = column;
        }

        public override string ToString() => $"({Line + 1},{Column + 1}): {Severity}: {Message}";
    }
}


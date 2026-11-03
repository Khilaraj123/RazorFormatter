using System;
using RazorFormatter.Core.Enums;
using RazorFormatter.Core.Lexing;

namespace RazorFormatter.Core
{
    /// <summary>One token from the Razor lexer.</summary>
    public readonly struct RazorToken : IEquatable<RazorToken>
    {
        public RazorSyntaxKind Kind { get; }
        public int Start { get; }
        public int End { get; }
        public int Line { get; }
        public int Column { get; }
        public LexicalState State { get; }

        public RazorToken(RazorSyntaxKind kind, int start, int end, int line, int column, LexicalState state = LexicalState.Html)
        {
            Kind = kind;
            Start = start;
            End = end;
            Line = line;
            Column = column;
            State = state;
        }

        public int Length => End - Start;

        public bool IsBlockLevelChange => Kind is RazorSyntaxKind.RazorBlockOpen
            or RazorSyntaxKind.RazorBlockClose
            or RazorSyntaxKind.HtmlTag;

        public bool Equals(RazorToken other)
            => Kind == other.Kind && Start == other.Start && End == other.End && Line == other.Line && Column == other.Column && State == other.State;

        public override bool Equals(object? obj)
            => obj is RazorToken t && Equals(t);

        public override int GetHashCode()
            => HashCode.Combine((int)Kind, Start, End, (int)State);

        public static bool operator ==(RazorToken a, RazorToken b)
            => a.Equals(b);

        public static bool operator !=(RazorToken a, RazorToken b)
            => !a.Equals(b);

        public override string ToString() => $"{Kind}@[{Start}..{End}) L{Line + 1}:C{Column + 1} ({State})";
    }
}

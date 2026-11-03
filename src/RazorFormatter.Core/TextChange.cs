namespace RazorFormatter.Core
{
    public readonly struct TextChange : IEquatable<TextChange>
    {
        public int Start { get; }
        public int End { get; }
        public string NewText { get; }

        public TextChange(int start, int end, string newText)
        {
            if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
            if (end < start) throw new ArgumentOutOfRangeException(nameof(end));
            Start = start;
            End = end;
            NewText = newText ?? string.Empty;
        }

        public int LengthDelta => NewText.Length - (End - Start);

        public static TextChange Insert(int offset, string text) => new(offset, offset, text);
        public static TextChange Delete(int start, int end) => new(start, end, string.Empty);
        public static TextChange Replace(int start, int end, string text) => new(start, end, text);

        public bool Equals(TextChange other) => Start == other.Start && End == other.End && NewText == other.NewText;
        public override bool Equals(object? obj) => obj is TextChange other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Start, End, NewText);
        public static bool operator ==(TextChange left, TextChange right) => left.Equals(right);
        public static bool operator !=(TextChange left, TextChange right) => !left.Equals(right);

        public override string ToString() => NewText.Length == 0
        ? $"delete[{Start}..{End})"
        : $"replace[{Start}..{End}) <- \"{NewText.Replace("\n", "\\n").Replace("\r", "\\r")}\"";
    }
}

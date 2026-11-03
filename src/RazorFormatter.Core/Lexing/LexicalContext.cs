namespace RazorFormatter.Core.Lexing
{
    /// <summary>
    /// Represents a frame on the lexical context stack during tokenization.
    /// </summary>
    public sealed class LexicalContext
    {
        public LexicalState State { get; }
        public int StartOffset { get; }
        public string? TagName { get; }
        public int HtmlElementDepth { get; set; }
        public int BraceDepth { get; set; }
        public int ParenDepth { get; set; }
        public bool IsSingleLineTransition { get; }

        public LexicalContext(
            LexicalState state,
            int startOffset,
            string? tagName = null,
            int htmlElementDepth = 0,
            int braceDepth = 0,
            int parenDepth = 0,
            bool isSingleLineTransition = false)
        {
            State = state;
            StartOffset = startOffset;
            TagName = tagName;
            HtmlElementDepth = htmlElementDepth;
            BraceDepth = braceDepth;
            ParenDepth = parenDepth;
            IsSingleLineTransition = isSingleLineTransition;
        }

        public override string ToString() => $"{State} (Offset: {StartOffset}, Tag: {TagName}, Braces: {BraceDepth})";
    }
}


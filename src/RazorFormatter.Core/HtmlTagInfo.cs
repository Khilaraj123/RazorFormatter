namespace RazorFormatter.Core
{
    /// <summary>Group of tokens that represent one HTML tag (open / close / self-close / void).</summary>
    public readonly struct HtmlTagInfo
    {
        public bool IsOpening { get; }
        public bool IsClosing { get; }
        public bool IsSelfClosing { get; }
        public bool IsVoidElement { get; }
        public string Name { get; }

        public HtmlTagInfo(bool isOpening, bool isClosing, bool isSelfClosing, bool isVoidElement, string name)
        {
            IsOpening = isOpening;
            IsClosing = isClosing;
            IsSelfClosing = isSelfClosing;
            IsVoidElement = isVoidElement;
            Name = name;
        }

        public bool IndentsChildren
            => IsOpening && !IsSelfClosing && !IsVoidElement && !IsClosing;
    }
}

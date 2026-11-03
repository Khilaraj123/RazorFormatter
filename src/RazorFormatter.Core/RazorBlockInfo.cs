namespace RazorFormatter.Core
{
    public enum RazorBlockKind
    {
        ControlFlow,
        CSharpCodeBlock,
    }

    public readonly struct RazorBlockInfo
    {
        public RazorBlockKind Kind { get; }

        public RazorBlockInfo(RazorBlockKind kind)
        {
            Kind = kind;
        }
    }
}


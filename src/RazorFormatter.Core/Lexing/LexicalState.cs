namespace RazorFormatter.Core.Lexing
{
    /// <summary>
    /// Explicit lexical states representing the active language or context.
    /// </summary>
    public enum LexicalState
    {
        Html,
        Razor,
        CSharp,
        RazorComment,
        HtmlComment,
        Css,
        JavaScript,
    }
}


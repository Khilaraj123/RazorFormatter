namespace RazorFormatter.Core.internals
{
    /// <summary>A pre-compiled list of HTML elements that never have children (no closing tag).</summary>
    internal static class HtmlVoidElements
    {
        private static readonly HashSet<string> s_voidElements = new(StringComparer.OrdinalIgnoreCase)
        {
            "area",
            "base",
            "br",
            "col",
            "embed",
            "hr",
            "img",
            "input",
            "link",
            "meta",
            "param",
            "source",
            "track",
            "wbr",
            "!doctype",
            "command",
            "keygen",
            "menuitem",
        };

        public static bool IsVoid(string tagName) 
            => s_voidElements.Contains(tagName);
    }
}

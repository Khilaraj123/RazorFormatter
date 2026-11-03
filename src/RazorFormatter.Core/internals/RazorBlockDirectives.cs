namespace RazorFormatter.Core.internals
{
    /// <summary>Razor directive keywords that introduce a brace-delimited block (vs. directives that end at newline).</summary>
    internal static class RazorBlockDirectives
    {
        private static readonly HashSet<string> s_blockDirectives = new(StringComparer.Ordinal)
        {
            "functions",
            "code", 
            "section", 
            "implements",
            "inherits",
        };

        /// <summary>True if <paramref name="directive"/> introduces a <c>{ ... }</c> block.</summary>
        public static bool IsBlockDirective(string directive)
            => s_blockDirectives.Contains(directive);
    }
}

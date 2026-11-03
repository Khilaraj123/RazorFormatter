namespace RazorFormatter.Core.Enums
{
    /// <summary>
    /// Strategy for formatting HTML and TagHelper attributes.
    /// </summary>
    public enum AttributeLayout
    {
        /// <summary>
        /// Preserves the existing layout: tags written on a single line remain single line; tags spanning multiple lines have their attributes aligned.
        /// </summary>
        Preserve,

        /// <summary>
        /// Formats each attribute on its own line if the tag has multiple attributes or already spans multiple lines.
        /// </summary>
        OnePerLine,

        /// <summary>
        /// Collapses all attributes onto the same line as the opening tag.
        /// </summary>
        SingleLine,
    }
}


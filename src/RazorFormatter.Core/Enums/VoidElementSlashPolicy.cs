namespace RazorFormatter.Core.Enums
{
    /// <summary>
    /// Policy for void elements (such as &lt;input&gt;, &lt;img&gt;, &lt;br&gt;, &lt;hr&gt;, &lt;meta&gt;, &lt;link&gt;).
    /// </summary>
    public enum VoidElementSlashPolicy
    {
        /// <summary>Preserve whatever slash format was in the source (e.g. &lt;input /&gt; or &lt;input&gt;).</summary>
        Preserve = 0,

        /// <summary>Ensure void elements always end with a self-closing slash (e.g. &lt;input /&gt;).</summary>
        Slash = 1,

        /// <summary>Ensure void elements do not have a self-closing slash (e.g. &lt;input&gt;).</summary>
        NoSlash = 2,
    }
}


namespace RazorFormatter.Core.Abstractions
{
    /// <summary>The single entry point used by all hosts (CLI, VSCode, VS).</summary>
    public interface IRazorFormatter
    {
        /// <summary>Returns the formatted output and the list of edits applied.</summary>
        FormatResult Format(string source, RazorFormatterOptions? options = null);

        /// <summary>Formats a file from disk, resolving project-level .editorconfig options.</summary>
        FormatResult FormatFile(string filePath, RazorFormatterOptions? explicitOptions = null);

        /// <summary>Formats a specific range within the document, returning only edits intersecting the range.</summary>
        FormatResult FormatRange(string source, int startOffset, int endOffset, RazorFormatterOptions? options = null);

        /// <summary>Same as <see cref="Format"/> but returns just the formatted text (the common case for CLI use).</summary>
        string FormatToText(string source, RazorFormatterOptions? options = null);
    }
}

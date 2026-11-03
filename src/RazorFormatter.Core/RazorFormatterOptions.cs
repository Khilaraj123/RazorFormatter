using System;
using System.Collections.Generic;
using RazorFormatter.Core.Enums;

namespace RazorFormatter.Core;

public sealed class RazorFormatterOptions
{
    private readonly HashSet<string> _explicitlySet = new(StringComparer.OrdinalIgnoreCase);

    private string? _filePath;
    private IndentStyle _indentStyle = IndentStyle.Space;
    private int _indentSize = 4;
    private EndOfLine _endOfLine = EndOfLine.Auto;
    private bool _trimTrailingWhitespace = true;
    private bool _insertFinalNewline = true;
    private bool _formatHtml = true;
    private bool _formatRazorControlFlow = true;
    private bool _formatCSharpBlocks = true;
    private bool _formatTagHelpers = true;
    private bool _formatEmbeddedLanguages = true;
    private AttributeLayout _attributeLayout = AttributeLayout.Preserve;
    private bool _collapseMultipleBlankLines = true;
    private int _maxConsecutiveBlankLines = 1;
    private VoidElementSlashPolicy _voidElementSlash = VoidElementSlashPolicy.Preserve;
    private bool _skipOnLexerError = true;

    /// <summary>Optional file path of the source being formatted, used for .editorconfig discovery and glob matching.</summary>
    public string? FilePath
    {
        get => _filePath;
        set { _filePath = value; _explicitlySet.Add(nameof(FilePath)); }
    }

    /// <summary>Indent style: 'space' (default) or 'tab'. When 'tab', <see cref="IndentSize"/> is the visual width of a tab.</summary>
    public IndentStyle IndentStyle
    {
        get => _indentStyle;
        set { _indentStyle = value; _explicitlySet.Add(nameof(IndentStyle)); }
    }

    /// <summary>Indent size in characters when using spaces, or tab width when using tabs. Defaults to 4.</summary>
    public int IndentSize
    {
        get => _indentSize;
        set { _indentSize = value; _explicitlySet.Add(nameof(IndentSize)); }
    }

    /// <summary>End-of-line style. <see cref="EndOfLine.Auto"/> means "preserve the dominant style in the source".</summary>
    public EndOfLine EndOfLine
    {
        get => _endOfLine;
        set { _endOfLine = value; _explicitlySet.Add(nameof(EndOfLine)); }
    }

    /// <summary>Trim trailing whitespace on each line. Defaults to <c>true</c>.</summary>
    public bool TrimTrailingWhitespace
    {
        get => _trimTrailingWhitespace;
        set { _trimTrailingWhitespace = value; _explicitlySet.Add(nameof(TrimTrailingWhitespace)); }
    }

    /// <summary>Ensure the file ends with a single newline. Defaults to <c>true</c>.</summary>
    public bool InsertFinalNewline
    {
        get => _insertFinalNewline;
        set { _insertFinalNewline = value; _explicitlySet.Add(nameof(InsertFinalNewline)); }
    }

    /// <summary>Toggle: indent children inside HTML tags.</summary>
    public bool FormatHtml
    {
        get => _formatHtml;
        set { _formatHtml = value; _explicitlySet.Add(nameof(FormatHtml)); }
    }

    /// <summary>Toggle: indent children inside @if/@for/@foreach/@while/@switch/@try/@lock blocks.</summary>
    public bool FormatRazorControlFlow
    {
        get => _formatRazorControlFlow;
        set { _formatRazorControlFlow = value; _explicitlySet.Add(nameof(FormatRazorControlFlow)); }
    }

    /// <summary>Toggle: indent inside @{ ... } and @functions { ... } blocks.</summary>
    public bool FormatCSharpBlocks
    {
        get => _formatCSharpBlocks;
        set { _formatCSharpBlocks = value; _explicitlySet.Add(nameof(FormatCSharpBlocks)); }
    }

    /// <summary>Toggle: indent children inside Tag Helpers / Razor Components (capitalised tag names).</summary>
    public bool FormatTagHelpers
    {
        get => _formatTagHelpers;
        set { _formatTagHelpers = value; _explicitlySet.Add(nameof(FormatTagHelpers)); }
    }

    /// <summary>Toggle: format content inside &lt;style&gt; and &lt;script&gt; blocks. Defaults to <c>true</c>.</summary>
    public bool FormatEmbeddedLanguages
    {
        get => _formatEmbeddedLanguages;
        set { _formatEmbeddedLanguages = value; _explicitlySet.Add(nameof(FormatEmbeddedLanguages)); }
    }

    /// <summary>Attribute layout strategy. Defaults to <see cref="AttributeLayout.Preserve"/>.</summary>
    public AttributeLayout AttributeLayout
    {
        get => _attributeLayout;
        set { _attributeLayout = value; _explicitlySet.Add(nameof(AttributeLayout)); }
    }

    /// <summary>Toggle: collapse multiple blank lines down to <see cref="MaxConsecutiveBlankLines"/>. Defaults to <c>true</c>.</summary>
    public bool CollapseMultipleBlankLines
    {
        get => _collapseMultipleBlankLines;
        set { _collapseMultipleBlankLines = value; _explicitlySet.Add(nameof(CollapseMultipleBlankLines)); }
    }

    /// <summary>Maximum number of consecutive blank lines allowed when collapsing. Defaults to 1.</summary>
    public int MaxConsecutiveBlankLines
    {
        get => _maxConsecutiveBlankLines;
        set { _maxConsecutiveBlankLines = value; _explicitlySet.Add(nameof(MaxConsecutiveBlankLines)); }
    }

    /// <summary>Policy for void element self-closing slashes (e.g. &lt;input /&gt; vs &lt;input&gt;). Defaults to <see cref="VoidElementSlashPolicy.Preserve"/>.</summary>
    public VoidElementSlashPolicy VoidElementSlash
    {
        get => _voidElementSlash;
        set { _voidElementSlash = value; _explicitlySet.Add(nameof(VoidElementSlash)); }
    }

    /// <summary>If true, the formatter is a no-op on files that encounter syntax/lexer errors (preserves the original source). Defaults to <c>true</c>.</summary>
    public bool SkipOnLexerError
    {
        get => _skipOnLexerError;
        set { _skipOnLexerError = value; _explicitlySet.Add(nameof(SkipOnLexerError)); }
    }

    public bool IsExplicitlySet(string propertyName) => _explicitlySet.Contains(propertyName);
    public void MarkExplicitlySet(string propertyName) => _explicitlySet.Add(propertyName);
    public void ClearExplicitlySet() => _explicitlySet.Clear();

    public string BuildIndentString(int level) => IndentStyle switch
    {
        IndentStyle.Space => new string(' ', level * IndentSize),
        IndentStyle.Tab => new string('\t', level),
        _ => new string(' ', level * IndentSize),
    };

    public RazorFormatterOptions Clone()
    {
        var clone = new RazorFormatterOptions
        {
            _filePath = _filePath,
            _indentStyle = _indentStyle,
            _indentSize = _indentSize,
            _endOfLine = _endOfLine,
            _trimTrailingWhitespace = _trimTrailingWhitespace,
            _insertFinalNewline = _insertFinalNewline,
            _formatHtml = _formatHtml,
            _formatRazorControlFlow = _formatRazorControlFlow,
            _formatCSharpBlocks = _formatCSharpBlocks,
            _formatTagHelpers = _formatTagHelpers,
            _formatEmbeddedLanguages = _formatEmbeddedLanguages,
            _attributeLayout = _attributeLayout,
            _collapseMultipleBlankLines = _collapseMultipleBlankLines,
            _maxConsecutiveBlankLines = _maxConsecutiveBlankLines,
            _voidElementSlash = _voidElementSlash,
            _skipOnLexerError = _skipOnLexerError,
        };

        foreach (var p in _explicitlySet)
        {
            clone._explicitlySet.Add(p);
        }

        return clone;
    }
}

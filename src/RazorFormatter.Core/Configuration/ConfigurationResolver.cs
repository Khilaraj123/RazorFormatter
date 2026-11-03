using System;
using System.Collections.Generic;
using System.IO;
using RazorFormatter.Core.Enums;

namespace RazorFormatter.Core.Configuration
{
    public static class ConfigurationResolver
    {
        public static RazorFormatterOptions Resolve(string? filePath, RazorFormatterOptions? explicitOptions = null)
        {
            var options = new RazorFormatterOptions();

            if (!string.IsNullOrEmpty(filePath))
            {
                options.FilePath = filePath;
                var configFiles = DiscoverConfigFiles(filePath);
                var mergedProperties = ResolveProperties(filePath, configFiles);
                ApplyProperties(options, mergedProperties);
            }

            if (explicitOptions != null)
            {
                MergeExplicitOptions(options, explicitOptions);
            }

            return options;
        }

        public static List<EditorConfigFile> DiscoverConfigFiles(string targetFilePath)
        {
            var files = new List<EditorConfigFile>();

            string? dir = null;
            try
            {
                if (File.Exists(targetFilePath) || Directory.Exists(targetFilePath))
                {
                    dir = File.Exists(targetFilePath)
                        ? Path.GetDirectoryName(Path.GetFullPath(targetFilePath))
                        : Path.GetFullPath(targetFilePath);
                }
                else
                {
                    // Path does not exist on disk, use directory name as given if possible
                    dir = Path.GetDirectoryName(targetFilePath);
                    if (string.IsNullOrEmpty(dir))
                        dir = Directory.GetCurrentDirectory();
                    else
                        dir = Path.GetFullPath(dir);
                }
            }
            catch
            {
                return files;
            }

            while (!string.IsNullOrEmpty(dir))
            {
                var configPath = Path.Combine(dir, ".editorconfig");
                if (File.Exists(configPath))
                {
                    try
                    {
                        var content = File.ReadAllText(configPath);
                        var parsed = EditorConfigParser.Parse(configPath, content);
                        files.Add(parsed);

                        if (parsed.IsRoot)
                        {
                            break;
                        }
                    }
                    catch
                    {
                        // Ignore unreadable config files
                    }
                }

                var parent = Directory.GetParent(dir);
                if (parent == null || parent.FullName == dir)
                {
                    break;
                }
                dir = parent.FullName;
            }

            // Reverse so that root-most files are processed first, and closest files override them
            files.Reverse();
            return files;
        }

        public static Dictionary<string, string> ResolveProperties(string targetFilePath, IEnumerable<EditorConfigFile> configFiles)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var normalizedTarget = Path.GetFullPath(targetFilePath).Replace('\\', '/');

            foreach (var configFile in configFiles)
            {
                var configDir = Path.GetFullPath(configFile.DirectoryPath).Replace('\\', '/');
                string relativePath;

                if (normalizedTarget.StartsWith(configDir, StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = normalizedTarget.Substring(configDir.Length).TrimStart('/');
                }
                else
                {
                    relativePath = Path.GetFileName(normalizedTarget);
                }

                foreach (var section in configFile.Sections)
                {
                    if (EditorConfigGlobMatcher.IsMatch(section.GlobPattern, relativePath))
                    {
                        foreach (var kvp in section.Properties)
                        {
                            result[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }

            return result;
        }

        public static void ApplyProperties(RazorFormatterOptions options, IReadOnlyDictionary<string, string> properties)
        {
            int? tabWidth = null;

            if (properties.TryGetValue("tab_width", out var tabWidthStr) && int.TryParse(tabWidthStr, out var tw))
            {
                tabWidth = tw;
            }

            if (properties.TryGetValue("indent_style", out var indentStyle))
            {
                if (string.Equals(indentStyle, "tab", StringComparison.OrdinalIgnoreCase))
                {
                    options.IndentStyle = IndentStyle.Tab;
                    if (tabWidth.HasValue) options.IndentSize = tabWidth.Value;
                }
                else if (string.Equals(indentStyle, "space", StringComparison.OrdinalIgnoreCase))
                {
                    options.IndentStyle = IndentStyle.Space;
                }
            }

            if (properties.TryGetValue("indent_size", out var indentSizeStr))
            {
                if (string.Equals(indentSizeStr, "tab", StringComparison.OrdinalIgnoreCase))
                {
                    if (tabWidth.HasValue) options.IndentSize = tabWidth.Value;
                }
                else if (int.TryParse(indentSizeStr, out var isize) && isize > 0)
                {
                    options.IndentSize = isize;
                }
            }

            if (properties.TryGetValue("end_of_line", out var eol))
            {
                if (string.Equals(eol, "lf", StringComparison.OrdinalIgnoreCase))
                    options.EndOfLine = EndOfLine.Lf;
                else if (string.Equals(eol, "crlf", StringComparison.OrdinalIgnoreCase))
                    options.EndOfLine = EndOfLine.Crlf;
                else if (string.Equals(eol, "cr", StringComparison.OrdinalIgnoreCase))
                    options.EndOfLine = EndOfLine.Crlf;
            }

            if (properties.TryGetValue("insert_final_newline", out var ifn) && bool.TryParse(ifn, out var ifnVal))
            {
                options.InsertFinalNewline = ifnVal;
            }

            if (properties.TryGetValue("trim_trailing_whitespace", out var ttw) && bool.TryParse(ttw, out var ttwVal))
            {
                options.TrimTrailingWhitespace = ttwVal;
            }

            // Razor-specific properties
            if (TryGetProperty(properties, out var attrLayout, "razor_attribute_layout", "razor_html_attribute_layout", "razor_wrap_attributes"))
            {
                var lower = attrLayout.ToLowerInvariant().Replace("-", "_");
                if (lower is "one_per_line" or "oneperline")
                    options.AttributeLayout = AttributeLayout.OnePerLine;
                else if (lower is "single_line" or "singleline")
                    options.AttributeLayout = AttributeLayout.SingleLine;
                else if (lower == "preserve")
                    options.AttributeLayout = AttributeLayout.Preserve;
            }

            if (TryGetBoolProperty(properties, out var formatHtml, "razor_format_html"))
                options.FormatHtml = formatHtml;

            if (TryGetBoolProperty(properties, out var formatTagHelpers, "razor_format_tag_helpers"))
                options.FormatTagHelpers = formatTagHelpers;

            if (TryGetBoolProperty(properties, out var formatControlFlow, "razor_format_control_flow", "razor_format_razor_control_flow"))
                options.FormatRazorControlFlow = formatControlFlow;

            if (TryGetBoolProperty(properties, out var formatCSharp, "razor_format_csharp_blocks", "razor_format_csharp"))
                options.FormatCSharpBlocks = formatCSharp;

            if (TryGetBoolProperty(properties, out var formatEmbedded, "razor_format_embedded_languages"))
                options.FormatEmbeddedLanguages = formatEmbedded;

            if (TryGetBoolProperty(properties, out var collapseBlank, "razor_collapse_multiple_blank_lines"))
                options.CollapseMultipleBlankLines = collapseBlank;

            if (TryGetIntProperty(properties, out var maxBlank, "razor_max_blank_lines", "razor_max_consecutive_blank_lines", "max_blank_lines"))
                options.MaxConsecutiveBlankLines = maxBlank;

            if (TryGetProperty(properties, out var voidSlash, "razor_void_element_slash", "razor_html_void_element_slash"))
            {
                var lower = voidSlash.ToLowerInvariant().Replace("-", "_");
                if (lower is "slash" or "always" or "self_close")
                    options.VoidElementSlash = VoidElementSlashPolicy.Slash;
                else if (lower is "no_slash" or "noslash" or "never" or "omit")
                    options.VoidElementSlash = VoidElementSlashPolicy.NoSlash;
                else if (lower == "preserve")
                    options.VoidElementSlash = VoidElementSlashPolicy.Preserve;
            }

            if (TryGetBoolProperty(properties, out var skipError, "razor_skip_on_lexer_error"))
                options.SkipOnLexerError = skipError;
        }

        private static void MergeExplicitOptions(RazorFormatterOptions target, RazorFormatterOptions explicitOptions)
        {
            if (explicitOptions.IsExplicitlySet(nameof(RazorFormatterOptions.IndentStyle)))
                target.IndentStyle = explicitOptions.IndentStyle;

            if (explicitOptions.IsExplicitlySet(nameof(RazorFormatterOptions.IndentSize)))
                target.IndentSize = explicitOptions.IndentSize;

            if (explicitOptions.IsExplicitlySet(nameof(RazorFormatterOptions.EndOfLine)))
                target.EndOfLine = explicitOptions.EndOfLine;

            if (explicitOptions.IsExplicitlySet(nameof(RazorFormatterOptions.TrimTrailingWhitespace)))
                target.TrimTrailingWhitespace = explicitOptions.TrimTrailingWhitespace;

            if (explicitOptions.IsExplicitlySet(nameof(RazorFormatterOptions.InsertFinalNewline)))
                target.InsertFinalNewline = explicitOptions.InsertFinalNewline;

            if (explicitOptions.IsExplicitlySet(nameof(RazorFormatterOptions.FormatHtml)))
                target.FormatHtml = explicitOptions.FormatHtml;

            if (explicitOptions.IsExplicitlySet(nameof(RazorFormatterOptions.FormatRazorControlFlow)))
                target.FormatRazorControlFlow = explicitOptions.FormatRazorControlFlow;

            if (explicitOptions.IsExplicitlySet(nameof(RazorFormatterOptions.FormatCSharpBlocks)))
                target.FormatCSharpBlocks = explicitOptions.FormatCSharpBlocks;

            if (explicitOptions.IsExplicitlySet(nameof(RazorFormatterOptions.FormatTagHelpers)))
                target.FormatTagHelpers = explicitOptions.FormatTagHelpers;

            if (explicitOptions.IsExplicitlySet(nameof(RazorFormatterOptions.FormatEmbeddedLanguages)))
                target.FormatEmbeddedLanguages = explicitOptions.FormatEmbeddedLanguages;

            if (explicitOptions.IsExplicitlySet(nameof(RazorFormatterOptions.AttributeLayout)))
                target.AttributeLayout = explicitOptions.AttributeLayout;

            if (explicitOptions.IsExplicitlySet(nameof(RazorFormatterOptions.CollapseMultipleBlankLines)))
                target.CollapseMultipleBlankLines = explicitOptions.CollapseMultipleBlankLines;

            if (explicitOptions.IsExplicitlySet(nameof(RazorFormatterOptions.MaxConsecutiveBlankLines)))
                target.MaxConsecutiveBlankLines = explicitOptions.MaxConsecutiveBlankLines;

            if (explicitOptions.IsExplicitlySet(nameof(RazorFormatterOptions.VoidElementSlash)))
                target.VoidElementSlash = explicitOptions.VoidElementSlash;

            if (explicitOptions.IsExplicitlySet(nameof(RazorFormatterOptions.SkipOnLexerError)))
                target.SkipOnLexerError = explicitOptions.SkipOnLexerError;

            if (!string.IsNullOrEmpty(explicitOptions.FilePath))
                target.FilePath = explicitOptions.FilePath;
        }

        private static bool TryGetProperty(IReadOnlyDictionary<string, string> properties, out string value, params string[] keys)
        {
            for (var i = 0; i < keys.Length; i++)
            {
                if (properties.TryGetValue(keys[i], out var v) && !string.IsNullOrWhiteSpace(v))
                {
                    value = v;
                    return true;
                }
            }
            value = string.Empty;
            return false;
        }

        private static bool TryGetBoolProperty(IReadOnlyDictionary<string, string> properties, out bool value, params string[] keys)
        {
            if (TryGetProperty(properties, out var v, keys) && bool.TryParse(v, out var b))
            {
                value = b;
                return true;
            }
            value = false;
            return false;
        }

        private static bool TryGetIntProperty(IReadOnlyDictionary<string, string> properties, out int value, params string[] keys)
        {
            if (TryGetProperty(properties, out var v, keys) && int.TryParse(v, out var n))
            {
                value = n;
                return true;
            }
            value = 0;
            return false;
        }
    }
}


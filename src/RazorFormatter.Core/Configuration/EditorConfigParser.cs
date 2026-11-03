using System;
using System.Collections.Generic;
using System.IO;

namespace RazorFormatter.Core.Configuration
{
    public static class EditorConfigParser
    {
        public static EditorConfigFile Parse(string filePath, string content)
        {
            var dir = Path.GetDirectoryName(filePath) ?? string.Empty;
            var isRoot = false;
            var sections = new List<EditorConfigSection>();

            string? currentPattern = null;
            var currentProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using var reader = new StringReader(content);
            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.StartsWith(";", StringComparison.Ordinal))
                    continue;

                // Strip inline comment if preceded by whitespace
                var commentIdx = FindInlineComment(trimmed);
                if (commentIdx >= 0)
                {
                    trimmed = trimmed.Substring(0, commentIdx).Trim();
                    if (trimmed.Length == 0) continue;
                }

                // Check for section header: [pattern]
                if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
                {
                    if (currentPattern != null)
                    {
                        sections.Add(new EditorConfigSection(currentPattern, new Dictionary<string, string>(currentProperties, StringComparer.OrdinalIgnoreCase)));
                        currentProperties.Clear();
                    }

                    currentPattern = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    continue;
                }

                // Check for key = value
                var eqIdx = trimmed.IndexOf('=');
                if (eqIdx > 0)
                {
                    var key = trimmed.Substring(0, eqIdx).Trim().ToLowerInvariant();
                    var val = trimmed.Substring(eqIdx + 1).Trim();

                    if (currentPattern == null)
                    {
                        // Preamble before any section
                        if (key == "root")
                        {
                            isRoot = bool.TryParse(val, out var r) && r;
                        }
                    }
                    else
                    {
                        currentProperties[key] = val;
                    }
                }
            }

            if (currentPattern != null)
            {
                sections.Add(new EditorConfigSection(currentPattern, new Dictionary<string, string>(currentProperties, StringComparer.OrdinalIgnoreCase)));
            }

            return new EditorConfigFile(filePath, dir, isRoot, sections);
        }

        private static int FindInlineComment(string line)
        {
            for (var i = 1; i < line.Length; i++)
            {
                if ((line[i] == '#' || line[i] == ';') && char.IsWhiteSpace(line[i - 1]))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}


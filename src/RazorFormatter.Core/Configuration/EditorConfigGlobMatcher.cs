using System;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace RazorFormatter.Core.Configuration
{
    /// <summary>
    /// Matches file paths against EditorConfig section glob patterns.
    /// Supports: *, **, ?, [seq], [!seq], {s1,s2,s3}.
    /// Includes ReDoS timeout protection and thread-safe regex caching.
    /// </summary>
    public static class EditorConfigGlobMatcher
    {
        private static readonly TimeSpan s_regexTimeout = TimeSpan.FromMilliseconds(250);
        private static readonly ConcurrentDictionary<string, Regex> s_cache = new(StringComparer.Ordinal);

        public static bool IsMatch(string pattern, string relativeFilePath)
        {
            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(relativeFilePath))
                return false;

            if (pattern == "*")
                return true;

            var normalizedPath = relativeFilePath.Replace('\\', '/').TrimStart('/');
            var fileName = System.IO.Path.GetFileName(normalizedPath);

            try
            {
                // If the pattern doesn't contain a slash, match against the filename only
                if (!pattern.Contains('/'))
                {
                    var regex = GetOrCompileRegex(pattern);
                    return regex.IsMatch(fileName) || regex.IsMatch(normalizedPath);
                }

                // Pattern contains a slash: match against relative path from the .editorconfig directory
                var patternToMatch = pattern.StartsWith("/", StringComparison.Ordinal)
                    ? pattern.Substring(1)
                    : pattern;

                var fullRegex = GetOrCompileRegex(patternToMatch);
                return fullRegex.IsMatch(normalizedPath);
            }
            catch (RegexMatchTimeoutException)
            {
                // ReDoS protection: abort match if computation exceeds 250ms
                return false;
            }
            catch (ArgumentException)
            {
                // Malformed glob pattern in .editorconfig
                return false;
            }
        }

        public static Regex GetOrCompileRegex(string glob)
        {
            return s_cache.GetOrAdd(glob, g => GlobToRegex(g));
        }

        public static Regex GlobToRegex(string glob)
        {
            var sb = new StringBuilder();
            sb.Append('^');

            var i = 0;
            while (i < glob.Length)
            {
                var c = glob[i];
                switch (c)
                {
                    case '*':
                        if (i + 1 < glob.Length && glob[i + 1] == '*')
                        {
                            // ** matches across directory boundaries
                            sb.Append(".*");
                            i += 2;
                            // Skip following slash if present to avoid matching double slashes
                            if (i < glob.Length && glob[i] == '/')
                            {
                                sb.Append("(?:/)?");
                                i++;
                            }
                        }
                        else
                        {
                            // * matches anything except path separator /
                            sb.Append("[^/]*");
                            i++;
                        }
                        break;

                    case '?':
                        sb.Append("[^/]");
                        i++;
                        break;

                    case '{':
                        // Brace expansion: {cshtml,razor} -> (?:cshtml|razor)
                        var closeBrace = FindMatchingBrace(glob, i);
                        if (closeBrace > i)
                        {
                            var inner = glob.Substring(i + 1, closeBrace - i - 1);
                            var choices = inner.Split(',');
                            sb.Append("(?:");
                            for (var j = 0; j < choices.Length; j++)
                            {
                                if (j > 0) sb.Append('|');
                                sb.Append(Regex.Escape(choices[j].Trim()));
                            }
                            sb.Append(')');
                            i = closeBrace + 1;
                        }
                        else
                        {
                            sb.Append(Regex.Escape("{"));
                            i++;
                        }
                        break;

                    case '[':
                        var closeBracket = glob.IndexOf(']', i);
                        if (closeBracket > i)
                        {
                            var inner = glob.Substring(i + 1, closeBracket - i - 1);
                            if (inner.Length == 0)
                            {
                                sb.Append("\\[\\]");
                            }
                            else if (inner.StartsWith("!", StringComparison.Ordinal))
                            {
                                sb.Append("[^").Append(Regex.Escape(inner.Substring(1))).Append(']');
                            }
                            else
                            {
                                sb.Append('[').Append(Regex.Escape(inner)).Append(']');
                            }
                            i = closeBracket + 1;
                        }
                        else
                        {
                            sb.Append(Regex.Escape("["));
                            i++;
                        }
                        break;

                    case '\\':
                    case '.':
                    case '$':
                    case '^':
                    case '+':
                    case '(':
                    case ')':
                    case '|':
                        sb.Append('\\').Append(c);
                        i++;
                        break;

                    default:
                        sb.Append(c);
                        i++;
                        break;
                }
            }

            sb.Append('$');
            return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, s_regexTimeout);
        }

        private static int FindMatchingBrace(string s, int openIndex)
        {
            var depth = 0;
            for (var i = openIndex; i < s.Length; i++)
            {
                if (s[i] == '{') depth++;
                else if (s[i] == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }
    }
}

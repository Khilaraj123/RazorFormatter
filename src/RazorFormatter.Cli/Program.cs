using System.Reflection;
using System.Text.Json;
using RazorFormatter.Core;
using RazorFormatter.Core.Configuration;
using RazorFormatter.Core.Enums;

namespace RazorFormatter.Cli
{
    public static class Program
    {
        public const int ExitSuccess = 0;
        public const int ExitDifferencesFound = 1;
        public const int ExitInvalidArguments = 2;
        public const int ExitFileOrFormatError = 3;

        public static int Main(string[] args)
        {
            if (args.Length > 0 && (args[0] == "-v" || args[0] == "--version"))
            {
                PrintVersion();
                return ExitSuccess;
            }

            if (args.Length > 0 && (args[0] == "-h" || args[0] == "--help"))
            {
                PrintHelp();
                return ExitSuccess;
            }

            var options = new RazorFormatterOptions();
            var checkMode = false;
            var diffMode = false;
            var forceStdout = false;
            var verbose = false;
            var changesJson = false;
            var targets = new List<string>();
            string? explicitConfigFile = null;
            var ignoreMatcher = new RazorIgnoreMatcher();

            var startIndex = 0;

            // Check for subcommands: "check", "diff"
            if (args.Length > 0 && !args[0].StartsWith("-"))
            {
                var cmd = args[0].ToLowerInvariant();
                if (cmd == "check")
                {
                    checkMode = true;
                    startIndex = 1;
                }
                else if (cmd == "diff")
                {
                    diffMode = true;
                    startIndex = 1;
                }
            }

            for (var i = startIndex; i < args.Length; i++)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "--server":
                        return new Server.FormatterServer().Run();
                    case "--changes-json":
                        changesJson = true;
                        break;
                    case "-c":
                    case "--check":
                        checkMode = true;
                        break;
                    case "-d":
                    case "--diff":
                        diffMode = true;
                        break;
                    case "-w":
                    case "--write":
                        // In-place is default, but keep flag for explicit compatibility
                        forceStdout = false;
                        break;
                    case "--stdout":
                        forceStdout = true;
                        break;
                    case "--verbose":
                        verbose = true;
                        break;
                    case "--tab":
                        options.IndentStyle = IndentStyle.Tab;
                        break;
                    case "--indent":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var indent))
                        {
                            options.IndentSize = indent;
                        }
                        else
                        {
                            Console.Error.WriteLine("Error: --indent requires an integer value.");
                            return ExitInvalidArguments;
                        }
                        break;
                    case "--lf":
                        options.EndOfLine = EndOfLine.Lf;
                        break;
                    case "--crlf":
                        options.EndOfLine = EndOfLine.Crlf;
                        break;
                    case "--attribute-layout":
                        if (i + 1 < args.Length)
                        {
                            var layoutStr = args[++i].ToLowerInvariant().Replace("-", "_");
                            if (layoutStr is "one_per_line" or "oneperline")
                                options.AttributeLayout = AttributeLayout.OnePerLine;
                            else if (layoutStr is "single_line" or "singleline")
                                options.AttributeLayout = AttributeLayout.SingleLine;
                            else if (layoutStr == "preserve")
                                options.AttributeLayout = AttributeLayout.Preserve;
                            else
                            {
                                Console.Error.WriteLine($"Error: Unknown attribute layout '{layoutStr}'. Expected: preserve, one-per-line, single-line.");
                                return ExitInvalidArguments;
                            }
                        }
                        else
                        {
                            Console.Error.WriteLine("Error: --attribute-layout requires a value.");
                            return ExitInvalidArguments;
                        }
                        break;
                    case "--config":
                        if (i + 1 < args.Length)
                        {
                            explicitConfigFile = args[++i];
                            if (!File.Exists(explicitConfigFile))
                            {
                                Console.Error.WriteLine($"Error: Configuration file not found: {explicitConfigFile}");
                                return ExitInvalidArguments;
                            }
                        }
                        else
                        {
                            Console.Error.WriteLine("Error: --config requires a file path.");
                            return ExitInvalidArguments;
                        }
                        break;
                    case "--ignore":
                        if (i + 1 < args.Length)
                        {
                            ignoreMatcher.AddPattern(args[++i]);
                        }
                        break;
                    case "-v":
                    case "--version":
                        PrintVersion();
                        return ExitSuccess;
                    case "-h":
                    case "--help":
                        PrintHelp();
                        return ExitSuccess;
                    default:
                        if (arg.StartsWith("-"))
                        {
                            Console.Error.WriteLine($"Error: Unknown option '{arg}'. Use --help for usage.");
                            return ExitInvalidArguments;
                        }
                        targets.Add(arg);
                        break;
                }
            }

            // Load .razorignore from current directory
            ignoreMatcher.DiscoverAndLoad(Directory.GetCurrentDirectory());
            var engine = RazorFormatterFactory.Create();

            // Stdin mode (no targets provided, or single '-' target)
            if (targets.Count == 0 || (targets.Count == 1 && targets[0] == "-"))
            {
                // If console input is not redirected, show help
                if (!Console.IsInputRedirected && targets.Count == 0)
                {
                    PrintHelp();
                    return ExitSuccess;
                }

                var input = Console.In.ReadToEnd();
                if (string.IsNullOrEmpty(input))
                {
                    return ExitSuccess;
                }

                RazorFormatterOptions effectiveOptions = options;
                if (!string.IsNullOrEmpty(explicitConfigFile) && File.Exists(explicitConfigFile))
                {
                    var parsed = EditorConfigParser.Parse(explicitConfigFile, File.ReadAllText(explicitConfigFile));
                    var props = ConfigurationResolver.ResolveProperties("stdin.cshtml", new[] { parsed });
                    effectiveOptions = options.Clone();
                    ConfigurationResolver.ApplyProperties(effectiveOptions, props);
                }

                var result = engine.Format(input, effectiveOptions);
                if (checkMode)
                {
                    return result.Changes.Count > 0 ? ExitDifferencesFound : ExitSuccess;
                }

                if (diffMode)
                {
                    if (result.Changes.Count > 0)
                    {
                        Console.Out.Write(DiffHelper.CreateUnifiedDiff("stdin", input, result.FormattedText));
                        return ExitDifferencesFound;
                    }
                    return ExitSuccess;
                }

                if (changesJson)
                {
                    var json = JsonSerializer.Serialize(new
                    {
                        wasSkipped = result.WasSkipped,
                        formattedText = result.FormattedText,
                        changes = result.Changes,
                        diagnostics = result.Diagnostics
                    });
                    Console.Out.Write(json);
                    return ExitSuccess;
                }

                Console.Out.Write(result.FormattedText);
                return ExitSuccess;
            }

            // Target files and directories mode
            var hasDifferences = false;
            var processedCount = 0;
            var modifiedCount = 0;

            foreach (var target in targets)
            {
                string[] matchedFiles;
                if (Directory.Exists(target))
                {
                    try
                    {
                        matchedFiles = Directory.EnumerateFiles(target, "*.*", SearchOption.AllDirectories)
                            .Where(f => f.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase) ||
                                        f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
                            .ToArray();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error scanning directory '{target}': {ex.Message}");
                        return ExitFileOrFormatError;
                    }
                }
                else if (File.Exists(target))
                {
                    matchedFiles = new[] { target };
                }
                else
                {
                    Console.Error.WriteLine($"File or directory not found: {target}");
                    return ExitFileOrFormatError;
                }

                foreach (var filePath in matchedFiles)
                {
                    if (ignoreMatcher.IsIgnored(filePath))
                    {
                        if (verbose) Console.Error.WriteLine($"[Ignored] {filePath}");
                        continue;
                    }

                    processedCount++;
                    FormatResult result;
                    string originalContent;

                    try
                    {
                        originalContent = File.ReadAllText(filePath);
                        if (!string.IsNullOrEmpty(explicitConfigFile) && File.Exists(explicitConfigFile))
                        {
                            var parsed = EditorConfigParser.Parse(explicitConfigFile, File.ReadAllText(explicitConfigFile));
                            var props = ConfigurationResolver.ResolveProperties(filePath, new[] { parsed });
                            var effectiveOptions = options.Clone();
                            effectiveOptions.FilePath = filePath;
                            ConfigurationResolver.ApplyProperties(effectiveOptions, props);
                            result = engine.Format(originalContent, effectiveOptions);
                        }
                        else
                        {
                            result = engine.FormatFile(filePath, options);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error processing file '{filePath}': {ex.Message}");
                        return ExitFileOrFormatError;
                    }

                    if (result.Changes.Count > 0)
                    {
                        hasDifferences = true;

                        if (checkMode)
                        {
                            Console.Error.WriteLine($"Needs formatting: {filePath}");
                        }
                        else if (diffMode)
                        {
                            Console.Out.Write(DiffHelper.CreateUnifiedDiff(filePath, originalContent, result.FormattedText));
                        }
                        else if (forceStdout)
                        {
                            Console.Out.Write(result.FormattedText);
                        }
                        else
                        {
                            // In-place modification by default (gofmt-style)
                            try
                            {
                                File.WriteAllText(filePath, result.FormattedText);
                                modifiedCount++;
                                if (verbose) Console.WriteLine($"Formatted: {filePath}");
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine($"Failed to write '{filePath}': {ex.Message}");
                                return ExitFileOrFormatError;
                            }
                        }
                    }
                    else
                    {
                        if (verbose) Console.WriteLine($"Clean: {filePath}");
                    }
                }
            }

            if (checkMode || diffMode)
            {
                return hasDifferences ? ExitDifferencesFound : ExitSuccess;
            }

            if (!forceStdout && verbose)
            {
                Console.WriteLine($"RazorFormatter: checked {processedCount} file(s), formatted {modifiedCount} file(s).");
            }

            return ExitSuccess;
        }

        private static void PrintVersion()
        {
            var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(Program).Assembly.GetName().Version?.ToString()
                ?? "1.0.0";
            Console.WriteLine($"razorfmt {version}");
        }

        private static void PrintHelp()
        {
            Console.WriteLine("razorfmt - Deterministic, Prettier-grade code formatter for ASP.NET Core Razor (.cshtml/.razor)");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  razorfmt [options] <files/folders...>       Format files in place");
            Console.WriteLine("  razorfmt check [options] <files/folders...> Check formatting without modifying files");
            Console.WriteLine("  razorfmt diff [options] <files/folders...>  Display formatting differences (unified diff)");
            Console.WriteLine("  cat file.cshtml | razorfmt                  Format standard input to standard output");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  check                       Check files and exit with code 1 if unformatted");
            Console.WriteLine("  diff                        Print unified diff of unformatted files and exit with code 1");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -c, --check                 Same as 'check' subcommand");
            Console.WriteLine("  -d, --diff                  Same as 'diff' subcommand");
            Console.WriteLine("  -w, --write                 Format in-place (default for file/directory paths)");
            Console.WriteLine("      --stdout                Print formatted text to stdout instead of overwriting");
            Console.WriteLine("      --server                Start persistent stdio JSON-RPC formatting server");
            Console.WriteLine("      --changes-json          Output discrete text changes and diagnostics as JSON");
            Console.WriteLine("      --verbose               Display detailed file processing information");
            Console.WriteLine("      --tab                   Use tabs for indentation (default: spaces)");
            Console.WriteLine("      --indent <n>            Number of spaces per indentation level (default: 4)");
            Console.WriteLine("      --lf                    Enforce Unix line endings (LF)");
            Console.WriteLine("      --crlf                  Enforce Windows line endings (CRLF)");
            Console.WriteLine("      --attribute-layout <l>  Attribute layout: preserve, one-per-line, single-line");
            Console.WriteLine("      --config <path>         Path to explicit .editorconfig file");
            Console.WriteLine("      --ignore <pattern>      Pattern to ignore (can be repeated)");
            Console.WriteLine("  -v, --version               Display version information");
            Console.WriteLine("  -h, --help                  Display this help message");
            Console.WriteLine();
            Console.WriteLine("Exit Codes:");
            Console.WriteLine("  0                           Success; no formatting issues found or files formatted");
            Console.WriteLine("  1                           Formatting differences found in check/diff mode");
            Console.WriteLine("  2                           Invalid arguments or configuration");
            Console.WriteLine("  3                           File access or formatting error");
        }
    }
}

# RazorFormatter

[![Build & Test](https://img.shields.io/badge/tests-124%20passed-brightgreen.svg)](#running-tests)
[![VS Code Marketplace](https://img.shields.io/badge/VS%20Code-Marketplace-blue.svg?logo=visualstudiocode)](https://marketplace.visualstudio.com/items?itemName=razor-formatter.razor-code-formatter)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](#contributing--license)

**RazorFormatter** is a fast, deterministic, Prettier-grade code formatter for ASP.NET Core Razor (`.cshtml` and `.razor`) files. It unifies HTML semantic structure, Razor block directives, embedded CSS/JavaScript, and C# control flow into an idempotent, high-performance formatting pipeline.

Designed as an extensible monorepo, RazorFormatter powers:
- **CLI & CI workflows** via `RazorFormatter.Cli`
- **VS Code, Cursor, and Antigravity** via `RazorFormatter.VSCode`
- **Visual Studio** via `RazorFormatter.VisualStudio`
- **Language Servers & IDE IPC** via `--server` (stdio JSON-RPC)
- **Embedded .NET applications** via `RazorFormatter.Core`

---

## Key Features

- **Semantic Mixed-Language Lexing**: Dedicated scanners (`RazorLexer`, `HtmlLexer`, `CSharpLexer`) parse Razor syntax, C# expressions, strings, raw string literals, HTML markup, and comments with zero gaps and zero overlaps.
- **C# Control Flow & Code Blocks**: Cleanly indents `@if`, `@else if`, `@else`, `@foreach`, `@for`, `@while`, `@switch`, `@try`, `@catch`, `@finally`, `@lock`, `@using`, `@code { ... }`, and `@{ ... }` blocks.
- **Embedded CSS & JavaScript Formatting**: Accurately handles embedded `<script>` and `<style>` blocks without premature termination or string delimiter corruption.
- **Attribute Wrapping Policies**: Supports `auto`, `preserve`, and `always` attribute wrapping modes, keeping multiline HTML tags and Razor Tag Helpers neatly aligned.
- **EditorConfig & Ignore Hierarchy**: Automatically traverses directories to resolve `.editorconfig` hierarchy with ReDoS-safe glob matching, and respects `.razorignore` ignore lists.
- **Two-Stage Localized Diffing**: Generates discrete, line-localized `TextChange` records rather than whole-buffer replacements, preserving editor cursor positions and undo history.
- **High-Performance Pipeline**: Memory-efficient `SourceText` line indexing processes over 50,000 lines per second.
- **Persistent IPC Server Mode**: The CLI provides a stdio JSON-RPC server (`--server`) for low-latency editor formatting.
- **100% Idempotent**: Formatting an already formatted document emits **0 text changes**.
- **Security Hardened**: Built-in ReDoS timeouts (250ms) with regex caching, safe workspace trust enforcement in VS Code, and shell-free process spawning (`shell: false`).

---

## Monorepo Architecture

```
RazorFormatter/
├── src/
│   ├── RazorFormatter.Core/          # Reusable formatting engine & syntax analysis
│   │   ├── Abstractions/             # IRazorFormatter public interface
│   │   ├── Configuration/            # ConfigurationResolver, EditorConfigParser, GlobMatcher
│   │   ├── Enums/                    # RazorSyntaxKind, IndentStyle, EndOfLine, AttributeWrapPolicy
│   │   ├── Formatting/               # IndentationEngine, FormattingWriter, LineWalker
│   │   ├── internals/                # SourceText, TextChangeApplier, Diagnostic helpers
│   │   ├── Lexing/                   # RazorLexer, HtmlLexer, CSharpLexer, RazorSyntaxLexer
│   │   ├── FormattingEngine.cs       # Pipeline coordinator & localized edit generator
│   │   ├── RazorFormatterFactory.cs  # Formatter factory entry point
│   │   ├── RazorFormatterOptions.cs  # Engine configuration options
│   │   └── TextChange.cs             # Insert, Replace, Delete edit model
│   │
│   ├── RazorFormatter.Cli/           # Command-line tool, CI runner & IPC server
│   │   ├── Server/                   # FormatterServer stdio JSON-RPC implementation
│   │   └── Program.cs                # CLI argument parsing and execution
│   │
│   ├── RazorFormatter.VSCode/        # VS Code / Cursor / Antigravity extension
│   │   ├── src/extension.ts          # Extension activation & command dispatch
│   │   ├── src/formatter.ts          # Server & one-shot CLI formatting bridge
│   │   ├── src/resolver.ts           # Safe executable & bundle path resolver
│   │   ├── package.json              # Extension manifest & machine-scoped configuration
│   │   └── tsconfig.json             # TypeScript compiler configuration
│   │
│   └── RazorFormatter.VisualStudio/  # Visual Studio IDE extension
│       ├── RazorFormatterPackage.cs  # VSIX package initializer
│       └── source.extension.vsixmanifest
│
├── tests/
│   └── RazorFormatter.Tests/         # 124 unit, structural, regression, and fixture tests
│       ├── Fixtures/                 # Golden-file paired test fixtures
│       ├── FixtureTests.cs           # Fixture regression suite
│       ├── FormattingTests.cs        # Indentation & option behavior tests
│       ├── IdempotencyTests.cs       # Zero-diff second pass verification
│       ├── InstructionRequirementTests.cs # Specification compliance tests
│       ├── LexingTests.cs            # Lexing, gaps/overlaps, and string handling
│       ├── StructuralTests.cs        # Block structure, nesting, and control flow
│       ├── AttributeAndEmbeddedTests.cs # Attributes, JS, and CSS formatting
│       ├── ConfigurationTests.cs     # EditorConfig & ignore matching
│       ├── PerformanceTests.cs       # Benchmark throughput & streaming tests
│       ├── SecurityAndBugRegressionTests.cs # Security & logic defect regressions
│       ├── RazorSyntaxTests.cs       # Syntax kind and token span tests
│       └── TextChangeApplierTests.cs # Edit range & boundary tests
│
├── samples/                          # Sample Razor test documents
│   ├── Basic.cshtml                  # Standard HTML5 + Razor markup
│   ├── ControlFlow.cshtml            # Nested control flow & loops
│   ├── TagHelpers.cshtml             # Tag Helpers and component syntax
│   └── NestedExpressions.cshtml      # Explicit/implicit expressions & attributes
│
├── .editorconfig                     # Repository code style definitions
├── .gitignore                        # Git ignore patterns (.NET, Node, IDE)
├── RazorFormatter.slnx               # Modern solution definition
└── README.md                         # Project documentation
```

---

## Installation & Getting Started

### 1. .NET Global Tool (CLI)
Install globally via NuGet to format Razor files directly from terminal or CI/CD pipelines:
```bash
# Install globally
dotnet tool install -g RazorCodeFormatter

# Format files or directories in-place
razor-format -w ./Views

# Check if any files require formatting (CI mode)
razor-format --check ./Pages
```

### 2. VS Code, Cursor, and Google Antigravity
Install directly from the [VS Code Marketplace](https://marketplace.visualstudio.com/items?itemName=razor-formatter.razor-code-formatter) by searching for **Razor Formatter** (`razor-code-formatter`), or install via terminal:
```bash
code --install-extension razor-formatter.razor-code-formatter
```

### 3. Visual Studio 2022
Download the extension from the [Visual Studio Marketplace](https://marketplace.visualstudio.com/manage/publishers/razor-formatter) or install the compiled `.vsix` from our [GitHub Releases](https://github.com/Khilaraj123/RazorFormatter/releases).

---

## Documentation

- 📊 **[Benchmark & Performance Report](docs/BENCHMARKS.md)**: Throughput metrics (>50k lines/sec), daemon latency (<1ms), and 100% idempotency verification.
- 📘 **[Compatibility & Syntax Guide](docs/COMPATIBILITY.md)**: Supported Razor directives, Tag Helpers, embedded CSS/JS, and IDE matrix.

---

## CLI Usage

The CLI formats `.cshtml` and `.razor` files in-place, validates files in CI/CD pipelines, streams across standard I/O, or acts as a persistent language server.

### Basic Commands

```bash
# Print formatted output to stdout
dotnet run --project src/RazorFormatter.Cli -- ./samples/Basic.cshtml

# Format files or directories in-place (-w / --write)
dotnet run --project src/RazorFormatter.Cli -- -w ./samples

# Check if files need formatting (-c / --check, returns exit code 1 if unformatted)
dotnet run --project src/RazorFormatter.Cli -- --check ./samples

# Format standard input to standard output
cat ./samples/Basic.cshtml | dotnet run --project src/RazorFormatter.Cli

# Output machine-readable JSON changes
dotnet run --project src/RazorFormatter.Cli -- --changes-json ./samples/Basic.cshtml

# Specify an explicit .editorconfig file
dotnet run --project src/RazorFormatter.Cli -- --config .editorconfig -w ./samples

# Launch persistent stdio JSON-RPC server (used by editor extensions)
dotnet run --project src/RazorFormatter.Cli -- --server
```

### CLI Options

| Option | Description |
|---|---|
| `-w`, `--write` | Format and overwrite files in-place |
| `-c`, `--check` | Check formatting status; returns exit code `1` if changes are needed, `0` if formatted |
| `--server` | Start persistent stdio JSON-RPC server for IDE extensions |
| `--changes-json` | Output changes as structured JSON instead of formatted text |
| `--config <path>` | Path to an explicit `.editorconfig` file |
| `--ignore-file <path>` | Path to a custom ignore file (default: `.razorignore`) |
| `--indent <n>` | Number of spaces per indentation level (default: `4`) |
| `--tab` | Use tabs for indentation instead of spaces |
| `--lf` | Enforce Unix line endings (`\n`) |
| `--crlf` | Enforce Windows line endings (`\r\n`) |
| `-h`, `--help` | Display help and usage information |

---

## Configuration

RazorFormatter integrates with `.editorconfig` files and `.razorignore` lists.

### EditorConfig Settings

Define formatting rules in `.editorconfig` matching Razor files:

```ini
root = true

[*.{cshtml,razor}]
indent_style = space
indent_size = 4
end_of_line = lf
trim_trailing_whitespace = true
insert_final_newline = true

# Razor-specific options
razor_wrap_attributes = auto       # auto | preserve | always
razor_format_embedded = true       # true | false (format CSS & JS blocks)
razor_collapse_empty_lines = true  # true | false
```

### `.razorignore` Files

Add a `.razorignore` file in your repository root or subdirectory to skip files using standard glob syntax:

```gitignore
# Ignore generated files and third-party vendor components
**/Generated/**
**/bin/**
**/obj/**
Pages/Legacy/*.razor
```

---

## Programmatic API (.NET)

Reference `RazorFormatter.Core` to format Razor source code programmatically:

```csharp
using RazorFormatter.Core;
using RazorFormatter.Core.Enums;

// Create the formatter instance
var formatter = RazorFormatterFactory.Create();

// Configure custom options or resolve from .editorconfig automatically
var options = new RazorFormatterOptions
{
    IndentStyle = IndentStyle.Space,
    IndentSize = 4,
    EndOfLine = EndOfLine.Lf,
    InsertFinalNewline = true,
    TrimTrailingWhitespace = true,
    CollapseMultipleBlankLines = true,
    WrapAttributes = AttributeWrapPolicy.Auto,
    FormatEmbeddedLanguages = true
};

// 1. Get the formatted string directly
string formatted = formatter.FormatToText(sourceCode, options);

// 2. Or get atomic, non-overlapping TextChange records for editor buffers
FormatResult result = formatter.Format(sourceCode, options);
foreach (TextChange change in result.Changes)
{
    Console.WriteLine($"Replace [{change.Start}..{change.End}] with '{change.NewText}'");
}
```

---

## Editor Integrations

### VS Code, Cursor, and Antigravity

The `src/RazorFormatter.VSCode` extension provides document formatting for `.cshtml` and `.razor` files using either the persistent CLI server or one-shot invocation.

#### Settings

Add the following to your user or workspace `settings.json`:

```json
{
  "[aspnetcorerazor]": {
    "editor.defaultFormatter": "razor-formatter.razor-code-formatter",
    "editor.formatOnSave": true
  },
  "razorFormatter.indentSize": 4,
  "razorFormatter.indentStyle": "space",
  "razorFormatter.cliPath": ""
}
```

> [!NOTE]
> `razorFormatter.cliPath` is configured with `machine` scope to protect against untrusted workspace execution. Child processes are executed with `shell: false`.

#### Extension Build & Packaging

```bash
cd src/RazorFormatter.VSCode
npm install
npm run build
```

---

## Running Tests

The test suite runs against .NET 10 using xUnit and includes 124 tests across syntax, indentation, embedded language formatting, EditorConfig parsing, performance streaming, and security regression checks:

```bash
# Run the complete test suite (124 tests)
dotnet test

# Run with detailed console output
dotnet test --logger "console;verbosity=detailed"

# Run a specific test suite
dotnet test --filter "FullyQualifiedName~SecurityAndBugRegressionTests"
dotnet test --filter "FullyQualifiedName~PerformanceTests"
```

---

## Contributing & License

Contributions are welcome! Please open an issue or pull request on GitHub.

Distributed under the [MIT License](LICENSE).

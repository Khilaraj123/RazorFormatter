# RazorFormatter

[![Build & Test](https://img.shields.io/badge/tests-31%20passed-brightgreen.svg)](#running-tests)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](#license)

**RazorFormatter** is a fast, deterministic, Prettier-grade code formatter for ASP.NET Core Razor (`.cshtml` and `.razor`) files. It brings modern code-formatting capabilities to Razor by understanding both HTML semantics and C# syntax transitions within a unified pipeline.

Designed as a monorepo, RazorFormatter powers:
- **CLI & CI workflows** via `RazorFormatter.Cli`
- **VS Code, Cursor, and Antigravity** via `RazorFormatter.VSCode`
- **Visual Studio** via `RazorFormatter.VisualStudio`
- **Embedded .NET applications** via `RazorFormatter.Core`

---

## Key Features

- **Razor & HTML Semantic Awareness**: Accurately tracks HTML elements, self-closing tags (`<br/>`), void elements (`<input>`, `<br>`, `<meta>`, `!doctype`), and ASP.NET Core Tag Helpers / Blazor components without corrupting sibling indentation.
- **Robust Attribute Parsing**: Properly handles multiline attributes and values containing `>` characters inside single/double quotes or Razor expressions (e.g., `data-val=@(x > y)`).
- **C# Control Flow & Code Blocks**: Cleanly indents `@if`, `@else if`, `@else`, `@foreach`, `@for`, `@while`, `@switch`, `@try`, `@lock`, `@code { ... }`, and `@{ ... }` blocks.
- **Razor Directives & Comments**: Formats directives (`@page`, `@model`, `@using`, `@inject`) and Razor comments (`@* ... *@`).
- **100% Idempotent**: Formatting an already formatted file produces **0 text changes**.
- **Atomic, Non-Overlapping Edits**: Emits ordered `TextChange` objects instead of replacing entire buffers, preserving editor undo history and cursor stability.
- **Configurable Styling**: Full support for spaces vs tabs, custom indent sizes (2, 4, etc.), trailing whitespace cleanup, blank-line collapse, and EOL normalization (`\n` vs `\r\n`).

---

## Monorepo Architecture

```
RazorFormatter/
├── src/
│   ├── RazorFormatter.Core/          # Reusable formatting engine & syntax analysis
│   │   ├── Abstractions/             # IRazorFormatter public interface
│   │   ├── Enums/                    # RazorSyntaxKind, IndentStyle, EndOfLine
│   │   ├── internals/                # IndentationEngine, FormattingWriter, LineWalker
│   │   ├── FormattingEngine.cs       # Pipeline coordinator
│   │   ├── RazorSyntaxLexer.cs       # Razor & HTML tokenizer
│   │   ├── TextChange.cs             # Insert, Replace, Delete edit model
│   │   └── TextChangeApplier.cs      # Range validator & edit applier
│   │
│   ├── RazorFormatter.Cli/           # Command-line tool & CI runner
│   │   └── Program.cs                # CLI entry point
│   │
│   ├── RazorFormatter.VSCode/        # VS Code / Cursor / Antigravity extension
│   │   ├── package.json              # Extension manifest & settings
│   │   └── src/extension.ts          # DocumentFormattingEditProvider
│   │
│   └── RazorFormatter.VisualStudio/  # Visual Studio extension
│       ├── RazorFormatterPackage.cs  # VSIX package initializer
│       └── source.extension.vsixmanifest
│
├── tests/
│   └── RazorFormatter.Tests/         # 31 unit, fixture, and idempotency tests
│       ├── Fixtures/                 # Golden-file paired test fixtures
│       ├── FixtureTests.cs           # Fixture regression suite
│       ├── RazorSyntaxTests.cs       # Lexer tokens and span tests
│       ├── FormattingTests.cs        # Indentation & option behavior tests
│       ├── TextChangeApplierTests.cs # Edit range & boundary tests
│       └── IdempotencyTests.cs       # Zero-diff second pass verification
│
├── samples/                          # Sample Razor test documents
│   ├── Basic.cshtml                  # Standard HTML5 + Razor markup
│   ├── ControlFlow.cshtml            # Nested control flow & loops
│   ├── TagHelpers.cshtml             # Tag Helpers and component syntax
│   └── NestedExpressions.cshtml      # Explicit/implicit expressions & attributes
│
├── .editorconfig                     # Repository code style definitions
├── RazorFormatter.slnx               # Solution definition
└── README.md                         # Project documentation
```

---

## CLI Usage

The CLI can format files in-place, validate files in CI/CD pipelines, or process input from standard streams.

### Basic Commands

```bash
# Print formatted output to stdout
dotnet run --project src/RazorFormatter.Cli -- ./samples/Basic.cshtml

# Format files in-place (-w / --write)
dotnet run --project src/RazorFormatter.Cli -- -w ./samples

# Check if files need formatting (-c / --check, exits with 1 if unformatted)
dotnet run --project src/RazorFormatter.Cli -- --check ./samples

# Format standard input to standard output
cat ./samples/Basic.cshtml | dotnet run --project src/RazorFormatter.Cli
```

### CLI Options

| Option | Description |
|---|---|
| `-w`, `--write` | Format and overwrite files in-place |
| `-c`, `--check` | Check formatting status; returns exit code `1` if changes are needed, `0` if formatted |
| `--indent <n>` | Set the number of spaces per indentation level (default: `4`) |
| `--tab` | Use tabs for indentation instead of spaces |
| `--lf` | Enforce Unix line endings (`\n`) |
| `--crlf` | Enforce Windows line endings (`\r\n`) |
| `-h`, `--help` | Display help and usage information |

---

## Programmatic API (.NET)

Reference `RazorFormatter.Core` in your project to format Razor source code programmatically:

```csharp
using RazorFormatter.Core;
using RazorFormatter.Core.Enums;

// Create the formatter instance
var formatter = RazorFormatterFactory.Create();

// Configure options
var options = new RazorFormatterOptions
{
    IndentStyle = IndentStyle.Space,
    IndentSize = 4,
    EndOfLine = EndOfLine.Lf,
    InsertFinalNewline = true,
    TrimTrailingWhitespace = true,
    CollapseMultipleBlankLines = true
};

// 1. Get the formatted string directly
string formattedText = formatter.FormatToText(sourceCode, options);

// 2. Or get atomic TextChange objects for editor buffers
FormatResult result = formatter.Format(sourceCode, options);
foreach (TextChange change in result.Changes)
{
    Console.WriteLine($"{change.Start}..{change.End}: \"{change.NewText}\"");
}
```

---

## Editor Integrations

### VS Code, Cursor, and Antigravity

The `src/RazorFormatter.VSCode` extension implements `vscode.languages.registerDocumentFormattingEditProvider` for `.cshtml` and `.razor` files.

#### Settings

In your `settings.json`:

```json
{
  "[aspnetcorerazor]": {
    "editor.defaultFormatter": "razor-formatter.razor-formatter",
    "editor.formatOnSave": true
  },
  "razorFormatter.indentSize": 4,
  "razorFormatter.indentStyle": "space",
  "razorFormatter.cliPath": ""
}
```

#### Extension Build & Packaging

```bash
cd src/RazorFormatter.VSCode
npm install
npm run build
```

---

## Running Tests

The test suite runs against .NET 10 using xUnit and includes lexer tests, indentation tests, golden-file fixtures, and idempotency checks:

```bash
# Run all 31 tests
dotnet test

# Run with detailed logging
dotnet test --logger "console;verbosity=detailed"

# Run only idempotency tests
dotnet test --filter "FullyQualifiedName~IdempotencyTests"
```

---

## Contributing & License

Contributions are welcome! Please open an issue or pull request on GitHub.

Distributed under the [MIT License](LICENSE).

# Razor Formatter for Visual Studio

[![Visual Studio 2022](https://img.shields.io/badge/Visual%20Studio-2022%20(v17.0%2B)-purple.svg)](https://visualstudio.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](https://github.com/Khilaraj123/RazorFormatter/blob/main/LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%20(x64)-blue.svg)](https://dotnet.microsoft.com/)

**Razor Formatter** brings fast, deterministic, Prettier-grade code formatting to ASP.NET Core Razor (`.cshtml`) and Blazor (`.razor`) documents inside Visual Studio 2022.

No more fighting inconsistent indentation, misplaced closing braces, or collapsed multiline HTML attributes. Razor Formatter uses an AST-aware semantic pipeline that harmonizes HTML structure and C# syntax transitions into clean, standardized code every time you save or format.

---

## Key Features

- **Semantic HTML & Razor Alignment**: Understands HTML5 void tags, self-closing elements, ASP.NET Core Tag Helpers, and Blazor component hierarchies without drifting nested indents.
- **C# Control Flow & Code Blocks**: Cleanly indents `@if`, `@else if`, `@else`, `@foreach`, `@for`, `@while`, `@switch`, `@try`, `@catch`, `@finally`, `@lock`, `@code { ... }`, and `@{ ... }` blocks.
- **Attribute Layout Policies**: Keeps multiline HTML and component attributes clean and readable with configurable wrapping (`auto`, `preserve`, `always`).
- **Embedded CSS & JavaScript Support**: Formats `<style>` and `<script>` blocks safely without premature tag termination or string literal corruption.
- **Native `.editorconfig` Support**: Automatically detects and applies project `.editorconfig` formatting rules and respects `.razorignore` exclusions.
- **Atomic & Localized Edits**: Generates precise, line-by-line edit spans rather than rewriting the entire buffer, preserving your cursor position, selection, and undo/redo stack.
- **100% Idempotent**: Formatting an already formatted document produces **zero text changes**.
- **Blazing Fast**: In-memory tokenization and formatting processes thousands of lines per second with sub-millisecond execution.

---

## Usage

Razor Formatter integrates directly into Visual Studio's standard formatting commands:

| Action | Shortcut | Description |
|---|---|---|
| **Format Document** | `Ctrl+K, Ctrl+D` | Formats the entire active Razor file |
| **Format Selection** | `Ctrl+K, Ctrl+F` | Formats the currently highlighted block or range |
| **Format on Save** | *Automatic* | Formats on document save (when enabled in VS Options) |

### Enabling Format on Save in Visual Studio
1. Navigate to **Tools** > **Options**.
2. Select **Text Editor** > **Code Cleanup**.
3. Check **Run Code Cleanup profile on Save**.

---

## Configuration via `.editorconfig`

Razor Formatter automatically respects your project or solution `.editorconfig` hierarchy. Place an `.editorconfig` file in your repository root or project directory:

```ini
root = true

[*.{cshtml,razor}]
# Standard EditorConfig options
indent_style = space
indent_size = 4
end_of_line = lf
trim_trailing_whitespace = true
insert_final_newline = true

# Razor Formatter specific options
razor_wrap_attributes = auto        # Options: auto, preserve, always
razor_format_embedded = true        # Options: true, false (formats <script> & <style>)
razor_collapse_empty_lines = true   # Options: true, false
```

### Ignoring Files (`.razorignore`)
To skip specific generated files or third-party templates, add a `.razorignore` file:
```gitignore
**/Generated/**
**/obj/**
**/bin/**
Components/Legacy/*.razor
```

---

## Requirements & Compatibility

- **Visual Studio Edition**: Visual Studio 2022 (Community, Professional, or Enterprise).
- **Architecture**: x64 (Visual Studio 2022 64-bit process).
- **Target Files**: ASP.NET Core Razor (`.cshtml`) and Blazor components (`.razor`).
- **Framework**: .NET Framework 4.7.2+ runtime (included with Visual Studio 2022).

---

## Feedback & Community

- **Source Code**: [GitHub Repository](https://github.com/Khilaraj123/RazorFormatter)
- **Issue Tracker**: [Report a Bug or Request a Feature](https://github.com/Khilaraj123/RazorFormatter/issues)
- **Discussions**: [Join the Community](https://github.com/Khilaraj123/RazorFormatter/discussions)
- **License**: [MIT License](https://github.com/Khilaraj123/RazorFormatter/blob/main/LICENSE)


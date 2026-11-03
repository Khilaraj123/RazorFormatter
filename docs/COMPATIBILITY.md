# RazorFormatter Compatibility & Syntax Guide

This guide details IDE support, operating system compatibility, supported Razor/HTML syntax constructs, and known limitations.

---

## 1. IDE & Editor Compatibility Matrix

| Editor | Extension Package | Supported Features | Minimum Version |
|:---|:---|:---|:---:|
| **VS Code** | `razor-code-formatter` (Marketplace) | Format Document, Format on Save, Server Daemon (`--server`), Workspace Trust | 1.90.0+ |
| **Cursor** | `razor-code-formatter` (Marketplace / VSIX) | Format Document, Format on Save, Server Daemon (`--server`), Workspace Trust | 0.40.0+ |
| **Google Antigravity** | `razor-code-formatter` (Marketplace / VSIX) | Format Document, Format on Save, Server Daemon (`--server`), Workspace Trust | 1.0.0+ |
| **Visual Studio 2022** | `RazorFormatter.VisualStudio` (VSIX) | Format Document, Visual Studio Core Editor MEF integration | 17.0+ (64-bit) |
| **CLI / Terminal / CI** | `razor-format` (.NET Global Tool / Binary) | In-place writing (`-w`), dry-run check (`--check`), JSON edits (`--changes-json`), stdio streaming | .NET 10.0 or Self-Contained |

---

## 2. Operating System Support

| Operating System | Architectures | Distribution Channel |
|:---|:---:|:---|
| **Windows** | `win-x64`, `win-arm64` | .NET Global Tool, Standalone `.exe`, Visual Studio VSIX, VS Code VSIX |
| **Linux** | `linux-x64`, `linux-arm64` | .NET Global Tool, Standalone single-file binary, VS Code / Cursor VSIX |
| **macOS** | `osx-x64`, `osx-arm64` (Apple Silicon) | .NET Global Tool, Standalone single-file binary, VS Code / Cursor VSIX |

---

## 3. Supported Syntax Catalog

### A. Razor Control-Flow Directives
- **Conditionals**: `@if`, `@else if`, `@else`, `@switch`, `@case`, `@default`.
- **Loops**: `@foreach`, `@for`, `@while`, `@do { ... } while (...)`.
- **Exception Handling**: `@try`, `@catch`, `@finally`.
- **Concurrency & Resources**: `@lock`, `@using`.
- **Code Blocks**: `@{ ... }`, `@functions { ... }`, `@code { ... }`.
- **Razor Sections**: `@section Header { ... }`.

### B. Razor Expressions & Transitions
- **Implicit Expressions**: `@Model.Name`, `@DateTime.UtcNow`, `@user.Profile.AvatarUrl`.
- **Explicit Expressions**: `@(item.Price * 1.15m)`.
- **Transitions**: Single `@`, escaped `@@` (emits literal `@`), `@:` (inline text transition), and `@<tag>` (inline template tags).

### C. HTML5 & Semantic Markup
- Standard HTML5 elements with nested children.
- Self-closing void tags (`<input />`, `<img />`, `<br />`, `<hr />`, `<meta />`).
- Multiline HTML comments `<!-- ... -->` and Razor comments `@* ... *@`.

### D. ASP.NET Core Tag Helpers & Blazor Components
- Standard Tag Helpers (`asp-for`, `asp-action`, `asp-controller`, `asp-validation-for`, `asp-route-*`).
- Blazor component tags (`<MyCustomComponent Title="Hello" OnClick="@HandleClick" />`).
- Attribute wrapping modes (`auto`, `preserve`, `always`) aligning parameters neatly.

### E. Embedded Languages
- **Embedded CSS** (`<style> ... </style>`): Content indentation aligns relative to the `<style>` container while preserving CSS block layout.
- **Embedded JavaScript** (`<script> ... </script>`): Content is formatted with relative offset alignment while preserving internal string literals and template literals.

### F. C# 11+ String Literals
- Normal strings (`"..."`)
- Verbatim strings (`@"..."`)
- Interpolated strings (`$"..."`)
- Raw string literals (`"""..."""`) preserved without character modification.

---

## 4. Known Limitations & Behaviors

1. **Unmatched Closing Tags**:
   - When encountering an unexpected closing tag (e.g. `</div>` without an opening `<div>`), the formatter emits a diagnostic, preserves the tag at current indentation, and does not crash or corrupt subsequent hierarchy.
2. **Preprocessor Directives (`#if`, `#endif`)**:
   - Razor files with conditional compilation `#if DEBUG` directives inside `@code` blocks are preserved as literal lines without indentation alteration.
3. **Range Formatting**:
   - Document-level formatting is the primary supported operation. Partial range formatting on an incomplete syntax snippet will expand to the nearest enclosing element/block boundary.

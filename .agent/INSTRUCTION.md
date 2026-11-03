# Features to implement

## Phase 1 — Robust Razor/C# lexical analysis [Completed]
This is the most important next phase. Do not add more formatting rules until the lexer can distinguish the languages correctly.

### Status: Completed
- [x] Explicit lexical states: HTML, Razor, C#, Razor comment, HTML comment, CSS, JavaScript.
- [x] C# string handling: normal, verbatim, interpolated, raw, and character literals.
- [x] C# line and block comments.
- [x] Nested braces and parentheses.
- [x] Razor explicit and implicit expressions.
- [x] Razor transitions such as @, @@, @:, and @<.
- [x] Multiline and nested Razor constructs.
- [x] Better handling of malformed and incomplete documents.

Implemented components:
```
Lexing/
├── RazorLexer.cs
├── HtmlLexer.cs
├── CSharpLexer.cs
├── LexicalState.cs
├── LexicalContext.cs
└── RazorToken.cs
```

Completion criteria met: tokens preserve the entire source without gaps or overlaps, and nested language transitions are tested and verified.

---

## Phase 2 — Structural formatting engine [Completed]

Replace the simplistic depth model with a more explicit representation of structural context.

### Status: Completed
- [x] Nested HTML elements.
- [x] Nested Razor control-flow blocks.
- [x] @functions, @code, @section, and @{} blocks.
- [x] @else, @else if, @catch, and @finally attachment to preceding blocks.
- [x] Nested expressions inside HTML attributes.
- [x] Tag Helper and component nesting.
- [x] Mismatched closing-tag recovery.
- [x] Inline versus multiline markup handling.

Implemented components:
```
Formatting/
├── FormattingEngine.cs
├── IndentationEngine.cs
├── StructuralContext.cs
├── BlockTracker.cs
├── ElementTracker.cs
└── FormattingWriter.cs
```
The lexer and structural formatting engine are strictly separated: the lexer identifies syntax; the structural engine determines indentation.

Completion criteria met: nested HTML and Razor constructs format correctly without one context corrupting another.

---

## Phase 3 — Attribute and embedded-language formatting [Completed]

This is where the formatter becomes considerably more useful for real ASP.NET Core projects.
High-value feature phase

### Features:
- [x] Multiline HTML attributes.
- [x] Configurable attribute layout.
- [x] One attribute per line.
- [x] Preserve existing layout when appropriate.
- [x] Razor expressions inside attributes.
- [x] Tag Helper attributes such as asp-for, asp-action, and asp-validation-for.
- [x] CSS formatting inside <style>.
- [x] JavaScript formatting inside <script>.
- [x] Preserve embedded-language content when parsing is uncertain.

Example target:
```cshtml
<form
    asp-controller="Users"
    asp-action="Create"
    method="post"
    class="form-container">

    <input
        asp-for="Email"
        class="form-control"
        placeholder="Email" />

</form>
```
Attribute layout should be configurable rather than enforcing one layout globally.

For embedded CSS and JavaScript, consider using existing formatters or parsers instead of writing your own. Your formatter should coordinate them, not reinvent their language grammars.

Completion criteria: attribute formatting is stable, and embedded-language formatting never corrupts Razor transitions or surrounding markup.

---

## Phase 4 — Configuration and formatting rules [Completed]

Your formatter needs a predictable configuration system if other developers are going to use it.

### Features:
- [x] .editorconfig support.
- [x] Configuration precedence.
- [x] Per-language formatting options.
- [x] Blank-line preservation rules.
- [x] Attribute formatting preferences.
- [x] HTML optional-tag handling.
- [x] Final newline and EOL policies.
- [x] Ignore patterns for CLI operations.
- [x] Explicit unsupported-syntax behavior.

Suggested configuration precedence:
1. Explicit API options.
2. Project-level .editorconfig.
3. Formatter defaults.

Support relevant standard EditorConfig keys first, including `indent_style`, `indent_size`, `tab_width`, `end_of_line`, `insert_final_newline`, and `trim_trailing_whitespace`.

Completion criteria: the same document and configuration produce identical output regardless of which editor invokes the formatter.

---

## Phase 5 — Editor integration and performance [Completed]
Your VS Code extension currently launches the CLI process and replaces the entire document with stdout. This is acceptable for an early prototype, but it is not the ideal final integration.

### Features:
- [x] Use the formatter's actual text changes in editor integrations.
- [x] Process cancellation should terminate the child process.
- [x] Improve process startup and error reporting.
- [x] Resolve the CLI executable independently of the user's workspace.
- [x] Avoid rebuilding the CLI every time a document is formatted.
- [x] Add format-on-save support.
- [x] Add range formatting where practical.
- [x] Add editor configuration synchronization.
- [x] Support workspace folders and multi-root workspaces.
- [x] Test compatibility with VS Code, Cursor, and Antigravity independently.

### Performance improvements:
- [x] Avoid repeated line/column calculations.
- [x] Reduce unnecessary allocations in lexing.
- [x] Benchmark large .cshtml files.
- [x] Consider incremental formatting for editor scenarios later.

A key architectural choice: consider exposing the core formatter through a long-running process or language server rather than launching dotnet run for every formatting request.

Completion criteria: formatting works reliably in an actual editor, including cancellation, errors, and repeated format-on-save operations.

---

## Phase 6 — Distribution and developer experience

### Features:
- [ ] Standalone CLI distribution.
- [ ] VS Code extension packaging and publishing.
- [ ] Actual Visual Studio VSIX implementation.
- [ ] GitHub Actions CI/CD.
- [ ] Automated regression tests across operating systems.
- [ ] Documentation with supported syntax and known limitations.
- [ ] Benchmark and compatibility reports.

Completion criteria: someone unfamiliar with the repository can install the formatter, configure it, and use it without building your source code.

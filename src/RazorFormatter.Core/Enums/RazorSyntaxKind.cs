using System;
using System.Collections.Generic;
using System.Text;

namespace RazorFormatter.Core.Enums
{
    /// <summary>Discriminated union of every top-level syntactic region in a Razor file.</summary>
    public enum RazorSyntaxKind
    {
        Html,
        HtmlTag,
        HtmlComment,
        RazorDirective,
        RazorCodeBlock,
        RazorExpression,
        RazorImplicitExpression,
        RazorExplicitExpression,
        RazorControlFlow,
        RazorComment,
        CSharp,
        RazorBlockOpen,
        RazorBlockClose,
        CSharpBlockOpen,
        CSharpBlockClose,
        RazorBlockTransition,
        TagHelper,
        RazorPragma,
        EmbeddedCss,
        EmbeddedJavaScript,
        Whitespace,
        Newline,
        Unknown,
    }
}

using System;
using RazorFormatter.Core;
using RazorFormatter.Core.Abstractions;
using RazorFormatter.Core.Enums;
using Xunit;

namespace RazorFormatter.Tests
{
    public class Phase2StructuralTests
    {
        private readonly IRazorFormatter _formatter = RazorFormatterFactory.Create();

        // -------------------------------------------------------------
        // 1. Nested HTML elements
        // -------------------------------------------------------------

        [Fact]
        public void Structural_NestedHtmlElements_IndentedCleanly()
        {
            var input = @"<div>
<header>
<nav>
<ul>
<li>
<a href=""/"">Home</a>
</li>
</ul>
</nav>
</header>
</div>
";
            var expected = @"<div>
    <header>
        <nav>
            <ul>
                <li>
                    <a href=""/"">Home</a>
                </li>
            </ul>
        </nav>
    </header>
</div>
";
            var actual = _formatter.FormatToText(input, new RazorFormatterOptions { EndOfLine = EndOfLine.Lf });
            Assert.Equal(expected, actual);
        }

        // -------------------------------------------------------------
        // 2. Nested Razor control-flow blocks
        // -------------------------------------------------------------

        [Fact]
        public void Structural_NestedRazorControlFlow_IndentedCorrectly()
        {
            var input = @"@if (Model.IsAdmin)
{
@foreach (var role in Model.Roles)
{
@if (role.IsActive)
{
<span>Active</span>
}
}
}
";
            var expected = @"@if (Model.IsAdmin)
{
    @foreach (var role in Model.Roles)
    {
        @if (role.IsActive)
        {
            <span>Active</span>
        }
    }
}
";
            var actual = _formatter.FormatToText(input, new RazorFormatterOptions { EndOfLine = EndOfLine.Lf });
            Assert.Equal(expected, actual);
        }

        // -------------------------------------------------------------
        // 3. @functions, @code, @section, and @{} blocks
        // -------------------------------------------------------------

        [Fact]
        public void Structural_CodeAndFunctionsBlocks_IndentBodyAndNestedBraces()
        {
            var input = @"@code
{
private int _counter = 0;
private void Increment()
{
_counter++;
}
}
";
            var expected = @"@code
{
    private int _counter = 0;
    private void Increment()
    {
        _counter++;
    }
}
";
            var actual = _formatter.FormatToText(input, new RazorFormatterOptions { EndOfLine = EndOfLine.Lf });
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Structural_SectionBlock_IndentsHtmlChildren()
        {
            var input = @"@section Scripts
{
<script src=""~/js/site.js""></script>
<div>
<p>Loaded</p>
</div>
}
";
            var expected = @"@section Scripts
{
    <script src=""~/js/site.js""></script>
    <div>
        <p>Loaded</p>
    </div>
}
";
            var actual = _formatter.FormatToText(input, new RazorFormatterOptions { EndOfLine = EndOfLine.Lf });
            Assert.Equal(expected, actual);
        }

        // -------------------------------------------------------------
        // 4. @else, @else if, @catch, and @finally attachment
        // -------------------------------------------------------------

        [Fact]
        public void Structural_IfElseIfElseChain_AlignedCorrectly()
        {
            var input = @"@if (Model.Score > 90)
{
<p>Grade A</p>
}
else if (Model.Score > 75)
{
<p>Grade B</p>
}
else
{
<p>Grade C</p>
}
";
            var expected = @"@if (Model.Score > 90)
{
    <p>Grade A</p>
}
else if (Model.Score > 75)
{
    <p>Grade B</p>
}
else
{
    <p>Grade C</p>
}
";
            var actual = _formatter.FormatToText(input, new RazorFormatterOptions { EndOfLine = EndOfLine.Lf });
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Structural_TryCatchFinallyChain_AlignedCorrectly()
        {
            var input = @"@try
{
<div>@Model.Render()</div>
}
catch (Exception ex)
{
<div class=""error"">@ex.Message</div>
}
finally
{
<span>Done</span>
}
";
            var expected = @"@try
{
    <div>@Model.Render()</div>
}
catch (Exception ex)
{
    <div class=""error"">@ex.Message</div>
}
finally
{
    <span>Done</span>
}
";
            var actual = _formatter.FormatToText(input, new RazorFormatterOptions { EndOfLine = EndOfLine.Lf });
            Assert.Equal(expected, actual);
        }

        // -------------------------------------------------------------
        // 5. Nested expressions inside HTML attributes
        // -------------------------------------------------------------

        [Fact]
        public void Structural_AttributesWithRazorExpressions_DoesNotCorruptIndentation()
        {
            var input = @"<div class=""card"">
<button class=""@(Model.IsEnabled ? ""btn-primary"" : ""btn-disabled"")"" data-url=""@Model.ActionUrl"">
Click Me
</button>
</div>
";
            var expected = @"<div class=""card"">
    <button class=""@(Model.IsEnabled ? ""btn-primary"" : ""btn-disabled"")"" data-url=""@Model.ActionUrl"">
        Click Me
    </button>
</div>
";
            var actual = _formatter.FormatToText(input, new RazorFormatterOptions { EndOfLine = EndOfLine.Lf });
            Assert.Equal(expected, actual);
        }

        // -------------------------------------------------------------
        // 6. Tag Helper and component nesting
        // -------------------------------------------------------------

        [Fact]
        public void Structural_TagHelpersAndComponents_NestedCorrectly()
        {
            var input = @"<MyContainer Title=""Main"">
<CardHeader>
<h3>Header</h3>
</CardHeader>
<CardBody>
<p>Text</p>
<Avatar ImageUrl=""pic.png"" />
</CardBody>
</MyContainer>
";
            var expected = @"<MyContainer Title=""Main"">
    <CardHeader>
        <h3>Header</h3>
    </CardHeader>
    <CardBody>
        <p>Text</p>
        <Avatar ImageUrl=""pic.png"" />
    </CardBody>
</MyContainer>
";
            var actual = _formatter.FormatToText(input, new RazorFormatterOptions { EndOfLine = EndOfLine.Lf });
            Assert.Equal(expected, actual);
        }

        // -------------------------------------------------------------
        // 7. Mismatched closing-tag recovery
        // -------------------------------------------------------------

        [Fact]
        public void Structural_MismatchedClosingTagRecovery_RecoversCleanly()
        {
            // <p> and <span> are unclosed when </div> appears
            var input = @"<div>
<span>
<p>
Text content
</div>
<section>
<p>Sibling content</p>
</section>
";
            var expected = @"<div>
    <span>
        <p>
            Text content
</div>
<section>
    <p>Sibling content</p>
</section>
";
            var actual = _formatter.FormatToText(input, new RazorFormatterOptions { EndOfLine = EndOfLine.Lf });
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Structural_RogueClosingTag_DoesNotCorruptStack()
        {
            var input = @"<div>
</unmatched>
<p>Valid paragraph</p>
</div>
";
            var expected = @"<div>
    </unmatched>
    <p>Valid paragraph</p>
</div>
";
            var actual = _formatter.FormatToText(input, new RazorFormatterOptions { EndOfLine = EndOfLine.Lf });
            Assert.Equal(expected, actual);
        }

        // -------------------------------------------------------------
        // 8. Inline versus multiline markup handling
        // -------------------------------------------------------------

        [Fact]
        public void Structural_InlineTags_DoNotIndentSubsequentLines()
        {
            var input = @"<div>
<h1>Title</h1>
<p>A paragraph with <b>bold</b> and <i>italic</i> words.</p>
<div>
<span>Another inline tag</span>
</div>
</div>
";
            var expected = @"<div>
    <h1>Title</h1>
    <p>A paragraph with <b>bold</b> and <i>italic</i> words.</p>
    <div>
        <span>Another inline tag</span>
    </div>
</div>
";
            var actual = _formatter.FormatToText(input, new RazorFormatterOptions { EndOfLine = EndOfLine.Lf });
            Assert.Equal(expected, actual);
        }

        // -------------------------------------------------------------
        // 9. Completion criteria: HTML & Razor constructs isolated
        // -------------------------------------------------------------

        [Fact]
        public void Structural_ComplexInterleavedHtmlAndRazor_DoesNotCorruptContext()
        {
            var input = @"<div class=""wrapper"">
@if (Model.IsAvailable)
{
<div class=""inner"">
@foreach (var item in Model.Items)
{
<div class=""item"">
<h4>@item.Title</h4>
@if (item.HasDiscount)
{
<span class=""badge"">Sale</span>
}
</div>
}
</div>
}
else
{
<p>No items</p>
}
</div>
";
            var expected = @"<div class=""wrapper"">
    @if (Model.IsAvailable)
    {
        <div class=""inner"">
            @foreach (var item in Model.Items)
            {
                <div class=""item"">
                    <h4>@item.Title</h4>
                    @if (item.HasDiscount)
                    {
                        <span class=""badge"">Sale</span>
                    }
                </div>
            }
        </div>
    }
    else
    {
        <p>No items</p>
    }
</div>
";
            var actual = _formatter.FormatToText(input, new RazorFormatterOptions { EndOfLine = EndOfLine.Lf });
            Assert.Equal(expected, actual);
        }
    }
}

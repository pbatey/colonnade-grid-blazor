using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace ColonnadeGrid.Internal;

/// <summary>
/// Renders the grid's own markup once its columns have registered. Columns
/// register while the <c>CascadingValue</c> ahead of this renders its content;
/// the grid's markup, rendered directly in the grid, would already have read
/// the previous render's columns by then, showing a changed title or an added
/// or removed column a render late. As a later sibling, this renders after the
/// columns' parameters are set, in the same render pass. Public only because
/// Razor doesn't recognize an internal component as a tag.
/// </summary>
public sealed class AfterColumns : ComponentBase
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent);
}

using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using ColonnadeGrid.Internal;
using ColonnadeGrid.Models;

namespace ColonnadeGrid;

public partial class GridColumn<TItem, TProp>
{
    [CascadingParameter]
    private GridContext<TItem>? Context { get; set; }

    /// <summary>The property this column reads and displays, e.g. <c>x =&gt; x.Title</c>.</summary>
    [Parameter, EditorRequired]
    public Expression<Func<TItem, TProp>> Field { get; set; } = default!;

    /// <summary>
    /// An explicit stable identifier for this column. Defaults to the
    /// property name extracted from <see cref="Field"/> when not set;
    /// only needed if two columns would otherwise derive the same id (e.g.
    /// two columns over the same property with different templates).
    /// </summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>The column's header text. Defaults to the property name extracted from <see cref="Field"/>.</summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    /// An optional label for this column in the columns (show/hide) menu, used
    /// when the header should stay text-less (e.g. an icon-only action column
    /// with a blank <see cref="Title"/> or a custom <see cref="HeaderTemplate"/>).
    /// Falls back to <see cref="Title"/>, then the column id, when not set.
    /// </summary>
    [Parameter]
    public string? MenuTitle { get; set; }

    /// <summary>Whether clicking this column's header sorts by it. Default <c>false</c>.</summary>
    [Parameter]
    public bool Sortable { get; set; }

    /// <summary>Whether this column exposes a filter control. Default <c>false</c>.</summary>
    [Parameter]
    public bool Filterable { get; set; }

    /// <summary>Whether this column can be selected as the group-by column. Default <c>false</c>.</summary>
    [Parameter]
    public bool Groupable { get; set; }

    /// <summary>
    /// The filter editor to show when <see cref="Filterable"/> is set. By default
    /// it's chosen from the property's type: a value checklist for enums and
    /// booleans, a range for numbers and durations, date presets and a range for
    /// dates, and an operator with text otherwise. Set
    /// <see cref="Models.FilterKind.Values"/> on a text column with a small set of
    /// values to pick from them instead.
    /// </summary>
    [Parameter]
    public FilterKind? FilterKind { get; set; }

    /// <summary>
    /// A format string (e.g. <c>"yyyy-MM-dd"</c>) applied via
    /// <see cref="IFormattable"/> when the property value implements it and
    /// no <see cref="CellTemplate"/> is given.
    /// </summary>
    [Parameter]
    public string? Format { get; set; }

    /// <summary>Custom cell content, receiving the row item as its context. Overrides the default text rendering.</summary>
    [Parameter]
    public RenderFragment<TItem>? CellTemplate { get; set; }

    /// <summary>Custom header content. Overrides the default <see cref="Title"/> text rendering.</summary>
    [Parameter]
    public RenderFragment? HeaderTemplate { get; set; }

    /// <summary>
    /// A custom filter editor for this column, shown instead of the built-in one
    /// (a value checklist, range, date, or text) when <see cref="Filterable"/> is
    /// set. Receives a <see cref="FilterEditorContext"/> with the current filter,
    /// the column's stats, and apply/clear callbacks, and owns its own UI — so it
    /// can render pills, a date picker, or any bespoke form. Overrides
    /// <see cref="FilterKind"/> for this column.
    /// </summary>
    [Parameter]
    public RenderFragment<FilterEditorContext>? FilterTemplate { get; set; }

    /// <summary>
    /// Shows this column's filter editor in a centered modal dialog instead of
    /// the header dropdown when <see cref="Filterable"/> is set. Useful for a
    /// richer editor — e.g. a calendar date range via <see cref="FilterTemplate"/>
    /// — that needs more room than the dropdown offers. Default <c>false</c>.
    /// </summary>
    [Parameter]
    public bool FilterInDialog { get; set; }

    private (Expression<Func<TItem, TProp>> Field, Func<TItem, TProp> Accessor)? _compiled;

    protected override void OnParametersSet()
    {
        if (Context is null)
        {
            throw new InvalidOperationException(
                $"{nameof(GridColumn<TItem, TProp>)} must be rendered inside a ColonnadeGrid's Columns content.");
        }

        var propertyName = PropertyNameExtractor.GetPropertyName(Field);

        // This runs on every grid render; don't recompile Field each time.
        var accessor = ColumnAccessors<TItem, TProp>.For(Field, _compiled);
        _compiled = (Field, accessor);

        var descriptor = new TypedGridColumn<TItem, TProp>(accessor, propertyName, Title, Id, Format)
        {
            MenuTitle = MenuTitle,
            Sortable = Sortable,
            Filterable = Filterable,
            Groupable = Groupable,
            FilterKind = FilterKind,
            CellTemplate = CellTemplate,
            HeaderTemplate = HeaderTemplate,
            FilterTemplate = FilterTemplate,
            FilterInDialog = FilterInDialog
        };

        Context.RegisterColumn(descriptor);
    }
}

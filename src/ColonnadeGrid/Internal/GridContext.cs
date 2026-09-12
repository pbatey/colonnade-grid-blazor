using ColonnadeGrid.Abstractions;

namespace ColonnadeGrid.Internal;

/// <summary>
/// Cascaded by <c>ColonnadeGrid&lt;TItem&gt;</c> to its <c>GridColumn</c>
/// children so each column can register itself without the table needing a
/// direct reference to its children. The table clears the list
/// (<see cref="ClearColumns"/>) immediately before rendering its
/// <c>Columns</c> render fragment on every render, and each
/// <c>GridColumn.OnParametersSet</c> re-registers itself — this preserves
/// markup order and supports conditionally-rendered (<c>@if</c>) columns
/// without needing <see cref="IDisposable"/>-based unregistration.
/// Constructible directly (no rendering involved) for unit testing.
/// </summary>
/// <typeparam name="TItem">The row item type.</typeparam>
public sealed class GridContext<TItem>
{
    private readonly List<GridColumnBase<TItem>> _columns = [];

    /// <summary>The columns currently registered, in markup order.</summary>
    public IReadOnlyList<GridColumnBase<TItem>> Columns => _columns;

    /// <summary>Removes all registered columns.</summary>
    internal void ClearColumns() => _columns.Clear();

    /// <summary>Adds a column to the end of the registered list.</summary>
    internal void RegisterColumn(GridColumnBase<TItem> column) => _columns.Add(column);
}

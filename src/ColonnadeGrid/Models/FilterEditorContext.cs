using Microsoft.AspNetCore.Components;

namespace ColonnadeGrid.Models;

/// <summary>
/// The model passed to a column's custom filter editor
/// (<c>GridColumn.FilterTemplate</c>): everything the template needs to render
/// the current filter state and apply or clear a filter.
/// <para>
/// Deliberately non-generic. Filter values travel through the grid as
/// culture-invariant strings (see <see cref="FilterDescriptor"/>) and columns
/// are type-erased once registered, so a custom editor never needs the column's
/// compile-time property type — it works from <see cref="PropertyName"/> and the
/// runtime <see cref="PropertyType"/>. That keeps <c>FilterTemplate</c> a plain
/// <c>RenderFragment&lt;FilterEditorContext&gt;</c> the host can author without
/// spelling any type parameters.
/// </para>
/// </summary>
/// <param name="PropertyName">
/// The <c>.NET</c> property name of the column being filtered — the key a
/// produced <see cref="FilterDescriptor"/> must use.
/// </param>
/// <param name="PropertyType">The runtime type of the column's property, for editors that adapt to it (e.g. numeric vs. text).</param>
/// <param name="CurrentFilter">The column's active filter, or <c>null</c> when it isn't filtered. Reflects the current state each time the editor opens.</param>
/// <param name="Stats">
/// The column's stats (distinct values, min/max, counts) when the data source is
/// an <see cref="Abstractions.IColumnStatsProvider{TItem}"/>; otherwise
/// <c>null</c>. A custom editor must render without them too.
/// </param>
/// <param name="Apply">
/// Applies a filter for this column. The grid runs it through the same path as
/// the built-in editors, so a descriptor that wouldn't exclude anything (see
/// <see cref="GridState.SetFilter"/>) clears the column's filter instead — a
/// custom editor can simply always <c>Apply</c> and let the grid decide.
/// </param>
/// <param name="Clear">Clears this column's filter.</param>
public sealed record FilterEditorContext(
    string PropertyName,
    Type PropertyType,
    FilterDescriptor? CurrentFilter,
    ColumnStats? Stats,
    EventCallback<FilterDescriptor> Apply,
    EventCallback Clear);

namespace ColonnadeGrid.Models;

/// <summary>
/// Describes one sort key: a single column identified by its property name,
/// and the direction to sort it in. A table can sort by up to two columns at
/// once (see <see cref="GridState.Sorts"/>); each key is one of these.
/// </summary>
/// <param name="PropertyName">
/// The <c>.NET</c> property name (not a display title) of the column being
/// sorted, matching <see cref="ColumnState.Id"/> / the value produced by the
/// column's <c>Field</c> expression.
/// </param>
/// <param name="Direction">The direction to sort in.</param>
public sealed record SortDescriptor(string PropertyName, SortDirection Direction);

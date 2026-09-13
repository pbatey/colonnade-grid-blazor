namespace ColonnadeGrid.Models;

/// <summary>
/// Asks an <see cref="Abstractions.IColumnStatsProvider{TItem}"/> about one
/// column's values, so a filter editor can offer choices and limits that fit the data.
/// </summary>
/// <param name="PropertyName">The column's property.</param>
/// <param name="Filters">
/// The filters on the <em>other</em> columns — the grid leaves out the column's
/// own filter, so its limits don't shrink to whatever it's already filtered to.
/// </param>
/// <param name="IncludeValueCounts">Whether to return each distinct value with its row count (for value-list editors).</param>
/// <param name="MaxValueCount">The most distinct values to return when <paramref name="IncludeValueCounts"/> is set.</param>
public sealed record ColumnStatsRequest(
    string PropertyName,
    IReadOnlyList<FilterDescriptor> Filters,
    bool IncludeValueCounts = false,
    int MaxValueCount = 200);

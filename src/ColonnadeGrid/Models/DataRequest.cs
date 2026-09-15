namespace ColonnadeGrid.Models;

/// <summary>
/// Describes the page of data a table wants from an
/// <see cref="Abstractions.IDataProvider{TItem}"/>: paging, sort, filters, and
/// an optional single group-by column.
/// </summary>
/// <param name="Skip">Number of items to skip, for paging.</param>
/// <param name="Take">
/// Maximum number of items to return, for paging.
/// <para>
/// A grid without <c>EnablePaging</c> always sends <c>Skip = 0</c> and
/// <c>Take = int.MaxValue</c> (every row). With paging, it sends the current
/// page's window, grouped or not. When grouped, page over the rows in group
/// order, and describe only the groups that have items on the page — each
/// clipped to its part of the page, with <see cref="DataGroup.TotalCount"/>
/// carrying its size across all pages.
/// </para>
/// </param>
/// <param name="Sort">
/// The primary sort, or <c>null</c> for unsorted. Providers that only handle
/// single-column sort can read just this; those supporting two-column sort
/// should read <see cref="Sorts"/> for the full ordered list.
/// </param>
/// <param name="Filters">The active column filters, combined with AND semantics.</param>
/// <param name="GroupByPropertyName">
/// The property to group by, or <c>null</c> for no grouping. Only a single
/// group-by column is supported in v1.
/// </param>
public sealed record DataRequest(
    int Skip,
    int Take,
    SortDescriptor? Sort,
    IReadOnlyList<FilterDescriptor> Filters,
    string? GroupByPropertyName)
{
    /// <summary>
    /// The active sort keys, in priority order (primary first, secondary
    /// breaks ties). Defaults to just <see cref="Sort"/> (or empty when it's
    /// <c>null</c>), so constructing a request positionally still carries the
    /// primary sort; the grid sets this explicitly for two-column sort.
    /// </summary>
    public IReadOnlyList<SortDescriptor> Sorts { get; init; } = Sort is null ? [] : [Sort];

    /// <summary>A request for the first page of unsorted, unfiltered, ungrouped data.</summary>
    public static DataRequest Default { get; } = new(0, int.MaxValue, null, [], null);
}

namespace ColonnadeGrid.Models;

/// <summary>
/// Describes the page of data a table wants from an
/// <see cref="Abstractions.IDataProvider{TItem}"/>: paging, sort, filters, and
/// an optional single group-by column.
/// </summary>
/// <param name="Skip">Number of items to skip, for paging. Ignored when <see cref="GroupByPropertyName"/> is set (see remarks).</param>
/// <param name="Take">
/// Maximum number of items to return, for paging.
/// <para>
/// <b>v1 limitation:</b> when <see cref="GroupByPropertyName"/> is set,
/// <see cref="Skip"/>/<see cref="Take"/> are ignored and providers are
/// expected to return every matching item (grouped, un-paged). ColonnadeGrid
/// does not ship a pager/infinite-scroll UI in v1, so paging only applies to
/// the ungrouped case. See the developer guide's "known limitations" section.
/// </para>
/// </param>
/// <param name="Sort">The single active sort, or <c>null</c> for unsorted. Multi-column sort is not supported in v1.</param>
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
    /// <summary>A request for the first page of unsorted, unfiltered, ungrouped data.</summary>
    public static DataRequest Default { get; } = new(0, int.MaxValue, null, [], null);
}

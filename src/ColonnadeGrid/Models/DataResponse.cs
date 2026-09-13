namespace ColonnadeGrid.Models;

/// <summary>
/// The page of data returned by an <see cref="Abstractions.IDataProvider{TItem}"/>
/// in response to a <see cref="DataRequest"/>.
/// </summary>
/// <typeparam name="TItem">The row item type.</typeparam>
public sealed class DataResponse<TItem>
{
    /// <summary>
    /// The items for this response, always as a single flat list — even when
    /// grouped. When <see cref="Groups"/> is non-null, items are ordered
    /// group-by-group (all of one group's items appear contiguously, in the
    /// range described by that group's <see cref="DataGroup.StartIndex"/> and
    /// <see cref="DataGroup.Count"/>).
    /// </summary>
    public required IReadOnlyList<TItem> Items { get; init; }

    /// <summary>
    /// The total number of items matching the request's filters, across all
    /// pages (not just <see cref="Items"/>.Count). The pager uses it to count
    /// pages; for an unpaged request it equals <c>Items.Count</c>.
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// Group boundary metadata over <see cref="Items"/> (only groups with items
    /// in it), or <c>null</c> if the request was not grouped
    /// (<see cref="DataRequest.GroupByPropertyName"/> was <c>null</c>).
    /// </summary>
    public IReadOnlyList<DataGroup>? Groups { get; init; }

    /// <summary>An empty, ungrouped response.</summary>
    public static DataResponse<TItem> Empty { get; } = new()
    {
        Items = [],
        TotalCount = 0,
        Groups = null
    };
}

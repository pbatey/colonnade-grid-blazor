namespace ColonnadeGrid.Models;

/// <summary>
/// Asks an <see cref="Abstractions.IGroupedDataProvider{TItem}"/> for a batch of
/// groups and their row counts, in display order.
/// </summary>
/// <param name="Sort">
/// The active sort. When it's on the grouped property, it sets the order of
/// the groups themselves; otherwise group order is the provider's choice, but
/// must be stable across calls.
/// </param>
/// <param name="Filters">The active column filters, combined with AND semantics.</param>
/// <param name="GroupByPropertyName">The property to group by.</param>
/// <param name="Skip">Number of groups to skip.</param>
/// <param name="Take">Maximum number of groups to return.</param>
public sealed record GroupListRequest(
    SortDescriptor? Sort,
    IReadOnlyList<FilterDescriptor> Filters,
    string GroupByPropertyName,
    int Skip,
    int Take);

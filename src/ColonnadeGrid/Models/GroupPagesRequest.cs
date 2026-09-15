namespace ColonnadeGrid.Models;

/// <summary>
/// Asks an <see cref="Abstractions.IGroupedDataProvider{TItem}"/> for a page of
/// rows from each of several groups at once, so expanding many groups costs one
/// call rather than one per group.
/// </summary>
/// <param name="Sort">
/// The primary sort, applied within each group. Providers supporting
/// two-column sort should read <see cref="Sorts"/> for the full ordered list.
/// </param>
/// <param name="Filters">The active column filters, combined with AND semantics.</param>
/// <param name="GroupByPropertyName">The property the groups are grouped by.</param>
/// <param name="Pages">The groups and page windows wanted.</param>
public sealed record GroupPagesRequest(
    SortDescriptor? Sort,
    IReadOnlyList<FilterDescriptor> Filters,
    string GroupByPropertyName,
    IReadOnlyList<GroupPageRequest> Pages)
{
    /// <summary>The active sort keys in priority order; defaults to just <see cref="Sort"/> (empty when <c>null</c>).</summary>
    public IReadOnlyList<SortDescriptor> Sorts { get; init; } = Sort is null ? [] : [Sort];
}

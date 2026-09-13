using ColonnadeGrid.Models;

namespace ColonnadeGrid.LargeData.Shared;

/// <summary>
/// The API's response to a <see cref="DataRequest"/>: one page of issues plus
/// the metadata the client needs to draw a pager.
/// </summary>
/// <param name="Items">The rows on this page, ordered group-by-group when grouped.</param>
/// <param name="TotalCount">Rows matching the filters across all pages.</param>
/// <param name="Groups">
/// Group boundaries over <paramref name="Items"/> (only the groups that appear
/// on this page), or <c>null</c> when ungrouped.
/// </param>
/// <param name="QueryMilliseconds">Time the API spent in Postgres, for comparing against round-trip time.</param>
public sealed record IssuePage(
    IReadOnlyList<Issue> Items,
    int TotalCount,
    IReadOnlyList<DataGroup>? Groups,
    double QueryMilliseconds);

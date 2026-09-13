using ColonnadeGrid.Models;

namespace ColonnadeGrid.LargeData.Shared;

/// <summary>The API's response to a <see cref="GroupListRequest"/>.</summary>
/// <param name="Groups">The requested batch of groups, ordered by key.</param>
/// <param name="TotalGroupCount">Groups matching the filters in all.</param>
/// <param name="TotalCount">Rows matching the filters, across all groups.</param>
/// <param name="QueryMilliseconds">Time the API spent in Postgres.</param>
public sealed record IssueGroupList(
    IReadOnlyList<GroupSummary> Groups,
    int TotalGroupCount,
    int TotalCount,
    double QueryMilliseconds);

/// <summary>The API's response to a <see cref="GroupPagesRequest"/>.</summary>
/// <param name="Pages">One page per requested group, in request order.</param>
/// <param name="QueryMilliseconds">Time the API spent in Postgres.</param>
public sealed record IssueGroupPages(IReadOnlyList<GroupPage<Issue>> Pages, double QueryMilliseconds);

namespace ColonnadeGrid.Models;

/// <summary>The response to a <see cref="GroupListRequest"/>.</summary>
/// <param name="Groups">The requested batch of groups, in display order.</param>
/// <param name="TotalGroupCount">How many groups match the filters in all, so the grid can say how many more there are to show.</param>
/// <param name="TotalCount">How many rows match the filters, across all groups.</param>
public sealed record GroupListResponse(IReadOnlyList<GroupSummary> Groups, int TotalGroupCount, int TotalCount);

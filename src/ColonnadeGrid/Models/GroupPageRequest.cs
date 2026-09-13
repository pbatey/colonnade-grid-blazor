namespace ColonnadeGrid.Models;

/// <summary>One group's page within a <see cref="GroupPagesRequest"/>.</summary>
/// <param name="GroupKey">A <see cref="GroupSummary.Key"/> the provider returned earlier.</param>
/// <param name="Skip">Number of the group's rows to skip.</param>
/// <param name="Take">Maximum number of the group's rows to return.</param>
public sealed record GroupPageRequest(string GroupKey, int Skip, int Take);

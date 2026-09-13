using ColonnadeGrid.Models;

namespace ColonnadeGrid.LargeData.Shared;

/// <summary>The API's response to a <see cref="ColumnStatsRequest"/>.</summary>
/// <param name="Stats">The column's stats.</param>
/// <param name="QueryMilliseconds">Time the API spent in Postgres.</param>
public sealed record IssueColumnStats(ColumnStats Stats, double QueryMilliseconds);

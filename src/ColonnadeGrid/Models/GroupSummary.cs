namespace ColonnadeGrid.Models;

/// <summary>One group in a <see cref="GroupListResponse"/>: its key, header text, and row count.</summary>
/// <param name="Key">The group's key; passed back in <see cref="GroupPageRequest.GroupKey"/> to fetch its rows. Opaque to the grid.</param>
/// <param name="DisplayText">The human-readable text to show in the group header.</param>
/// <param name="Count">The number of rows in the group that match the request's filters.</param>
public sealed record GroupSummary(string Key, string DisplayText, int Count);

namespace ColonnadeGrid.Models;

/// <summary>
/// Metadata describing one contiguous run of grouped items within a
/// <see cref="DataResponse{TItem}.Items"/> list. <see cref="DataResponse{TItem}"/>
/// is always flat; grouping is expressed purely as boundaries over that flat
/// list so collapsing/expanding a group is a client-side concern that never
/// needs to re-fetch data.
/// </summary>
/// <param name="Key">
/// The group's key as an invariant-culture string (e.g. the raw enum name or
/// stringified property value). Used to correlate a group across renders,
/// including for <see cref="GridState.CollapsedGroupKeys"/>.
/// </param>
/// <param name="DisplayText">The human-readable text to show in the group header.</param>
/// <param name="Count">The total number of items in this group.</param>
/// <param name="StartIndex">The index into <see cref="DataResponse{TItem}.Items"/> where this group's items begin.</param>
public sealed record DataGroup(string Key, string DisplayText, int Count, int StartIndex);

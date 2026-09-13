namespace ColonnadeGrid.Models;

/// <summary>One group's rows in response to a <see cref="GroupPageRequest"/>.</summary>
/// <typeparam name="TItem">The row item type.</typeparam>
/// <param name="GroupKey">The requested group's key.</param>
/// <param name="Items">The rows in the requested window, in sort order.</param>
/// <param name="Count">
/// The group's current row count. The grid uses it to refresh the group
/// header, and to move to the group's last page if the requested one no
/// longer exists. A key that matches no rows returns no items and a count of 0.
/// </param>
public sealed record GroupPage<TItem>(string GroupKey, IReadOnlyList<TItem> Items, int Count);

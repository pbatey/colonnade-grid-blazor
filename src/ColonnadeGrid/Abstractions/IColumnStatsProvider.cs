using ColonnadeGrid.Models;

namespace ColonnadeGrid.Abstractions;

/// <summary>
/// An <see cref="IDataProvider{TItem}"/> that can describe a column's values —
/// its range, how many are empty, and (for value lists) which values occur. The
/// grid asks when a filter editor opens, so the editor's choices fit the data:
/// value lists show counts, range sliders span the data's min and max, and date
/// presets that wouldn't change anything are hidden. Without it, editors still
/// work, just without those limits.
/// </summary>
/// <typeparam name="TItem">The row item type.</typeparam>
public interface IColumnStatsProvider<TItem> : IDataProvider<TItem>
{
    /// <summary>
    /// Returns stats for <see cref="ColumnStatsRequest.PropertyName"/> over the rows
    /// matching <see cref="ColumnStatsRequest.Filters"/>, with values formatted by
    /// <see cref="FilterValues.Format"/>.
    /// </summary>
    Task<ColumnStats> GetColumnStatsAsync(ColumnStatsRequest request, CancellationToken cancellationToken = default);
}

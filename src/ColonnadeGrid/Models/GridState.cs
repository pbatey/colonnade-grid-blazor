namespace ColonnadeGrid.Models;

/// <summary>
/// The user-customizable "view configuration" of a <c>ColonnadeGrid</c>:
/// column order/visibility/width, sort, filters, group-by column, and which
/// groups are collapsed.
/// <para>
/// This is an immutable record. Every mutation method below returns a new
/// instance rather than changing <c>this</c> in place, and returns the same
/// instance unchanged when nothing actually changed (e.g. moving a column to
/// its own position). That makes two things correct "for free": binding via
/// <c>@bind-State</c> (each real change produces a new reference, so a host
/// component's <c>StateChanged</c> always fires exactly when something
/// changed), and change detection for persistence (e.g. only write to
/// <c>localStorage</c> when <c>!ReferenceEquals(oldState, newState)</c>).
/// </para>
/// <para>
/// Deliberately excluded from this type: row selection. Selected row keys are
/// ephemeral interaction state, not view configuration a user expects to
/// survive a reload — see <c>ColonnadeGrid&lt;TItem&gt;</c>'s separate
/// <c>@bind-SelectedKeys</c> parameter.
/// </para>
/// </summary>
public sealed record GridState
{
    /// <summary>The default minimum column width (pixels) used by <see cref="SetColumnWidth"/> when not overridden.</summary>
    public const double DefaultMinColumnWidth = 40;

    /// <summary>The default maximum column width (pixels) used by <see cref="SetColumnWidth"/> when not overridden.</summary>
    public const double DefaultMaxColumnWidth = 2000;

    /// <summary>The maximum number of columns the table can sort by at once.</summary>
    public const int MaxSortColumns = 2;

    /// <summary>
    /// The table's columns, in display order. A column's position in this
    /// list <em>is</em> its display order — there is no separate index field.
    /// </summary>
    public required IReadOnlyList<ColumnState> Columns { get; init; }

    /// <summary>
    /// The active sort keys, in priority order: the first is the primary sort,
    /// the second (if any) breaks ties within it. Empty when unsorted. A table
    /// sorts by at most <see cref="MaxSortColumns"/> columns, so this holds 0, 1,
    /// or 2 entries.
    /// </summary>
    public IReadOnlyList<SortDescriptor> Sorts { get; init; } = [];

    /// <summary>
    /// The primary sort key, or <c>null</c> for unsorted — the first of
    /// <see cref="Sorts"/>. Kept for callers (and data providers) that only
    /// handle single-column sort; those see just the primary key and silently
    /// ignore any secondary one.
    /// </summary>
    public SortDescriptor? Sort => Sorts.Count > 0 ? Sorts[0] : null;

    /// <summary>The active column filters.</summary>
    public IReadOnlyList<FilterDescriptor> Filters { get; init; } = [];

    /// <summary>The property currently grouped by, or <c>null</c> for no grouping.</summary>
    public string? GroupByPropertyName { get; init; }

    /// <summary>The set of group keys (see <see cref="DataGroup.Key"/>) currently collapsed.</summary>
    public IReadOnlySet<string> CollapsedGroupKeys { get; init; } = new HashSet<string>();

    /// <summary>
    /// Group keys the user explicitly expanded. Only used when groups are paged
    /// separately (a paged grid over an <c>IGroupedDataProvider</c>), where a group
    /// in neither this set nor <see cref="CollapsedGroupKeys"/> starts expanded or
    /// collapsed according to the grid's row budget.
    /// </summary>
    public IReadOnlySet<string> ExpandedGroupKeys { get; init; } = new HashSet<string>();

    /// <summary>
    /// Builds the default state for a table whose columns (in order) have the
    /// given ids: all columns visible, no explicit width, unsorted,
    /// unfiltered, ungrouped.
    /// </summary>
    public static GridState Create(IEnumerable<string> columnIds) =>
        new() { Columns = columnIds.Select(id => new ColumnState(id)).ToList() };

    /// <summary>
    /// Returns a state that lists every one of <paramref name="columnIds"/>: ids
    /// it doesn't have yet are appended, visible, in the given order. Columns it
    /// already lists keep their position, visibility, and width — including ones
    /// not in <paramref name="columnIds"/>, so a column that's only rendered some
    /// of the time keeps its settings. Returns <c>this</c> when nothing is missing.
    /// The grid calls this to bring a saved state up to date with the columns
    /// declared since it was saved.
    /// </summary>
    public GridState AddMissingColumns(IEnumerable<string> columnIds)
    {
        var listed = Columns.Select(c => c.Id).ToHashSet();
        var missing = columnIds.Where(listed.Add).Select(id => new ColumnState(id)).ToList();
        return missing.Count == 0 ? this : this with { Columns = [.. Columns, .. missing] };
    }

    /// <summary>Whether <paramref name="other"/> asks the data source for the same rows: the same sort, filters, and group-by column.</summary>
    internal bool HasSameQueryAs(GridState other) =>
        Sorts.SequenceEqual(other.Sorts)
        && GroupByPropertyName == other.GroupByPropertyName
        && Filters.Count == other.Filters.Count
        && Filters.Zip(other.Filters).All(pair => FiltersEqual(pair.First, pair.Second));

    /// <summary>Whether <paramref name="other"/> records the same expanded and collapsed groups.</summary>
    internal bool HasSameGroupExpansionAs(GridState other) =>
        CollapsedGroupKeys.SetEquals(other.CollapsedGroupKeys) && ExpandedGroupKeys.SetEquals(other.ExpandedGroupKeys);

    // Record equality compares Values by reference, so compare it by content instead.
    private static bool FiltersEqual(FilterDescriptor a, FilterDescriptor b) =>
        a with { Values = null } == b with { Values = null }
        && (a.Values ?? []).SequenceEqual(b.Values ?? []);

    /// <summary>Returns the column state for the given id, or <c>null</c> if no such column exists.</summary>
    public ColumnState? FindColumn(string columnId) =>
        Columns.FirstOrDefault(c => c.Id == columnId);

    /// <summary>Returns whether the group with the given key is currently collapsed.</summary>
    public bool IsGroupCollapsed(string groupKey) => CollapsedGroupKeys.Contains(groupKey);

    /// <summary>
    /// Moves the column at <paramref name="fromIndex"/> to <paramref name="toIndex"/>,
    /// shifting the columns between them. Returns <c>this</c> unchanged if the
    /// indexes are equal.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Either index is outside the bounds of <see cref="Columns"/>.</exception>
    public GridState MoveColumn(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Columns.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(fromIndex), fromIndex,
                $"Must be within [0, {Columns.Count}).");
        }

        if (toIndex < 0 || toIndex >= Columns.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(toIndex), toIndex,
                $"Must be within [0, {Columns.Count}).");
        }

        if (fromIndex == toIndex)
        {
            return this;
        }

        var columns = Columns.ToList();
        var moving = columns[fromIndex];
        columns.RemoveAt(fromIndex);
        columns.Insert(toIndex, moving);
        return this with { Columns = columns };
    }

    /// <summary>Moves the column with the given id to <paramref name="toIndex"/>. See <see cref="MoveColumn(int,int)"/>.</summary>
    /// <exception cref="ArgumentException">No column with <paramref name="columnId"/> exists.</exception>
    public GridState MoveColumn(string columnId, int toIndex) =>
        MoveColumn(IndexOfColumnOrThrow(columnId), toIndex);

    /// <summary>Returns a new state with the given column's visibility set.</summary>
    /// <exception cref="ArgumentException">No column with <paramref name="columnId"/> exists.</exception>
    public GridState SetColumnVisible(string columnId, bool visible)
    {
        var index = IndexOfColumnOrThrow(columnId);
        if (Columns[index].Visible == visible)
        {
            return this;
        }

        var columns = Columns.ToList();
        columns[index] = columns[index] with { Visible = visible };
        return this with { Columns = columns };
    }

    /// <summary>
    /// Returns a new state with the given column's width set, clamped to
    /// <paramref name="minWidth"/>/<paramref name="maxWidth"/>.
    /// </summary>
    /// <exception cref="ArgumentException">No column with <paramref name="columnId"/> exists, or <paramref name="minWidth"/> exceeds <paramref name="maxWidth"/>.</exception>
    public GridState SetColumnWidth(
        string columnId,
        double width,
        double minWidth = DefaultMinColumnWidth,
        double maxWidth = DefaultMaxColumnWidth)
    {
        if (minWidth > maxWidth)
        {
            throw new ArgumentException(
                $"{nameof(minWidth)} ({minWidth}) cannot exceed {nameof(maxWidth)} ({maxWidth}).");
        }

        var index = IndexOfColumnOrThrow(columnId);
        var clamped = Math.Clamp(width, minWidth, maxWidth);
        if (Columns[index].Width == clamped)
        {
            return this;
        }

        var columns = Columns.ToList();
        columns[index] = columns[index] with { Width = clamped };
        return this with { Columns = columns };
    }

    /// <summary>
    /// Applies a header click for the given property.
    /// <para>
    /// If the column is already a sort key, cycles its direction in place —
    /// Ascending → Descending → off — keeping its position among the other
    /// sort keys. Turning it off removes it and promotes any key behind it
    /// (so clearing the primary sort makes the secondary the new primary).
    /// </para>
    /// <para>
    /// If it isn't a sort key yet, it's added as the newest key: it becomes
    /// the secondary sort when there's already a primary (replacing an
    /// existing secondary, since a table sorts by at most
    /// <see cref="MaxSortColumns"/> columns), or the primary when there was no
    /// sort at all. New keys always start Ascending.
    /// </para>
    /// </summary>
    public GridState SetSort(string propertyName)
    {
        var existing = Sorts.FirstOrDefault(s => s.PropertyName == propertyName);
        if (existing is not null)
        {
            var next = existing.Direction switch
            {
                SortDirection.Ascending => SortDirection.Descending,
                _ => SortDirection.None
            };

            return next == SortDirection.None
                ? RemoveSort(propertyName)
                : SetSortColumn(new SortDescriptor(propertyName, next));
        }

        return SetSortColumn(new SortDescriptor(propertyName, SortDirection.Ascending));
    }

    /// <summary>
    /// Returns a new state that sorts <em>only</em> by the given key (or is
    /// unsorted, if <c>null</c>), discarding any other sort keys. Use
    /// <see cref="AddSort"/> to build a two-column sort instead of replacing.
    /// </summary>
    public GridState SetSort(SortDescriptor? sort) =>
        this with { Sorts = sort is null ? [] : [sort] };

    /// <summary>Returns a new state with <see cref="Sorts"/> set directly, capped at <see cref="MaxSortColumns"/> keys.</summary>
    public GridState SetSorts(IReadOnlyList<SortDescriptor> sorts) =>
        this with { Sorts = sorts.Take(MaxSortColumns).ToList() };

    /// <summary>
    /// Adds or updates a sort key. If the column is already a sort key, its
    /// direction is updated in place, keeping its priority. Otherwise it's
    /// added as the newest (lowest-priority) key: when that would exceed
    /// <see cref="MaxSortColumns"/>, the current secondary key is dropped so
    /// the new one takes its slot, always leaving the primary sort intact.
    /// A key with <see cref="SortDirection.None"/> is removed instead.
    /// </summary>
    public GridState AddSort(SortDescriptor sort) =>
        sort.Direction == SortDirection.None ? RemoveSort(sort.PropertyName) : SetSortColumn(sort);

    /// <summary>
    /// Removes the sort key for the given property, if any, promoting the keys
    /// behind it (so removing the primary sort makes the secondary the new
    /// primary). Returns <c>this</c> unchanged when the column isn't sorted.
    /// </summary>
    public GridState RemoveSort(string propertyName)
    {
        if (Sorts.All(s => s.PropertyName != propertyName))
        {
            return this;
        }

        return this with { Sorts = Sorts.Where(s => s.PropertyName != propertyName).ToList() };
    }

    private GridState SetSortColumn(SortDescriptor sort)
    {
        var index = -1;
        for (var i = 0; i < Sorts.Count; i++)
        {
            if (Sorts[i].PropertyName == sort.PropertyName)
            {
                index = i;
                break;
            }
        }

        var updated = Sorts.ToList();
        if (index >= 0)
        {
            // Already a sort key: update its direction, keep its priority.
            if (updated[index] == sort)
            {
                return this;
            }

            updated[index] = sort;
        }
        else if (updated.Count < MaxSortColumns)
        {
            updated.Add(sort);
        }
        else
        {
            // At capacity: the newest key replaces the current secondary,
            // leaving the primary sort untouched.
            updated[^1] = sort;
        }

        return this with { Sorts = updated };
    }

    /// <summary>
    /// Adds or replaces the filter for <paramref name="filter"/>'s property.
    /// A filter that wouldn't exclude anything is treated as "cleared" and
    /// removed instead, matching the expected UX of emptying a filter's input
    /// box: a value-taking operator with no <see cref="FilterDescriptor.Value"/>,
    /// <see cref="FilterOperator.In"/> with no values (and no empty values), or
    /// <see cref="FilterOperator.Between"/> open at both ends.
    /// </summary>
    public GridState SetFilter(FilterDescriptor filter)
    {
        var isEffectivelyCleared = filter.Operator switch
        {
            FilterOperator.IsEmpty or FilterOperator.IsNotEmpty => false,
            FilterOperator.In => (filter.Values is null || filter.Values.Count == 0) && !filter.IncludeEmpty,
            FilterOperator.Between => string.IsNullOrEmpty(filter.Value) && string.IsNullOrEmpty(filter.ValueTo),
            _ => string.IsNullOrEmpty(filter.Value)
        };

        var remaining = Filters.Where(f => f.PropertyName != filter.PropertyName).ToList();
        if (!isEffectivelyCleared)
        {
            remaining.Add(filter);
        }

        return this with { Filters = remaining };
    }

    /// <summary>Returns a new state with the filter on the given property removed, if any.</summary>
    public GridState RemoveFilter(string propertyName)
    {
        if (Filters.All(f => f.PropertyName != propertyName))
        {
            return this;
        }

        return this with { Filters = Filters.Where(f => f.PropertyName != propertyName).ToList() };
    }

    /// <summary>Returns a new state with all filters removed.</summary>
    public GridState ClearFilters() => Filters.Count == 0 ? this : this with { Filters = [] };

    /// <summary>
    /// Returns a new state grouped by the given property (or ungrouped, if
    /// <c>null</c>). Also resets <see cref="CollapsedGroupKeys"/> and
    /// <see cref="ExpandedGroupKeys"/>, since keys from a previous grouping
    /// column are meaningless once the grouping column changes.
    /// </summary>
    public GridState SetGroupBy(string? propertyName) =>
        this with
        {
            GroupByPropertyName = propertyName,
            CollapsedGroupKeys = new HashSet<string>(),
            ExpandedGroupKeys = new HashSet<string>()
        };

    /// <summary>
    /// Returns a new state recording that the user expanded (or collapsed) the
    /// given group: the key moves into <see cref="ExpandedGroupKeys"/> (or
    /// <see cref="CollapsedGroupKeys"/>) and out of the other set. Returns
    /// <c>this</c> if that's already recorded.
    /// </summary>
    public GridState SetGroupExpanded(string groupKey, bool expanded)
    {
        var target = expanded ? ExpandedGroupKeys : CollapsedGroupKeys;
        var other = expanded ? CollapsedGroupKeys : ExpandedGroupKeys;
        if (target.Contains(groupKey) && !other.Contains(groupKey))
        {
            return this;
        }

        var updatedTarget = new HashSet<string>(target) { groupKey };
        var updatedOther = new HashSet<string>(other);
        updatedOther.Remove(groupKey);

        return expanded
            ? this with { ExpandedGroupKeys = updatedTarget, CollapsedGroupKeys = updatedOther }
            : this with { CollapsedGroupKeys = updatedTarget, ExpandedGroupKeys = updatedOther };
    }

    /// <summary>Returns a new state with the given group's collapsed/expanded state toggled.</summary>
    public GridState ToggleGroupCollapsed(string groupKey)
    {
        var updated = new HashSet<string>(CollapsedGroupKeys);
        if (!updated.Remove(groupKey))
        {
            updated.Add(groupKey);
        }

        return this with { CollapsedGroupKeys = updated };
    }

    private int IndexOfColumnOrThrow(string columnId)
    {
        for (var i = 0; i < Columns.Count; i++)
        {
            if (Columns[i].Id == columnId)
            {
                return i;
            }
        }

        throw new ArgumentException($"No column with Id '{columnId}' is present in this state.", nameof(columnId));
    }
}

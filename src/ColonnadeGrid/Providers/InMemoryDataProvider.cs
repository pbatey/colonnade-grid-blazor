using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using ColonnadeGrid.Abstractions;
using ColonnadeGrid.Models;

namespace ColonnadeGrid.Providers;

/// <summary>
/// The default <see cref="IDataProvider{TItem}"/> used when a
/// <c>ColonnadeGrid</c> is given an in-memory list via its <c>Items</c>
/// parameter. Applies filtering, sorting, grouping, and paging entirely
/// client-side via reflection and LINQ. It also implements
/// <see cref="IGroupedDataProvider{TItem}"/>, so a paged, grouped grid pages
/// each group separately.
/// <para>
/// Deliberately resolves property names via its own reflection-based
/// compiled-accessor cache rather than reusing whatever
/// <c>GridColumn</c>s happen to be registered on a live table — this keeps
/// it fully decoupled and independently constructible/testable.
/// </para>
/// <para>
/// Pipeline order: filter → sort → group → page. Groups appear in the order
/// their first item does (so sorting by the grouped column orders the groups),
/// with keys from <see cref="GroupKeys.From"/>. When a grouped
/// <see cref="DataRequest"/> is paged, the page is taken from the rows in group
/// order, and only the groups with items on the page are returned — clipped to
/// the page, with each group's <see cref="DataGroup.TotalCount"/> across all pages.
/// </para>
/// </summary>
/// <typeparam name="TItem">The row item type.</typeparam>
public sealed class InMemoryDataProvider<TItem> : IGroupedDataProvider<TItem>, IColumnStatsProvider<TItem>
{
    private static readonly ConcurrentDictionary<string, Func<TItem, object?>> AccessorCache = new();

    private readonly IReadOnlyList<TItem> _items;
    private readonly TimeProvider _timeProvider;

    /// <summary>Wraps a snapshot of <paramref name="items"/>. Later changes to the source collection are not observed; construct a new provider to reflect new data.</summary>
    /// <param name="items">The rows.</param>
    /// <param name="timeProvider">The clock for <see cref="FilterOperator.WithinLast"/> filters, which use local time; defaults to the system clock.</param>
    public InMemoryDataProvider(IEnumerable<TItem> items, TimeProvider? timeProvider = null)
    {
        _items = items as IReadOnlyList<TItem> ?? items.ToList();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public Task<DataResponse<TItem>> GetDataAsync(DataRequest request, CancellationToken cancellationToken = default)
    {
        var sorted = FilterAndSort(request.Filters, request.Sort);
        var totalCount = sorted.Count;

        if (request.GroupByPropertyName is { } groupProperty)
        {
            var (groupedItems, groups) = ApplyGroup(sorted, groupProperty);
            var (pageItems, pageGroups) = PageGroupedItems(groupedItems, groups, request.Skip, request.Take);
            return Task.FromResult(new DataResponse<TItem>
            {
                Items = pageItems,
                TotalCount = totalCount,
                Groups = pageGroups
            });
        }

        var paged = sorted.Skip(request.Skip).Take(request.Take).ToList();
        return Task.FromResult(new DataResponse<TItem>
        {
            Items = paged,
            TotalCount = totalCount,
            Groups = null
        });
    }

    /// <inheritdoc />
    public Task<GroupListResponse> GetGroupsAsync(GroupListRequest request, CancellationToken cancellationToken = default)
    {
        var sorted = FilterAndSort(request.Filters, request.Sort);
        var groups = GroupByKey(sorted, request.GroupByPropertyName);

        var page = groups
            .Skip(Math.Max(0, request.Skip))
            .Take(Math.Max(0, request.Take))
            .Select(g => new GroupSummary(g.Key, g.DisplayText, g.Items.Count))
            .ToList();

        return Task.FromResult(new GroupListResponse(page, groups.Count, sorted.Count));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<GroupPage<TItem>>> GetGroupPagesAsync(GroupPagesRequest request, CancellationToken cancellationToken = default)
    {
        var sorted = FilterAndSort(request.Filters, request.Sort);
        var groups = GroupByKey(sorted, request.GroupByPropertyName).ToDictionary(g => g.Key, g => g.Items);

        IReadOnlyList<GroupPage<TItem>> pages = request.Pages
            .Select(page => groups.TryGetValue(page.GroupKey, out var items)
                ? new GroupPage<TItem>(
                    page.GroupKey,
                    items.Skip(Math.Max(0, page.Skip)).Take(Math.Max(0, page.Take)).ToList(),
                    items.Count)
                : new GroupPage<TItem>(page.GroupKey, [], 0))
            .ToList();

        return Task.FromResult(pages);
    }

    /// <inheritdoc />
    public Task<ColumnStats> GetColumnStatsAsync(ColumnStatsRequest request, CancellationToken cancellationToken = default)
    {
        var otherFilters = request.Filters.Where(f => f.PropertyName != request.PropertyName).ToList();
        var accessor = GetAccessor(request.PropertyName);
        var values = FilterAndSort(otherFilters, sort: null).Select(accessor).ToList();
        var nonEmpty = values.Where(value => !IsEmptyValue(value)).ToList();
        var comparer = Comparer<object?>.Create(CompareValues);

        IReadOnlyList<ColumnValueCount>? valueCounts = null;
        var hasMoreValues = false;
        if (request.IncludeValueCounts)
        {
            var distinct = nonEmpty
                .GroupBy(value => FilterValues.Format(value)!)
                .Select(group => (Sample: group.First(), Text: group.Key, Count: group.Count()))
                .OrderBy(group => group.Sample, comparer)
                .ToList();
            hasMoreValues = distinct.Count > request.MaxValueCount;
            valueCounts = distinct
                .Take(Math.Max(0, request.MaxValueCount))
                .Select(group => new ColumnValueCount(group.Text, group.Count))
                .ToList();
        }

        return Task.FromResult(new ColumnStats(
            Min: nonEmpty.Count == 0 ? null : FilterValues.Format(nonEmpty.Min(comparer)),
            Max: nonEmpty.Count == 0 ? null : FilterValues.Format(nonEmpty.Max(comparer)),
            EmptyCount: values.Count - nonEmpty.Count,
            TotalCount: values.Count,
            Values: valueCounts,
            HasMoreValues: hasMoreValues));
    }

    private List<TItem> FilterAndSort(IReadOnlyList<FilterDescriptor> filters, SortDescriptor? sort)
    {
        // One "now" per query, so every row is compared against the same instant.
        var now = _timeProvider.GetLocalNow();
        IEnumerable<TItem> filtered = _items;
        foreach (var filter in filters)
        {
            filtered = filtered.Where(item => MatchesFilter(item, filter, now));
        }

        return sort is { Direction: not SortDirection.None } activeSort
            ? ApplySort(filtered, activeSort).ToList()
            : filtered.ToList();
    }

    private static IEnumerable<TItem> ApplySort(IEnumerable<TItem> items, SortDescriptor sort)
    {
        var accessor = GetAccessor(sort.PropertyName);
        var comparer = Comparer<object?>.Create(CompareValues);
        return sort.Direction == SortDirection.Ascending
            ? items.OrderBy(accessor, comparer)
            : items.OrderByDescending(accessor, comparer);
    }

    /// <summary>
    /// Groups <paramref name="items"/> by key. GroupBy preserves the
    /// first-appearance order of each key from the source sequence, and source
    /// order within each group — so grouping after sorting means groups
    /// naturally reflect that sort (e.g. sorting and grouping by the same
    /// column produces alphabetically/naturally ordered groups "for free").
    /// Grouping by the key rather than the raw value keeps <c>null</c> and
    /// <c>""</c> apart (see <see cref="GroupKeys.Null"/>) while guaranteeing
    /// every group has a distinct key.
    /// </summary>
    private static List<(string Key, string DisplayText, List<TItem> Items)> GroupByKey(
        IEnumerable<TItem> items,
        string propertyName)
    {
        var accessor = GetAccessor(propertyName);
        return items
            .GroupBy(item => GroupKeys.From(accessor(item)))
            .Select(group => (group.Key, accessor(group.First())?.ToString() ?? "", group.ToList()))
            .ToList();
    }

    private static (IReadOnlyList<TItem> Items, IReadOnlyList<DataGroup> Groups) ApplyGroup(
        IEnumerable<TItem> items,
        string propertyName)
    {
        var flatItems = new List<TItem>();
        var groups = new List<DataGroup>();

        foreach (var group in GroupByKey(items, propertyName))
        {
            groups.Add(new DataGroup(group.Key, group.DisplayText, group.Items.Count, flatItems.Count, group.Items.Count));
            flatItems.AddRange(group.Items);
        }

        return (flatItems, groups);
    }

    /// <summary>
    /// Takes the <paramref name="skip"/>/<paramref name="take"/> window of an
    /// already-grouped flat list, clipping each group to the part that falls on
    /// this page (groups with no items on it are dropped). Each group keeps its
    /// <see cref="DataGroup.TotalCount"/> across all pages.
    /// </summary>
    private static (IReadOnlyList<TItem> Items, IReadOnlyList<DataGroup> Groups) PageGroupedItems(
        IReadOnlyList<TItem> items,
        IReadOnlyList<DataGroup> groups,
        int skip,
        int take)
    {
        var start = Math.Clamp(skip, 0, items.Count);
        var end = (int)Math.Min((long)start + Math.Max(take, 0), items.Count);
        if (start == 0 && end == items.Count)
        {
            return (items, groups);
        }

        var pageGroups = new List<DataGroup>();
        foreach (var group in groups)
        {
            var from = Math.Max(group.StartIndex, start);
            var to = Math.Min(group.StartIndex + group.Count, end);
            if (from < to)
            {
                pageGroups.Add(group with { Count = to - from, StartIndex = from - start });
            }
        }

        return (items.Skip(start).Take(end - start).ToList(), pageGroups);
    }

    private static bool MatchesFilter(TItem item, FilterDescriptor filter, DateTimeOffset now)
    {
        var accessor = GetAccessor(filter.PropertyName);
        var rawValue = accessor(item);

        switch (filter.Operator)
        {
            case FilterOperator.IsEmpty:
                return IsEmptyValue(rawValue);
            case FilterOperator.IsNotEmpty:
                return !IsEmptyValue(rawValue);
            case FilterOperator.In or FilterOperator.Between or FilterOperator.WithinLast when IsEmptyValue(rawValue):
                return filter.IncludeEmpty;
            case FilterOperator.In:
                return (filter.Values ?? []).Any(value => CompareToFilterValue(rawValue, value) == 0);
            case FilterOperator.Between:
                return (string.IsNullOrEmpty(filter.Value) || CompareToFilterValue(rawValue, filter.Value) >= 0)
                    && (string.IsNullOrEmpty(filter.ValueTo) || CompareToFilterValue(rawValue, filter.ValueTo) <= 0);
            case FilterOperator.WithinLast:
                return IsWithinLast(rawValue!, filter.Value, now);
        }

        var text = rawValue?.ToString() ?? "";
        switch (filter.Operator)
        {
            case FilterOperator.Contains:
                return text.Contains(filter.Value ?? "", StringComparison.OrdinalIgnoreCase);
            case FilterOperator.StartsWith:
                return text.StartsWith(filter.Value ?? "", StringComparison.OrdinalIgnoreCase);
        }

        var comparison = CompareToFilterValue(rawValue, filter.Value);
        return filter.Operator switch
        {
            FilterOperator.Equals => comparison == 0,
            FilterOperator.NotEquals => comparison != 0,
            FilterOperator.GreaterThan => comparison > 0,
            FilterOperator.LessThan => comparison < 0,
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter.Operator, "Unrecognized filter operator.")
        };
    }

    private static bool IsEmptyValue(object? value) =>
        value is null || (value is string text && text.Length == 0);

    /// <summary>Whether a date value falls within <paramref name="period"/> before <paramref name="now"/>, up to now. Non-date values and invalid periods never match.</summary>
    private static bool IsWithinLast(object value, string? period, DateTimeOffset now)
    {
        if (!RelativeDatePeriod.TryGetStart(period, now.DateTime, out var start))
        {
            return false;
        }

        return value switch
        {
            DateTime dateTime => dateTime >= start && dateTime <= now.DateTime,
            DateTimeOffset dateTimeOffset => dateTimeOffset >= new DateTimeOffset(start, now.Offset) && dateTimeOffset <= now,
            DateOnly date => date >= DateOnly.FromDateTime(start) && date <= DateOnly.FromDateTime(now.DateTime),
            _ => false
        };
    }

    /// <summary>
    /// Converts <paramref name="filterValueText"/> to <paramref name="rawValue"/>'s
    /// runtime type (via <see cref="TypeConverter"/>, invariant culture) and
    /// compares them, so e.g. numeric/date filters compare numerically/chronologically
    /// rather than lexically. Falls back to an ordinal string comparison if
    /// the value can't be converted.
    /// </summary>
    private static int CompareToFilterValue(object? rawValue, string? filterValueText)
    {
        if (rawValue is null && filterValueText is null)
        {
            return 0;
        }

        if (rawValue is null)
        {
            return -1;
        }

        if (filterValueText is null)
        {
            return 1;
        }

        var targetType = rawValue.GetType();
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        object? convertedFilterValue;
        try
        {
            if (underlyingType.IsEnum)
            {
                convertedFilterValue = Enum.Parse(underlyingType, filterValueText, ignoreCase: true);
            }
            else
            {
                var converter = TypeDescriptor.GetConverter(underlyingType);
                convertedFilterValue = converter.CanConvertFrom(typeof(string))
                    ? converter.ConvertFromInvariantString(filterValueText)
                    : Convert.ChangeType(filterValueText, underlyingType, CultureInfo.InvariantCulture);
            }
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or NotSupportedException or ArgumentException or OverflowException)
        {
            return string.Compare(rawValue.ToString(), filterValueText, StringComparison.OrdinalIgnoreCase);
        }

        return rawValue is IComparable comparable
            ? comparable.CompareTo(convertedFilterValue)
            : string.Compare(rawValue.ToString(), convertedFilterValue?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareValues(object? x, object? y)
    {
        if (x is null && y is null)
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        return x is IComparable comparable
            ? comparable.CompareTo(y)
            : string.Compare(x.ToString(), y.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static Func<TItem, object?> GetAccessor(string propertyName) =>
        AccessorCache.GetOrAdd(propertyName, BuildAccessor);

    private static Func<TItem, object?> BuildAccessor(string propertyName)
    {
        var property = typeof(TItem).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new ArgumentException(
                $"Type '{typeof(TItem).Name}' has no public instance property named '{propertyName}'.",
                nameof(propertyName));

        var parameter = Expression.Parameter(typeof(TItem), "item");
        var propertyAccess = Expression.Property(parameter, property);
        var boxed = Expression.Convert(propertyAccess, typeof(object));
        return Expression.Lambda<Func<TItem, object?>>(boxed, parameter).Compile();
    }
}

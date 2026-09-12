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
/// client-side via reflection and LINQ.
/// <para>
/// Deliberately resolves property names via its own reflection-based
/// compiled-accessor cache rather than reusing whatever
/// <c>GridColumn</c>s happen to be registered on a live table — this keeps
/// it fully decoupled and independently constructible/testable.
/// </para>
/// <para>
/// Pipeline order: filter → sort → group → page. Per the v1 grouping
/// limitation documented on <see cref="DataRequest"/>, when
/// <see cref="DataRequest.GroupByPropertyName"/> is set, paging
/// (<see cref="DataRequest.Skip"/>/<see cref="DataRequest.Take"/>) is
/// ignored and every matching item is returned, grouped.
/// </para>
/// </summary>
/// <typeparam name="TItem">The row item type.</typeparam>
public sealed class InMemoryDataProvider<TItem> : IDataProvider<TItem>
{
    private static readonly ConcurrentDictionary<string, Func<TItem, object?>> AccessorCache = new();

    private readonly IReadOnlyList<TItem> _items;

    /// <summary>Wraps a snapshot of <paramref name="items"/>. Later changes to the source collection are not observed; construct a new provider to reflect new data.</summary>
    public InMemoryDataProvider(IEnumerable<TItem> items)
    {
        _items = items as IReadOnlyList<TItem> ?? items.ToList();
    }

    /// <inheritdoc />
    public Task<DataResponse<TItem>> GetDataAsync(DataRequest request, CancellationToken cancellationToken = default)
    {
        IEnumerable<TItem> filtered = _items;
        foreach (var filter in request.Filters)
        {
            filtered = filtered.Where(item => MatchesFilter(item, filter));
        }

        var filteredList = filtered.ToList();
        var totalCount = filteredList.Count;

        IEnumerable<TItem> sorted = request.Sort is { Direction: not SortDirection.None } sort
            ? ApplySort(filteredList, sort)
            : filteredList;

        if (request.GroupByPropertyName is { } groupProperty)
        {
            var (groupedItems, groups) = ApplyGroup(sorted, groupProperty);
            return Task.FromResult(new DataResponse<TItem>
            {
                Items = groupedItems,
                TotalCount = totalCount,
                Groups = groups
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

    private static IEnumerable<TItem> ApplySort(IEnumerable<TItem> items, SortDescriptor sort)
    {
        var accessor = GetAccessor(sort.PropertyName);
        var comparer = Comparer<object?>.Create(CompareValues);
        return sort.Direction == SortDirection.Ascending
            ? items.OrderBy(accessor, comparer)
            : items.OrderByDescending(accessor, comparer);
    }

    private static (IReadOnlyList<TItem> Items, IReadOnlyList<DataGroup> Groups) ApplyGroup(
        IEnumerable<TItem> items,
        string propertyName)
    {
        var accessor = GetAccessor(propertyName);

        var flatItems = new List<TItem>();
        var groups = new List<DataGroup>();

        // GroupBy preserves first-appearance order of each key from the
        // source sequence, and source order within each group — so grouping
        // after sorting means groups naturally reflect that sort (e.g.
        // sorting and grouping by the same column produces alphabetically/
        // naturally ordered groups "for free").
        foreach (var group in items.GroupBy(accessor))
        {
            var keyText = group.Key?.ToString() ?? "";
            var startIndex = flatItems.Count;
            var groupItems = group.ToList();
            flatItems.AddRange(groupItems);
            groups.Add(new DataGroup(keyText, keyText, groupItems.Count, startIndex));
        }

        return (flatItems, groups);
    }

    private static bool MatchesFilter(TItem item, FilterDescriptor filter)
    {
        var accessor = GetAccessor(filter.PropertyName);
        var rawValue = accessor(item);

        switch (filter.Operator)
        {
            case FilterOperator.IsEmpty:
                return IsEmptyValue(rawValue);
            case FilterOperator.IsNotEmpty:
                return !IsEmptyValue(rawValue);
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

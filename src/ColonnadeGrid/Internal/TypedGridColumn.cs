using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ColonnadeGrid.Abstractions;

namespace ColonnadeGrid.Internal;

/// <summary>
/// The concrete <see cref="GridColumnBase{TItem}"/> produced by
/// <c>GridColumn&lt;TItem, TProp&gt;</c>. Keeping the property type
/// (<typeparamref name="TProp"/>) here — rather than boxing everything to
/// <see cref="object"/> at the column-definition level — means sorting uses a
/// correctly-typed <see cref="Comparer{T}.Default"/> instead of comparing
/// boxed objects.
/// </summary>
internal sealed class TypedGridColumn<TItem, TProp> : GridColumnBase<TItem>
{
    private readonly Func<TItem, TProp> _accessor;
    private readonly string? _format;

    [SetsRequiredMembers]
    public TypedGridColumn(
        Func<TItem, TProp> accessor,
        string propertyName,
        string? title = null,
        string? id = null,
        string? format = null)
    {
        _accessor = accessor;
        _format = format;
        Id = id ?? propertyName;
        PropertyName = propertyName;
        Title = title ?? propertyName;
    }

    public override object? GetCellValue(TItem item) => _accessor(item);

    public override string GetDisplayText(TItem item)
    {
        var value = _accessor(item);
        if (value is null)
        {
            return "";
        }

        if (_format is not null && value is IFormattable formattable)
        {
            return formattable.ToString(_format, CultureInfo.InvariantCulture);
        }

        return value.ToString() ?? "";
    }

    public override int Compare(TItem a, TItem b) =>
        Comparer<TProp>.Default.Compare(_accessor(a), _accessor(b));
}

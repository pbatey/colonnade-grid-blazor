using Microsoft.AspNetCore.Components;
using ColonnadeGrid.Models;

namespace ColonnadeGrid.Abstractions;

/// <summary>
/// A live column definition registered with a table: identity, display
/// options, and typed value access/comparison, independent of the column's
/// property type (<c>TProp</c>) so a table can hold a single homogeneous list
/// of columns whose properties are different types. Instances are produced by
/// the <c>GridColumn&lt;TItem, TProp&gt;</c> component and are otherwise
/// plain data — they are not Razor components themselves, so they can be
/// constructed and exercised directly in unit tests without any rendering.
/// </summary>
/// <typeparam name="TItem">The row item type.</typeparam>
public abstract class GridColumnBase<TItem>
{
    /// <summary>
    /// The column's stable identifier, used as the key in
    /// <see cref="Models.ColumnState"/>/<see cref="Models.GridState"/>.
    /// Defaults to <see cref="PropertyName"/> when not explicitly set.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The <c>.NET</c> property name this column reads from its <c>Field</c>
    /// expression. Used as the key in <see cref="Models.SortDescriptor"/>/
    /// <see cref="Models.FilterDescriptor"/>/<see cref="Models.DataRequest.GroupByPropertyName"/>.
    /// </summary>
    public required string PropertyName { get; init; }

    /// <summary>The column's display header text.</summary>
    public required string Title { get; init; }

    /// <summary>Whether clicking this column's header sorts by it.</summary>
    public bool Sortable { get; init; }

    /// <summary>Whether this column exposes a filter control.</summary>
    public bool Filterable { get; init; }

    /// <summary>Whether this column can be selected as the group-by column.</summary>
    public bool Groupable { get; init; }

    /// <summary>The type of the property this column reads.</summary>
    public virtual Type PropertyType => typeof(object);

    /// <summary>
    /// The filter editor to show, overriding the one chosen from
    /// <see cref="PropertyType"/>; <c>null</c> to choose automatically.
    /// </summary>
    public FilterKind? FilterKind { get; init; }

    /// <summary>The filter editor this column shows: <see cref="FilterKind"/> if set, otherwise the default for <see cref="PropertyType"/>.</summary>
    public FilterKind EffectiveFilterKind => FilterKind ?? FilterKinds.ForType(PropertyType);

    /// <summary>Custom cell content. When <c>null</c>, <see cref="GetDisplayText"/> is rendered as plain text.</summary>
    public RenderFragment<TItem>? CellTemplate { get; init; }

    /// <summary>Custom header content. When <c>null</c>, <see cref="Title"/> is rendered as plain text.</summary>
    public RenderFragment? HeaderTemplate { get; init; }

    /// <summary>Returns the raw property value for the given item, boxed.</summary>
    public abstract object? GetCellValue(TItem item);

    /// <summary>
    /// Returns the display text for the given item — used for the default
    /// (template-less) cell rendering and for group header text.
    /// </summary>
    public abstract string GetDisplayText(TItem item);

    /// <summary>
    /// Compares two items by this column's property value, using the
    /// property type's natural ordering (<see cref="Comparer{T}.Default"/>).
    /// </summary>
    public abstract int Compare(TItem a, TItem b);
}

namespace ColonnadeGrid.Models;

/// <summary>
/// The user-customizable state of a single column: whether it's shown and how
/// wide it is. A column's position in <see cref="GridState.Columns"/>
/// <em>is</em> its display order — there is no separate order/index field to
/// keep in sync.
/// </summary>
/// <param name="Id">
/// The column's stable identifier. Defaults to the property name derived from
/// its <c>Field</c> expression, or an explicit <c>Id</c> the column declares.
/// </param>
/// <param name="Visible">Whether the column is currently shown.</param>
/// <param name="Width">
/// The column's width in pixels, or <c>null</c> to use the column's default/auto width.
/// </param>
public sealed record ColumnState(string Id, bool Visible = true, double? Width = null);

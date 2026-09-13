namespace ColonnadeGrid.Models;

/// <summary>
/// A single active column filter. Values are deliberately culture-invariant
/// strings rather than <see cref="object"/>: a <see cref="DataRequest"/> is the
/// contract a real server-side <see cref="Abstractions.IDataProvider{TItem}"/>
/// implementation will typically serialize (query string, JSON body, gRPC
/// message, ...), and a polymorphic <c>object</c> value is a classic "what type
/// is this on the wire" source of bugs. <see cref="FilterValues.Format"/>
/// produces them; <see cref="Providers.InMemoryDataProvider{TItem}"/> converts
/// them back to the column's property type using
/// <see cref="System.ComponentModel.TypeConverter"/>s.
/// </summary>
/// <param name="PropertyName">The property being filtered, matching the column's <c>Field</c> expression.</param>
/// <param name="Operator">The comparison to apply.</param>
/// <param name="Value">
/// The filter value: the comparison value for most operators, the lower bound
/// (or <c>null</c> for none) for <see cref="FilterOperator.Between"/>, and the
/// period for <see cref="FilterOperator.WithinLast"/>. Ignored by
/// <see cref="FilterOperator.IsEmpty"/>, <see cref="FilterOperator.IsNotEmpty"/>,
/// and <see cref="FilterOperator.In"/>.
/// </param>
/// <param name="ValueTo">The upper bound for <see cref="FilterOperator.Between"/>, or <c>null</c> for none.</param>
/// <param name="Values">The accepted values for <see cref="FilterOperator.In"/>.</param>
/// <param name="IncludeEmpty">
/// For <see cref="FilterOperator.In"/>, <see cref="FilterOperator.Between"/>, and
/// <see cref="FilterOperator.WithinLast"/>: whether rows with an empty (null or
/// <c>""</c>) value match too. Otherwise those operators never match empty values.
/// </param>
public sealed record FilterDescriptor(
    string PropertyName,
    FilterOperator Operator,
    string? Value,
    string? ValueTo = null,
    IReadOnlyList<string>? Values = null,
    bool IncludeEmpty = false);

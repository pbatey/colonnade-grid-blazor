namespace ColonnadeGrid.Models;

/// <summary>
/// A single active column filter. <see cref="Value"/> is deliberately a
/// culture-invariant string rather than <see cref="object"/>: a
/// <see cref="DataRequest"/> is the contract a real server-side
/// <see cref="IDataProvider{TItem}"/> implementation will typically serialize
/// (query string, JSON body, gRPC message, ...), and a polymorphic
/// <c>object</c> value is a classic "what type is this on the wire" source of
/// bugs. <see cref="Providers.InMemoryDataProvider{TItem}"/> converts the
/// string to the column's property type itself using
/// <see cref="System.ComponentModel.TypeConverter"/>s.
/// </summary>
/// <param name="PropertyName">The property being filtered, matching the column's <c>Field</c> expression.</param>
/// <param name="Operator">The comparison to apply.</param>
/// <param name="Value">
/// The filter value as an invariant-culture string. Ignored by
/// <see cref="FilterOperator.IsEmpty"/> and <see cref="FilterOperator.IsNotEmpty"/>.
/// </param>
public sealed record FilterDescriptor(string PropertyName, FilterOperator Operator, string? Value);

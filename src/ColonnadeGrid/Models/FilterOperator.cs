namespace ColonnadeGrid.Models;

/// <summary>
/// The comparison applied by a single column filter. <see cref="IsEmpty"/> and
/// <see cref="IsNotEmpty"/> ignore <see cref="FilterDescriptor.Value"/>.
/// </summary>
public enum FilterOperator
{
    /// <summary>The stringified property value contains the filter value (case-insensitive).</summary>
    Contains,

    /// <summary>The property value equals the filter value.</summary>
    Equals,

    /// <summary>The property value does not equal the filter value.</summary>
    NotEquals,

    /// <summary>The stringified property value starts with the filter value (case-insensitive).</summary>
    StartsWith,

    /// <summary>The property value is greater than the filter value. Requires a comparable property type.</summary>
    GreaterThan,

    /// <summary>The property value is less than the filter value. Requires a comparable property type.</summary>
    LessThan,

    /// <summary>The property value is null or an empty string.</summary>
    IsEmpty,

    /// <summary>The property value is neither null nor an empty string.</summary>
    IsNotEmpty,

    /// <summary>
    /// The property value equals one of <see cref="FilterDescriptor.Values"/>.
    /// Used by the value-list filter editor (enums, booleans, and columns set to
    /// <see cref="FilterKind.Values"/>).
    /// </summary>
    In,

    /// <summary>
    /// The property value is at least <see cref="FilterDescriptor.Value"/> and at
    /// most <see cref="FilterDescriptor.ValueTo"/>; either may be <c>null</c> for an
    /// open end. Used by the number, date, and duration range editors.
    /// </summary>
    Between,

    /// <summary>
    /// A date property value falls within the period in
    /// <see cref="FilterDescriptor.Value"/> (e.g. <c>"P30D"</c>, see
    /// <see cref="RelativeDatePeriod"/>) before now, up to now. "Now" is taken when
    /// the data source runs the query, so a saved "last 30 days" filter stays relative.
    /// </summary>
    WithinLast
}

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
    IsNotEmpty
}

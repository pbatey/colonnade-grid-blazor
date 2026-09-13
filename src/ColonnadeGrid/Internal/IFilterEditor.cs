using ColonnadeGrid.Models;

namespace ColonnadeGrid.Internal;

/// <summary>A filter editor hosted by <c>FilterPopover</c>, which owns the Apply/Clear buttons.</summary>
internal interface IFilterEditor
{
    /// <summary>Whether the current input makes a valid filter (Apply is disabled otherwise).</summary>
    bool CanApply { get; }

    /// <summary>The filter the current input describes, or <c>null</c> if it wouldn't exclude anything.</summary>
    FilterDescriptor? BuildFilter();
}

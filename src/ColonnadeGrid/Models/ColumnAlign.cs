namespace ColonnadeGrid.Models;

/// <summary>
/// Horizontal text alignment for a column's header and body cells. Applied to
/// both so the header label lines up over its values. <see cref="Left"/> is the
/// default and matches the grid's original behavior.
/// </summary>
public enum ColumnAlign
{
    /// <summary>Left-aligned (default).</summary>
    Left,

    /// <summary>Center-aligned.</summary>
    Center,

    /// <summary>Right-aligned — typical for numeric/duration columns.</summary>
    Right
}

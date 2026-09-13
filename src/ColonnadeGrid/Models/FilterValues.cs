using System.Globalization;

namespace ColonnadeGrid.Models;

/// <summary>
/// The culture-invariant text form of values in filters and column stats
/// (<see cref="FilterDescriptor.Value"/>, <see cref="ColumnStats.Min"/>, ...), so
/// the grid, the in-memory provider, and a server all read them the same way.
/// </summary>
public static class FilterValues
{
    /// <summary>
    /// Formats <paramref name="value"/>: round-trip ("O") ISO 8601 for dates and
    /// times, the constant ("c") format for <see cref="TimeSpan"/>
    /// (<c>d.hh:mm:ss</c>), member names for enums, and invariant formatting for
    /// numbers. Returns <c>null</c> for <c>null</c>.
    /// </summary>
    public static string? Format(object? value) => value switch
    {
        null => null,
        string text => text,
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
        TimeOnly time => time.ToString("O", CultureInfo.InvariantCulture),
        TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
        Enum member => member.ToString(),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
}

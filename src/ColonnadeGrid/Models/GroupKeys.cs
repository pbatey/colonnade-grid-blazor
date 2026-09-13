using System.Globalization;

namespace ColonnadeGrid.Models;

/// <summary>
/// Turns group values into the string keys used by <see cref="DataGroup.Key"/>
/// and <see cref="GroupSummary.Key"/>. The grid treats keys as opaque — a
/// provider only has to recognize the keys it produced itself — so this is the
/// convention <see cref="Providers.InMemoryDataProvider{TItem}"/> uses, offered
/// for reuse rather than required.
/// </summary>
public static class GroupKeys
{
    /// <summary>
    /// The key for the group whose value is <c>null</c>. A single NUL character,
    /// so it can't be confused with an empty string's key (<c>""</c>).
    /// </summary>
    public const string Null = "\0";

    /// <summary>
    /// A culture-invariant key for <paramref name="value"/>: <see cref="Null"/>
    /// for <c>null</c>, round-trip ("O") format for dates, and invariant
    /// formatting for other formattable values (numbers, enums).
    /// </summary>
    public static string From(object? value) => value switch
    {
        null => Null,
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };
}

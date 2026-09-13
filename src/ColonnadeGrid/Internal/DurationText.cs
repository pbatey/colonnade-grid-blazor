namespace ColonnadeGrid.Internal;

/// <summary>Short duration labels for the duration filter editor, in the units it's using.</summary>
internal static class DurationText
{
    /// <summary>The seconds in a "month" in duration filters, which count a month as 30 days.</summary>
    public const double SecondsPerMonth = 30 * SecondsPerDay;

    public const double SecondsPerDay = 86_400;

    /// <summary>
    /// Formats <paramref name="duration"/> as months, weeks, and days ("1mo 2w 3d")
    /// when <paramref name="longUnits"/> is set, otherwise as hours and minutes ("26h 5m").
    /// Durations under a day always use hours and minutes, so "30m" doesn't round to "0d".
    /// </summary>
    public static string Format(TimeSpan duration, bool longUnits)
    {
        var parts = new List<string>();
        if (longUnits && duration.TotalDays >= 1)
        {
            var days = (long)Math.Round(duration.TotalDays);
            AddPart(parts, days / 30, "mo");
            AddPart(parts, days % 30 / 7, "w");
            AddPart(parts, days % 30 % 7, "d");
            return parts.Count == 0 ? "0d" : string.Join(" ", parts);
        }

        var minutes = (long)Math.Round(duration.TotalMinutes);
        AddPart(parts, minutes / 60, "h");
        AddPart(parts, minutes % 60, "m");
        return parts.Count == 0 ? "0m" : string.Join(" ", parts);
    }

    private static void AddPart(List<string> parts, long amount, string suffix)
    {
        if (amount > 0)
        {
            parts.Add($"{amount}{suffix}");
        }
    }
}

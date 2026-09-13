using System.Globalization;

namespace ColonnadeGrid.Models;

/// <summary>
/// The periods used by <see cref="FilterOperator.WithinLast"/>: a small subset of
/// ISO 8601 durations with one unit — <c>P{n}D</c> (days), <c>P{n}W</c> (weeks),
/// <c>P{n}M</c> (months), or <c>P{n}Y</c> (years), for a positive whole number n.
/// Months and years are calendar months and years, so <c>P1M</c> before March 31
/// is February 28 (or 29).
/// </summary>
public static class RelativeDatePeriod
{
    /// <summary>Parses a period into its amount and unit (<c>'D'</c>, <c>'W'</c>, <c>'M'</c>, or <c>'Y'</c>).</summary>
    public static bool TryParse(string? period, out int amount, out char unit)
    {
        amount = 0;
        unit = default;
        if (period is not { Length: >= 3 } || period[0] != 'P' || "DWMY".IndexOf(period[^1]) < 0)
        {
            return false;
        }

        if (!int.TryParse(period.AsSpan(1, period.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out amount)
            || amount <= 0)
        {
            return false;
        }

        unit = period[^1];
        return true;
    }

    /// <summary>The start of <paramref name="period"/> ending at <paramref name="now"/>.</summary>
    public static bool TryGetStart(string? period, DateTime now, out DateTime start)
    {
        start = default;
        if (!TryParse(period, out var amount, out var unit))
        {
            return false;
        }

        start = unit switch
        {
            'D' => now.AddDays(-amount),
            'W' => now.AddDays(-7 * amount),
            'M' => now.AddMonths(-amount),
            _ => now.AddYears(-amount)
        };
        return true;
    }
}

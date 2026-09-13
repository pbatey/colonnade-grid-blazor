namespace ColonnadeGrid.Models;

/// <summary>One distinct column value and how many rows have it.</summary>
/// <param name="Value">The value, formatted with <see cref="FilterValues.Format"/>.</param>
/// <param name="Count">The number of rows with the value.</param>
public sealed record ColumnValueCount(string Value, int Count);

/// <summary>The response to a <see cref="ColumnStatsRequest"/>.</summary>
/// <param name="Min">The smallest non-empty value, formatted with <see cref="FilterValues.Format"/>, or <c>null</c> if there are none.</param>
/// <param name="Max">The largest non-empty value, formatted the same way, or <c>null</c> if there are none.</param>
/// <param name="EmptyCount">Rows whose value is null (or an empty string).</param>
/// <param name="TotalCount">Rows matching the request's filters, empty or not.</param>
/// <param name="Values">
/// When requested, the distinct non-empty values with their counts, in the
/// column's sort order; otherwise <c>null</c>.
/// </param>
/// <param name="HasMoreValues">Whether there were more distinct values than <see cref="ColumnStatsRequest.MaxValueCount"/>.</param>
public sealed record ColumnStats(
    string? Min,
    string? Max,
    int EmptyCount,
    int TotalCount,
    IReadOnlyList<ColumnValueCount>? Values = null,
    bool HasMoreValues = false);

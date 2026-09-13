using ColonnadeGrid.Models;
using ColonnadeGrid.Providers;

namespace ColonnadeGrid.Tests.Providers;

/// <summary>The value-list, range, and relative-date filters, and column stats.</summary>
public class InMemoryDataProviderFilterTests
{
    private enum Kind
    {
        Bug,
        Feature,
        Chore
    }

    private sealed record WorkItem(string Name, Kind? Kind, double? Estimate, DateTime? Due, TimeSpan? Spent, DateOnly? Day);

    private static readonly WorkItem[] Items =
    [
        new("A", Kind.Bug, 1.5, new DateTime(2026, 9, 10), TimeSpan.FromMinutes(30), new DateOnly(2026, 9, 10)),
        new("B", Kind.Feature, 8.0, new DateTime(2026, 6, 1), TimeSpan.FromDays(2), new DateOnly(2026, 6, 1)),
        new("C", null, null, null, null, null),
        new("D", Kind.Chore, 40.25, new DateTime(2024, 1, 15), TimeSpan.FromDays(30), new DateOnly(2024, 1, 15)),
        new("E", Kind.Bug, 0.5, new DateTime(2026, 9, 14), TimeSpan.FromMinutes(75), new DateOnly(2026, 9, 14)),
    ];

    /// <summary>A clock stopped at 2026-09-13 12:00 in a UTC local time zone.</summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static readonly InMemoryDataProvider<WorkItem> Provider = new(Items, new FixedTimeProvider());

    private static async Task<List<string>> NamesMatching(params FilterDescriptor[] filters)
    {
        var response = await Provider.GetDataAsync(new DataRequest(0, int.MaxValue, null, filters, null));
        return response.Items.Select(i => i.Name).ToList();
    }

    // ----- In -----

    [Fact]
    public async Task In_MatchesAnyListedValue()
    {
        Assert.Equal(["A", "D", "E"], await NamesMatching(new FilterDescriptor("Kind", FilterOperator.In, null, Values: ["Bug", "Chore"])));
    }

    [Fact]
    public async Task In_WithIncludeEmpty_AlsoMatchesEmptyValues()
    {
        Assert.Equal(["B", "C"], await NamesMatching(new FilterDescriptor("Kind", FilterOperator.In, null, Values: ["Feature"], IncludeEmpty: true)));
    }

    // ----- Between -----

    [Theory]
    [InlineData("1.5", "8", new[] { "A", "B" })]
    [InlineData(null, "1.5", new[] { "A", "E" })]
    [InlineData("8", null, new[] { "B", "D" })]
    public async Task Between_Numbers_IsInclusive_WithOpenEnds(string? from, string? to, string[] expected)
    {
        Assert.Equal(expected, await NamesMatching(new FilterDescriptor("Estimate", FilterOperator.Between, from, to)));
    }

    [Fact]
    public async Task Between_ExcludesEmptyValues_UnlessIncludeEmpty()
    {
        Assert.Equal(["D"], await NamesMatching(new FilterDescriptor("Estimate", FilterOperator.Between, "10", null)));
        Assert.Equal(["C", "D"], await NamesMatching(new FilterDescriptor("Estimate", FilterOperator.Between, "10", null, IncludeEmpty: true)));
    }

    [Fact]
    public async Task Between_DateTimes()
    {
        Assert.Equal(["A", "B"], await NamesMatching(
            new FilterDescriptor("Due", FilterOperator.Between, "2026-01-01T00:00:00", "2026-09-13T23:59:59.9999999")));
    }

    [Fact]
    public async Task Between_DateOnly()
    {
        Assert.Equal(["A", "B"], await NamesMatching(new FilterDescriptor("Day", FilterOperator.Between, "2026-06-01", "2026-09-10")));
    }

    [Fact]
    public async Task Between_TimeSpans()
    {
        Assert.Equal(["A", "B", "E"], await NamesMatching(new FilterDescriptor("Spent", FilterOperator.Between, "00:30:00", "2.00:00:00")));
    }

    // ----- WithinLast -----

    [Theory]
    [InlineData("P30D", new[] { "A" })]   // E's due date is in the future
    [InlineData("P1Y", new[] { "A", "B" })]
    [InlineData("P5Y", new[] { "A", "B", "D" })]
    public async Task WithinLast_DateTimes_FromPeriodStartUpToNow(string period, string[] expected)
    {
        Assert.Equal(expected, await NamesMatching(new FilterDescriptor("Due", FilterOperator.WithinLast, period)));
    }

    [Fact]
    public async Task WithinLast_DateOnly()
    {
        Assert.Equal(["A"], await NamesMatching(new FilterDescriptor("Day", FilterOperator.WithinLast, "P1W")));
    }

    [Fact]
    public async Task WithinLast_InvalidPeriod_MatchesNothing()
    {
        Assert.Empty(await NamesMatching(new FilterDescriptor("Due", FilterOperator.WithinLast, "30 days")));
    }

    // ----- column stats -----

    [Fact]
    public async Task ColumnStats_MinMaxAndCounts()
    {
        var stats = await Provider.GetColumnStatsAsync(new ColumnStatsRequest("Estimate", []));

        Assert.Equal(new ColumnStats("0.5", "40.25", EmptyCount: 1, TotalCount: 5), stats);
    }

    [Fact]
    public async Task ColumnStats_FormatsDatesAndDurationsInvariantly()
    {
        var due = await Provider.GetColumnStatsAsync(new ColumnStatsRequest("Due", []));
        var spent = await Provider.GetColumnStatsAsync(new ColumnStatsRequest("Spent", []));

        Assert.Equal(("2024-01-15T00:00:00.0000000", "2026-09-14T00:00:00.0000000"), (due.Min, due.Max));
        Assert.Equal(("00:30:00", "30.00:00:00"), (spent.Min, spent.Max));
    }

    [Fact]
    public async Task ColumnStats_AppliesOtherColumnsFilters_ButNotItsOwn()
    {
        var stats = await Provider.GetColumnStatsAsync(new ColumnStatsRequest("Estimate",
        [
            new FilterDescriptor("Estimate", FilterOperator.Between, "1", "10"),
            new FilterDescriptor("Kind", FilterOperator.In, null, Values: ["Bug"])
        ]));

        Assert.Equal(("0.5", "1.5", 2), (stats.Min, stats.Max, stats.TotalCount));
    }

    [Fact]
    public async Task ColumnStats_ValueCounts_InSortOrder()
    {
        var stats = await Provider.GetColumnStatsAsync(new ColumnStatsRequest("Kind", [], IncludeValueCounts: true));

        Assert.Equal([new ColumnValueCount("Bug", 2), new ColumnValueCount("Feature", 1), new ColumnValueCount("Chore", 1)], stats.Values);
        Assert.Equal(1, stats.EmptyCount);
        Assert.False(stats.HasMoreValues);
    }

    [Fact]
    public async Task ColumnStats_ValueCounts_StopAtMaxValueCount()
    {
        var stats = await Provider.GetColumnStatsAsync(new ColumnStatsRequest("Kind", [], IncludeValueCounts: true, MaxValueCount: 2));

        Assert.Equal(2, stats.Values!.Count);
        Assert.True(stats.HasMoreValues);
    }

    [Fact]
    public async Task ColumnStats_NoNonEmptyValues_HasNoMinOrMax()
    {
        var stats = await Provider.GetColumnStatsAsync(new ColumnStatsRequest("Estimate",
            [new FilterDescriptor("Name", FilterOperator.Equals, "C")]));

        Assert.Equal(new ColumnStats(null, null, EmptyCount: 1, TotalCount: 1), stats);
    }
}

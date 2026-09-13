using ColonnadeGrid.Internal;
using ColonnadeGrid.Models;

namespace ColonnadeGrid.Tests.Models;

public class FilterValuesTests
{
    private enum Color
    {
        Red,
        Green
    }

    public static TheoryData<object?, string?> FormatCases => new()
    {
        { null, null },
        { "text", "text" },
        { 3.25, "3.25" },
        { 1234567, "1234567" },
        { 2.5m, "2.5" },
        { Color.Green, "Green" },
        { true, "True" },
        { new DateTime(2026, 9, 13, 8, 30, 0), "2026-09-13T08:30:00.0000000" },
        { new DateOnly(2026, 9, 13), "2026-09-13" },
        { new TimeSpan(1, 2, 3, 4), "1.02:03:04" },
    };

    [Theory]
    [MemberData(nameof(FormatCases))]
    public void Format_IsCultureInvariant(object? value, string? expected)
    {
        Assert.Equal(expected, FilterValues.Format(value));
    }

    [Theory]
    [InlineData("P30D", "2026-08-14T12:00:00")]
    [InlineData("P2W", "2026-08-30T12:00:00")]
    [InlineData("P6M", "2026-03-13T12:00:00")]
    [InlineData("P1Y", "2025-09-13T12:00:00")]
    public void RelativeDatePeriod_TryGetStart(string period, string expected)
    {
        Assert.True(RelativeDatePeriod.TryGetStart(period, new DateTime(2026, 9, 13, 12, 0, 0), out var start));
        Assert.Equal(DateTime.Parse(expected), start);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("30D")]
    [InlineData("P0D")]
    [InlineData("P-1D")]
    [InlineData("PXD")]
    [InlineData("P1H")]
    public void RelativeDatePeriod_RejectsInvalidPeriods(string? period)
    {
        Assert.False(RelativeDatePeriod.TryGetStart(period, DateTime.Now, out _));
    }

    private sealed class Row
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public double? Ratio { get; set; }
        public decimal Price { get; set; }
        public Color Color { get; set; }
        public bool? Done { get; set; }
        public DateTime? Due { get; set; }
        public DateOnly Day { get; set; }
        public DateTimeOffset Stamp { get; set; }
        public TimeSpan? Spent { get; set; }
        public Guid Id { get; set; }
    }

    [Fact]
    public void FilterKind_IsChosenFromThePropertyType()
    {
        Assert.Equal(FilterKind.Text, new TypedGridColumn<Row, string>(r => r.Name, "Name").EffectiveFilterKind);
        Assert.Equal(FilterKind.Number, new TypedGridColumn<Row, int>(r => r.Count, "Count").EffectiveFilterKind);
        Assert.Equal(FilterKind.Number, new TypedGridColumn<Row, double?>(r => r.Ratio, "Ratio").EffectiveFilterKind);
        Assert.Equal(FilterKind.Number, new TypedGridColumn<Row, decimal>(r => r.Price, "Price").EffectiveFilterKind);
        Assert.Equal(FilterKind.Values, new TypedGridColumn<Row, Color>(r => r.Color, "Color").EffectiveFilterKind);
        Assert.Equal(FilterKind.Values, new TypedGridColumn<Row, bool?>(r => r.Done, "Done").EffectiveFilterKind);
        Assert.Equal(FilterKind.Date, new TypedGridColumn<Row, DateTime?>(r => r.Due, "Due").EffectiveFilterKind);
        Assert.Equal(FilterKind.Date, new TypedGridColumn<Row, DateOnly>(r => r.Day, "Day").EffectiveFilterKind);
        Assert.Equal(FilterKind.Date, new TypedGridColumn<Row, DateTimeOffset>(r => r.Stamp, "Stamp").EffectiveFilterKind);
        Assert.Equal(FilterKind.Duration, new TypedGridColumn<Row, TimeSpan?>(r => r.Spent, "Spent").EffectiveFilterKind);
        Assert.Equal(FilterKind.Text, new TypedGridColumn<Row, Guid>(r => r.Id, "Id").EffectiveFilterKind);
    }

    [Fact]
    public void FilterKind_CanBeOverridden()
    {
        var column = new TypedGridColumn<Row, string>(r => r.Name, "Name") { FilterKind = FilterKind.Values };

        Assert.Equal(FilterKind.Values, column.EffectiveFilterKind);
    }
}

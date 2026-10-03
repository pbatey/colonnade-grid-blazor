using System.Globalization;
using ColonnadeGrid.Models;

namespace ColonnadeGrid.Tests.Models;

/// <summary>
/// Covers <see cref="GroupKeys.From"/>, the culture-invariant key convention the
/// in-memory provider uses to label groups.
/// </summary>
public class GroupKeysTests
{
    private enum Priority
    {
        Low,
        High
    }

    [Fact]
    public void From_Null_IsTheNullKey()
    {
        Assert.Equal(GroupKeys.Null, GroupKeys.From(null));
        Assert.Equal("\0", GroupKeys.Null);
    }

    [Fact]
    public void From_NullKey_IsDistinctFromEmptyString()
    {
        Assert.NotEqual(GroupKeys.From(""), GroupKeys.From(null));
        Assert.Equal("", GroupKeys.From(""));
    }

    [Fact]
    public void From_DateTime_UsesRoundTripFormat()
    {
        var value = new DateTime(2026, 9, 13, 8, 30, 0);

        Assert.Equal(value.ToString("O", CultureInfo.InvariantCulture), GroupKeys.From(value));
    }

    [Fact]
    public void From_DateTimeOffset_UsesRoundTripFormat()
    {
        var value = new DateTimeOffset(2026, 9, 13, 8, 30, 0, TimeSpan.FromHours(-5));

        Assert.Equal(value.ToString("O", CultureInfo.InvariantCulture), GroupKeys.From(value));
    }

    [Fact]
    public void From_FormattableValues_AreCultureInvariant()
    {
        Assert.Equal("1234.5", GroupKeys.From(1234.5));
        Assert.Equal("42", GroupKeys.From(42));
        Assert.Equal("High", GroupKeys.From(Priority.High));
    }

    [Fact]
    public void From_PlainString_IsItself()
    {
        Assert.Equal("Open", GroupKeys.From("Open"));
    }
}

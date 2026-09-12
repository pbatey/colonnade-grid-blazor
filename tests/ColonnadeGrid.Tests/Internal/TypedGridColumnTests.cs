using ColonnadeGrid.Internal;

namespace ColonnadeGrid.Tests.Internal;

public class TypedGridColumnTests
{
    private sealed class Widget
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public DateTime Created { get; set; }
    }

    [Fact]
    public void GetCellValue_ReturnsRawPropertyValue()
    {
        var column = new TypedGridColumn<Widget, int>(w => w.Count, "Count");
        var widget = new Widget { Count = 7 };

        Assert.Equal(7, column.GetCellValue(widget));
    }

    [Fact]
    public void GetDisplayText_WithoutFormat_UsesToString()
    {
        var column = new TypedGridColumn<Widget, int>(w => w.Count, "Count");
        var widget = new Widget { Count = 7 };

        Assert.Equal("7", column.GetDisplayText(widget));
    }

    [Fact]
    public void GetDisplayText_NullValue_ReturnsEmptyString()
    {
        var column = new TypedGridColumn<Widget, string?>(w => null, "Name");
        var widget = new Widget();

        Assert.Equal("", column.GetDisplayText(widget));
    }

    [Fact]
    public void GetDisplayText_WithFormat_UsesFormattable()
    {
        var column = new TypedGridColumn<Widget, DateTime>(w => w.Created, "Created", format: "yyyy-MM-dd");
        var widget = new Widget { Created = new DateTime(2026, 1, 15) };

        Assert.Equal("2026-01-15", column.GetDisplayText(widget));
    }

    [Fact]
    public void Compare_UsesTypedComparer_NotBoxedObjectComparison()
    {
        var column = new TypedGridColumn<Widget, int>(w => w.Count, "Count");
        var low = new Widget { Count = 2 };
        var high = new Widget { Count = 10 };

        // A boxed-object comparison (e.g. via Comparer<object>.Default on the
        // stringified/boxed value) would incorrectly say "10" < "2" for some
        // naive implementations; the typed Comparer<int> must get this right.
        Assert.True(column.Compare(low, high) < 0);
        Assert.True(column.Compare(high, low) > 0);
        Assert.Equal(0, column.Compare(low, low));
    }
}

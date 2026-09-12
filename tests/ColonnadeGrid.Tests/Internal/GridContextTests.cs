using ColonnadeGrid.Internal;

namespace ColonnadeGrid.Tests.Internal;

public class GridContextTests
{
    private sealed class Widget
    {
        public string Name { get; set; } = "";
    }

    [Fact]
    public void RegisterColumn_AddsToColumnsInOrder()
    {
        var context = new GridContext<Widget>();
        var nameColumn = new TypedGridColumn<Widget, string>(w => w.Name, "Name");

        context.RegisterColumn(nameColumn);

        var registered = Assert.Single(context.Columns);
        Assert.Same(nameColumn, registered);
    }

    [Fact]
    public void ClearColumns_RemovesAllRegistrations()
    {
        var context = new GridContext<Widget>();
        context.RegisterColumn(new TypedGridColumn<Widget, string>(w => w.Name, "Name"));

        context.ClearColumns();

        Assert.Empty(context.Columns);
    }

    [Fact]
    public void ClearThenReregister_PreservesNewMarkupOrder()
    {
        var context = new GridContext<Widget>();
        var first = new TypedGridColumn<Widget, string>(w => w.Name, "Name");
        var second = new TypedGridColumn<Widget, string>(w => w.Name, "Name");
        context.RegisterColumn(first);

        context.ClearColumns();
        context.RegisterColumn(second);

        var registered = Assert.Single(context.Columns);
        Assert.Same(second, registered);
    }
}

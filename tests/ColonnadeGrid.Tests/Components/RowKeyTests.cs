using Bunit;
using Microsoft.AspNetCore.Components;

namespace ColonnadeGrid.Tests.Components;

public class RowKeyTests : BunitContext
{
    private struct PointRow
    {
        public int X { get; set; }
    }

    public RowKeyTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void ValueTypeItems_WithRowSelection_AndNoRowKey_Throws()
    {
        // Each boxing of a struct is a new object, so an identity-based key
        // would change on every lookup and selection would never stick.
        var exception = Assert.Throws<InvalidOperationException>(() => Render<ColonnadeGrid<PointRow>>(p => p
            .Add(x => x.Items, [new PointRow { X = 1 }])
            .Add(x => x.EnableRowSelection, true)
            .Add(x => x.Columns, (RenderFragment)(_ => { }))));

        Assert.Contains("RowKey", exception.Message);
    }

    [Fact]
    public void ValueTypeItems_WithoutRowSelection_DoNotNeedRowKey()
    {
        var cut = Render<ColonnadeGrid<PointRow>>(p => p
            .Add(x => x.Items, [new PointRow { X = 1 }])
            .Add(x => x.Columns, (RenderFragment)(_ => { })));

        Assert.NotNull(cut.Instance);
    }
}

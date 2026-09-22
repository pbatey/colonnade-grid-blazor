using Bunit;
using ColonnadeGrid.Models;
using ColonnadeGrid.Tests.TestSupport;

namespace ColonnadeGrid.Tests.Components;

public class DialogFilterTests : BunitContext
{
    public DialogFilterTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private GridState? _appliedState;

    private IRenderedComponent<DialogFilterGridHost> RenderGrid(params FilterDescriptor[] filters)
    {
        var state = filters.Aggregate(GridState.Create(DialogFilterGridHost.ColumnIds), (s, f) => s.SetFilter(f));
        var cut = Render<DialogFilterGridHost>(p => p
            .Add(x => x.Items, SampleTickets.Create())
            .Add(x => x.State, state)
            .Add(x => x.StateChanged, (GridState s) => _appliedState = s));
        cut.WaitForState(() => cut.FindAll(".cg-column-menu-button").Count == DialogFilterGridHost.ColumnIds.Length);
        return cut;
    }

    private static void OpenTeamFilter(IRenderedComponent<DialogFilterGridHost> cut)
    {
        cut.Find("[data-column-id='Team'] .cg-column-menu-button").Click();
        cut.FindAll(".cg-column-menu-item").Single(item => item.TextContent.Contains("Filter by values")).Click();
    }

    private FilterDescriptor AppliedFilter(string propertyName)
    {
        Assert.NotNull(_appliedState);
        return Assert.Single(_appliedState!.Filters, f => f.PropertyName == propertyName);
    }

    [Fact]
    public void FilterInDialogColumn_OpensInDialog_WithDropdownHidden()
    {
        var cut = RenderGrid();
        OpenTeamFilter(cut);

        cut.WaitForAssertion(() =>
        {
            // The dialog shows and hosts the filter editor; the dropdown menu is
            // hidden (it stays mounted to host the dialog, but isn't shown as a
            // popover next to the header).
            Assert.Single(cut.FindAll(".cg-filter-dialog"));
            Assert.Single(cut.FindAll(".cg-filter-dialog .cg-filter-popover"));
            Assert.Single(cut.FindAll(".cg-column-menu-hidden"));
        });
    }

    [Fact]
    public void ApplyingAFilterFromTheDialog_SetsTheFilter()
    {
        var cut = RenderGrid();
        OpenTeamFilter(cut);
        cut.WaitForState(() => cut.FindAll(".cg-values-option").Count > 0);

        // Deselect one value, then Apply — an In filter over the rest.
        cut.FindAll(".cg-values-option")
            .Single(o => o.QuerySelector(".cg-values-label")!.TextContent == "Web")
            .QuerySelector("input")!.Change(false);
        cut.FindAll(".cg-filter-popover-actions button")[0].Click();

        var filter = AppliedFilter("Team");
        Assert.Equal(FilterOperator.In, filter.Operator);
        Assert.DoesNotContain("Web", filter.Values!);
    }

    [Fact]
    public void ClearFromTheDialog_RemovesTheFilter()
    {
        var cut = RenderGrid(new FilterDescriptor("Team", FilterOperator.In, null, Values: ["Core"]));
        OpenTeamFilter(cut);
        cut.WaitForState(() => cut.FindAll(".cg-filter-popover-actions button").Count == 2);

        // The second action button is Clear.
        cut.FindAll(".cg-filter-popover-actions button")[1].Click();

        cut.WaitForAssertion(() => Assert.Empty(_appliedState!.Filters));
    }

    [Fact]
    public void BackdropClick_ClosesDialogWithoutApplying()
    {
        var cut = RenderGrid();
        OpenTeamFilter(cut);
        cut.WaitForState(() => cut.FindAll(".cg-filter-dialog").Count == 1);

        cut.Find(".cg-filter-dialog-backdrop").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".cg-filter-dialog")));
        Assert.Null(_appliedState);
    }
}

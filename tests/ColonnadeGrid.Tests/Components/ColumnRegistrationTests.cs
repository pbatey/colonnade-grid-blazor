using Bunit;
using ColonnadeGrid.Tests.TestSupport;

namespace ColonnadeGrid.Tests.Components;

/// <summary>Column markup the host changes after the first render shows up in that same render pass, not a render later.</summary>
public class ColumnRegistrationTests : BunitContext
{
    public ColumnRegistrationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static List<string> HeaderTexts(IRenderedComponent<ConditionalColumnsHost> cut) =>
        cut.FindAll("[data-column-id] .cg-header-text").Select(h => h.TextContent.Trim()).ToList();

    private IRenderedComponent<ConditionalColumnsHost> RenderHost(bool showAssignee)
    {
        var cut = Render<ConditionalColumnsHost>(p => p
            .Add(x => x.Items, SampleIssues.Create())
            .Add(x => x.ShowAssignee, showAssignee));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);
        return cut;
    }

    [Fact]
    public void ChangedTitle_ShowsInTheHeader()
    {
        var cut = RenderHost(showAssignee: false);

        cut.Render(p => p.Add(x => x.TitleText, "Summary"));

        cut.WaitForAssertion(() => Assert.Equal(["Summary"], HeaderTexts(cut)));
    }

    [Fact]
    public void RemovedColumn_DisappearsFromTheHeader()
    {
        var cut = RenderHost(showAssignee: true);
        Assert.Equal(["Title", "Assignee"], HeaderTexts(cut));

        cut.Render(p => p.Add(x => x.ShowAssignee, false));

        cut.WaitForAssertion(() => Assert.Equal(["Title"], HeaderTexts(cut)));
    }

    [Fact]
    public void ColumnRenderedAgain_ReappearsInTheHeader()
    {
        // Unlike a brand-new column, it's already in the grid's state, so
        // nothing about the state changes when it comes back.
        var cut = RenderHost(showAssignee: true);
        cut.Render(p => p.Add(x => x.ShowAssignee, false));
        cut.WaitForAssertion(() => Assert.Equal(["Title"], HeaderTexts(cut)));

        cut.Render(p => p.Add(x => x.ShowAssignee, true));

        cut.WaitForAssertion(() => Assert.Equal(["Title", "Assignee"], HeaderTexts(cut)));
    }

    [Fact]
    public void RemovedColumn_DisappearsFromTheRows()
    {
        var cut = RenderHost(showAssignee: true);

        cut.Render(p => p.Add(x => x.ShowAssignee, false));

        cut.WaitForAssertion(() => Assert.All(cut.FindAll(".cg-body-row"),
            row => Assert.Single(row.QuerySelectorAll(".cg-cell"))));
    }
}

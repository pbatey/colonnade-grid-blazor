using Bunit;
using ColonnadeGrid.Models;
using ColonnadeGrid.Tests.TestSupport;

namespace ColonnadeGrid.Tests.Components;

/// <summary>A <see cref="GridState"/> the host passes in, rather than one the user builds through the grid's menus.</summary>
public class HostStateTests : BunitContext
{
    private static readonly string[] ColumnIds = ["Title", "Status", "Priority", "Assignee"];

    public HostStateTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void StateWithFilter_PassedAfterFirstRender_ReloadsRows()
    {
        // Restoring a saved view: the grid has already loaded with its default
        // state by the time the host reads the saved one.
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        var saved = GridState.Create(ColumnIds).SetFilter(new FilterDescriptor("Title", FilterOperator.Contains, "login"));
        cut.Render(p => p.Add(x => x.State, saved));

        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 1);
        Assert.Contains("Fix login bug", cut.Find(".cg-body-row").TextContent);
    }

    [Fact]
    public void StateWithSort_PassedAfterFirstRender_ReordersRows()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        var saved = GridState.Create(ColumnIds).SetSort(new SortDescriptor("Priority", SortDirection.Descending));
        cut.Render(p => p.Add(x => x.State, saved));

        cut.WaitForAssertion(() => Assert.Contains("Add dark mode", cut.FindAll(".cg-body-row")[0].TextContent));
    }

    [Fact]
    public void HostRerender_WithSameUnboundState_KeepsTheUsersChanges()
    {
        // A host that passes State without binding StateChanged keeps passing
        // the same instance on every re-render; that mustn't undo the user's sort.
        var initial = GridState.Create(ColumnIds);
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.Create())
            .Add(x => x.State, initial));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        cut.Find("[data-column-id='Priority'] .cg-column-menu-button").Click();
        cut.FindAll(".cg-column-menu-item").Single(item => item.TextContent.Contains("Sort descending")).Click();
        cut.WaitForAssertion(() => Assert.Contains("Add dark mode", cut.FindAll(".cg-body-row")[0].TextContent));

        cut.Render(p => p.Add(x => x.EnableRowSelection, true));

        cut.WaitForState(() => cut.FindAll(".cg-body-row .cg-select-cell").Count == 4);
        Assert.Contains("cg-header-cell-active", cut.Find("[data-column-id='Priority']").ClassList);
        Assert.Contains("Add dark mode", cut.FindAll(".cg-body-row")[0].TextContent);
    }

    [Fact]
    public void State_MissingADeclaredColumn_ShowsThatColumn_AndReportsTheCompletedState()
    {
        // A view saved before the Assignee column was added to the markup.
        GridState? reported = null;
        var saved = GridState.Create(["Title", "Status", "Priority"]);
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.Create())
            .Add(x => x.State, saved)
            .Add(x => x.StateChanged, (GridState s) => reported = s));

        cut.WaitForState(() => cut.FindAll("[data-column-id]").Count == 4);
        Assert.Equal(ColumnIds, cut.FindAll("[data-column-id]").Select(h => h.GetAttribute("data-column-id")));
        Assert.NotNull(reported);
        Assert.Equal(ColumnIds, reported.Columns.Select(c => c.Id));
    }

    [Fact]
    public void State_WithAHiddenColumn_KeepsItHidden_WhenAddingMissingOnes()
    {
        var saved = GridState.Create(["Title", "Status", "Priority"]).SetColumnVisible("Status", false);
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.Create())
            .Add(x => x.State, saved));

        cut.WaitForState(() => cut.FindAll("[data-column-id]").Count == 3);
        Assert.Equal(["Title", "Priority", "Assignee"], cut.FindAll("[data-column-id]").Select(h => h.GetAttribute("data-column-id")));
    }

    [Fact]
    public void MoveLeft_SkipsAStateColumnThatIsNoLongerDeclared()
    {
        // "Gone" is listed, visible, but no longer in the markup, so it isn't shown.
        var saved = GridState.Create(["Title", "Gone", "Status", "Priority", "Assignee"]);
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.Create())
            .Add(x => x.State, saved));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        cut.Find("[data-column-id='Status'] .cg-column-menu-button").Click();
        cut.FindAll(".cg-column-menu-item").Single(item => item.TextContent.Contains("Move left")).Click();

        cut.WaitForAssertion(() => Assert.Equal(["Status", "Title", "Priority", "Assignee"],
            cut.FindAll("[data-column-id]").Select(h => h.GetAttribute("data-column-id"))));
    }

    [Fact]
    public void MoveLeft_IsDisabledForTheFirstShownColumn_WhenOnlyUndeclaredColumnsPrecedeIt()
    {
        var saved = GridState.Create(["Gone", "Title", "Status", "Priority", "Assignee"]);
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.Create())
            .Add(x => x.State, saved));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        cut.Find("[data-column-id='Title'] .cg-column-menu-button").Click();

        var moveLeft = cut.FindAll(".cg-column-menu-item").Single(item => item.TextContent.Contains("Move left"));
        Assert.True(((AngleSharp.Html.Dom.IHtmlButtonElement)moveLeft).IsDisabled);
    }
}

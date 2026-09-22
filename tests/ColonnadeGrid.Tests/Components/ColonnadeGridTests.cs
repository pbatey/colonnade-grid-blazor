using AngleSharp.Html.Dom;
using Bunit;
using ColonnadeGrid.Abstractions;
using ColonnadeGrid.Models;
using ColonnadeGrid.Tests.TestSupport;

namespace ColonnadeGrid.Tests.Components;

public class ColonnadeGridTests : BunitContext
{
    public ColonnadeGridTests()
    {
        // JS interop (column resize, select-all indeterminate state) is a
        // progressive enhancement the component itself already tolerates
        // failing (see ColonnadeGrid.razor.cs's try/catch + null checks) —
        // loose mode just lets unconfigured calls no-op instead of throwing.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static List<string> GetColumnText(IRenderedComponent<IssueGridHost> cut, int cellIndex) =>
        cut.FindAll(".cg-body-row")
            .Select(row => row.QuerySelectorAll(".cg-cell")[cellIndex].TextContent)
            .ToList();

    /// <summary>Opens the given column's "..." menu and waits for it to render.</summary>
    private static void OpenColumnMenu(IRenderedComponent<IssueGridHost> cut, string columnId)
    {
        cut.Find($"[data-column-id='{columnId}'] .cg-column-menu-button").Click();
        cut.WaitForState(() => cut.FindAll(".cg-column-menu").Count == 1);
    }

    /// <summary>Clicks the open column menu's action item whose label contains the given text.</summary>
    private static void ClickColumnMenuItem(IRenderedComponent<IssueGridHost> cut, string labelContains)
    {
        cut.FindAll(".cg-column-menu-item").Single(item => item.TextContent.Contains(labelContains)).Click();
    }

    [Fact]
    public void HeaderCells_NoSortFilterOrGroup_AreNotMarkedActive()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        Assert.All(cut.FindAll("[data-column-id]"),
            cell => Assert.DoesNotContain("cg-header-cell-active", cell.ClassList));
    }

    [Theory]
    [InlineData("Priority", "Sort ascending")]
    [InlineData("Status", "Group by values")]
    public void HeaderCell_AfterSortOrGroup_IsMarkedActive_OtherCellsAreNot(string columnId, string menuItemLabel)
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        OpenColumnMenu(cut, columnId);
        ClickColumnMenuItem(cut, menuItemLabel);

        cut.WaitForAssertion(() =>
            Assert.Contains("cg-header-cell-active", cut.Find($"[data-column-id='{columnId}']").ClassList));

        // Only the column actually being sorted/grouped gets the dark
        // underline — every other header cell keeps its plain muted border.
        Assert.All(cut.FindAll("[data-column-id]").Where(c => c.GetAttribute("data-column-id") != columnId),
            cell => Assert.DoesNotContain("cg-header-cell-active", cell.ClassList));
    }

    [Fact]
    public void HeaderCell_AfterFilterApplied_IsMarkedActive_OtherCellsAreNot()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        OpenColumnMenu(cut, "Title");
        ClickColumnMenuItem(cut, "Filter by values");
        cut.WaitForState(() => cut.FindAll(".cg-filter-popover").Count == 1);
        cut.Find(".cg-filter-value").Input("login");
        cut.FindAll(".cg-filter-popover-actions button")[0].Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("cg-header-cell-active", cut.Find("[data-column-id='Title']").ClassList));

        Assert.All(cut.FindAll("[data-column-id]").Where(c => c.GetAttribute("data-column-id") != "Title"),
            cell => Assert.DoesNotContain("cg-header-cell-active", cell.ClassList));
    }

    [Fact]
    public void HeaderAndBodyRows_ShareIdenticalGridTemplateColumns()
    {
        // Regression test: the header row previously had one extra trailing
        // grid track (for its "+" add-column button) that body/group rows
        // didn't share. Since a `1fr` track's resolved pixel width depends
        // on how many *other* tracks are competing for the same row width,
        // that extra track made the header's data columns a different width
        // than the body's — visibly misaligning every column until a column
        // was resized (which pins that column to a fixed px width instead of
        // 1fr). All rows must use the exact same grid-template-columns.
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.Create())
            .Add(x => x.EnableRowSelection, true));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        var headerStyle = cut.Find(".cg-header-row").GetAttribute("style");
        var bodyStyle = cut.Find(".cg-body-row").GetAttribute("style");

        Assert.Equal(headerStyle, bodyStyle);
    }

    [Fact]
    public void HeaderAndGroupRows_ShareIdenticalGridTemplateColumns()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        OpenColumnMenu(cut, "Status");
        ClickColumnMenuItem(cut, "Group by values");
        cut.WaitForState(() => cut.FindAll(".cg-group-header-row").Count == 3);

        var headerStyle = cut.Find(".cg-header-row").GetAttribute("style");
        var groupStyle = cut.Find(".cg-group-header-row").GetAttribute("style");

        Assert.Equal(headerStyle, groupStyle);
    }

    [Fact]
    public void Renders_HeaderCells_InDeclaredOrder()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));

        var headerIds = cut.FindAll("[role='columnheader'][data-column-id]")
            .Select(h => h.GetAttribute("data-column-id"))
            .ToList();

        Assert.Equal(["Title", "Status", "Priority", "Assignee"], headerIds);
    }

    [Fact]
    public void Renders_OneBodyRowPerItem()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));

        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);
    }

    [Fact]
    public void ColumnMenu_SortAscendingThenDescendingThenClear_ReordersRows()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        OpenColumnMenu(cut, "Priority");
        ClickColumnMenuItem(cut, "Sort ascending");
        cut.WaitForAssertion(() =>
            Assert.Equal("ascending", cut.Find("[data-column-id='Priority']").GetAttribute("aria-sort")));
        Assert.Equal(
            ["Fix login bug", "Refactor auth", "Write docs", "Add dark mode"],
            GetColumnText(cut, 0));

        OpenColumnMenu(cut, "Priority");
        ClickColumnMenuItem(cut, "Sort descending");
        cut.WaitForAssertion(() =>
            Assert.Equal("descending", cut.Find("[data-column-id='Priority']").GetAttribute("aria-sort")));
        Assert.Equal(
            ["Add dark mode", "Write docs", "Fix login bug", "Refactor auth"],
            GetColumnText(cut, 0));

        OpenColumnMenu(cut, "Priority");
        cut.Find(".cg-column-menu-clear").Click();
        cut.WaitForAssertion(() =>
            Assert.Equal("none", cut.Find("[data-column-id='Priority']").GetAttribute("aria-sort")));
        Assert.Equal(
            ["Fix login bug", "Add dark mode", "Write docs", "Refactor auth"],
            GetColumnText(cut, 0));
    }

    [Fact]
    public void SortIndicator_ClickToggles_WithoutOpeningMenu()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        OpenColumnMenu(cut, "Priority");
        ClickColumnMenuItem(cut, "Sort ascending");
        cut.WaitForAssertion(() =>
            Assert.Equal("ascending", cut.Find("[data-column-id='Priority']").GetAttribute("aria-sort")));

        // The indicator itself (not the "..." menu) should now be a direct toggle.
        cut.Find("[data-column-id='Priority'] .cg-sort-indicator").Click();
        cut.WaitForAssertion(() =>
            Assert.Equal("descending", cut.Find("[data-column-id='Priority']").GetAttribute("aria-sort")));

        cut.Find("[data-column-id='Priority'] .cg-sort-indicator").Click();
        cut.WaitForAssertion(() =>
            Assert.Equal("ascending", cut.Find("[data-column-id='Priority']").GetAttribute("aria-sort")));
    }

    [Fact]
    public void GroupIndicator_ShowsOnlyOnTheActiveGroupByColumn()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        Assert.Empty(cut.FindAll(".cg-group-indicator"));

        OpenColumnMenu(cut, "Status");
        ClickColumnMenuItem(cut, "Group by values");
        cut.WaitForState(() => cut.FindAll(".cg-group-header-row").Count == 3);

        var indicator = Assert.Single(cut.FindAll(".cg-group-indicator"));
        Assert.Equal("Status", indicator.Closest("[data-column-id]")?.GetAttribute("data-column-id"));
    }

    [Fact]
    public void FilterIndicator_ShowsOnlyOnColumnsWithAnActiveFilter()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        Assert.Empty(cut.FindAll(".cg-filter-indicator"));

        OpenColumnMenu(cut, "Title");
        ClickColumnMenuItem(cut, "Filter by values");
        cut.WaitForState(() => cut.FindAll(".cg-filter-popover").Count == 1);
        cut.Find(".cg-filter-value").Input("login");
        cut.FindAll(".cg-filter-popover-actions button")[0].Click();

        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 1);
        var indicator = Assert.Single(cut.FindAll(".cg-filter-indicator"));
        Assert.Equal("Title", indicator.Closest("[data-column-id]")?.GetAttribute("data-column-id"));
    }

    [Fact]
    public void FilterIndicator_ClickOpensMenuDirectlyToFilterView()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        OpenColumnMenu(cut, "Title");
        ClickColumnMenuItem(cut, "Filter by values");
        cut.WaitForState(() => cut.FindAll(".cg-filter-popover").Count == 1);
        cut.Find(".cg-filter-value").Input("login");
        cut.FindAll(".cg-filter-popover-actions button")[0].Click();
        cut.WaitForState(() => cut.FindAll(".cg-filter-indicator").Count == 1);

        // Clicking the indicator (not the "..." menu) should land directly on
        // the filter editor, not the default action list.
        cut.Find("[data-column-id='Title'] .cg-filter-indicator").Click();

        cut.WaitForState(() => cut.FindAll(".cg-filter-popover").Count == 1);
        Assert.Empty(cut.FindAll(".cg-column-menu-item"));
        Assert.Equal("login", ((AngleSharp.Html.Dom.IHtmlInputElement)cut.Find(".cg-filter-value")).Value);
    }

    [Fact]
    public void ColumnMenu_ClickingBackdrop_ClosesIt()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        OpenColumnMenu(cut, "Title");
        cut.Find(".cg-dropdown-backdrop").Click();

        cut.WaitForState(() => cut.FindAll(".cg-column-menu").Count == 0);
        Assert.Empty(cut.FindAll(".cg-dropdown-backdrop"));
    }

    [Fact]
    public void ColumnsMenu_ClickingBackdrop_ClosesIt()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        cut.Find(".cg-columns-button").Click();
        cut.WaitForState(() => cut.FindAll(".cg-columns-menu").Count == 1);

        cut.Find(".cg-dropdown-backdrop").Click();

        cut.WaitForState(() => cut.FindAll(".cg-columns-menu").Count == 0);
    }

    [Fact]
    public void OpeningColumnsMenu_ClosesAnAlreadyOpenColumnMenu()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        OpenColumnMenu(cut, "Title");
        cut.Find(".cg-columns-button").Click();

        cut.WaitForState(() => cut.FindAll(".cg-columns-menu").Count == 1);
        Assert.Empty(cut.FindAll(".cg-column-menu"));
    }

    [Fact]
    public void OpeningColumnMenu_ClosesAnAlreadyOpenColumnsMenu()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        cut.Find(".cg-columns-button").Click();
        cut.WaitForState(() => cut.FindAll(".cg-columns-menu").Count == 1);

        OpenColumnMenu(cut, "Title");

        cut.WaitForState(() => cut.FindAll(".cg-column-menu").Count == 1);
        Assert.Empty(cut.FindAll(".cg-columns-menu"));
    }

    [Fact]
    public void ColumnMenuButton_ShowsActiveClass_OnlyWhileItsMenuIsOpen()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        var button = cut.Find("[data-column-id='Title'] .cg-column-menu-button");
        Assert.DoesNotContain("cg-header-icon-button-active", button.ClassList);

        OpenColumnMenu(cut, "Title");
        Assert.Contains("cg-header-icon-button-active",
            cut.Find("[data-column-id='Title'] .cg-column-menu-button").ClassList);
    }

    [Fact]
    public void AddColumnButton_ShowsActiveClass_OnlyWhileItsMenuIsOpen()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        Assert.DoesNotContain("cg-header-icon-button-active", cut.Find(".cg-columns-button").ClassList);

        cut.Find(".cg-columns-button").Click();
        cut.WaitForState(() => cut.FindAll(".cg-columns-menu").Count == 1);
        Assert.Contains("cg-header-icon-button-active", cut.Find(".cg-columns-button").ClassList);
    }

    [Fact]
    public void ApplyingColumnFilter_UpdatesRenderedRows()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        OpenColumnMenu(cut, "Title");
        ClickColumnMenuItem(cut, "Filter by values");
        cut.WaitForState(() => cut.FindAll(".cg-filter-popover").Count == 1);

        cut.Find(".cg-filter-value").Input("login");
        cut.FindAll(".cg-filter-popover-actions button")[0].Click();

        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 1);
        Assert.Equal(["Fix login bug"], GetColumnText(cut, 0));
    }

    [Fact]
    public void ClearingColumnFilter_RestoresAllRows()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        OpenColumnMenu(cut, "Title");
        ClickColumnMenuItem(cut, "Filter by values");
        cut.WaitForState(() => cut.FindAll(".cg-filter-popover").Count == 1);
        cut.Find(".cg-filter-value").Input("login");
        cut.FindAll(".cg-filter-popover-actions button")[0].Click();
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 1);

        OpenColumnMenu(cut, "Title");
        ClickColumnMenuItem(cut, "Filter by values");
        cut.WaitForState(() => cut.FindAll(".cg-filter-popover").Count == 1);
        cut.FindAll(".cg-filter-popover-actions button")[1].Click();

        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);
    }

    [Fact]
    public void GroupingByColumn_RendersGroupHeadersWithCounts()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        OpenColumnMenu(cut, "Status");
        ClickColumnMenuItem(cut, "Group by values");

        cut.WaitForState(() => cut.FindAll(".cg-group-header-row").Count == 3);
        var todoHeader = cut.FindAll(".cg-group-header-row")
            .Single(h => h.QuerySelector(".cg-group-title")!.TextContent == "Todo");
        Assert.Equal("2", todoHeader.QuerySelector(".cg-group-count")!.TextContent);
    }

    [Fact]
    public void CollapsingGroup_HidesItsRows()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        OpenColumnMenu(cut, "Status");
        ClickColumnMenuItem(cut, "Group by values");
        cut.WaitForState(() => cut.FindAll(".cg-group-header-row").Count == 3);

        var todoHeader = cut.FindAll(".cg-group-header-row")
            .Single(h => h.QuerySelector(".cg-group-title")!.TextContent == "Todo");
        todoHeader.Click();

        // Todo (2 rows) collapsed; InProgress (1) + Done (1) remain visible.
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 2);
        cut.WaitForState(() => cut.FindAll(".cg-group-header-row").Count == 3);
    }

    [Fact]
    public void ColumnMenu_GroupByToggledTwice_TogglesGroupingOff()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        OpenColumnMenu(cut, "Status");
        ClickColumnMenuItem(cut, "Group by values");
        cut.WaitForState(() => cut.FindAll(".cg-group-header-row").Count == 3);

        OpenColumnMenu(cut, "Status");
        cut.Find(".cg-column-menu-clear").Click();
        cut.WaitForState(() => cut.FindAll(".cg-group-header-row").Count == 0);
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);
    }

    [Fact]
    public void ColumnMenu_HideField_HidesColumn()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        OpenColumnMenu(cut, "Priority");
        ClickColumnMenuItem(cut, "Hide field");

        cut.WaitForState(() => cut.FindAll("[data-column-id='Priority']").Count == 0);
        Assert.Equal(["Title", "Status", "Assignee"],
            cut.FindAll("[data-column-id]").Select(h => h.GetAttribute("data-column-id")));
    }

    [Fact]
    public void ColumnMenu_HideField_DisabledWhenOnlyOneColumnVisible()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        foreach (var columnId in new[] { "Status", "Priority", "Assignee" })
        {
            OpenColumnMenu(cut, columnId);
            ClickColumnMenuItem(cut, "Hide field");
            cut.WaitForState(() => cut.FindAll($"[data-column-id='{columnId}']").Count == 0);
        }

        OpenColumnMenu(cut, "Title");
        var hideButton = cut.FindAll(".cg-column-menu-item").Single(item => item.TextContent.Contains("Hide field"));
        Assert.True(((AngleSharp.Html.Dom.IHtmlButtonElement)hideButton).IsDisabled);
    }

    [Fact]
    public void ColumnMenu_MoveRight_ReordersColumns()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        OpenColumnMenu(cut, "Title");
        ClickColumnMenuItem(cut, "Move right");

        cut.WaitForState(() =>
            cut.FindAll("[data-column-id]").FirstOrDefault()?.GetAttribute("data-column-id") == "Status");
        Assert.Equal(["Status", "Title", "Priority", "Assignee"],
            cut.FindAll("[data-column-id]").Select(h => h.GetAttribute("data-column-id")));
    }

    [Fact]
    public void ColumnMenu_MoveLeftAndRight_KeepTheMenuOpen()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        // Move right, then left again — the menu should stay open the whole
        // time so the column can be stepped over several positions without
        // reopening it (Move to start/end still close).
        OpenColumnMenu(cut, "Title");
        ClickColumnMenuItem(cut, "Move right");
        cut.WaitForState(() =>
            cut.FindAll("[data-column-id]").FirstOrDefault()?.GetAttribute("data-column-id") == "Status");
        Assert.Single(cut.FindAll(".cg-column-menu"));

        ClickColumnMenuItem(cut, "Move left");
        cut.WaitForState(() =>
            cut.FindAll("[data-column-id]").FirstOrDefault()?.GetAttribute("data-column-id") == "Title");
        Assert.Single(cut.FindAll(".cg-column-menu"));
    }

    [Fact]
    public void ColumnMenu_MoveToEnd_MovesColumnToLastPosition()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        OpenColumnMenu(cut, "Title");
        ClickColumnMenuItem(cut, "Move to end");

        cut.WaitForState(() =>
            cut.FindAll("[data-column-id]").LastOrDefault()?.GetAttribute("data-column-id") == "Title");
        Assert.Equal(["Status", "Priority", "Assignee", "Title"],
            cut.FindAll("[data-column-id]").Select(h => h.GetAttribute("data-column-id")));
    }

    [Fact]
    public void ColumnMenu_MoveLeftAndToStart_DisabledWhenEveryColumnToTheLeftIsHidden()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        OpenColumnMenu(cut, "Title");
        ClickColumnMenuItem(cut, "Hide field");
        cut.WaitForState(() => cut.FindAll("[data-column-id='Title']").Count == 0);

        // Status is now the leftmost *visible* column, even though the
        // underlying column order still has hidden Title before it.
        OpenColumnMenu(cut, "Status");
        var moveLeft = cut.FindAll(".cg-column-menu-item").Single(item => item.TextContent.Contains("Move left"));
        var moveToStart = cut.FindAll(".cg-column-menu-item").Single(item => item.TextContent.Contains("Move to start"));
        Assert.True(((AngleSharp.Html.Dom.IHtmlButtonElement)moveLeft).IsDisabled);
        Assert.True(((AngleSharp.Html.Dom.IHtmlButtonElement)moveToStart).IsDisabled);
    }

    [Fact]
    public void ColumnMenu_MoveRightAndToEnd_DisabledWhenEveryColumnToTheRightIsHidden()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        OpenColumnMenu(cut, "Assignee");
        ClickColumnMenuItem(cut, "Hide field");
        cut.WaitForState(() => cut.FindAll("[data-column-id='Assignee']").Count == 0);

        // Priority is now the rightmost *visible* column.
        OpenColumnMenu(cut, "Priority");
        var moveRight = cut.FindAll(".cg-column-menu-item").Single(item => item.TextContent.Contains("Move right"));
        var moveToEnd = cut.FindAll(".cg-column-menu-item").Single(item => item.TextContent.Contains("Move to end"));
        Assert.True(((AngleSharp.Html.Dom.IHtmlButtonElement)moveRight).IsDisabled);
        Assert.True(((AngleSharp.Html.Dom.IHtmlButtonElement)moveToEnd).IsDisabled);
    }

    [Fact]
    public void ColumnMenu_MoveLeft_SwapsWithNearestVisibleColumn_SkippingHiddenOnes()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        // Raw order is Title, Status, Priority, Assignee; hide Status so
        // there's a hidden column directly between Title and Priority.
        OpenColumnMenu(cut, "Status");
        ClickColumnMenuItem(cut, "Hide field");
        cut.WaitForState(() => cut.FindAll("[data-column-id='Status']").Count == 0);

        // Moving Priority left must swap it with Title (the nearest visible
        // column), not with hidden Status — a single click should always
        // produce a visible reorder.
        OpenColumnMenu(cut, "Priority");
        ClickColumnMenuItem(cut, "Move left");

        cut.WaitForState(() =>
            cut.FindAll("[data-column-id]").FirstOrDefault()?.GetAttribute("data-column-id") == "Priority");
        Assert.Equal(["Priority", "Title", "Assignee"],
            cut.FindAll("[data-column-id]").Select(h => h.GetAttribute("data-column-id")));
    }

    [Fact]
    public void ColumnsMenu_TogglingVisibility_HidesColumn()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        cut.Find(".cg-columns-button").Click();
        cut.WaitForState(() => cut.FindAll(".cg-columns-menu").Count == 1);

        var priorityCheckbox = cut.FindAll(".cg-columns-menu-item")
            .Single(li => li.TextContent.Contains("Priority"))
            .QuerySelector("input[type='checkbox']")!;
        priorityCheckbox.Change(false);

        cut.WaitForState(() => cut.FindAll("[data-column-id='Priority']").Count == 0);
        Assert.Equal(["Title", "Status", "Assignee"],
            cut.FindAll("[data-column-id]").Select(h => h.GetAttribute("data-column-id")));
    }

    [Fact]
    public void RowSelection_TogglingRowCheckbox_RaisesSelectedKeysChanged_AndShowsIndeterminateHeader()
    {
        IReadOnlySet<string> selected = new HashSet<string>();
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.Create())
            .Add(x => x.EnableRowSelection, true)
            .Add(x => x.RowKey, (Func<Issue, string>)(i => i.Title))
            .Add(x => x.SelectedKeys, selected)
            .Add(x => x.SelectedKeysChanged, (IReadOnlySet<string> s) => selected = s));

        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        cut.FindAll(".cg-body-row .cg-select-cell input")[0].Change(true);

        cut.WaitForState(() => selected.Count == 1);
        Assert.Contains("Fix login bug", selected);

        var headerCheckbox = (IHtmlInputElement)cut.Find("[data-select-all]");
        Assert.False(headerCheckbox.IsChecked);
        Assert.Equal("true", headerCheckbox.GetAttribute("data-indeterminate"));
    }

    [Fact]
    public void RowSelection_HeaderCheckbox_SelectsAndDeselectsAllVisibleRows()
    {
        IReadOnlySet<string> selected = new HashSet<string>();
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.Create())
            .Add(x => x.EnableRowSelection, true)
            .Add(x => x.RowKey, (Func<Issue, string>)(i => i.Title))
            .Add(x => x.SelectedKeys, selected)
            .Add(x => x.SelectedKeysChanged, (IReadOnlySet<string> s) => selected = s));

        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        cut.Find("[data-select-all]").Change(true);
        cut.WaitForState(() => selected.Count == 4);

        var headerCheckbox = (IHtmlInputElement)cut.Find("[data-select-all]");
        Assert.True(headerCheckbox.IsChecked);
        Assert.Equal("false", headerCheckbox.GetAttribute("data-indeterminate"));

        cut.Find("[data-select-all]").Change(false);
        cut.WaitForState(() => selected.Count == 0);
    }

    [Fact]
    public void RecordingProvider_ReceivesExpectedRequests_OnSortAndGroupChanges()
    {
        var provider = new RecordingDataProvider<Issue>(SampleIssues.Create());
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.DataProvider, (IDataProvider<Issue>)provider)
            .Add(x => x.RowKey, (Func<Issue, string>)(i => i.Title)));

        cut.WaitForState(() => provider.Requests.Count >= 1);
        Assert.Null(provider.Requests[0].Sort);
        Assert.Empty(provider.Requests[0].Filters);
        Assert.Null(provider.Requests[0].GroupByPropertyName);

        OpenColumnMenu(cut, "Priority");
        ClickColumnMenuItem(cut, "Sort ascending");
        cut.WaitForState(() => provider.Requests.Count >= 2);
        Assert.Equal(new SortDescriptor("Priority", SortDirection.Ascending), provider.Requests[^1].Sort);

        OpenColumnMenu(cut, "Status");
        ClickColumnMenuItem(cut, "Group by values");
        cut.WaitForState(() => provider.Requests.Count >= 3);
        Assert.Equal("Status", provider.Requests[^1].GroupByPropertyName);
    }

    [Fact]
    public void SortingSecondColumn_SendsBothSortKeys_AndShowsOrderBadges()
    {
        var provider = new RecordingDataProvider<Issue>(SampleIssues.Create());
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.DataProvider, (IDataProvider<Issue>)provider)
            .Add(x => x.RowKey, (Func<Issue, string>)(i => i.Title)));

        cut.WaitForState(() => provider.Requests.Count >= 1);

        OpenColumnMenu(cut, "Priority");
        ClickColumnMenuItem(cut, "Sort ascending");
        cut.WaitForState(() => provider.Requests.Count >= 2);

        // Only one column sorted: no order badge yet.
        Assert.Empty(cut.FindAll(".cg-sort-order"));

        OpenColumnMenu(cut, "Title");
        ClickColumnMenuItem(cut, "Sort descending");
        cut.WaitForState(() => provider.Requests.Count >= 3);

        // Both keys reach the provider, in priority order.
        Assert.Equal(
            new[]
            {
                new SortDescriptor("Priority", SortDirection.Ascending),
                new SortDescriptor("Title", SortDirection.Descending)
            },
            provider.Requests[^1].Sorts);

        // Two columns sorted: each shows its 1-based priority badge (Priority is
        // primary = 1, Title is secondary = 2), regardless of column order.
        var priorityBadge = cut.Find("[data-column-id='Priority'] .cg-sort-order").TextContent.Trim();
        var titleBadge = cut.Find("[data-column-id='Title'] .cg-sort-order").TextContent.Trim();
        Assert.Equal("1", priorityBadge);
        Assert.Equal("2", titleBadge);
    }

    // R5 — graceful degradation. In this test host JS interop is Loose (see the
    // constructor), so positionFloatingPanel never runs and the panel keeps its
    // pre-JS state: the CSS-fallback markup from ColumnMenu.razor.css
    // (position:absolute; right:0; top:calc(100% + 4px)) rendered inside its
    // anchor. bUnit has no layout engine, so these assert the *markup/structure*
    // that the fallback CSS targets — never pixel positions or inline fixed styles.

    [Fact]
    public void ColumnMenu_WithoutJsInterop_RendersFallbackMarkupInsideItsAnchor()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        OpenColumnMenu(cut, "Title");

        // The panel renders with the class the fallback CSS rule (.cg-column-menu)
        // targets, and it lives inside the .cg-column-menu-anchor span next to its
        // trigger — the containing block the fallback's right:0 / top:100% resolves
        // against. No JS interop has repositioned it.
        var panel = cut.Find(".cg-column-menu");
        Assert.Contains("cg-column-menu", panel.ClassList);

        var anchor = panel.Closest(".cg-column-menu-anchor");
        Assert.NotNull(anchor);
        Assert.NotNull(anchor!.QuerySelector(".cg-column-menu-button"));
    }

    [Fact]
    public void ColumnsMenu_WithoutJsInterop_RendersFallbackMarkupInsideItsAnchor()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        cut.Find(".cg-columns-button").Click();
        cut.WaitForState(() => cut.FindAll(".cg-columns-menu").Count == 1);

        var panel = cut.Find(".cg-columns-menu");
        Assert.Contains("cg-columns-menu", panel.ClassList);

        var anchor = panel.Closest(".cg-columns-menu-anchor");
        Assert.NotNull(anchor);
        Assert.NotNull(anchor!.QuerySelector(".cg-columns-button"));
    }

    [Fact]
    public void MissingItemsAndDataProvider_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Render<IssueGridHost>());
    }

    [Fact]
    public void BothItemsAndDataProvider_Throws()
    {
        var provider = new RecordingDataProvider<Issue>(SampleIssues.Create());
        Assert.Throws<InvalidOperationException>(() => Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.Create())
            .Add(x => x.DataProvider, (IDataProvider<Issue>)provider)));
    }

    [Fact]
    public void DataProviderWithoutRowKey_Throws()
    {
        var provider = new RecordingDataProvider<Issue>(SampleIssues.Create());
        Assert.Throws<InvalidOperationException>(() => Render<IssueGridHost>(p => p
            .Add(x => x.DataProvider, (IDataProvider<Issue>)provider)));
    }
}

using Bunit;
using ColonnadeGrid.Abstractions;
using ColonnadeGrid.Models;
using ColonnadeGrid.Tests.TestSupport;

namespace ColonnadeGrid.Tests.Components;

/// <summary>
/// Per-group paging: a paged, grouped grid over an IGroupedDataProvider (the
/// in-memory provider behind Items is one). The data throughout is
/// SampleIssues.CreateMany(23), grouped by Status: InProgress (8 rows), Done
/// (8), Todo (7), in that order.
/// </summary>
public class ColonnadeGridGroupPagingTests : BunitContext
{
    public ColonnadeGridGroupPagingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>
    /// Renders a paged grid (PageSize 10, so a 10-row budget; 5 rows per group
    /// page) and groups it by Status. With that budget, InProgress and Done
    /// start expanded (5 + 5 rows) and Todo starts collapsed.
    /// </summary>
    private IRenderedComponent<IssueGridHost> RenderGroupedByStatus(
        IDataProvider<Issue>? provider = null,
        Action<ComponentParameterCollectionBuilder<IssueGridHost>>? configure = null)
    {
        var cut = Render<IssueGridHost>(p =>
        {
            if (provider is null)
            {
                p.Add(x => x.Items, SampleIssues.CreateMany(23));
            }
            else
            {
                p.Add(x => x.DataProvider, provider).Add(x => x.RowKey, (Func<Issue, string>)(i => i.Title));
            }

            p.Add(x => x.EnablePaging, true).Add(x => x.PageSize, 10).Add(x => x.GroupPageSize, 5);
            configure?.Invoke(p);
        });

        cut.WaitForState(() => cut.FindAll(".cg-column-menu-button").Count == 4);
        cut.Find("[data-column-id='Status'] .cg-column-menu-button").Click();
        cut.FindAll(".cg-column-menu-item").Single(item => item.TextContent.Contains("Group by values")).Click();
        cut.WaitForState(() => cut.FindAll(".cg-group-header-row").Count > 0);
        return cut;
    }

    private static List<string> Headers(IRenderedComponent<IssueGridHost> cut) =>
        cut.FindAll(".cg-group-header-row")
            .Select(h => $"{h.QuerySelector(".cg-group-title")!.TextContent} {h.QuerySelector(".cg-group-count")!.TextContent}")
            .ToList();

    private static List<string> Titles(IRenderedComponent<IssueGridHost> cut) =>
        cut.FindAll(".cg-body-row").Select(row => row.QuerySelector(".cg-cell")!.TextContent).ToList();

    private static IEnumerable<string> Issues(params int[] numbers) => numbers.Select(n => $"Issue {n:00}");

    [Fact]
    public void ListsAllGroupsWithCounts_AndExpandsOnlyGroupsWithinTheRowBudget()
    {
        var cut = RenderGroupedByStatus();

        cut.WaitForAssertion(() => Assert.Equal(Issues(1, 4, 7, 10, 13, 2, 5, 8, 11, 14), Titles(cut)));
        Assert.Equal(["InProgress 8", "Done 8", "Todo 7"], Headers(cut));
        Assert.Equal(["1–5 of 8", "1–5 of 8"], cut.FindAll(".cg-group-pager-range").Select(e => e.TextContent));
        Assert.Equal("3 groups · 23 rows", cut.Find(".cg-group-footer-summary").TextContent);
        Assert.Empty(cut.FindAll(".cg-pager"));
        Assert.Empty(cut.FindAll(".cg-group-footer-more"));
    }

    [Fact]
    public void GroupPager_ShowsTheNextPageOfThatGroupOnly()
    {
        var cut = RenderGroupedByStatus();
        cut.WaitForAssertion(() => Assert.Equal(10, Titles(cut).Count));

        cut.FindAll(".cg-group-pager-next")[0].Click();

        cut.WaitForAssertion(() => Assert.Equal(Issues(16, 19, 22, 2, 5, 8, 11, 14), Titles(cut)));
        Assert.Equal(["6–8 of 8", "1–5 of 8"], cut.FindAll(".cg-group-pager-range").Select(e => e.TextContent));
    }

    [Fact]
    public void ExpandingAGroup_LoadsItsRows_CollapsingAGroup_DropsThem()
    {
        var cut = RenderGroupedByStatus();
        cut.WaitForAssertion(() => Assert.Equal(10, Titles(cut).Count));

        cut.FindAll(".cg-group-header-row")[2].Click();
        cut.WaitForAssertion(() => Assert.Equal(Issues(1, 4, 7, 10, 13, 2, 5, 8, 11, 14, 3, 6, 9, 12, 15), Titles(cut)));

        cut.FindAll(".cg-group-header-row")[0].Click();
        cut.WaitForAssertion(() => Assert.Equal(Issues(2, 5, 8, 11, 14, 3, 6, 9, 12, 15), Titles(cut)));
    }

    [Fact]
    public void TogglingAGroup_IsRecordedInGridState()
    {
        GridState? state = null;
        var cut = RenderGroupedByStatus(configure: p => p.Add(x => x.StateChanged, (GridState s) => state = s));
        cut.WaitForAssertion(() => Assert.Equal(10, Titles(cut).Count));

        cut.FindAll(".cg-group-header-row")[2].Click();
        cut.FindAll(".cg-group-header-row")[0].Click();

        cut.WaitForAssertion(() => Assert.Contains("Todo", state!.ExpandedGroupKeys));
        Assert.Contains("InProgress", state!.CollapsedGroupKeys);
    }

    [Fact]
    public void ShowMoreGroups_ListsTheNextBatch_AndSaysHowManyRemain()
    {
        var cut = RenderGroupedByStatus(configure: p => p.Add(x => x.GroupsPerLoad, 1));

        cut.WaitForAssertion(() => Assert.Equal(["InProgress 8"], Headers(cut)));
        Assert.Equal("1 of 3 groups · 23 rows", cut.Find(".cg-group-footer-summary").TextContent);
        Assert.Equal("Show 1 more group (2 remaining)", cut.Find(".cg-group-footer-more").TextContent.Trim());

        cut.Find(".cg-group-footer-more").Click();
        cut.WaitForAssertion(() => Assert.Equal(["InProgress 8", "Done 8"], Headers(cut)));
        Assert.Equal("Show 1 more group", cut.Find(".cg-group-footer-more").TextContent.Trim());

        // Done fits in what's left of the budget, so it starts expanded.
        cut.WaitForAssertion(() => Assert.Equal(10, Titles(cut).Count));

        cut.Find(".cg-group-footer-more").Click();
        cut.WaitForAssertion(() => Assert.Equal(["InProgress 8", "Done 8", "Todo 7"], Headers(cut)));
        Assert.Empty(cut.FindAll(".cg-group-footer-more"));
        Assert.Equal(10, Titles(cut).Count);
    }

    [Fact]
    public async Task LoadingGroups_ShowSkeletonRowsSizedToTheirPages_FetchedInOneRequest()
    {
        var provider = new RecordingGroupedDataProvider<Issue>(SampleIssues.CreateMany(23)) { PagesGate = new TaskCompletionSource() };
        var cut = RenderGroupedByStatus(provider);

        cut.WaitForState(() => cut.FindAll(".cg-skeleton-row").Count == 10);
        Assert.All(cut.FindAll(".cg-skeleton-row"), row => Assert.Equal("true", row.GetAttribute("aria-busy")));
        Assert.Empty(cut.FindAll(".cg-body-row"));
        Assert.Equal(
            [new GroupPageRequest("InProgress", 0, 5), new GroupPageRequest("Done", 0, 5)],
            Assert.Single(provider.GroupPagesRequests).Pages);

        await cut.InvokeAsync(() => provider.PagesGate.SetResult());

        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 10);
        Assert.Empty(cut.FindAll(".cg-skeleton-row"));
    }

    [Fact]
    public void FailedGroupLoad_ShowsErrorInTheGroup_AndRetryLoadsIt()
    {
        var provider = new RecordingGroupedDataProvider<Issue>(SampleIssues.CreateMany(23)) { PagesFailuresRemaining = 1 };
        var cut = RenderGroupedByStatus(provider);

        cut.WaitForState(() => cut.FindAll(".cg-group-error").Count == 2);
        Assert.Contains("boom", cut.Find(".cg-group-error").TextContent);

        cut.FindAll(".cg-group-retry")[0].Click();

        cut.WaitForAssertion(() => Assert.Equal(Issues(1, 4, 7, 10, 13), Titles(cut)));
        Assert.Single(cut.FindAll(".cg-group-error"));
    }

    [Fact]
    public void SortChange_ReordersGroupsAndRows_AndResetsGroupPages()
    {
        var cut = RenderGroupedByStatus();
        cut.WaitForAssertion(() => Assert.Equal(10, Titles(cut).Count));
        cut.FindAll(".cg-group-pager-next")[0].Click();
        cut.WaitForAssertion(() => Assert.Equal("6–8 of 8", cut.FindAll(".cg-group-pager-range")[0].TextContent));

        cut.Find("[data-column-id='Title'] .cg-column-menu-button").Click();
        cut.FindAll(".cg-column-menu-item").Single(item => item.TextContent.Contains("Sort descending")).Click();

        // Groups follow first appearance in the new order: Issue 23 is Done.
        cut.WaitForAssertion(() => Assert.Equal(["Done 8", "InProgress 8", "Todo 7"], Headers(cut)));
        cut.WaitForAssertion(() => Assert.Equal(Issues(23, 20, 17, 14, 11, 22, 19, 16, 13, 10), Titles(cut)));
        Assert.Equal(["1–5 of 8", "1–5 of 8"], cut.FindAll(".cg-group-pager-range").Select(e => e.TextContent));
    }

    [Fact]
    public void LargerBudget_ExpandsMoreGroups()
    {
        var cut = RenderGroupedByStatus(configure: p => p.Add(x => x.GroupRowBudget, 15));

        cut.WaitForAssertion(() => Assert.Equal(15, Titles(cut).Count));
    }
}

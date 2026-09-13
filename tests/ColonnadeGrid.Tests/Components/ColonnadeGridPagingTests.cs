using Bunit;
using ColonnadeGrid.Abstractions;
using ColonnadeGrid.Tests.TestSupport;

namespace ColonnadeGrid.Tests.Components;

public class ColonnadeGridPagingTests : BunitContext
{
    public ColonnadeGridPagingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static void AssertRange(IRenderedComponent<IssueGridHost> cut, string expected) =>
        cut.WaitForAssertion(() => Assert.Equal(expected, cut.Find(".cg-pager-range").TextContent));

    private static string FirstTitle(IRenderedComponent<IssueGridHost> cut) =>
        cut.Find(".cg-body-row .cg-cell").TextContent;

    [Fact]
    public void PagingDisabled_RequestsEveryRow_AndRendersNoPager()
    {
        var provider = new RecordingDataProvider<Issue>(SampleIssues.CreateMany(23));
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.DataProvider, (IDataProvider<Issue>)provider)
            .Add(x => x.RowKey, (Func<Issue, string>)(i => i.Title)));

        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 23);
        Assert.Equal(0, provider.Requests[^1].Skip);
        Assert.Equal(int.MaxValue, provider.Requests[^1].Take);
        Assert.Empty(cut.FindAll(".cg-pager"));
    }

    [Fact]
    public void PagingEnabled_ShowsFirstPage_WithRangeAndPageButtons()
    {
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.CreateMany(23))
            .Add(x => x.EnablePaging, true)
            .Add(x => x.PageSize, 10));

        AssertRange(cut, "1–10 of 23");
        Assert.Equal(10, cut.FindAll(".cg-body-row").Count);
        Assert.Equal("Issue 01", FirstTitle(cut));
        Assert.Equal(["1", "2", "3"], cut.FindAll(".cg-pager-page").Select(b => b.TextContent.Trim()));
        Assert.Equal("page", cut.Find(".cg-pager-page-current").GetAttribute("aria-current"));
        Assert.True(cut.Find(".cg-pager-previous").HasAttribute("disabled"));
    }

    [Fact]
    public void PagingEnabled_SendsCurrentPageToProvider()
    {
        var provider = new RecordingDataProvider<Issue>(SampleIssues.CreateMany(23));
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.DataProvider, (IDataProvider<Issue>)provider)
            .Add(x => x.RowKey, (Func<Issue, string>)(i => i.Title))
            .Add(x => x.EnablePaging, true)
            .Add(x => x.PageSize, 10));
        AssertRange(cut, "1–10 of 23");
        Assert.Equal((0, 10), (provider.Requests[^1].Skip, provider.Requests[^1].Take));

        cut.Find(".cg-pager-next").Click();

        AssertRange(cut, "11–20 of 23");
        Assert.Equal((10, 10), (provider.Requests[^1].Skip, provider.Requests[^1].Take));
    }

    [Fact]
    public void NextAndPageButtons_ShowThatPage()
    {
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.CreateMany(23))
            .Add(x => x.EnablePaging, true)
            .Add(x => x.PageSize, 10));
        AssertRange(cut, "1–10 of 23");

        cut.Find(".cg-pager-next").Click();
        AssertRange(cut, "11–20 of 23");
        Assert.Equal("Issue 11", FirstTitle(cut));

        cut.Find(".cg-pager-page[aria-label='Page 3']").Click();
        AssertRange(cut, "21–23 of 23");
        Assert.Equal(3, cut.FindAll(".cg-body-row").Count);
        Assert.True(cut.Find(".cg-pager-next").HasAttribute("disabled"));
    }

    [Fact]
    public void SortChange_ReturnsToFirstPage_AndRaisesPageIndexChanged()
    {
        var pageIndex = -1;
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.CreateMany(23))
            .Add(x => x.EnablePaging, true)
            .Add(x => x.PageSize, 10)
            .Add(x => x.PageIndexChanged, (int i) => pageIndex = i));
        AssertRange(cut, "1–10 of 23");
        cut.Find(".cg-pager-next").Click();
        AssertRange(cut, "11–20 of 23");
        Assert.Equal(1, pageIndex);

        cut.Find("[data-column-id='Priority'] .cg-column-menu-button").Click();
        cut.FindAll(".cg-column-menu-item").Single(item => item.TextContent.Contains("Sort ascending")).Click();

        AssertRange(cut, "1–10 of 23");
        Assert.Equal(0, pageIndex);
    }

    [Fact]
    public void PageSizeChange_KeepsFirstVisibleRowOnScreen_AndRaisesCallbacks()
    {
        int? pageSize = null;
        int? pageIndex = null;
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.CreateMany(23))
            .Add(x => x.EnablePaging, true)
            .Add(x => x.PageSize, 5)
            .Add(x => x.PageSizeOptions, [5, 10, 25])
            .Add(x => x.PageSizeChanged, (int s) => pageSize = s)
            .Add(x => x.PageIndexChanged, (int i) => pageIndex = i));
        AssertRange(cut, "1–5 of 23");
        cut.Find(".cg-pager-page[aria-label='Page 3']").Click();
        AssertRange(cut, "11–15 of 23");

        cut.Find(".cg-pager-size-select").Change("10");

        AssertRange(cut, "11–20 of 23");
        Assert.Equal(10, pageSize);
        Assert.Equal(1, pageIndex);
    }

    [Fact]
    public void PageIndexParameter_OpensThatPage_AndHostChangesLoadNewPage()
    {
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.CreateMany(23))
            .Add(x => x.EnablePaging, true)
            .Add(x => x.PageSize, 10)
            .Add(x => x.PageIndex, 1));
        AssertRange(cut, "11–20 of 23");

        cut.Render(p => p.Add(x => x.PageIndex, 2));

        AssertRange(cut, "21–23 of 23");
    }

    [Fact]
    public void UnboundPageIndex_HostReRender_DoesNotResetPage()
    {
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.CreateMany(23))
            .Add(x => x.EnablePaging, true)
            .Add(x => x.PageSize, 10));
        AssertRange(cut, "1–10 of 23");
        cut.Find(".cg-pager-next").Click();
        AssertRange(cut, "11–20 of 23");

        // The host still passes its original PageIndex (0) on re-render.
        cut.Render(p => p.Add(x => x.EnableRowSelection, true));

        cut.WaitForState(() => cut.FindAll(".cg-body-row .cg-select-cell").Count == 10);
        AssertRange(cut, "11–20 of 23");
    }

    [Fact]
    public void PageIndexPastLastPage_ShowsLastPage()
    {
        int? pageIndex = null;
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.CreateMany(23))
            .Add(x => x.EnablePaging, true)
            .Add(x => x.PageSize, 10)
            .Add(x => x.PageIndex, 99)
            .Add(x => x.PageIndexChanged, (int i) => pageIndex = i));

        AssertRange(cut, "21–23 of 23");
        Assert.Equal(2, pageIndex);
    }

    [Fact]
    public void Grouped_WithoutGroupedProvider_PagesRowsAcrossGroups_AndHeadersShowTotals()
    {
        // RecordingDataProvider only implements IDataProvider, so grouping pages
        // over the grouped rows instead of paging each group separately.
        var provider = new RecordingDataProvider<Issue>(SampleIssues.CreateMany(23));
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.DataProvider, (IDataProvider<Issue>)provider)
            .Add(x => x.RowKey, (Func<Issue, string>)(i => i.Title))
            .Add(x => x.EnablePaging, true)
            .Add(x => x.PageSize, 10));
        AssertRange(cut, "1–10 of 23");

        cut.Find("[data-column-id='Status'] .cg-column-menu-button").Click();
        cut.FindAll(".cg-column-menu-item").Single(item => item.TextContent.Contains("Group by values")).Click();

        // Groups in first-appearance order: InProgress (8 rows), Done (8), Todo (7).
        // Page 1 holds all of InProgress and the first 2 of Done.
        cut.WaitForState(() => cut.FindAll(".cg-group-header-row").Count == 2);
        Assert.Equal(10, cut.FindAll(".cg-body-row").Count);
        Assert.Equal(
            ["InProgress 8", "Done 8"],
            cut.FindAll(".cg-group-header-row").Select(h =>
                $"{h.QuerySelector(".cg-group-title")!.TextContent} {h.QuerySelector(".cg-group-count")!.TextContent}"));
    }

    [Fact]
    public void NullPageSizeOptions_HidesPageSizeSelector()
    {
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.CreateMany(23))
            .Add(x => x.EnablePaging, true)
            .Add(x => x.PageSizeOptions, null));

        AssertRange(cut, "1–23 of 23");
        Assert.Empty(cut.FindAll(".cg-pager-size-select"));
    }

    [Fact]
    public void PageSizeBelowOne_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.CreateMany(23))
            .Add(x => x.EnablePaging, true)
            .Add(x => x.PageSize, 0)));
    }
}

using Bunit;
using ColonnadeGrid.Internal;

namespace ColonnadeGrid.Tests.Components;

/// <summary>
/// Renders the per-group pager directly to cover its page-math, disabled states,
/// and the page indices it reports when its buttons are clicked.
/// </summary>
public class GroupPagerTests : BunitContext
{
    private IRenderedComponent<GroupPager> RenderPager(
        int pageIndex, int pageSize, int totalCount, Action<int> onPageRequested) =>
        Render<GroupPager>(p => p
            .Add(x => x.PageIndex, pageIndex)
            .Add(x => x.PageSize, pageSize)
            .Add(x => x.TotalCount, totalCount)
            .Add(x => x.GroupName, "Open")
            .Add(x => x.OnPageRequested, onPageRequested));

    [Fact]
    public void FirstPage_DisablesFirstAndPreviousButtons()
    {
        var cut = RenderPager(pageIndex: 0, pageSize: 10, totalCount: 23, _ => { });

        Assert.True(cut.Find(".cg-group-pager-first").HasAttribute("disabled"));
        Assert.True(cut.Find(".cg-group-pager-previous").HasAttribute("disabled"));
        Assert.False(cut.Find(".cg-group-pager-next").HasAttribute("disabled"));
        Assert.False(cut.Find(".cg-group-pager-last").HasAttribute("disabled"));
        Assert.Equal("Page 1 of 3", cut.Find(".cg-group-pager-status").TextContent.Trim());
    }

    [Fact]
    public void LastPage_DisablesNextAndLastButtons()
    {
        var cut = RenderPager(pageIndex: 2, pageSize: 10, totalCount: 23, _ => { });

        Assert.False(cut.Find(".cg-group-pager-first").HasAttribute("disabled"));
        Assert.False(cut.Find(".cg-group-pager-previous").HasAttribute("disabled"));
        Assert.True(cut.Find(".cg-group-pager-next").HasAttribute("disabled"));
        Assert.True(cut.Find(".cg-group-pager-last").HasAttribute("disabled"));
        Assert.Equal("Page 3 of 3", cut.Find(".cg-group-pager-status").TextContent.Trim());
    }

    [Fact]
    public void Buttons_ReportTheExpectedPageIndex()
    {
        var requested = new List<int>();
        var cut = RenderPager(pageIndex: 1, pageSize: 10, totalCount: 23, requested.Add);

        cut.Find(".cg-group-pager-first").Click();
        cut.Find(".cg-group-pager-previous").Click();
        cut.Find(".cg-group-pager-next").Click();
        cut.Find(".cg-group-pager-last").Click();

        Assert.Equal(new[] { 0, 0, 2, 2 }, requested);
    }

    [Fact]
    public void SinglePage_ReportsPageOneOfOne()
    {
        var cut = RenderPager(pageIndex: 0, pageSize: 10, totalCount: 4, _ => { });

        Assert.Equal("Page 1 of 1", cut.Find(".cg-group-pager-status").TextContent.Trim());
        Assert.True(cut.Find(".cg-group-pager-next").HasAttribute("disabled"));
    }
}

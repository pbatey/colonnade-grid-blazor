using Bunit;
using ColonnadeGrid.Models;
using ColonnadeGrid.Tests.TestSupport;

namespace ColonnadeGrid.Tests.Components;

/// <summary>
/// A host-provided <see cref="GridState"/> that changes the query returns to the
/// first page, as the same change from the grid's menus does. The data is
/// SampleIssues.CreateMany(23) at 10 rows per page, starting on the second page.
/// </summary>
public class HostStatePagingTests : BunitContext
{
    private static readonly string[] ColumnIds = ["Title", "Status", "Priority", "Assignee"];

    private int? _reportedPageIndex;

    public HostStatePagingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<IssueGridHost> RenderOnSecondPage()
    {
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.CreateMany(23))
            .Add(x => x.EnablePaging, true)
            .Add(x => x.PageSize, 10)
            .Add(x => x.PageIndexChanged, (int pageIndex) => _reportedPageIndex = pageIndex));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 10);

        cut.Find(".cg-pager-next").Click();
        cut.WaitForAssertion(() => Assert.Contains("Issue 11", cut.Find(".cg-body-row").TextContent));
        Assert.Equal(1, _reportedPageIndex);
        return cut;
    }

    private static string FirstRow(IRenderedComponent<IssueGridHost> cut) => cut.Find(".cg-body-row").TextContent;

    [Fact]
    public void HostStateWithANewSort_ReturnsToTheFirstPage()
    {
        var cut = RenderOnSecondPage();

        cut.Render(p => p.Add(x => x.State, GridState.Create(ColumnIds).SetSort(new SortDescriptor("Title", SortDirection.Descending))));

        cut.WaitForAssertion(() => Assert.Contains("Issue 23", FirstRow(cut)));
        Assert.Equal(0, _reportedPageIndex);
    }

    [Fact]
    public void HostStateWithANewFilter_ReturnsToTheFirstPage()
    {
        var cut = RenderOnSecondPage();

        // Every row still matches, so the second page would still exist.
        cut.Render(p => p.Add(x => x.State, GridState.Create(ColumnIds).SetFilter(new FilterDescriptor("Title", FilterOperator.Contains, "Issue"))));

        cut.WaitForAssertion(() => Assert.Contains("Issue 01", FirstRow(cut)));
        Assert.Equal(0, _reportedPageIndex);
    }

    [Fact]
    public void HostStateWithANewGroupBy_ReturnsToTheFirstPage()
    {
        var cut = RenderOnSecondPage();

        cut.Render(p => p.Add(x => x.State, GridState.Create(ColumnIds).SetGroupBy("Status")));

        cut.WaitForState(() => cut.FindAll(".cg-group-header-row").Count == 3);
        Assert.Equal(0, _reportedPageIndex);
    }

    [Fact]
    public void HostStateAndPageIndexChangedTogether_ShowTheHostsPage()
    {
        // A host restoring both a view and a page (e.g. from a URL) gets that page.
        var cut = RenderOnSecondPage();

        cut.Render(p => p
            .Add(x => x.State, GridState.Create(ColumnIds).SetSort(new SortDescriptor("Title", SortDirection.Descending)))
            .Add(x => x.PageIndex, 2));

        cut.WaitForAssertion(() => Assert.Contains("Issue 03", FirstRow(cut)));
        Assert.Equal(1, _reportedPageIndex);
    }

    [Fact]
    public void HostStateChangingOnlyColumns_KeepsTheCurrentPage()
    {
        var cut = RenderOnSecondPage();

        cut.Render(p => p.Add(x => x.State, GridState.Create(ColumnIds).SetColumnVisible("Assignee", false)));

        cut.WaitForState(() => cut.FindAll("[data-column-id='Assignee']").Count == 0);
        Assert.Contains("Issue 11", FirstRow(cut));
        Assert.Equal(1, _reportedPageIndex);
    }
}

using Bunit;
using ColonnadeGrid.Abstractions;
using ColonnadeGrid.Models;
using ColonnadeGrid.Providers;
using ColonnadeGrid.Tests.TestSupport;

namespace ColonnadeGrid.Tests.Components;

/// <summary>A data source that throws: the grid shows the failure with a way to retry, rather than letting it escape.</summary>
public class LoadErrorTests : BunitContext
{
    public LoadErrorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>Fails the next <see cref="FailuresRemaining"/> requests, then serves the items.</summary>
    private sealed class FailingDataProvider(IEnumerable<Issue> items) : IDataProvider<Issue>
    {
        private readonly InMemoryDataProvider<Issue> _inner = new(items);

        public int FailuresRemaining { get; set; }

        public Task<DataResponse<Issue>> GetDataAsync(DataRequest request, CancellationToken cancellationToken = default)
        {
            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;
                throw new InvalidOperationException("boom");
            }

            return _inner.GetDataAsync(request, cancellationToken);
        }
    }

    private static readonly Func<Issue, string> TitleKey = i => i.Title;

    private static List<string> Headers(IRenderedComponent<IssueGridHost> cut) =>
        cut.FindAll(".cg-group-header-row").Select(h => h.QuerySelector(".cg-group-title")!.TextContent).ToList();

    [Fact]
    public void FailedLoad_ShowsTheError_AndRetryLoadsTheRows()
    {
        var provider = new FailingDataProvider(SampleIssues.Create()) { FailuresRemaining = 1 };
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.DataProvider, (IDataProvider<Issue>)provider)
            .Add(x => x.RowKey, TitleKey));

        cut.WaitForState(() => cut.FindAll(".cg-load-error").Count == 1);
        Assert.Contains("boom", cut.Find(".cg-load-error").TextContent);
        Assert.Empty(cut.FindAll(".cg-body-row"));

        cut.Find(".cg-load-retry").Click();

        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);
        Assert.Empty(cut.FindAll(".cg-load-error"));
    }

    [Fact]
    public void FailedPageLoad_ShowsTheError_InsteadOfThrowing()
    {
        var provider = new FailingDataProvider(SampleIssues.CreateMany(23));
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.DataProvider, (IDataProvider<Issue>)provider)
            .Add(x => x.RowKey, TitleKey)
            .Add(x => x.EnablePaging, true)
            .Add(x => x.PageSize, 10));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 10);

        provider.FailuresRemaining = 1;
        cut.Find(".cg-pager-next").Click();

        cut.WaitForState(() => cut.FindAll(".cg-load-error").Count == 1);

        cut.Find(".cg-load-retry").Click();
        cut.WaitForAssertion(() => Assert.Contains("Issue 11", cut.Find(".cg-body-row").TextContent));
    }

    [Fact]
    public void FailedGroupListLoad_ShowsTheError_AndRetryListsTheGroups()
    {
        var provider = new RecordingGroupedDataProvider<Issue>(SampleIssues.CreateMany(23)) { GroupListFailuresRemaining = 1 };
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.DataProvider, (IDataProvider<Issue>)provider)
            .Add(x => x.RowKey, TitleKey)
            .Add(x => x.EnablePaging, true)
            .Add(x => x.State, GridState.Create(["Title", "Status", "Priority", "Assignee"]).SetGroupBy("Status")));

        cut.WaitForState(() => cut.FindAll(".cg-load-error").Count == 1);
        Assert.Contains("boom", cut.Find(".cg-load-error").TextContent);

        cut.Find(".cg-load-retry").Click();

        cut.WaitForAssertion(() => Assert.Equal(["InProgress", "Done", "Todo"], Headers(cut)));
        Assert.Empty(cut.FindAll(".cg-load-error"));
    }

    [Fact]
    public void FailedShowMoreGroups_ShowsTheErrorInTheFooter_AndKeepsTheListedGroups()
    {
        var provider = new RecordingGroupedDataProvider<Issue>(SampleIssues.CreateMany(23));
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.DataProvider, (IDataProvider<Issue>)provider)
            .Add(x => x.RowKey, TitleKey)
            .Add(x => x.EnablePaging, true)
            .Add(x => x.GroupsPerLoad, 1)
            .Add(x => x.State, GridState.Create(["Title", "Status", "Priority", "Assignee"]).SetGroupBy("Status")));
        cut.WaitForAssertion(() => Assert.Equal(["InProgress"], Headers(cut)));

        provider.GroupListFailuresRemaining = 1;
        cut.Find(".cg-group-footer-more").Click();

        cut.WaitForState(() => cut.FindAll(".cg-group-footer-error").Count == 1);
        Assert.Contains("boom", cut.Find(".cg-group-footer-error").TextContent);
        Assert.Equal(["InProgress"], Headers(cut));

        // The "Show more" button stays, and works as the retry.
        cut.Find(".cg-group-footer-more").Click();

        cut.WaitForAssertion(() => Assert.Equal(["InProgress", "Done"], Headers(cut)));
        Assert.Empty(cut.FindAll(".cg-group-footer-error"));
    }
}

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

    /// <summary>Serves rows, but fails every column stats request.</summary>
    private sealed class FailingStatsProvider(IEnumerable<Issue> items) : IDataProvider<Issue>, IColumnStatsProvider<Issue>
    {
        private readonly InMemoryDataProvider<Issue> _inner = new(items);

        public Task<DataResponse<Issue>> GetDataAsync(DataRequest request, CancellationToken cancellationToken = default) =>
            _inner.GetDataAsync(request, cancellationToken);

        public Task<ColumnStats> GetColumnStatsAsync(ColumnStatsRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("stats boom");
    }

    [Fact]
    public void FailedLoad_RaisesOnLoadError_AndShowsTheFormattedMessage()
    {
        var errors = new List<Exception>();
        var provider = new FailingDataProvider(SampleIssues.Create()) { FailuresRemaining = 1 };
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.DataProvider, (IDataProvider<Issue>)provider)
            .Add(x => x.RowKey, TitleKey)
            .Add(x => x.OnLoadError, (Exception e) => errors.Add(e))
            .Add(x => x.FormatLoadError, (Func<Exception, string>)(_ => "Something went wrong.")));

        cut.WaitForState(() => cut.FindAll(".cg-load-error").Count == 1);
        Assert.Contains("Something went wrong.", cut.Find(".cg-load-error").TextContent);
        Assert.DoesNotContain("boom", cut.Find(".cg-load-error").TextContent);
        Assert.Equal("boom", Assert.Single(errors).Message);
    }

    [Fact]
    public void FailedGroupPageLoad_RaisesOnLoadError_AndShowsTheFormattedMessage()
    {
        var errors = new List<Exception>();
        var provider = new RecordingGroupedDataProvider<Issue>(SampleIssues.CreateMany(23)) { PagesFailuresRemaining = 1 };
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.DataProvider, (IDataProvider<Issue>)provider)
            .Add(x => x.RowKey, TitleKey)
            .Add(x => x.EnablePaging, true)
            .Add(x => x.State, GridState.Create(["Title", "Status", "Priority", "Assignee"]).SetGroupBy("Status"))
            .Add(x => x.OnLoadError, (Exception e) => errors.Add(e))
            .Add(x => x.FormatLoadError, (Func<Exception, string>)(_ => "Something went wrong.")));

        cut.WaitForState(() => cut.FindAll(".cg-group-error").Count > 0);
        Assert.All(cut.FindAll(".cg-group-error"), error => Assert.Contains("Something went wrong.", error.TextContent));
        Assert.Equal("boom", Assert.Single(errors).Message);
    }

    [Fact]
    public void FailedShowMoreGroups_ShowsTheFormattedMessage()
    {
        var provider = new RecordingGroupedDataProvider<Issue>(SampleIssues.CreateMany(23));
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.DataProvider, (IDataProvider<Issue>)provider)
            .Add(x => x.RowKey, TitleKey)
            .Add(x => x.EnablePaging, true)
            .Add(x => x.GroupsPerLoad, 1)
            .Add(x => x.State, GridState.Create(["Title", "Status", "Priority", "Assignee"]).SetGroupBy("Status"))
            .Add(x => x.FormatLoadError, (Func<Exception, string>)(_ => "Something went wrong.")));
        cut.WaitForAssertion(() => Assert.Equal(["InProgress"], Headers(cut)));

        provider.GroupListFailuresRemaining = 1;
        cut.Find(".cg-group-footer-more").Click();

        cut.WaitForState(() => cut.FindAll(".cg-group-footer-error").Count == 1);
        Assert.Contains("Something went wrong.", cut.Find(".cg-group-footer-error").TextContent);
    }

    [Fact]
    public void FailedColumnStatsLoad_RaisesOnLoadError()
    {
        var errors = new List<Exception>();
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.DataProvider, (IDataProvider<Issue>)new FailingStatsProvider(SampleIssues.Create()))
            .Add(x => x.RowKey, TitleKey)
            .Add(x => x.OnLoadError, (Exception e) => errors.Add(e)));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        // Priority is a number, so its filter editor loads the column's stats.
        cut.Find("[data-column-id='Priority'] .cg-column-menu-button").Click();
        cut.FindAll(".cg-column-menu-item").Single(item => item.TextContent.Contains("Filter by values")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Couldn't load", cut.Find(".cg-filter-status").TextContent));
        Assert.Equal("stats boom", Assert.Single(errors).Message);
    }
}

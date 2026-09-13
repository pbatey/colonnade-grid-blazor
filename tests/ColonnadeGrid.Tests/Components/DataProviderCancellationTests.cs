using Bunit;
using ColonnadeGrid.Abstractions;
using ColonnadeGrid.Models;
using ColonnadeGrid.Providers;
using ColonnadeGrid.Tests.TestSupport;

namespace ColonnadeGrid.Tests.Components;

public class DataProviderCancellationTests : BunitContext
{
    public DataProviderCancellationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>A provider whose first request never finishes unless it's cancelled, like a slow network call.</summary>
    private sealed class FirstRequestHangsProvider(IEnumerable<Issue> items) : IDataProvider<Issue>
    {
        private readonly InMemoryDataProvider<Issue> _inner = new(items);
        private int _requestCount;

        public async Task<DataResponse<Issue>> GetDataAsync(DataRequest request, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _requestCount) == 1)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return await _inner.GetDataAsync(request, cancellationToken);
        }
    }

    [Fact]
    public void CancelledFirstLoad_StillFinishesInitialization()
    {
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.DataProvider, (IDataProvider<Issue>)new FirstRequestHangsProvider(SampleIssues.Create()))
            .Add(x => x.RowKey, (Func<Issue, string>)(i => i.Title)));
        cut.WaitForState(() => cut.FindAll(".cg-column-menu-button").Count == 4);

        // Sorting starts a second load while the first-render load is still
        // pending, cancelling it — the provider then throws
        // OperationCanceledException, as HttpClient or Task.Delay would.
        cut.Find("[data-column-id='Priority'] .cg-column-menu-button").Click();
        cut.FindAll(".cg-column-menu-item").Single(item => item.TextContent.Contains("Sort ascending")).Click();
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        // The rest of first-render setup must still have run (it used to be
        // skipped when the first load was cancelled): swapping in a new
        // provider has to reload from it.
        var replacement = new RecordingDataProvider<Issue>(SampleIssues.Create().Take(1));
        cut.Render(p => p.Add(x => x.DataProvider, (IDataProvider<Issue>)replacement));

        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 1);
    }
}

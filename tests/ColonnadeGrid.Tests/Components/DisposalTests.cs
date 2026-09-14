using System.Collections.Concurrent;
using Bunit;
using ColonnadeGrid.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace ColonnadeGrid.Tests.Components;

/// <summary>Releasing the grid's JS objects on disposal, including when it races first-render setup or the circuit is gone.</summary>
public class DisposalTests : BunitContext
{
    /// <summary>
    /// A JS object that records calls, can hold a call's result until the test
    /// provides it, and can fail calls as a disconnected circuit would. bUnit's
    /// own JS interop can't do the first two for calls returning JS objects.
    /// </summary>
    private sealed class FakeJSObjectReference(bool disconnected = false) : IJSObjectReference
    {
        public ConcurrentQueue<string> Invocations { get; } = new();

        /// <summary>Results for calls by identifier; calls without one return the default.</summary>
        public ConcurrentDictionary<string, TaskCompletionSource<object?>> Results { get; } = new();

        public bool Disposed { get; private set; }

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Invocations.Enqueue(identifier);
            if (disconnected)
            {
                throw new JSDisconnectedException("gone");
            }

            return Results.TryGetValue(identifier, out var result) ? (TValue)(await result.Task)! : default!;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return disconnected ? throw new JSDisconnectedException("gone") : ValueTask.CompletedTask;
        }
    }

    /// <summary>Returns <paramref name="module"/> for the grid's module import.</summary>
    private sealed class FakeJSRuntime(IJSObjectReference module) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult((TValue)module);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }

    private static TaskCompletionSource<object?> Pending() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private IRenderedComponent<IssueGridHost> RenderWithModule(FakeJSObjectReference module)
    {
        Services.AddSingleton<IJSRuntime>(new FakeJSRuntime(module));
        return Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
    }

    [Fact]
    public async Task DisposedWhileJsSetupIsPending_DisposesTheHandleItGetsAfterwards()
    {
        var module = new FakeJSObjectReference();
        var initResize = Pending();
        module.Results["initResize"] = initResize;
        var cut = RenderWithModule(module);
        cut.WaitForAssertion(() => Assert.Contains("initResize", module.Invocations));

        await DisposeComponentsAsync();
        var resizeHandle = new FakeJSObjectReference();
        initResize.SetResult(resizeHandle);

        // Its document listeners are only removed by calling its dispose().
        Assert.True(SpinWait.SpinUntil(() => resizeHandle.Disposed, TimeSpan.FromSeconds(5)));
        Assert.Contains("dispose", resizeHandle.Invocations);
        Assert.DoesNotContain("initStickyHeaderShadow", module.Invocations);
        Assert.True(module.Disposed);
    }

    [Fact]
    public async Task Dispose_AfterTheCircuitIsGone_StillReleasesEveryHandle()
    {
        var module = new FakeJSObjectReference();
        var resizeHandle = new FakeJSObjectReference(disconnected: true);
        var stickyHandle = new FakeJSObjectReference(disconnected: true);
        module.Results["initResize"] = Pending();
        module.Results["initResize"].SetResult(resizeHandle);
        module.Results["initStickyHeaderShadow"] = Pending();
        module.Results["initStickyHeaderShadow"].SetResult(stickyHandle);
        var cut = RenderWithModule(module);
        cut.WaitForAssertion(() => Assert.Contains("initStickyHeaderShadow", module.Invocations));

        await DisposeComponentsAsync();

        // The first handle's failure mustn't stop the rest from being released.
        Assert.Contains("dispose", resizeHandle.Invocations);
        Assert.Contains("dispose", stickyHandle.Invocations);
        Assert.True(module.Disposed);
    }
}

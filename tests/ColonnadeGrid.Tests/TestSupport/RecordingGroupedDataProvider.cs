using ColonnadeGrid.Abstractions;
using ColonnadeGrid.Models;
using ColonnadeGrid.Providers;

namespace ColonnadeGrid.Tests.TestSupport;

/// <summary>
/// An <see cref="IGroupedDataProvider{TItem}"/> test double that records the
/// group requests it receives, can hold group page requests until released,
/// and can fail them — delegating the actual data to a real
/// <see cref="InMemoryDataProvider{TItem}"/>.
/// </summary>
public sealed class RecordingGroupedDataProvider<TItem>(IEnumerable<TItem> items) : IGroupedDataProvider<TItem>
{
    private readonly InMemoryDataProvider<TItem> _inner = new(items);

    public List<GroupListRequest> GroupListRequests { get; } = [];

    public List<GroupPagesRequest> GroupPagesRequests { get; } = [];

    /// <summary>While set and incomplete, group page requests wait for it.</summary>
    public TaskCompletionSource? PagesGate { get; set; }

    /// <summary>How many upcoming group page requests should throw.</summary>
    public int PagesFailuresRemaining { get; set; }

    /// <summary>How many upcoming group list requests should throw.</summary>
    public int GroupListFailuresRemaining { get; set; }

    public Task<DataResponse<TItem>> GetDataAsync(DataRequest request, CancellationToken cancellationToken = default) =>
        _inner.GetDataAsync(request, cancellationToken);

    public Task<GroupListResponse> GetGroupsAsync(GroupListRequest request, CancellationToken cancellationToken = default)
    {
        GroupListRequests.Add(request);

        if (GroupListFailuresRemaining > 0)
        {
            GroupListFailuresRemaining--;
            throw new InvalidOperationException("boom");
        }

        return _inner.GetGroupsAsync(request, cancellationToken);
    }

    public async Task<IReadOnlyList<GroupPage<TItem>>> GetGroupPagesAsync(GroupPagesRequest request, CancellationToken cancellationToken = default)
    {
        GroupPagesRequests.Add(request);

        if (PagesGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken);
        }

        if (PagesFailuresRemaining > 0)
        {
            PagesFailuresRemaining--;
            throw new InvalidOperationException("boom");
        }

        return await _inner.GetGroupPagesAsync(request, cancellationToken);
    }
}

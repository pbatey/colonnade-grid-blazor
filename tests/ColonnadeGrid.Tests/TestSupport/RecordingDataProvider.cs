using ColonnadeGrid.Abstractions;
using ColonnadeGrid.Models;
using ColonnadeGrid.Providers;

namespace ColonnadeGrid.Tests.TestSupport;

/// <summary>
/// An <see cref="IDataProvider{TItem}"/> test double that records every
/// <see cref="DataRequest"/> it receives (so tests can assert the table sends
/// the expected request on each state change — the server-paging contract,
/// not just the in-memory convenience path) while delegating the actual data
/// computation to a real <see cref="InMemoryDataProvider{TItem}"/>.
/// </summary>
public sealed class RecordingDataProvider<TItem>(IEnumerable<TItem> items) : IDataProvider<TItem>
{
    private readonly InMemoryDataProvider<TItem> _inner = new(items);

    public List<DataRequest> Requests { get; } = [];

    public Task<DataResponse<TItem>> GetDataAsync(DataRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return _inner.GetDataAsync(request, cancellationToken);
    }
}

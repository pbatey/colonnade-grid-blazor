using ColonnadeGrid.Abstractions;
using ColonnadeGrid.Models;
using ColonnadeGrid.Providers;

namespace ColonnadeGrid.Demo.Services;

/// <summary>
/// A worked example of implementing <see cref="IDataProvider{TItem}"/> for a
/// "remote" source, as opposed to the simpler <c>Items</c> convenience path.
/// Delegates the actual sort/filter/group/page computation to
/// <see cref="InMemoryDataProvider{TItem}"/> (standing in for what would
/// otherwise be a database query or HTTP call) but adds a simulated network
/// delay, so this page also demonstrates the table's loading state and
/// confirms the request/response contract works end-to-end asynchronously —
/// not just for the synchronous in-memory case.
/// </summary>
public sealed class SimulatedRemoteDataProvider<TItem>(IEnumerable<TItem> items, TimeSpan? delay = null)
    : IDataProvider<TItem>
{
    private readonly InMemoryDataProvider<TItem> _inner = new(items);
    private readonly TimeSpan _delay = delay ?? TimeSpan.FromMilliseconds(400);

    public async Task<DataResponse<TItem>> GetDataAsync(DataRequest request, CancellationToken cancellationToken = default)
    {
        await Task.Delay(_delay, cancellationToken);
        return await _inner.GetDataAsync(request, cancellationToken);
    }
}

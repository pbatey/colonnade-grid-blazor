using ColonnadeGrid.Models;

namespace ColonnadeGrid.Abstractions;

/// <summary>
/// Supplies data to a <c>ColonnadeGrid&lt;TItem&gt;</c>. Implement this to
/// back the table with a remote/server-side source (an HTTP API, EF Core
/// query, etc.) that can apply paging, sorting, filtering, and grouping
/// itself rather than loading everything into memory on the client. For the
/// common in-memory case, pass a list directly to the table's <c>Items</c>
/// parameter instead — it is wrapped in <see cref="Providers.InMemoryDataProvider{TItem}"/>
/// automatically.
/// </summary>
/// <typeparam name="TItem">The row item type.</typeparam>
public interface IDataProvider<TItem>
{
    /// <summary>
    /// Returns the page of data described by <paramref name="request"/>. See
    /// <see cref="DataRequest"/>/<see cref="DataResponse{TItem}"/> for the
    /// paging/sort/filter/group contract, including the v1 rule that
    /// <see cref="DataRequest.Skip"/>/<see cref="DataRequest.Take"/> are
    /// ignored when <see cref="DataRequest.GroupByPropertyName"/> is set.
    /// </summary>
    Task<DataResponse<TItem>> GetDataAsync(DataRequest request, CancellationToken cancellationToken = default);
}

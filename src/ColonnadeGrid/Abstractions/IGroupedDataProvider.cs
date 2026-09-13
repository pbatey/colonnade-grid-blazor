using ColonnadeGrid.Models;

namespace ColonnadeGrid.Abstractions;

/// <summary>
/// An <see cref="IDataProvider{TItem}"/> that can also list groups with their
/// row counts and load rows one group at a time. When a grid has
/// <c>EnablePaging</c> set and is grouped, a provider implementing this gets
/// per-group paging: group headers load first from their counts, each expanded
/// group loads and pages its own rows, and groups beyond the grid's row budget
/// start collapsed. Providers that don't implement it page over the grouped
/// rows instead (see <see cref="DataRequest"/>).
/// </summary>
/// <typeparam name="TItem">The row item type.</typeparam>
public interface IGroupedDataProvider<TItem> : IDataProvider<TItem>
{
    /// <summary>Returns a batch of groups with their row counts, plus the total number of groups and rows.</summary>
    Task<GroupListResponse> GetGroupsAsync(GroupListRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the requested page of rows for each group in
    /// <see cref="GroupPagesRequest.Pages"/>, in any order. The grid cancels
    /// <paramref name="cancellationToken"/> when the view changes underneath the request.
    /// </summary>
    Task<IReadOnlyList<GroupPage<TItem>>> GetGroupPagesAsync(GroupPagesRequest request, CancellationToken cancellationToken = default);
}

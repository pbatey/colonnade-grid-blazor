using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using ColonnadeGrid.Abstractions;
using ColonnadeGrid.LargeData.Shared;
using ColonnadeGrid.Models;

namespace ColonnadeGrid.LargeData.Client.Services;

/// <summary>The outcome of one API call, reported to the page for its timing and error display.</summary>
/// <param name="Error">A message describing the failure, or <c>null</c> on success.</param>
/// <param name="QueryMilliseconds">Time the API spent in Postgres, or <c>null</c> if the call failed.</param>
/// <param name="RoundTripMilliseconds">Request, server work, and response deserialization, as seen from the browser.</param>
public sealed record IssueLoadResult(string? Error, double? QueryMilliseconds, double RoundTripMilliseconds);

/// <summary>
/// Backs the grid with the sample API. Ungrouped (and flat grouped) pages come
/// from <c>POST /api/issues/query</c>, which receives the grid's
/// <see cref="DataRequest"/> as-is. Implementing <see cref="IGroupedDataProvider{TItem}"/>
/// gives a paged, grouped grid per-group paging, via <c>/api/issues/groups</c>
/// and <c>/api/issues/group-pages</c>.
/// <para>
/// Cancellation is left to propagate: the grid cancels a request when a newer
/// one replaces it (e.g. clicking through pages quickly) and discards it.
/// </para>
/// </summary>
public sealed class IssueApiProvider(HttpClient http, Action<IssueLoadResult> onLoaded)
    : IGroupedDataProvider<Issue>, IColumnStatsProvider<Issue>
{
    public async Task<DataResponse<Issue>> GetDataAsync(DataRequest request, CancellationToken cancellationToken = default)
    {
        var (page, error) = await PostAsync<DataRequest, IssuePage>(
            "api/issues/query", request, result => result.QueryMilliseconds, cancellationToken);

        // Throwing lets the grid show the failure in place of the rows, with a retry button.
        return page is null
            ? throw new InvalidOperationException(error)
            : new DataResponse<Issue> { Items = page.Items, TotalCount = page.TotalCount, Groups = page.Groups };
    }

    public async Task<GroupListResponse> GetGroupsAsync(GroupListRequest request, CancellationToken cancellationToken = default)
    {
        var (list, error) = await PostAsync<GroupListRequest, IssueGroupList>(
            "api/issues/groups", request, result => result.QueryMilliseconds, cancellationToken);

        return list is null
            ? throw new InvalidOperationException(error)
            : new GroupListResponse(list.Groups, list.TotalGroupCount, list.TotalCount);
    }

    public async Task<IReadOnlyList<GroupPage<Issue>>> GetGroupPagesAsync(GroupPagesRequest request, CancellationToken cancellationToken = default)
    {
        var (pages, error) = await PostAsync<GroupPagesRequest, IssueGroupPages>(
            "api/issues/group-pages", request, result => result.QueryMilliseconds, cancellationToken);

        // Throwing lets the grid show the failure inside the affected groups, with a retry button.
        return pages?.Pages ?? throw new InvalidOperationException(error);
    }

    public async Task<ColumnStats> GetColumnStatsAsync(ColumnStatsRequest request, CancellationToken cancellationToken = default)
    {
        var (stats, error) = await PostAsync<ColumnStatsRequest, IssueColumnStats>(
            "api/issues/column-stats", request, result => result.QueryMilliseconds, cancellationToken);

        // Throwing shows the failure in the filter editor, which still works without stats.
        return stats?.Stats ?? throw new InvalidOperationException(error);
    }

    private async Task<(TResult? Result, string? Error)> PostAsync<TRequest, TResult>(
        string url,
        TRequest request,
        Func<TResult, double> queryMilliseconds,
        CancellationToken cancellationToken)
        where TResult : class
    {
        var stopwatch = Stopwatch.StartNew();
        string error;
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(request, options: LargeDataJson.Options)
            };

            // The API's "now" for relative date filters, so they use the
            // browser's clock, as the in-memory provider does.
            message.Headers.Add("X-Client-Now", DateTime.Now.ToString("s", CultureInfo.InvariantCulture));

            using var response = await http.SendAsync(message, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<TResult>(LargeDataJson.Options, cancellationToken)
                    ?? throw new InvalidOperationException("The API returned an empty body.");
                onLoaded(new IssueLoadResult(null, queryMilliseconds(result), stopwatch.Elapsed.TotalMilliseconds));
                return (result, null);
            }

            error = await ReadProblemDetailAsync(response, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            error = $"Couldn't reach the API: {ex.Message}";
        }

        onLoaded(new IssueLoadResult(error, null, stopwatch.Elapsed.TotalMilliseconds));
        return (null, error);
    }

    private static async Task<string> ReadProblemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var fallback = $"The API returned {(int)response.StatusCode} {response.ReasonPhrase}.";
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            return problem.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } text
                ? text
                : fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }
}

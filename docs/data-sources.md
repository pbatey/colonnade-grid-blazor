# Data sources: `Items` vs. `IDataProvider<TItem>`

`ColonnadeGrid<TItem>` takes **exactly one** of two mutually-exclusive
parameters — passing both, or neither, throws `InvalidOperationException` at
render time with a message identifying the problem:

- **`Items` (`IEnumerable<TItem>`)** — the convenience path. The table wraps
  it in `ColonnadeGrid.Providers.InMemoryDataProvider<TItem>` automatically
  and re-wraps it whenever the `Items` *reference* changes (assign a new list
  instance to refresh the table's data).
- **`DataProvider` (`IDataProvider<TItem>`)** — implement this yourself to
  back the table with a remote source (HTTP API, EF Core query, etc.) that
  can apply paging/sort/filter/group server-side instead of loading
  everything into WebAssembly memory:

  ```csharp
  public interface IDataProvider<TItem>
  {
      Task<DataResponse<TItem>> GetDataAsync(DataRequest request, CancellationToken cancellationToken = default);
  }
  ```

  `DataRequest` carries `Skip`, `Take`, `Sort` (a single `SortDescriptor?`),
  `Filters` (a list of `FilterDescriptor`), and `GroupByPropertyName`.
  `DataResponse<TItem>` returns `Items` (always a flat list, even when
  grouped — see below), `TotalCount`, and an optional `Groups` list. When
  `DataProvider` is set, **`RowKey` is required** (see
  [Row selection](row-selection.md)).

  A minimal real-world implementation typically looks like:

  ```csharp
  public class RemoteIssueProvider : IDataProvider<Issue>
  {
      private readonly HttpClient _http;
      public RemoteIssueProvider(HttpClient http) => _http = http;

      public async Task<DataResponse<Issue>> GetDataAsync(DataRequest request, CancellationToken ct = default)
      {
          // Translate `request` into query-string parameters, call your API,
          // and map the API's response into a DataResponse<Issue>.
          var response = await _http.GetFromJsonAsync<ApiIssuePage>(BuildUrl(request), ct);
          return new DataResponse<Issue>
          {
              Items = response!.Items,
              TotalCount = response.TotalCount,
              Groups = response.Groups?.Select(g => new DataGroup(g.Key, g.DisplayText, g.Count, g.StartIndex)).ToList()
          };
      }
  }
  ```

  See [`SimulatedRemoteDataProvider`](../samples/ColonnadeGrid.Demo/Services/SimulatedRemoteDataProvider.cs)
  in the sample app for a runnable example (it wraps `InMemoryDataProvider`
  with an artificial delay to demonstrate the table's loading state and the
  provider contract working end-to-end asynchronously), and the
  [large-data sample](../samples/ColonnadeGrid.LargeData) for one backed by a
  real database. [Working with large data sets](large-data.md) covers
  implementing a data source for a big table.

**Custom filter operators or value types** aren't directly extensible in v1's
fixed `FilterOperator` enum; if you need something bespoke, implement your own
`IDataProvider<TItem>` and interpret `FilterDescriptor` however you like
(nothing requires you to use `InMemoryDataProvider`'s interpretation of it).

The value, range, and date filter editors send `In`, `Between`, and
`WithinLast` filters; see [the filter contract](filtering.md#the-filter-contract)
for what each means. To give those editors choices and limits from your data,
implement [`IColumnStatsProvider<TItem>`](filtering.md#implementing-icolumnstatsprovider)
as well.

## When a load fails

If a data source throws — anything other than an `OperationCanceledException`
for a request the grid cancelled itself — the grid shows the exception's
message with a **Retry** button instead of letting the exception escape the
component, which on Blazor Server would end the circuit. Where it appears
depends on what failed:

- **`GetDataAsync`, or `IGroupedDataProvider.GetGroupsAsync` listing the
  first groups** — in place of the rows.
- **`GetGroupPagesAsync`** — inside each affected group.
- **`GetGroupsAsync` for "Show more"** — in the footer; the groups already
  listed stay, and the "Show more" button retries.

The message is shown to the user as-is, so throw exceptions whose `Message`
is fit for them rather than, say, raw SQL errors.

## The grouped-response contract

`DataResponse<TItem>.Items` is **always a flat list** — even when grouped.
Grouping is expressed as boundary metadata over that flat list via
`Groups: IReadOnlyList<DataGroup>?`:

```csharp
public sealed record DataGroup(string Key, string DisplayText, int Count, int StartIndex, int? TotalCount = null);
```

`StartIndex`/`Count` describe a contiguous range within `Items` (all of one
group's items must appear together, in order). This means collapsing or
expanding a group is a pure client-side operation — it never needs to
re-fetch data. `TotalCount` is the group's size across all pages; the group
header shows it when set, and `Count` otherwise.

When [paging](paging.md) is on, a grouped request is paged too: take the
`Skip`/`Take` window from the rows in group order, and return only the groups
that have items in that window, each clipped to its part of the page (so
`Count` is the rows on this page, and `TotalCount` the rows in all). Order the
rows so each group stays contiguous across pages — e.g. by the group key first
in SQL. `ColonnadeGrid.Providers.InMemoryDataProvider<TItem>` is a complete
reference implementation of this contract if you want to see how it's
constructed from an in-memory sequence.

For large grouped data, implement `IGroupedDataProvider<TItem>` as well, so a
paged grid can list groups from their counts and page each one separately; see
[Paging each group separately](paging.md#paging-each-group-separately).

# Paging

Set `EnablePaging="true"` to load and show one page at a time, with a pager
below the rows:

```razor
<ColonnadeGrid TItem="Issue" DataProvider="@provider" RowKey="@(i => i.Id.ToString())"
               EnablePaging="true" @bind-PageSize="_pageSize" @bind-PageIndex="_pageIndex">
    ...
</ColonnadeGrid>
```

| Parameter | Type | Default | Purpose |
|---|---|---|---|
| `EnablePaging` | `bool` | `false` | Turns paging and the pager on. |
| `PageSize` | `int` | `50` | Rows per page; must be at least 1. Supports `@bind-PageSize`. |
| `PageIndex` | `int` | `0` | The zero-based current page. Supports `@bind-PageIndex`. |
| `PageSizeOptions` | `IReadOnlyList<int>?` | `25, 50, 100` | Choices in the pager's "Rows per page" selector. `null` or empty hides the selector. |

- **Requests.** Without `EnablePaging`, the grid asks for every row
  (`Skip = 0`, `Take = int.MaxValue`). With it, each request asks for the
  current page (`Skip = PageIndex × PageSize`, `Take = PageSize`), and the
  response's `TotalCount` (rows matching the filters, across all pages) sets
  the number of pages. This works the same for `Items` and for an
  `IDataProvider<TItem>`, which does the paging itself.
- **Moving between pages.** A new sort, filter, or group-by returns to the first
  page. A new page size keeps the first row that was showing on screen. If the
  current page stops existing — the data shrank, or a host passed a
  `PageIndex` past the end — the grid moves to the last page. Each of these
  raises `PageIndexChanged`/`PageSizeChanged`.
- **Binding is optional.** Unbound, the grid keeps its own page state; a host
  re-rendering with the same initial `PageIndex` doesn't send the user back to
  that page. A host passing a *different* value moves the grid to it, so
  `@bind-PageIndex` can drive the page from outside (e.g. a URL query string).
- **Grouping.** With an `IGroupedDataProvider<TItem>` — including the built-in
  provider behind `Items` — each group is paged separately; see [Paging each
  group separately](#paging-each-group-separately). Other providers page over
  the grouped rows, so a large group can span several pages (see [the
  grouped-response contract](data-sources.md#the-grouped-response-contract));
  the group header count is the group's total across all pages, and collapsing
  a group hides its rows on the current page.
- **Selection.** Selected keys persist across pages; the select-all checkbox
  only covers the current page.
- **The pager** shows the row range ("51–100 of 1,234"), the page-size
  selector, and Previous/Next with up to seven page buttons (first, last, and
  the current page's neighbors, with "…" gaps). It sticks to the bottom of the
  grid's scroll container, so it stays reachable while scrolling a long page,
  and is themed by the same custom properties as the rest of the grid — the
  current page uses `--cg-accent-color`.

For choosing page sizes and implementing paging efficiently over a big table,
see [Working with large data sets](large-data.md).

## Paging each group separately

When `EnablePaging` is set, the view is grouped, and the data source
implements `IGroupedDataProvider<TItem>` (the built-in provider behind `Items`
does), groups are loaded and paged one at a time instead of as one long list:

1. The grid first asks for a batch of groups with their row counts, and shows
   their headers.
2. Groups start expanded, in order, while their first pages fit in the row
   budget; the rest start collapsed. The expanded groups' first pages load in a
   single call, with placeholder rows showing meanwhile.
3. An expanded group with more than one page gets a small pager below its rows
   ("‹ 11–20 of 34,873 ›").
4. A footer replaces the page pager, showing how many groups and rows there are
   ("50 of 201 groups · 100,000 rows") and a button for the next batch of
   groups ("Show 50 more groups (151 remaining)").

| Parameter | Type | Default | Purpose |
|---|---|---|---|
| `GroupPageSize` | `int` | `10` | Rows per page inside each group. |
| `GroupRowBudget` | `int?` | `PageSize` | How many rows groups may load when they first appear (see below). |
| `GroupsPerLoad` | `int` | `50` | How many groups each batch lists. |

- **The row budget** bounds how much the browser holds when a view opens.
  Going through the groups in order, a group starts expanded if its first page
  (`min(Count, GroupPageSize)` rows) still fits in the budget; the first group
  that doesn't fit, and every group after it, starts collapsed — expansion never
  skips a big group to open a later, smaller one. The first group always starts
  expanded. Paging within a group replaces its rows rather than adding to them,
  so each group holds at most one page.
- **Expanding and collapsing.** Expanding a group loads its current page;
  collapsing it drops the rows (they load again if it's re-expanded). The choice
  is recorded in `GridState.ExpandedGroupKeys`/`CollapsedGroupKeys` and overrides
  the budget for that group only — it never changes how other groups start.
- **Placeholders.** A loading group shows as many skeleton rows as its page will
  have, so nothing shifts when the rows arrive. They fade in after 150 ms, so a
  fast load doesn't flash them, and don't shimmer for users who prefer reduced
  motion.
- **Failures.** If a group's rows fail to load, the error shows inside that
  group with a Retry button, and the rest of the grid keeps working. A failure
  listing the groups themselves propagates like any other load.
- **Changing the view.** A new sort, filter, or group-by reloads the group list
  and returns every group to its first page. Each page load refreshes the
  group's header count, and a group whose current page no longer exists moves
  to its last page.
- **Selection.** Select-all covers the rows loaded in expanded groups.

To support this in your own data source, implement `IGroupedDataProvider<TItem>`:

```csharp
public interface IGroupedDataProvider<TItem> : IDataProvider<TItem>
{
    Task<GroupListResponse> GetGroupsAsync(GroupListRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GroupPage<TItem>>> GetGroupPagesAsync(GroupPagesRequest request, CancellationToken cancellationToken = default);
}
```

- **`GetGroupsAsync`** returns the `Skip`/`Take` batch of groups
  (`GroupSummary(Key, DisplayText, Count)`), plus `TotalGroupCount` and
  `TotalCount` across all groups — the footer uses the first to say how many
  groups remain. Keep group order stable across calls, and when sorting by the
  grouped column, order the groups by it.
- **`GetGroupPagesAsync`** returns a page for each requested
  `GroupPageRequest(GroupKey, Skip, Take)`, with the group's current `Count` (0,
  with no items, for a key that matches nothing). A single call can ask for many
  groups, so answer it in one query where you can — the [large-data
  sample](../samples/ColonnadeGrid.LargeData) uses one SQL `UNION ALL` with a
  branch per group.
- **Keys are opaque to the grid**: return whatever identifies a group, and accept
  it back. The built-in provider uses `GroupKeys.From`, which gives `null` its own
  key (`GroupKeys.Null`) so it can't collide with an empty string's.

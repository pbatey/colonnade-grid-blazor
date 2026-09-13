# Working with large data sets

ColonnadeGrid renders every row it's given — there's no row virtualization —
so the job with a large data set is to keep the browser holding only what's on
screen, and let the data source do the sorting, filtering, and grouping. This
guide covers choosing an approach, configuring the grid, implementing the data
source, and the pitfalls found building the
[large-data sample](../samples/ColonnadeGrid.LargeData): 100,000 rows in
Postgres behind an ASP.NET Core API, which is a working reference for
everything below.

## Choose an approach

| Your data | Use |
|---|---|
| Small enough to send to the browser and render all at once | `Items` |
| Small enough to send to the browser, too many rows to render at once | `Items` with `EnablePaging` |
| Too big to send to the browser | Your own `IDataProvider<TItem>` with `EnablePaging` |
| Too big to send to the browser, and users group it | An `IGroupedDataProvider<TItem>` with `EnablePaging` |

There's no row count where one approach becomes the other; it depends on your
row size, columns, cell templates, and users' devices. Two things to weigh:

- **`Items` downloads everything.** The built-in `InMemoryDataProvider` then
  filters, sorts, and groups the whole list in WebAssembly on every change.
  It supports paging (including paging each group separately), so rendering
  stays bounded, but download size and in-browser work don't.
- **Rendering cost is per row, per column.** Each row is a grid `div` with a
  cell per visible column, plus whatever your `CellTemplate`s render. Measure
  page sizes on the slowest device you support; the large-data sample offers
  page sizes up to 10,000 so you can see where rendering starts to lag.

## Configure the grid

```razor
<div class="table-container">
    <ColonnadeGrid TItem="Issue"
                   DataProvider="_provider"
                   RowKey="@(i => i.Id.ToString())"
                   EnablePaging="true"
                   @bind-PageSize="_pageSize"
                   PageSizeOptions="[50, 100, 500]"
                   GroupPageSize="10"
                   GroupsPerLoad="50">
        <Columns>...</Columns>
    </ColonnadeGrid>
</div>
```

- **`PageSize` bounds what an ungrouped view holds.** It also sets the default
  `GroupRowBudget`, so a grouped view holds about as many rows as an ungrouped
  page.
- **`GroupPageSize` decides how many groups open.** Groups start expanded while
  their first pages fit in the budget, so smaller group pages mean more groups
  open at once: with a 100-row budget and 10 rows per group, the first 10
  groups start expanded. See [Paging each group
  separately](paging.md#paging-each-group-separately) for the exact rule.
- **`GroupsPerLoad` bounds group headers.** Grouping by a high-cardinality
  column (say, a timestamp) can produce thousands of groups; the grid lists them
  in batches with a "Show more groups" button.
- **`RowKey` is required** with a data provider. Selection is kept across pages,
  but select-all only covers loaded rows.
- **Give the grid a bounded-height, scrolling container** (for example
  `height: calc(100vh - 180px); overflow: auto;`) so the sticky header and the
  pager stay visible while scrolling a long page. See
  [Known limitations](known-limitations.md) for why an unbounded container with
  horizontal scrolling can't keep the header stuck.

## Implement the data source

### Pages: `IDataProvider<TItem>`

`GetDataAsync` receives a `DataRequest` with the sort, filters, group-by, and —
with `EnablePaging` — the page's `Skip`/`Take`:

1. Apply the filters, then count the matches for `TotalCount`.
2. Sort, **always ending with a unique column** (such as the primary key).
   Without a tiebreaker, rows with equal sort values can swap between queries
   and appear on two pages, or none.
3. Apply `Skip`/`Take`.
4. Pass the `CancellationToken` to your HTTP or database call. The grid cancels
   a request when a newer one replaces it; throwing `OperationCanceledException`
   is fine.

If you also want grouping without implementing `IGroupedDataProvider`, follow
[the grouped-response contract](data-sources.md#the-grouped-response-contract).

### Groups: `IGroupedDataProvider<TItem>`

With this interface, a grouped, paged grid lists groups from their counts and
pages each group separately (see [Paging](paging.md#paging-each-group-separately)
for the interface). Implementation notes:

- **`GetGroupsAsync` in one query.** Return a batch of groups with their counts,
  plus the total number of groups and rows, which the footer uses for "151
  remaining". Window functions computed over the grouped rows give all of that
  in a single query (see below).
- **`GetGroupPagesAsync` for many groups at once.** When a grouped view opens,
  the grid asks for the first page of every group that starts expanded in one
  call. Answer it with one query, not one per group.
- **Keys must round-trip exactly.** The grid treats group keys as opaque and sends
  back whatever you returned. Accept only that exact form, and match counts by
  it too: the sample's API returns a group's text as Postgres prints it, and a
  key like `todo` for `Todo` matches nothing, so a group's rows and its count
  can't disagree. Give `null` a key of its own (`GroupKeys.Null`) so it can't
  collide with an empty string.
- **Return each group's current count** with its page. The grid refreshes the
  group header from it, and moves to the group's last page if the requested one
  no longer exists.

### Filter limits: `IColumnStatsProvider<TItem>`

The filter editors offer choices and limits from the data: value checklists with
counts, sliders spanning a column's minimum and maximum, and only the date
presets that would change the result. They get these from
`IColumnStatsProvider<TItem>`, which the grid calls each time an editor opens,
with the other columns' filters. Answer with an aggregate query over the filtered
rows, plus a `GROUP BY` when value counts are requested; see [Implementing
`IColumnStatsProvider`](filtering.md#implementing-icolumnstatsprovider).

## SQL patterns

These come from the sample's `IssueQueryBuilder` (Postgres). Every identifier
comes from an allow-list of queryable columns, and every value is a bound
parameter.

**A page, with its total count** — two statements sent as one `NpgsqlBatch`:

```sql
SELECT count(*) FROM issues WHERE priority > $1::issue_priority;

SELECT id, title, status::text, ...
FROM issues
WHERE priority > $1::issue_priority
ORDER BY issues.created_at ASC NULLS FIRST, issues.id ASC   -- unique tiebreaker
LIMIT $2 OFFSET $3;
```

**A batch of groups, with totals over all groups** — window functions run
before `LIMIT`, so every returned row carries the totals:

```sql
SELECT issues.assignee::text,
       count(*),                          -- rows in this group
       count(*) OVER (),                  -- number of groups
       (sum(count(*)) OVER ())::bigint    -- rows in all groups
FROM issues
GROUP BY issues.assignee
ORDER BY issues.assignee ASC NULLS FIRST
LIMIT $1 OFFSET $2;
```

A batch past the last group returns no rows, and so no totals; the sample runs
a separate count query for that case.

**A page from each of several groups** — one `UNION ALL` branch per group,
each an index range scan on the group column, wrapped in a subquery and put
back in order with `row_number()`:

```sql
SELECT * FROM (
    (SELECT 0 AS page_index, row_number() OVER (ORDER BY issues.created_at, issues.id) AS row_index, id, title, ...
     FROM issues WHERE issues.assignee IS NULL
     ORDER BY issues.created_at, issues.id LIMIT $1 OFFSET $2)
  UNION ALL
    (SELECT 1 AS page_index, row_number() OVER (ORDER BY issues.created_at, issues.id) AS row_index, id, title, ...
     FROM issues WHERE issues.assignee = $3
     ORDER BY issues.created_at, issues.id LIMIT $4 OFFSET $5)
) AS pages
ORDER BY page_index, row_index;

-- The requested groups' current counts, in one pass:
SELECT issues.assignee::text, count(*)
FROM issues
WHERE (issues.assignee IS NULL OR issues.assignee = $1)
GROUP BY issues.assignee;
```

**The new filter operators** map directly: `In` to `col IN (...)`, `Between` to
`col >= $from AND col <= $to` (omitting an open end), and `WithinLast` to
`col >= $start AND col <= $now`, with the start computed by
`RelativeDatePeriod.TryGetStart`. Wrap each in `(col IS NULL OR ...)` when
`IncludeEmpty` is set.

**Column stats for a filter editor** — the range and empty count, and the
distinct values with their counts:

```sql
SELECT min(issues.cycle_time), max(issues.cycle_time),
       count(*) FILTER (WHERE issues.cycle_time IS NULL), count(*)
FROM issues WHERE status IN ($1::issue_status);

SELECT issues.project, count(*)
FROM issues WHERE status IN ($1::issue_status) AND NOT (issues.project IS NULL OR issues.project = '')
GROUP BY issues.project ORDER BY issues.project ASC LIMIT $2;
```

**Indexes** on each sortable and groupable column, with the primary key second
(`(status, id)`), serve both the sort-with-tiebreaker and the per-group range
scans. A trigram index (`gin (title gin_trgm_ops)`) serves `ILIKE '%...%'`.

## Pitfalls

Each of these caused a real bug or slowdown in the sample:

- **`ORDER BY status` can sort by the wrong thing.** A bare name in `ORDER BY`
  binds to an *output* column first. With `status::text` in the select list —
  output as `status` — enum columns sorted alphabetically instead of in
  declaration order. Qualify sort columns (`issues.status`).
- **Collation changes text order.** The Postgres Docker image sorts text
  bytewise, so "SSO" came before "admins", unlike .NET's culture-aware ordering.
  The sample initializes the database with ICU collation
  (`POSTGRES_INITDB_ARGS: --locale-provider=icu --icu-locale=und`).
- **Enum order must match.** Postgres enums sort in declaration order; declare
  them in the same order as the C# enum's values.
- **Format stats from .NET types, not the database's text.** Postgres prints an
  `interval` as `"1 day 02:00:00"`, which the grid can't read as a `TimeSpan`, and
  Npgsql reads a `date` column as `DateOnly` even if your model uses `DateTime`.
  Read typed values, convert them to your model's types, and format them with
  `FilterValues.Format`.
- **A single-branch `UNION` rejects a trailing `ORDER BY`.** Postgres attaches it
  to that branch's own `SELECT`, which already has one ("multiple ORDER BY
  clauses not allowed"). Wrap the union in a subquery. Test requests for a
  single group — the grid sends them whenever you page within one group.
- **One count per group is slow.** Counting 100 groups with a
  `count(*) FILTER (WHERE ...)` per group took about 98 ms, since every filter
  runs on every row; one `GROUP BY` took about 13 ms.
- **JIT compilation can dwarf the query.** The 100-group page query runs in about
  30 ms but has a high estimated cost, which crossed Postgres's JIT threshold;
  with JIT on, requests took 300–800 ms. The sample turns JIT off
  (`postgres -c jit=off`).
- **Null handling decides which rows match.** To behave like the built-in
  provider, nulls sort first when ascending, and `NotEquals` and `LessThan`
  match null values. Decide your rules deliberately, and apply them in both
  filters and sorts.
- **Deep offsets get slower.** `OFFSET` still reads and discards the skipped
  rows: a page 30,000 rows into a group took about 17 ms on 100,000 rows, and
  that grows with the table. Going further would need keyset ("seek") paging,
  which the grid's `Skip`/`Take` contract doesn't support.

The sample caught several of these by running the same requests through its API
and through `InMemoryDataProvider` over the same rows, and comparing the results.
That's a cheap way to check your own data source's filter, sort, and group
semantics.

## What to expect

Measured with the sample: 100,000 rows, Postgres 17 in Docker on a development
laptop, JIT off. Times are spent in Postgres, as reported by the API.

| Request | Time |
|---|---|
| A page of 100 rows, filtered and sorted | 6–12 ms |
| Row counts for every group of a column (full scan) | about 15 ms |
| 50 of 201 groups, with totals | about 9 ms |
| The first page of 100 groups (1,000 rows) | about 40 ms |
| A page 30,000 rows into one group | about 17 ms |
| A column's stats for a filter editor (range and empty count, or distinct values) | 6–20 ms |

## Limits

- **No row virtualization or infinite scroll** — pages render all at once.
- **No cross-page select-all.**
- **Counts and rows come from separate queries**, so a change between them can
  briefly show a header count that doesn't match the rows; the next load
  corrects it.
- **Per-group paging needs `EnablePaging`**; a grouped view without paging loads
  every row.

See [Known limitations](known-limitations.md) for the rest.

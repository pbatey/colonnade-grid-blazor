# ColonnadeGrid large-data sample

Runs ColonnadeGrid against 100,000 rows in Postgres. Sorting, filtering,
grouping, and paging all run in the database; the browser only holds what's on
screen. For the general approach this sample demonstrates, see
[Working with large data sets](../../docs/large-data.md).

```
Blazor WebAssembly client                                ASP.NET Core API               Postgres
  ColonnadeGrid (EnablePaging)   ── POST /api/issues/query ──────▶  IssueQueryBuilder  ── SQL ──▶
  IssueApiProvider               ── POST /api/issues/groups ─────▶  IssueRepository
    (IGroupedDataProvider,       ── POST /api/issues/group-pages ▶
     IColumnStatsProvider)       ── POST /api/issues/column-stats ▶
```

## Running it

Prerequisites: Docker, and the .NET SDK from the repo's `global.json`.

```bash
cd samples/ColonnadeGrid.LargeData

# Start Postgres and seed 100,000 issues. Seeding is skipped if the table
# already has rows.
docker compose up -d

# Start the API. It also serves the client: http://localhost:5080
dotnet run --project ColonnadeGrid.LargeData.Api
```

Reseeding and cleanup:

```bash
SEED_FORCE=1 docker compose run --rm seed                    # drop and reseed
SEED_ROWS=1000000 SEED_FORCE=1 docker compose run --rm seed  # a bigger table
docker compose down -v                                        # stop and delete the data
```

The seed data has estimate, time spent, and cycle time columns for the filter
editors. If your database was seeded before those were added, reseed with
`SEED_FORCE=1`.

Postgres listens on `localhost:5432`. If that port is taken, set
`POSTGRES_PORT=5433` for `docker compose up` and change the connection string
in `ColonnadeGrid.LargeData.Api/appsettings.json` to match.

To run the seed script from your own machine instead of the container:
`pip install -r db/requirements.txt && python db/seed.py --help`.

## Projects

| Path | What's in it |
|---|---|
| `docker-compose.yml` | Postgres 17 (with JIT off — see below), plus a one-shot `seed` service |
| `db/seed.py`, `db/schema.sql`, `db/indexes.sql` | Schema and seed data. Data comes from a fixed random seed, so a given `--rows`/`--seed` always produces the same table. Loaded with `COPY`, then indexed. |
| `ColonnadeGrid.LargeData.Shared` | `Issue`, the API's response records, and the JSON settings both sides use |
| `ColonnadeGrid.LargeData.Api` | The service layer: `IssueQueryBuilder` turns the grid's requests into SQL, `IssueRepository` runs it, and `Program.cs` exposes it and hosts the client |
| `ColonnadeGrid.LargeData.Client` | `IssueApiProvider` (an `IGroupedDataProvider<Issue>`) and the page |

## How requests become SQL

The grid has `EnablePaging` set. Ungrouped, each page is one
`POST /api/issues/query` carrying the grid's `DataRequest` unchanged. Grouped,
the provider's `IGroupedDataProvider` support makes the grid page each group
separately:

- **`POST /api/issues/groups`** lists a batch of groups (50 at a time) with
  their row counts: one `GROUP BY`, ordered by key. Window functions over the
  grouped rows (`count(*) OVER ()`, `sum(count(*)) OVER ()`) carry the total
  number of groups and rows on every row, so the same query tells the grid how
  many more groups there are. About 9 ms for the 201 Assignee groups.
- **`POST /api/issues/group-pages`** returns a page of rows from each of up to
  100 groups in one round trip: a single `UNION ALL` query with a branch per
  group (each an index range scan on the group column), plus one `GROUP BY` for
  the groups' current counts. The first page of 100 Assignee groups, sorted by
  creation date, takes about 40 ms; a page 30,000 rows into the Todo group about
  17 ms.
- **Group keys** are the column's text as Postgres prints it, or `GroupKeys.Null`
  for the null group. Any other spelling of a value (`todo`, `05`) matches
  nothing, so a group's rows and its count always agree.
- **`POST /api/issues/column-stats`** gives a filter editor its limits: one
  aggregate for the column's min, max, and empty count, plus a `GROUP BY` for
  distinct values with counts when the editor is a checklist — both over the
  rows matching the other columns' filters. About 6–20 ms.

The rest applies to every endpoint:

- **Only allow-listed columns reach the SQL.** Property names are looked up in
  `IssueColumns`, and unknown names return HTTP 400. Every value is a bound
  parameter.
- **Filters follow `InMemoryDataProvider`'s rules**, so the grid acts the same
  with either data source. Filter values are converted to the column's type;
  if a value doesn't convert, it's compared as case-insensitive text. `Contains`
  and `StartsWith` become `ILIKE`, with `%` and `_` escaped. Nulls count as
  smaller than any value, so `NotEquals` and `LessThan` match null rows, and
  ascending sorts list nulls first.
- **The filter editors' operators** become plain comparisons: `In` is
  `col IN (...)`, `Between` is `col >= from AND col <= to` (skipping an open
  end), and `WithinLast` is a timestamp range ending at the API's current time.
  `IncludeEmpty` adds `col IS NULL OR ...`. Durations are `interval` columns,
  sent and read as `TimeSpan`.
- **Paging is stable.** `id` is always the final sort key, so rows with equal
  values don't move between pages. `Take` is capped at 10,000.
- **JIT is off** (`docker-compose.yml`). The multi-group query is fast but has a
  high estimated cost, which crossed Postgres's JIT threshold: with JIT on, the
  100-group request spent about 270 ms compiling a query that runs in about
  30 ms.

## Differences from the in-memory provider

- **Group order.** The in-memory provider lists groups in the order they first
  appear. This API lists them by key.
- **Text form of non-text values.** `Contains` and `StartsWith` on dates and
  numbers match against Postgres's text form (for example `2025-07-24`), not
  .NET's `ToString()`.

A scratch check ran the same requests through this API and
`InMemoryDataProvider` over the same 100,000 rows, and all of them matched:

- 42 sort and filter requests, with identical row order.
- 1,360 checks of the group endpoints: group keys, counts, and totals, and rows
  and counts for first pages of up to 100 groups at once, deep pages, the null
  group, unknown keys, and single-group requests, across five grouped columns,
  four sorts, and two filters.
- 18 requests using `In`, `Between`, and `WithinLast`, with and without
  `IncludeEmpty`, on enum, text, integer, decimal, duration, timestamp, and date
  columns.
- 32 column-stats checks: min, max, empty and total counts, and distinct values
  with counts, for eleven columns, with and without other columns' filters.

## Things to try

- **Filter each kind of column.** Status and Priority get a value checklist with
  counts; Project and Assignee are text columns set to list their values;
  Estimate gets a number range; Time spent (under a day) and Cycle time (up to
  nine months) get duration ranges in different units; Created and Due get date
  presets and a custom range.
- **Watch limits follow other filters.** Filter Status to Done, then open
  Estimate or Assignee: the slider range and value counts cover only Done issues.

- **Group by Assignee.** 201 groups: the first 10 start expanded (10 rows each
  fill the 100-row page size), the footer offers the next 50 groups, and each
  expanded group has its own pager.
- **Big pages.** Ungrouped, "Rows per page" goes up to 10,000, so you can see how
  the non-virtualized grid copes with rendering that many rows in the browser.

# Filtering

Set `Filterable="true"` on a column and its "..." menu gets a **Filter by
values…** item, which opens a filter editor chosen from the column's type.
Filters on different columns combine with AND.

## Filter editors

| Property type | Editor | Filter it applies |
|---|---|---|
| enums, `bool` | A checklist of values | `In` |
| numbers (`int`, `long`, `double`, `decimal`, ...) | A range, with a slider | `Between` |
| `TimeSpan` | A duration range, with a slider | `Between` |
| `DateTime`, `DateTimeOffset`, `DateOnly` | Relative presets, or a custom date range | `WithinLast` or `Between` |
| anything else | An operator and a text value | `Contains`, `Equals`, ... |

Nullable versions of these types get the same editor. To choose a different
editor, set `FilterKind` on the column — most usefully `FilterKind.Values` on a
text column with a handful of distinct values, so users pick from the values in
the data instead of typing:

```razor
<GridColumn Field="(Issue x) => x.Project" Title="Project" Filterable="true" FilterKind="FilterKind.Values" />
```

`FilterKind.Text` gives any column the operator-and-text editor.

### Value checklist

- Lists an enum's or boolean's own values. For other types (a text column set to
  `FilterKind.Values`), it lists the distinct values in the data, which requires
  a data source that can provide [column stats](#how-limits-follow-the-data);
  without one, the column falls back to the text editor.
- Shows how many rows have each value. A value no row has, given the other
  columns' filters, is dimmed but can still be selected.
- Adds an **(empty)** option for nullable columns that have empty values.
- Adds a search box when there are more than 8 values. **Select all** and
  **Select none** apply to the values the search shows.
- Selecting everything means no filter. At most 200 values are listed; past that,
  the editor says it's showing the first 200.

### Number and duration ranges

- A two-handle slider spans the data's minimum and maximum, and **From**/**To**
  inputs take exact values. Either end can be left open.
- A bound at or beyond the data's minimum or maximum is left open, so rows added
  later with values past today's limits still match.
- Integer columns step by 1.
- Durations are entered in months, weeks, and days when the data reaches a day or
  more (a month counts as 30 days), and in hours and minutes otherwise. The
  slider's labels use the same units, like "30m" to "1mo 2w 1d".
- Nullable columns get an **Include empty** checkbox.

### Dates

- Relative presets: last 7 days, 30 days, 90 days, 6 months, year, 2 years, and
  5 years. A preset is saved as relative (`WithinLast` with a period like
  `"P30D"`) and evaluated when the data source runs the query, so a saved view
  keeps showing the most recent 30 days.
- Presets that wouldn't change the result are hidden: those reaching back before
  the oldest date (they'd include everything) and those starting after the
  newest (they'd include nothing).
- **Custom range** takes a from and to date, and includes both whole days.
- Nullable columns get an **Include empty** checkbox.

## How limits follow the data

When an editor opens, the grid asks the data source for the column's stats:
its minimum and maximum, how many rows are empty, and, for checklists, each
distinct value with its count. The request carries every *other* column's
filter but not the column's own, so the limits narrow as you filter other
columns without collapsing to the column's current selection.

The built-in provider behind `Items` supplies stats. For your own data source,
implement [`IColumnStatsProvider<TItem>`](#implementing-icolumnstatsprovider).
Without it, the editors still work, just without limits: no slider, no counts,
every date preset, and text columns set to `FilterKind.Values` use the text
editor. If loading stats fails, the editor says so and works without them.

## The filter contract

Editors produce `FilterDescriptor`s, which reach your data source in
`DataRequest.Filters`:

```csharp
public sealed record FilterDescriptor(
    string PropertyName,
    FilterOperator Operator,
    string? Value,
    string? ValueTo = null,
    IReadOnlyList<string>? Values = null,
    bool IncludeEmpty = false);
```

| Operator | Uses | Matches |
|---|---|---|
| `In` | `Values` | a value equal to one of `Values` |
| `Between` | `Value` (from), `ValueTo` (to) | `from ≤ value ≤ to`; a `null` bound is open |
| `WithinLast` | `Value` (a period) | `now − period ≤ value ≤ now` |

- **Empty values** (null, or an empty string) never match these three operators
  unless `IncludeEmpty` is set.
- **Values are culture-invariant strings**, as formatted by
  `FilterValues.Format`: numbers like `"3.25"`, dates in ISO 8601 round-trip form
  (`"2026-09-13T00:00:00.0000000"`, or `"2026-09-13"` for `DateOnly`), durations
  in the constant format `d.hh:mm:ss` (`"2.04:30:00"`), and enums by name.
- **Periods** are `P{n}D`, `P{n}W`, `P{n}M`, or `P{n}Y` (days, weeks, calendar
  months, or calendar years). `RelativeDatePeriod.TryGetStart(period, now, out
  start)` computes the start. "Now" is the data source's clock at query time;
  `InMemoryDataProvider` takes a `TimeProvider` so tests can fix it.
- `GridState.SetFilter` drops a filter that wouldn't exclude anything: `In` with
  no values, `Between` open at both ends, or a missing value.

### Implementing `IColumnStatsProvider`

```csharp
public interface IColumnStatsProvider<TItem> : IDataProvider<TItem>
{
    Task<ColumnStats> GetColumnStatsAsync(ColumnStatsRequest request, CancellationToken cancellationToken = default);
}

public sealed record ColumnStatsRequest(string PropertyName, IReadOnlyList<FilterDescriptor> Filters,
    bool IncludeValueCounts = false, int MaxValueCount = 200);

public sealed record ColumnStats(string? Min, string? Max, int EmptyCount, int TotalCount,
    IReadOnlyList<ColumnValueCount>? Values = null, bool HasMoreValues = false);
```

- Apply `request.Filters` as usual; the grid has already left out the column's
  own filter.
- Format `Min`, `Max`, and each value with `FilterValues.Format`, from the value's
  .NET type. Reading a database's own text form doesn't work for every type:
  Postgres prints an `interval` as `"1 day 02:00:00"`, which isn't a `TimeSpan`
  string.
- When `IncludeValueCounts` is set, return up to `MaxValueCount` distinct
  non-empty values in the column's sort order, and set `HasMoreValues` if there
  were more.

In SQL, that's an aggregate over the filtered rows, plus a `GROUP BY` for the
values. From the [large-data sample](../samples/ColonnadeGrid.LargeData), which
answers each in about 6–20 ms on 100,000 rows:

```sql
SELECT min(issues.estimate_hours), max(issues.estimate_hours),
       count(*) FILTER (WHERE issues.estimate_hours IS NULL), count(*)
FROM issues
WHERE status IN ($1::issue_status);   -- the other columns' filters

SELECT issues.assignee::text, count(*)
FROM issues
WHERE status IN ($1::issue_status) AND NOT (issues.assignee IS NULL OR issues.assignee = '')
GROUP BY issues.assignee
ORDER BY issues.assignee ASC
LIMIT 201;                            -- one more than asked for, to detect "more values"
```

# Testing

```bash
dotnet test tests/ColonnadeGrid.Tests/ColonnadeGrid.Tests.csproj
```

The suite is xUnit + [bUnit](https://bunit.dev/), organized bottom-up to
match the library's layers:

- `Models/GridStateTests.cs` — pure state-mutation logic, no rendering.
- `Internal/PropertyNameExtractorTests.cs`, `Internal/TypedGridColumnTests.cs`,
  `Internal/GridContextTests.cs` — column-abstraction internals, no
  rendering.
- `Providers/InMemoryDataProviderTests.cs` — every filter operator, sort
  directions, grouping, and paging, including paged groups.
- `Providers/InMemoryDataProviderFilterTests.cs` — the `In`, `Between`, and
  `WithinLast` filters (with a fixed clock) and column stats.
- `Models/FilterValuesTests.cs` — invariant filter value formatting, relative
  periods, and choosing a column's filter editor from its type.
- `Components/FilterEditorTests.cs` — each filter editor through the component:
  value checklists, number and duration ranges, date presets and custom ranges,
  limits following the other columns' filters, and editors without column stats.
- `Internal/GridPagerTests.cs` — which page buttons and gaps the pager shows.
- `Components/ColonnadeGridPagingTests.cs` — paging through the component:
  the requests sent, pager navigation, returning to the first page, page-size
  changes, `PageIndex` binding, and paged groups.
- `Components/DataProviderCancellationTests.cs` — a provider cancelled by a
  newer request doesn't derail the grid.
- `Components/ColonnadeGridGroupPagingTests.cs` — paging each group separately:
  the row budget, per-group pagers, expanding and collapsing, "show more
  groups", placeholder rows and the single batched request, per-group errors
  with retry, and sort changes.
- `Components/ColonnadeGridTests.cs` — full component tests via bUnit:
  rendering, sort/filter/group interactions, the Columns menu, row
  selection, and a recording `IDataProvider<TItem>` test double that asserts
  the exact `DataRequest` sent to the provider on each state change (proving
  the server-paging contract, not just the in-memory convenience path).

Column resize and the select-all `indeterminate` state are the one part of
the feature set **not** covered by this suite (bUnit has no real
pointer/drag DOM) — verify those manually via the sample app.

**bUnit does not do real CSS layout.** It renders to a DOM tree and lets you
assert on markup/attributes/classes — including the `grid-template-columns`
*string* two rows were given — but it has no layout engine, so it cannot
catch a bug where two elements were given the *same* CSS yet a real browser
computes *different* rendered widths for them, which is exactly what
happened with the `width: max-content` row-sizing bug described in
[Architecture](architecture.md). Any change to layout-affecting
CSS (row/column sizing in particular) should additionally be spot-checked in
a real browser — e.g. by loading the sample app and measuring
`getBoundingClientRect()` on a few rows with visibly different content
lengths, not just glancing at it.

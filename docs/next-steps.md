# Next steps

Work from a review of the library and its samples: what was fixed, and what's
still open. Deliberate v1 scope boundaries (virtualization, multi-column sort,
cross-page select-all) are in [Known limitations](known-limitations.md), not
here.

## Fixed in the review

These were fixed alongside the prioritized list below, each with tests:

- A `State` passed after the first load (e.g. a view restored from
  `localStorage`) showed its sort and filter indicators but didn't reload the
  rows. The grid now reloads when the query changes, and no longer lets a
  re-render with the same unbound `State` undo the user's changes.
- A saved state that predated a newly declared column hid that column for
  good, with no way to show it. Missing columns are now appended.
- Rows without `RowKey` were keyed by object hash code, which isn't unique, so
  selecting one row could select another. Keys are now unique per instance,
  and a value-type `TItem` needs `RowKey` for selection.
- A data source exception on a flat load, a group list, or "Show more"
  escaped the component (ending a Blazor Server circuit). It's now shown with
  a retry, as per-group loads already were.

## P1 — done

1. **CI.** `.github/workflows/ci.yml` builds the solution and runs the tests
   on every push and pull request.
2. **Release order.** The release workflow pushes the version commit and tag
   before publishing to NuGet, so a failed push publishes nothing.
3. **Column accessors.** `Field` expressions are no longer compiled on every
   grid render. A direct member access is compiled once per member and
   cached (`ColumnAccessors`); anything else is reused while the expression
   instance is unchanged.
4. **Column registration lag.** Confirmed: a changed title, or a column
   added or removed with `@if`, stayed stale until something else re-rendered
   the grid. The grid's markup now renders in `AfterColumns`, after the
   columns register (see [Architecture](architecture.md)).
5. **Disposal.** Disposal releases every JS handle even when the circuit is
   gone; handles that arrive after disposal (from first-render setup still in
   progress) are released too; and per-group linked token sources are
   disposed when replaced.

## P2 — done

6. **Load failures reach the host.** `OnLoadError` receives the exception for
   any data source failure, including a column's filter stats, and
   `FormatLoadError` replaces the message shown to the user (see [When a load
   fails](data-sources.md#when-a-load-fails)).
7. **Group page requests are batched.** `MaxGroupPagesPerRequest` (default 50)
   caps how many groups one `GetGroupPagesAsync` call asks for; more are split
   across calls made together.
8. **Column resize edge cases** (`colonnadeGrid.js`):
   - Clicking a resize handle without dragging no longer pins the column to
     its current width.
   - The drag preview uses the same 40–2000px limits as `GridState`, so the
     column doesn't jump when the drag ends.
   - The handle captures the pointer, and `pointercancel` undoes the preview.
9. **Moving columns skips undeclared ones.** "Move left/right" only count
   columns the grid shows, so a column a saved state lists but the markup no
   longer declares can't absorb the click.
10. **A host-driven query change returns to the first page.** A `State` from
    the host with a different sort, filters, or group-by resets to page 0 and
    raises `PageIndexChanged`, as the same change from the menus does — unless
    the host passes a new `PageIndex` in the same render, e.g. restoring both
    a view and a page from a URL.

## P3 — done

11. **`InMemoryDataProvider`** looks up each filter's property accessor once
    per query rather than once per row. Group display text following the
    current culture, while keys stay invariant, is deliberate — it's for
    display — and is now documented and tested.
12. **The large-data sample and the in-memory provider agree.**
    - Text comparisons already matched; the review's note was wrong. The
      in-memory provider compares strings with the current culture, not
      ordinally, and the sample database uses ICU root collation — both
      linguistic.
    - `WithinLast` uses the browser's clock, sent in an `X-Client-Now` header,
      instead of the API server's.
    - The page's own error banner is gone; the grid shows failures itself.
13. **Long comments trimmed** in `ColonnadeGrid.razor.cs`,
    `ColonnadeGrid.razor.css`, and `colonnadeGrid.js`, leaving the history in
    [Architecture](architecture.md).

## Still open

- **The resize changes have no automated tests** — there's no JS test setup.
  Check them by hand in the demo: a click on a resize handle without dragging,
  dragging a column below 40px, and a touch drag the browser cancels. A
  browser test runner (e.g. Playwright) would cover these, and the
  hit-testing cases bUnit can't (see [Testing](testing.md)).
- **CI hasn't run yet**; it will on the first push of this branch.
- **The large-data sample's API changes build but haven't been run** against
  Postgres.
- **`ColumnMenu` and `ColumnsMenu` catch `JSException` but not
  `JSDisconnectedException`** when positioning their panels, the same gap the
  grid's own first-render setup had.

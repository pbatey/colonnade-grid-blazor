# Next steps

Open work from a review of the library and its samples, most important
first. Deliberate v1 scope boundaries (virtualization, multi-column sort,
cross-page select-all) are in [Known limitations](known-limitations.md), not
here.

## Fixed in the review

For context, these were fixed alongside this list, each with tests:

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

## P1 — do next

These are cheap, or they affect every user.

1. **Run the tests on every push and pull request.** The only workflow is
   the manual release, so a broken build is found at release time. Add a
   workflow that runs `dotnet build` and `dotnet test` for the solution.
2. **Push the release commit and tag before publishing to NuGet.** The
   release workflow publishes first and pushes afterwards. If the push fails
   (branch protection, a concurrent commit), the package is on NuGet with no
   matching commit or tag, and the version can't be republished.
3. **Stop recompiling column accessors on every render.**
   `GridColumn.OnParametersSet` calls `Field.Compile()`, and it runs on every
   grid render — Blazor always treats an `Expression` parameter as changed.
   Every checkbox click and menu toggle recompiles every column, which is
   slow under WebAssembly's interpreter. Keep the compiled delegate and only
   recompile when `Field` is a different expression tree (compare the member
   it accesses, since the host builds a new tree each render).
4. **Check whether column registration lags a render behind.** Columns
   register while the `CascadingValue` renders its content, after the grid's
   own markup has read the column list. A column added or removed with
   `@if`, or a changed `Title`, may not show until the next render. Write a
   test that toggles a column from the host; if it fails, re-render once when
   the registered columns differ from the previous render.
5. **Close the disposal gaps in `ColonnadeGrid.DisposeAsync`.**
   - `DisposeAsync()` on the JS handles runs outside the `try`, so it throws
     when the circuit is already gone.
   - Disposing while the first-render JS setup is still awaiting leaks the
     `document` pointer listeners, because the handles arrive afterwards.
     Check a disposed flag after each await.
   - The first-render setup catches `JSException` but not
     `JSDisconnectedException`.
   - Per-group paging's linked `CancellationTokenSource`s are never disposed.

## P2 — soon

These are smaller correctness and API gaps.

6. **Let the host observe load failures.** Failures are now shown in the
   grid, but the host can't log them or replace the message. Add an
   `OnLoadError` callback (receiving the exception), and consider a way to
   supply the message shown to the user. Exception messages from a server
   can reveal internals.
7. **Batch per-group page requests.** The grid sends every expanded group
   still waiting for rows in one `GetGroupPagesAsync` call, with no upper
   bound. The sample API rejects more than 100 at once, which a large
   `GroupsPerLoad` or many user-expanded groups can exceed. Split requests
   into batches, or document a limit providers can rely on.
8. **Fix the column resize edge cases** in `colonnadeGrid.js`:
   - Clicking a resize handle without dragging saves the column's current
     width, turning a flexible column into a fixed one. Only commit when the
     pointer moved.
   - The drag preview allows 20px, but `GridState.SetColumnWidth` clamps to
     40px, so the column jumps on release. Use the same minimum.
   - Handle `pointercancel` and use pointer capture, so a cancelled touch
     doesn't leave a drag active.
9. **Ignore stale columns when moving columns.** `ColumnMenu`'s "Move
   left/right" treats a state column that's no longer declared as a visible
   neighbor, so the click can swap with a column that isn't shown and appear
   to do nothing. Check against the registered columns, not just `Visible`.
10. **Decide whether a host-driven query change resets the page.** A user's
    sort or filter change goes back to the first page. A `State` passed by
    the host keeps the current page (falling back to the last one), so a
    restored view can open mid-list. Either is defensible; pick one and
    document it in [Paging](paging.md).

## P3 — when convenient

11. **`InMemoryDataProvider` performance and consistency.**
    - `MatchesFilter` looks up the property accessor for every row; look it
      up once per filter.
    - Group display text uses culture-sensitive `ToString()`, while group
      keys are invariant; format display text with the column's format or
      the current culture deliberately.
12. **Align the large-data sample with the in-memory provider.**
    - `Between` on text compares with the database collation; the in-memory
      provider compares ordinal, ignoring case.
    - `WithinLast` uses the API server's clock and time zone; the in-memory
      provider uses the browser's. Consider sending "now" or the client's
      offset with the request.
    - The sample client now throws on API errors so the grid shows them. The
      page's own error banner shows the same failure too; keep one of the two.
13. **Trim long code comments.** Several comments run to a paragraph or more
    (`BuildGridTemplateColumns`, `findScrollParent`, the dropdown backdrop
    CSS). The reasoning is worth keeping, but a short statement of the
    constraint, with the history in [Architecture](architecture.md), would be
    easier to maintain.

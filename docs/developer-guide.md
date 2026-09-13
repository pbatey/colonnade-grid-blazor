# ColonnadeGrid developer guide

ColonnadeGrid is a Blazor WebAssembly component library for a data grid
inspired by GitHub's Projects backlog/table view: customizable
columns (show/hide, reorder, resize), sorting, filtering, and single-column
grouping with collapsible group headers.

This guide covers everything needed to consume the library, implement a
custom data source, understand its architecture, and extend it. It's split
into these documents:

## Using the grid

- [Getting started](getting-started.md) — installation and a quick start.
- [Columns](columns.md) — the `GridColumn` reference; showing, hiding,
  reordering, and resizing columns; custom cell and header rendering.
- [Sorting, filtering, grouping, and view state](view-state.md) — how each
  works, and saving a view with `GridState`.
- [Filtering](filtering.md) — the filter editors (value checklists, number,
  duration, and date ranges), how their limits follow the data, and the filter
  contract.
- [Row selection](row-selection.md) — checkboxes, `RowKey`, and
  `@bind-SelectedKeys`.
- [Paging](paging.md) — the pager, and paging each group separately.
- [Styling](styling.md) — sticky header, compact mode, and theming with CSS
  custom properties.

## Data

- [Data sources](data-sources.md) — `Items` vs. your own
  `IDataProvider<TItem>`, and the grouped-response contract.
- [Working with large data sets](large-data.md) — choosing an approach,
  configuring the grid, and implementing a fast server side.

## Reference

- [Known limitations](known-limitations.md) — deliberate v1 scope boundaries.
- [Next steps](next-steps.md) — prioritized open work from the project review.
- [Architecture](architecture.md) — how the grid is built and why, including
  the bugs that shaped it.
- [Testing](testing.md) — running the test suite, and what it can't catch.

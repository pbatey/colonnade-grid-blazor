# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- The column "…" menu's **Move left** / **Move right** now keep the menu open and
  re-anchor it to the column's new position, so a column can be stepped across
  several slots without reopening the menu. **Move to start** / **Move to end**
  still close the menu.

## [1.0.0-preview.4]

### Added

- Filter dialog mode. Set `FilterInDialog="true"` on a column to open its filter
  editor in a centered modal dialog instead of the header dropdown, giving a
  richer editor room. Escape, the close button, or an outside click dismisses it.
  See [Filtering](docs/filtering.md#showing-the-filter-in-a-dialog).
  A dialog pairs well with a column's own `FilterTemplate` for hosting a richer
  editor (e.g. a date-range picker) from the host app's component library.

- Clickable rows. Set the new `OnRowClick` (`EventCallback<TItem>`) parameter to
  make every body row act as a button: rows gain a pointer cursor, a
  keyboard-focusable `role="button"`/`tabindex`, Enter/Space activation, and a
  colored accent bar down their left edge (themable via `--cg-accent-color`).
  Clicking the selection checkbox still toggles selection without raising the
  row click. See [Row selection & clickable rows](docs/row-selection.md).

- Two-column sorting. Sorting a second column now keeps the first as the
  primary sort and adds the new one as a tie-breaker, rather than replacing it.
  When two columns are sorted, each shows a small `1`/`2` badge next to its sort
  arrow indicating its priority (the badge is hidden for a single sort). Cycling
  a column's direction keeps its position; turning off the primary sort promotes
  the secondary; and selecting a third column replaces the secondary, so at most
  two columns sort at once. `GridState` gains an ordered `Sorts` list (capped at
  two) alongside the existing `Sort`, which is now the primary key for
  backward compatibility; `DataRequest`, `GroupListRequest`, and
  `GroupPagesRequest` likewise expose `Sorts` while keeping `Sort` as the primary
  so single-column data providers keep working unchanged. `GridState` adds
  `AddSort`, `RemoveSort`, and `SetSorts`. The built-in `InMemoryDataProvider`
  applies both keys (`OrderBy`/`ThenBy`).

## [1.0.0-preview.3]

### Fixed

- Column-header "…" menu (`ColumnMenu`) and the "+" columns menu (`ColumnsMenu`)
  now anchor to their trigger button instead of appearing at the grid's
  bottom-right corner, including grids nested inside `overflow-x: auto`
  containers. The menus are now rendered in a `document.body` portal while open,
  so a host ancestor with a `transform`/`filter`/`contain` (which makes it the
  containing block for `position: fixed`) or `overflow: hidden` can no longer
  mis-position or clip the popup. The click-away backdrop is lifted alongside
  the menu so an outside click still closes it. The CSS fallback (no-JS) is
  unchanged.
- The filter-view ("Filter by values…") popup now repositions correctly after
  it grows, instead of jumping to the lower-right, and its Apply button stays
  on-screen.
- The "Rows per page" selector keeps the host's initially-configured page size
  as a choice even after switching away from it, so a size not in
  `PageSizeOptions` (e.g. a `PageSize` of 15 with the default `[25, 50, 100]`)
  remains selectable.
- The row hover highlight, the row bottom border, and the header's
  active-column underbar now span the full scrollable width of the grid, not
  just the initially visible columns, when the grid overflows horizontally.
- The columns (show/hide) menu no longer renders a blank, unidentifiable row
  for a column with no `Title` (e.g. an icon-only action column).

### Added

- `GridColumn.MenuTitle` (and `GridColumnBase.MenuTitle`/`EffectiveMenuTitle`):
  an optional label for a column in the columns (show/hide) menu, independent
  of the header `Title`. Falls back to `Title`, then the column id, so a
  title-less column is still nameable.

### Changed

- Redesigned the footer pager (`GridPager`) and per-group pager (`GroupPager`)
  from numbered page buttons to first / previous / "Page X of Y" / next / last
  controls, with a shared compact, borderless button style. First/last use a new
  `DoubleChevron` icon that matches the BlazorOcticons single chevrons. The
  footer now renders at 12px to match the group pager. The pager's
  `OnPageRequested`/`OnPageSizeRequested` contract is unchanged.

## [1.0.0-preview.2]

- Previous preview release.

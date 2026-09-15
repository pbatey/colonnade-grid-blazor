# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

# Architecture

- **CSS Grid, not `<table>`.** All rows (header, body, group header) are
  `display: grid` divs sharing one **identical** `grid-template-columns`
  track list from `BuildGridTemplateColumns()`, including its trailing 40px
  track for the header's "+" add-column button — body/group rows reserve
  that same track too (as blank space; explicit CSS Grid tracks are sized
  from the track-list definition and the container's width, independent of
  whether anything actually occupies them, so an unused trailing track costs
  nothing but doesn't need to differ either). **That trailing track is only
  a fixed 40px when at least one data column is still `minmax(140px, 1fr)`
  (unset width) — once every column has an explicit fixed width (the user
  has resized them all), it becomes `minmax(40px, 1fr)` instead.** Without
  that, an all-fixed-widths row's tracks can sum to less than the row's own
  (100%-of-container) width, leaving a track-less gap past the "+" cell.
  That gap is otherwise invisible except for one real, reported symptom:
  the header's per-column bottom border (see `.cg-header-cell` et al.
  below) is drawn per-*cell*, not on the row as a whole, so nothing draws a
  border across that gap — the "+" column's border visibly stopped short of
  the row's actual right edge. Letting the trailing track flex only in that
  specific case (rather than unconditionally, which would make the "+" cell
  needlessly wide whenever a real `1fr` data column already exists to
  absorb the extra space) fixes the gap without changing the common case's
  layout at all. This isn't just for
  simplicity: when a row is wider than its own content — which is the usual
  case, via `width: max-content; min-width: 100%` (see below) — a `1fr`
  track's resolved pixel width depends on how many *other* tracks are
  competing for that same row width. A header row with one extra
  fixed-width track than the body would resolve its `1fr` data columns to a
  measurably different width than the body's, visibly misaligning every
  column on first load (a bug this library shipped briefly: it only became
  invisible once a column was resized, because resizing pins that one
  column to a fixed px width instead of `1fr`, which happened to mask the
  discrepancy for that column). Giving every row type the exact same track
  list is what keeps them pixel-for-pixel aligned regardless of how many
  columns are visible or how wide the table is. CSS Grid also sidesteps
  `colspan` recomputation when columns are hidden, and makes a full-width
  group header trivial (`grid-column: 1 / -1` — no such thing needed with a
  table's own row model). Semantic roles
  (`role="table"/"row"/"columnheader"/"gridcell"`, `aria-sort`) restore the
  accessibility a real `<table>` would give for free.
- **No outer border, no horizontal scroll — both are the host's job.**
  `.cg-grid` has no `border`/`border-radius` and no `overflow-x: auto`.
  Whatever element you wrap `ColonnadeGrid` in decides both: give it a
  border/rounded corners if you want the "card" look, and `overflow-x: auto`
  if you want the table to scroll horizontally within that box rather than
  overflowing the page when columns don't fit. See
  [Getting started](getting-started.md)'s quick-start snippet and the sample app's
  `Pages/Home.razor` (its "Layout" toggle switches between a bordered,
  centered container and a full-width one, both supplying their own
  `.demo-table-container` styles) for a concrete example.
- **Why rows must NOT be `width: max-content`** (this matters regardless of
  who owns scrolling): each `.cg-row` is left at its default width (`auto`,
  i.e. 100% of `.cg-grid`'s content box) rather than being sized to its
  content. This is a corrected version of an earlier, shipped design:
  `.cg-row` was originally `width: max-content`, on the theory that a `1fr`
  track would just resolve to its `minmax()` floor under max-content sizing,
  so a row's own natural width would only exceed 100% when columns
  genuinely didn't fit. That theory was wrong — per the CSS Grid sizing
  algorithm, a `minmax(140px, 1fr)` track's *max-content contribution* (used
  when the grid container's own width is `max-content`) is computed from
  that track's actual content, much like `auto`, not just its 140px floor.
  Two rows with different (unwrapped, `white-space: nowrap`) text lengths
  therefore computed *different* max-content row widths, and since each
  `.cg-row` is an independent grid formatting context, their `1fr` columns
  resolved to visibly different pixel widths from row to row — a real
  misalignment bug caught only by loading the rendered page in a real
  browser and measuring layout (see [Testing](testing.md) — bUnit doesn't do
  layout, so this class of bug
  is invisible to the component test suite). Leaving `width` at its default
  fixes this: every row's `1fr` tracks now resolve identically regardless of
  content, since none of them size from their own content anymore.
  Horizontal overflow still works correctly with this fix in place: when the
  140px floors don't all fit, the tracks simply refuse to shrink further and
  overflow the row's now-fixed 100% box — it's then up to whatever ancestor
  the host provides (or, absent one, the page itself) to decide what happens
  to that overflow. The group header row uses the *same*
  grid-template-columns track list as data rows (via a shared
  `GridTemplateStyle` parameter) rather than its own unrelated full-width
  layout, specifically so it can't fall out of sync with the data rows'
  width.
- **CSS isolation is per-component, not just per-file.** Blazor's CSS
  isolation only auto-scopes a component's *own* markup, not markup rendered
  by its children. Because `ColonnadeGrid` delegates rows to
  `Internal/GridRow.razor` and `Internal/GroupHeaderRow.razor`, and popovers
  to `Internal/ColumnsMenu.razor`/`Internal/FilterPopover.razor`, each of
  those files carries its own `.razor.css` — including re-declaring the
  small shared `.cg-row`/`.cg-cell` base rules `ColonnadeGrid.razor.css`
  also declares for its own (header row) markup. The custom properties
  themselves still inherit normally across component boundaries (CSS custom
  property inheritance isn't affected by Blazor's scope attributes), which
  is what keeps theming centralized in one place despite the structural CSS
  being split up.
- **Column registration**: `GridColumn<TItem, TProp>` renders no markup of
  its own. It's a plain declaration that registers a `GridColumnBase<TItem>`
  descriptor with a cascaded `GridContext<TItem>`. `ColonnadeGrid` clears
  that context's list immediately before rendering its `Columns` content on
  every render, and each `GridColumn` re-registers itself — this preserves
  markup order and supports conditionally-rendered (`@if`) columns without
  needing any `IDisposable`-based unregistration.
- **JS interop boundary**: one small module, `wwwroot/colonnadeGrid.js`,
  handling exactly four things neither pure Blazor nor pure CSS can do: (1)
  low-latency pointer-drag tracking for column resize — including its live
  width preview, both tracked entirely in JS so intermediate drag frames
  never round-trip into Blazor's render loop, with only the final width, on
  pointerup, committed via `[JSInvokable] OnColumnResizedAsync`, (2) setting
  the select-all checkbox's `indeterminate` DOM property, which has no HTML
  attribute equivalent and so can't be set via a plain Blazor bool binding,
  (3) `positionFloatingPanel`, which repositions a dropdown panel to
  `position: fixed` using viewport-measured coordinates — see below, and (4)
  `initStickyHeaderShadow`, which detects when the sticky header row has
  actually scrolled into its stuck position — see below. All four calls
  degrade gracefully (the component still renders and functions without
  them) if the module fails to load — e.g., in a test host with no real JS
  engine.
- **The header row's drop shadow only fades in once it's genuinely stuck,
  not just because it's `position: sticky` at all times — detected via an
  `IntersectionObserver` watching a zero-height sentinel rendered
  immediately above it (`.cg-sticky-sentinel`).** CSS has no selector for
  "is this element currently in its stuck position," so `pointerdown`-style
  pure-CSS tricks don't apply here; the sentinel scrolls out of its
  observer's root at exactly the scroll offset where the header starts
  sticking (since it sits right above the header, which is pinned to
  `top: 0`), and `initStickyHeaderShadow` toggles `.cg-header-row-stuck` on
  that transition, which a CSS `transition` on `box-shadow` turns into a
  fade rather than an abrupt appearance. **The observer's `root` has to be
  found manually, not left as the default viewport, and getting this wrong
  is a real, non-obvious trap**: `findScrollParent` walks up from the
  sentinel checking each ancestor's computed `overflow-y`, because a host
  container that isn't the viewport (an `overflow-y: auto` wrapper, say)
  needs to be the observer's root instead — but a CSS quirk means checking
  computed `overflow-y` alone isn't sufficient. Setting `overflow-x: auto`
  on an element (as this library's own demo does, for horizontal table
  scrolling) forces that element's *computed* `overflow-y` to `auto` as
  well, per the CSS Overflow spec's "neither axis can compute to `visible`
  if the other doesn't" rule — even though the author never asked for
  vertical scrolling and the element never actually has vertical overflow
  to scroll. Using such an element as the observer's root breaks detection
  silently: intersection is then judged against a box that never moves
  internally, so the sentinel appears permanently "intersecting" and the
  shadow never appears, no matter how far the real page scrolls. This was
  caught by loading the real demo in a browser and confirming the
  IntersectionObserver's callback actually fired on scroll — a unit test
  with no real layout/scroll engine couldn't have caught it (see
  [Testing](testing.md)). The fix: `findScrollParent`
  also checks `scrollHeight > clientHeight` — whether the candidate
  actually has vertical content to scroll — before accepting it as the
  root, not just that its computed style permits scrolling.
- **The live resize preview mutates `grid-template-columns` directly on
  every row's DOM element, bypassing Blazor entirely until the drag ends.**
  All rows share one identical track-list string (see
  `BuildGridTemplateColumns`'s own comment), so the dragged column's
  position among its header row's direct children is also its index into
  that shared track list — `initResize` reads that index once at
  `pointerdown`, then on every `pointermove` splices just that one token
  (preserving every other track, including any still-`minmax(140px, 1fr)`
  unset column, byte-for-byte) and writes the resulting string onto every
  row found under the grid container. This is safe to leave for Blazor to
  paper over on `pointerup`: `OnColumnResizedAsync` changes `_state`, which
  causes a real re-render with a freshly computed track-list string, and
  Blazor's diffing compares that against what it last rendered (the
  pre-drag string) — not against whatever this in-between JS mutation left
  in the live DOM — so it reliably overwrites the temporary preview once a
  real width is committed.
- **`InMemoryDataProvider<TItem>` is deliberately decoupled** from whatever
  `GridColumn`s a live table has registered: it resolves property names via
  its own reflection-based compiled-accessor cache, so it can be constructed
  and unit-tested in complete isolation from any component/rendering
  concerns.
- **The per-column "..." menu (`Internal/ColumnMenu.razor`) embeds
  `FilterPopover` as its "Filter by values…" view** rather than duplicating
  the operator/value editing UI. `FilterPopover` itself has no positioning
  CSS of its own (no `position: absolute`, no border/shadow) — `ColumnMenu`
  supplies the floating panel's chrome, and `FilterPopover` just owns the
  form. `ColumnMenu` expresses its own move actions (left/right/to-start/to-
  end) as a single shared contract, `EventCallback<(string ColumnId, int
  NewIndex)>` carrying an **absolute target index**, rather than four
  different relative-direction contracts — the target index is computed at
  the call site from the column's current position.
- **`ColumnsMenu` (the "+" show/hide panel) only shows/hides columns — it
  has no reorder controls of its own.** Reordering already has a dedicated,
  more complete home in each column's own "..." menu (left/right/to-start/
  to-end); duplicating that as a second, less expressive up/down control
  here was redundant. Its "Columns" header/close button were removed for
  the same reason the per-column menus have none — the click-away backdrop
  (see below) already closes it, so an explicit close affordance had no
  distinct purpose. Each row's `<label>` now stretches to fill the item so
  the whole row is clickable and shows a hover highlight, not just the
  checkbox itself.
- **Icons are BlazorOcticons components** (`@using BlazorOcticons.Octicons`,
  declared only in the files that render icons — `ColonnadeGrid.razor`,
  `Internal/ColumnMenu.razor`, `Internal/GroupHeaderRow.razor`), one
  component per icon+size (e.g. `<Plus16 Color="currentColor" />`, not a
  single icon component parameterized by name). Since a sort indicator can be
  either of two icons depending on direction (and a group chevron either of
  two depending on collapsed state), those spots pick between two
  `<SortAsc16>`/`<SortDesc16>` (or `<ChevronRight16>`/`<ChevronDown16>`)
  components with a plain `@if`, rather than trying to parameterize a single
  icon by a runtime string. Each icon component defaults its `Color` to a
  hardcoded `#000`, so every usage passes `Color="currentColor"` explicitly
  to pick up our own CSS `color` instead — and since `<PlusIcon>` (like any
  foreign component) renders markup outside of `ColonnadeGrid`'s own CSS
  isolation boundary, that `color` is always set on our *own* wrapping
  `<span>`/`<button>`, not passed as a `class` to the icon component itself.
- **The header's icon buttons share one ghost-button class,
  `.cg-header-icon-button`** (sort toggle, filter indicator, "..." trigger,
  "+" trigger — all rendered directly by `ColonnadeGrid.razor`, so the class
  only needs declaring once, unlike the `.cg-row`/`.cg-cell` situation).
  It's uncolored/borderless by default (`color: var(--cg-muted-text-color)`)
  with a light grey background on `:hover`/`:focus-visible`; a second,
  explicit `.cg-header-icon-button-active` modifier class gives the
  "..."/"+" triggers that same background *without* hovering, applied in
  markup while their own dropdown is open, so the trigger for an open menu
  reads as "pressed" rather than looking identical to a closed one. The
  group indicator is deliberately excluded — it isn't a button (no click
  handler), just a status badge, so it keeps a plain muted color instead of
  the interactive ghost treatment.
- **Dropdowns close on an outside click via a plain full-viewport backdrop
  (`.cg-dropdown-backdrop`), not JS interop.** Rendered whenever a
  per-column "..." menu or the "+" columns menu is open, positioned behind
  the open dropdown; a click anywhere the dropdown doesn't cover reaches the
  backdrop and closes whichever menu is open, while `@onclick:stopPropagation`
  on the dropdowns themselves means a click *inside* one only ever reaches
  the item clicked, not the backdrop underneath.
  **`.cg-root` sets `isolation: isolate`, and this is load-bearing, not
  decorative.** Without it, this component's internal z-index values
  (header row, backdrop, dropdown panels) would be compared directly against
  a host page's own z-index'd elements — and, more subtly, an *earlier
  version of this exact feature shipped a real bug* from a wrong mental
  model of z-index: the backdrop was given a z-index lower than the dropdown
  panels' own (`9` vs. the panels' `10`) on the assumption that the panel's
  larger number would "win" and paint on top regardless. It didn't: the
  dropdown panels are nested *inside* the header row, so their z-index only
  orders them among the header row's own children — it does nothing to lift
  the header row itself, as a whole, above a sibling like the backdrop.
  Stacking-context comparisons are ancestor-first, not "biggest number
  anywhere in the subtree wins," so the backdrop (a sibling of the header
  row's container) actually painted on top of the entire header row,
  silently swallowing every click meant for a dropdown item. This was only
  caught by loading the real page in a browser and clicking with real
  hit-testing — bUnit's simulated `.Click()` invokes a target element's
  handler directly, bypassing hit-testing entirely, so it could not have
  caught this (see [Testing](testing.md) for this same
  caveat elsewhere). The fix: `isolation: isolate` contains all of this
  component's z-index values to their own local stacking context (so they
  can use small numbers freely without leaking into the host page's own
  layering), and within that context the header row's z-index (`2`) is
  higher than the backdrop's (`1`) — the *header row* has to outrank the
  backdrop as a stacking unit; the dropdown panel's own higher z-index
  inside it was never the relevant comparison.
- **Dropdown panels reposition themselves to `position: fixed` via
  `positionFloatingPanel` (in `colonnadeGrid.js`), escaping the hosting
  page's own scroll/overflow containers.** Both `ColumnMenu` and
  `ColumnsMenu` ship with a CSS fallback of `position: absolute; right: 0;
  top: calc(100% + 4px)`, anchored to their trigger button — this is what
  renders before JS interop has run, and what remains if it never does (no
  JS engine, e.g. a test host). But this component deliberately doesn't own
  its own horizontal scrolling (see the "container owns border/scroll"
  decision above) — a host page's container legitimately sets
  `overflow-x: auto`, and `position: absolute` panels get clipped by
  whichever ancestor establishes that overflow, exactly like any other
  absolutely-positioned content would. `position: fixed` isn't clipped by
  an ancestor's `overflow`, so on `OnAfterRenderAsync`, each panel calls
  `positionFloatingPanel(panelEl)`, which reads `panelEl.parentElement`
  (the trigger's wrapping `<span>`, which needs no explicit
  `ElementReference` passed down since it's just the DOM parent) and the
  panel's own measured size, then sets `left`/`top` in viewport
  coordinates — right-aligned under the anchor by default (matching the CSS
  fallback, so there's no visible jump once JS interop kicks in), flipping
  to left-aligned if that would extend past the viewport's left edge,
  clamping if it would still overflow the right edge, and flipping to open
  *above* the anchor if it would overflow the bottom of the viewport.
  `ColumnMenu` re-runs this whenever its internal view changes (its default
  action list and its "Filter by values…" view differ in height), not just
  on `ColumnMenu`'s first render, since the previous position would
  otherwise sit under- or over-sized for the newly-swapped content;
  `ColumnsMenu` has no internal views, so first-render-only is sufficient.
  Like the other two interop calls, this degrades to the CSS fallback if
  the module fails to load. This positioning logic can only be verified in
  a real browser (it depends on actual layout/viewport measurement, which
  bUnit doesn't have) — see [Testing](testing.md).

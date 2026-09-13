# Known limitations (v1)

These are deliberate v1 scope boundaries, not accidents — flagged here so
they're a documented decision rather than a surprise:

- **Numbered pages only — no infinite scroll or row virtualization.**
  Everything on the current page (or, without `EnablePaging`, every row) is
  rendered at once, so very large pages are slow to render.
- **No cross-page "select all."** Select-all only ever affects the
  currently-loaded/visible rows, not "all N rows matching the current
  filter across the server." That would need a provider-level key-only query
  or an exclude-list model.
- **Single-column sort and single-column group-by.** No multi-column sort,
  no multi-level grouping.
- **`Field` must be a simple property access** (`x => x.Name`), not a
  computed expression (`x => x.FirstName + " " + x.LastName`) — use a
  `CellTemplate` for anything computed instead.
- **`EnableStickyHeader` silently does nothing useful if the table's
  immediate hosting container has `overflow-x: auto`/`scroll`/`hidden` (or
  any `overflow-y` other than `visible`) and no bounded height of its own.**
  This isn't a bug in this component — it's an unavoidable interaction with
  the CSS Overflow spec: setting `overflow-x` to anything but `visible`
  forces that element's *computed* `overflow-y` to `auto` too, even if the
  author never touched `overflow-y` and the element never actually has
  vertical overflow. That, in turn, makes the browser treat that container
  — not the page/viewport — as `position: sticky`'s containing block. If
  the container has no independent vertical scroll of its own (its height
  just grows to fit its content, as this library's own demo's "Centered
  container" mode does), the header never visibly sticks: it just scrolls
  away with the page, because there's nothing for it to stick *within*. The
  demo's "Full width" mode happens to avoid this only because that
  particular container variant doesn't set `overflow-x`; "Centered
  container" does (for horizontal scrolling when columns don't fit a
  `max-width` box), and so hits exactly this limitation. **The fix, when you
  need both horizontal scroll and a working sticky header, is to give that
  container an explicit bounded height plus its own `overflow-y: auto`** —
  turning it into a genuine internally-scrolling panel, where the header
  sticks to the top of the panel as the panel's own content scrolls,
  instead of trying to stick to the page. There is no way to keep
  `overflow-x: auto` on an unconstrained-height container and still have
  the header stick to the actual page scroll — the axis coupling above
  makes that combination impossible in standard CSS, not just untried here.

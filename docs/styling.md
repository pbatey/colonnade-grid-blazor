# Styling

## Sticky header

`EnableStickyHeader` (defaults to `true`) makes the column header row stick
to the top of its scroll container while scrolling, with a drop shadow that
fades in once it's actually stuck (not just because it's positioned sticky —
see [Architecture](architecture.md) for how "stuck" is detected).
Set it to `false` for a header that scrolls away normally with the rest of
the table.

**This only visibly sticks to the page if the table's hosting container has
no `overflow` set (or is bounded-height with its own `overflow-y: auto`) —
see [Known limitations](known-limitations.md) for the CSS reason a
horizontally-scrolling, unconstrained-height container silently defeats
it.** This is worth calling out separately from that limitations entry
because it's the single most likely way to set `EnableStickyHeader="true"`
and see nothing happen.

## Compact mode

`CompactMode` (defaults to `false`) shrinks row height and cell padding —
including group-header bars — to fit more rows on screen. It's a pure CSS
toggle: setting it adds a modifier class that overrides three custom
properties — `--cg-row-height` (`40px` → `32px`), `--cg-cell-padding-x`
(`12px` → `8px`), and `--cg-group-header-padding-y` (`6px` → `4px`) — which
every cell rule already reads from rather than a hardcoded value (see
[Theming](#theming)). These are fixed absolute values, not a percentage
scale-down: if you've already overridden any of the three yourself for a
custom "comfortable" density, `CompactMode` replaces your value with its own
rather than shrinking it proportionally.

**The "..."/"+" dropdowns deliberately keep their own comfortable spacing
regardless of `CompactMode`.** This needs an explicit reset, not just
"don't touch the dropdown CSS": `line-height` inherits by default, and a
dropdown panel is still a DOM *descendant* of its trigger's header cell even
once `position: fixed` moves it elsewhere on screen (CSS positioning changes
where an element paints, not its place in the DOM tree) — the header cell's
own `line-height: var(--cg-row-height)` (shrunk by `CompactMode`) would
otherwise quietly carry down into every menu item's spacing. `ColumnMenu`
and `ColumnsMenu` each already reset `font-size` for the same
DOM-descendant reason (see the comment on either panel's root rule); both
now also pin `line-height` to its own dedicated `--cg-popover-line-height`
token (`40px`, independent of `--cg-row-height` — see
[Theming](#theming)), which stops that inheritance chain at the panel
itself so every item below it sizes off this fixed value, not the row's
height. This is pinned to a fixed value rather than reset to `normal`:
`normal` sizes a line off the font's own natural metrics (roughly `17px`
for `14px` text), which reads as *more* cramped than the dropdown's tuned
default spacing, not merely "unaffected by `CompactMode`."

## Theming

All visual styling lives in CSS custom properties defined on the table's
root element (`.cg-root`), with GitHub-Projects-inspired defaults. Override
any of them from your host app to re-theme without touching the library's
CSS:

```css
:root {
    /* GitHub Primer's actual borderColor-muted/fgColor-muted values. */
    --cg-border-color: #d1d9e0b3;
    /* Opaque page color behind the (sticky) header row, pager, and footers. */
    --cg-canvas-bg: #fff;
    --cg-header-bg: #f6f8fa;
    --cg-row-hover-bg: #f6f8fa;
    --cg-row-selected-bg: #ddf4ff;
    --cg-text-color: #1f2328;
    --cg-muted-text-color: #59636e;
    --cg-accent-color: #0969da;
    /* A group whose rows failed to load. */
    --cg-danger-color: #d1242f;
    --cg-font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Helvetica, Arial, sans-serif;
    --cg-font-size: 14px;
    --cg-row-height: 40px;
    --cg-cell-padding-x: 12px;
    --cg-group-header-padding-y: 6px;
    --cg-radius: 6px;

    /* Floating dropdown chrome only — see below. */
    --cg-popover-bg: #fff;
    --cg-popover-text-color: #1f2328;
    --cg-popover-muted-text-color: #a5b0ba;
    --cg-popover-hover-bg: #f6f8fa;
    --cg-popover-border-color: #d1d9e080;
    /* Dropdown item spacing — deliberately not tied to --cg-row-height, so
       CompactMode never shrinks it (see Compact mode). */
    --cg-popover-line-height: 40px;
}
```

**None of these follow `prefers-color-scheme`** — every value above is
fixed, light-mode-only, regardless of the visitor's OS/browser color-scheme
preference. An earlier version *did* redefine most of them under a
`@media (prefers-color-scheme: dark)` block, and it repeatedly caused
elements (row hover, dropdown backgrounds) to render in an unexpectedly
dark/"black" shade whenever a visitor's system preferred dark mode, even
though the host page around the table often had no dark styling of its own
to match — a jarringly inconsistent result, not a "supports dark mode"
feature. If you want a themeable dark variant, override these custom
properties yourself, scoped under whatever condition your own app uses to
opt into dark mode (a class on `<html>`/`<body>`, your own media query,
etc.) — that puts the *host app* in control of when the table goes dark,
rather than the table silently reacting to a system setting the rest of the
page may be ignoring.

`--cg-border-color` is used everywhere a border/separator appears *inside*
the table: the vertical column separators and the horizontal separators
between ordinary rows. It's deliberately semi-transparent (the trailing
`b3` alpha channel), so it reads consistently regardless of which surface
color it's drawn over (plain row background vs. the shaded
group-header/header-active-row backgrounds). It is **not** used for an outer
border around the whole table — ColonnadeGrid doesn't draw one; see
[Getting started](getting-started.md) and [Architecture](architecture.md)
for why that, along with horizontal scrolling,
is a hosting-container concern instead.

`--cg-muted-text-color` is the default color for **body cell text** (not
just a "muted" accent used sparingly) — GitHub's own table cells render this
way too, reserving full-strength `--cg-text-color` for things that should
stand out more (header row text uses it inverted the other way: header
labels are muted, while an *active* header's underline switches to
full-strength `--cg-text-color` for contrast — see below).

The header row's own text is smaller than the body's: `.cg-header-row` sets
`font-size: 12px` directly (overriding `--cg-font-size`'s 14px, which the
body still uses) — a fixed value, not its own custom property, since nothing
so far has needed to theme header and body text sizes independently of each
other.

The header row's bottom border is also **thicker** (2px vs. 1px for
ordinary rows), and it's drawn per cell: a column that's sorted, filtered, or
grouped by switches its own header cell's border from `--cg-border-color` to
the full-strength `--cg-text-color` (`ColonnadeGrid.razor.cs`'s
`IsColumnActive` adds a `cg-header-cell-active` class) — a lightweight visual
cue that the view is modified, and by which columns, without needing a
separate "active filters" badge or banner.

The six `--cg-popover-*` tokens exist as a **separate** set from the
table's own tokens (rather than `ColumnMenu`/`ColumnsMenu`/`FilterPopover`
just reusing `--cg-text-color`/`--cg-row-hover-bg`/etc.) so a host app can
re-theme the table and its dropdowns independently of each other — override
only `--cg-popover-*` to change just the dropdowns, or only the table's own
tokens to leave dropdowns as-is.

Structural CSS (grid layout, spacing, borders-vs-background split) lives
alongside each component in its own `.razor.css` file — see
[Architecture](architecture.md) for why it's split that way.

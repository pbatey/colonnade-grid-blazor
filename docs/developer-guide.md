# ColonnadeGrid developer guide

ColonnadeGrid is a Blazor WebAssembly component library for a data grid
inspired by GitHub's Projects backlog/table view: customizable
columns (show/hide, reorder, resize), sorting, filtering, and single-column
grouping with collapsible group headers.

This guide covers everything needed to consume the library, implement a
custom data source, understand its architecture, and extend it.

## Contents

1. [Installation](#installation)
2. [Quick start](#quick-start)
3. [Column reference](#column-reference)
4. [Data sources: `Items` vs. `IDataProvider<TItem>`](#data-sources-items-vs-idataprovidertitem)
5. [`GridState`: view configuration and persistence](#gridstate-view-configuration-and-persistence)
6. [Sorting, filtering, and grouping semantics](#sorting-filtering-and-grouping-semantics)
7. [Column customization](#column-customization)
8. [Row selection](#row-selection)
9. [Sticky header](#sticky-header)
10. [Compact mode](#compact-mode)
11. [Theming](#theming)
12. [Architecture notes](#architecture-notes)
13. [Known limitations (v1)](#known-limitations-v1)
14. [Running the tests](#running-the-tests)
15. [Extending ColonnadeGrid](#extending-colonnadegrid)

## Installation

Add a project reference (or, once packaged, a NuGet package reference) from
your Blazor WebAssembly app to `src/ColonnadeGrid/ColonnadeGrid.csproj`,
then add to your app's `_Imports.razor`:

```razor
@using ColonnadeGrid
@using ColonnadeGrid.Abstractions
@using ColonnadeGrid.Models
```

No CSS or JS `<script>`/`<link>` tags need to be added manually: the
component's isolated CSS and its `wwwroot/colonnadeGrid.js` module are
packaged as static web assets and loaded automatically. The only thing your
app's `wwwroot/index.html` needs is the standard Blazor CSS-isolation bundle
link, which the WebAssembly project template already includes:

```html
<link href="{YourApp}.styles.css" rel="stylesheet" />
```

ColonnadeGrid's icons are [GitHub's own Octicons](https://primer.style/foundations/icons),
via the [BlazorOcticons](https://www.nuget.org/packages/BlazorOcticons)
package — each icon is a small, self-contained inline-SVG component (no icon
font, no external stylesheet, no static asset wiring required). You do not
need to add a `BlazorOcticons` package reference yourself; ColonnadeGrid
already depends on it.

## Quick start

The simplest usage passes an in-memory list via `Items`. ColonnadeGrid draws
no outer border and doesn't scroll itself (see [Architecture
notes](#architecture-notes)), so wrap it in a container that supplies
whatever look you want — at minimum, a border and `overflow-x: auto` so wide
tables scroll within that box instead of overflowing the page:

```razor
<div style="border: 1px solid #d1d9e0b3; border-radius: 6px; overflow-x: auto;">
    <ColonnadeGrid TItem="Issue" Items="@issues" EnableRowSelection="true">
        <Columns>
            <GridColumn Field="(Issue x) => x.Title" Title="Title" Sortable="true" Filterable="true" />
            <GridColumn Field="(Issue x) => x.Status" Title="Status" Groupable="true">
                <CellTemplate Context="issue">
                    <StatusBadge Status="issue.Status" />
                </CellTemplate>
            </GridColumn>
            <GridColumn Field="(Issue x) => x.Assignee" Title="Assignee" Filterable="true" />
        </Columns>
    </ColonnadeGrid>
</div>

@code {
    private List<Issue> issues = LoadIssues();
}
```

Notes:
- The container is entirely yours to style — add `max-width`/`margin: 0 auto`
  to center it in a narrower column, leave it at `width: 100%` (or just don't
  constrain it) to let the table fill its parent, or skip the border
  entirely if the table should blend into a page that already has its own
  card/panel chrome around this spot.
- `TItem` on `ColonnadeGrid` is the only place you need to spell out the row
  type — `ColonnadeGrid<TItem>` declares
  `[CascadingTypeParameter(nameof(TItem))]`, so nested `<GridColumn>` elements
  infer it automatically. `GridColumn`'s own `TProp` is inferred from the
  `Field` expression's return type, the same way `Field="(Issue x) => x.Title"`
  gets you a `string`-typed column without spelling out `TProp` either.
- A lambda's parameter type must be stated explicitly (`(Issue x) => x.Title`,
  not `x => x.Title`) when `TItem` is only known via cascading, since type
  inference needs a concrete parameter type to work from.
- See the [full runnable example](../samples/ColonnadeGrid.Demo/Pages/Home.razor)
  in the sample app for every feature wired up together, including a
  `IDataProvider<TItem>`-based data source.

## Column reference

Each `<GridColumn Field="..." ...>` inside a table's `<Columns>` content
supports:

| Parameter | Type | Default | Purpose |
|---|---|---|---|
| `Field` | `Expression<Func<TItem, TProp>>` | *(required)* | The property to read. Must be a simple property access (`x => x.Name`), not a computed expression — see [Known limitations](#known-limitations-v1). |
| `Id` | `string?` | property name from `Field` | Explicit stable column id; only needed if two columns would otherwise derive the same id (e.g. the same property shown twice with different templates). |
| `Title` | `string?` | property name from `Field` | Header text. |
| `Sortable` | `bool` | `false` | Whether clicking the header sorts by this column. |
| `Filterable` | `bool` | `false` | Whether the column shows a filter (▾) control. |
| `Groupable` | `bool` | `false` | Whether this column appears in the "Group by" selector. |
| `Format` | `string?` | `null` | A format string applied via `IFormattable` (e.g. `"yyyy-MM-dd"`, `"C"`) when the value implements it and no `CellTemplate` is given. |
| `CellTemplate` | `RenderFragment<TItem>?` | `null` | Custom cell content, given the row item as context. Overrides `Format`/default text rendering entirely. |
| `HeaderTemplate` | `RenderFragment?` | `null` | Custom header content, overriding `Title`. |

A column's `PropertyName` (derived from `Field`) is the key used everywhere
else in the library: `SortDescriptor.PropertyName`,
`FilterDescriptor.PropertyName`, `DataRequest.GroupByPropertyName`, and
`ColumnState.Id` (unless overridden via `Id`) all refer to it.

## Data sources: `Items` vs. `IDataProvider<TItem>`

`ColonnadeGrid<TItem>` takes **exactly one** of two mutually-exclusive
parameters — passing both, or neither, throws `InvalidOperationException` at
render time with a message identifying the problem:

- **`Items` (`IEnumerable<TItem>`)** — the convenience path. The table wraps
  it in `ColonnadeGrid.Providers.InMemoryDataProvider<TItem>` automatically
  and re-wraps it whenever the `Items` *reference* changes (assign a new list
  instance to refresh the table's data).
- **`DataProvider` (`IDataProvider<TItem>`)** — implement this yourself to
  back the table with a remote source (HTTP API, EF Core query, etc.) that
  can apply paging/sort/filter/group server-side instead of loading
  everything into WebAssembly memory:

  ```csharp
  public interface IDataProvider<TItem>
  {
      Task<DataResponse<TItem>> GetDataAsync(DataRequest request, CancellationToken cancellationToken = default);
  }
  ```

  `DataRequest` carries `Skip`, `Take`, `Sort` (a single `SortDescriptor?`),
  `Filters` (a list of `FilterDescriptor`), and `GroupByPropertyName`.
  `DataResponse<TItem>` returns `Items` (always a flat list, even when
  grouped — see below), `TotalCount`, and an optional `Groups` list. When
  `DataProvider` is set, **`RowKey` is required** (see
  [Row selection](#row-selection)).

  A minimal real-world implementation typically looks like:

  ```csharp
  public class RemoteIssueProvider : IDataProvider<Issue>
  {
      private readonly HttpClient _http;
      public RemoteIssueProvider(HttpClient http) => _http = http;

      public async Task<DataResponse<Issue>> GetDataAsync(DataRequest request, CancellationToken ct = default)
      {
          // Translate `request` into query-string parameters, call your API,
          // and map the API's response into a DataResponse<Issue>.
          var response = await _http.GetFromJsonAsync<ApiIssuePage>(BuildUrl(request), ct);
          return new DataResponse<Issue>
          {
              Items = response!.Items,
              TotalCount = response.TotalCount,
              Groups = response.Groups?.Select(g => new DataGroup(g.Key, g.DisplayText, g.Count, g.StartIndex)).ToList()
          };
      }
  }
  ```

  See [`SimulatedRemoteDataProvider`](../samples/ColonnadeGrid.Demo/Services/SimulatedRemoteDataProvider.cs)
  in the sample app for a runnable example (it wraps `InMemoryDataProvider`
  with an artificial delay to demonstrate the table's loading state and the
  provider contract working end-to-end asynchronously).

### The grouped-response contract

`DataResponse<TItem>.Items` is **always a flat list** — even when grouped.
Grouping is expressed as boundary metadata over that flat list via
`Groups: IReadOnlyList<DataGroup>?`:

```csharp
public sealed record DataGroup(string Key, string DisplayText, int Count, int StartIndex);
```

`StartIndex`/`Count` describe a contiguous range within `Items` (all of one
group's items must appear together, in order). This means collapsing or
expanding a group is a pure client-side operation — it never needs to
re-fetch data. `ColonnadeGrid.Providers.InMemoryDataProvider<TItem>` is a
complete reference implementation of this contract if you want to see how
it's constructed from an in-memory sequence.

## `GridState`: view configuration and persistence

`GridState` is the table's "view configuration": column order,
visibility, and width; the active sort; active filters; the group-by column;
and which groups are collapsed. It is **not** where row selection lives (see
[Row selection](#row-selection)) — selection is ephemeral interaction state,
not something a user expects to survive a page reload, whereas column setup
and sort/filter/group choices reasonably might.

Bind it two-way to observe or persist it:

```razor
<ColonnadeGrid TItem="Issue" Items="@issues" @bind-State="_state">
    ...
</ColonnadeGrid>

@code {
    private GridState? _state;
}
```

`GridState` is an **immutable record**: every mutation
(`MoveColumn`, `SetColumnVisible`, `SetColumnWidth`, `SetSort`, `SetFilter`,
`RemoveFilter`, `ClearFilters`, `SetGroupBy`, `ToggleGroupCollapsed`) returns
a *new* instance rather than changing the existing one, and returns the
*same* instance unchanged when nothing actually changed (e.g. moving a
column to its own current position). Two things fall out of this for free:

- **`@bind-State` correctness** — every real change produces a new
  reference, so a host component's `StateHasChanged` fires exactly when
  something changed, no more and no less.
- **Cheap persistence change-detection** — `!ReferenceEquals(oldState,
  newState)` reliably tells you whether to write to storage, without a deep
  comparison.

`GridState` is a plain, JSON-serializable object, so persisting it (e.g.
to `localStorage`) is straightforward:

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        var json = await JS.InvokeAsync<string?>("localStorage.getItem", "issues-table-state");
        if (json is not null)
        {
            _state = JsonSerializer.Deserialize<GridState>(json);
            StateHasChanged();
        }
    }
}

private async Task OnStateChanged(GridState newState)
{
    _state = newState;
    var json = JsonSerializer.Serialize(newState);
    await JS.InvokeVoidAsync("localStorage.setItem", "issues-table-state", json);
}
```

(bind `StateChanged="OnStateChanged"` instead of `@bind-State` if you want to
intercept every change like this, or keep using `@bind-State` and persist
from wherever else makes sense in your app — e.g. on a timer or on
navigation-away.)

If you don't bind `State` at all, the table manages it internally: on first
render, once its columns are known, it builds a default state (all columns
visible, in declaration order, unsorted/unfiltered/ungrouped) and uses that.

## Sorting, filtering, and grouping semantics

Every one of these lives in a single place: the **"..."** (kebab) menu on
each column's header, matching GitHub Projects' own per-column menu. Active
sort, filter, and group state each also show as a small icon next to the
column title — a status indicator independent of the menu, and (for sort and
filter) a shortcut to acting on it directly. All header buttons (the sort
icon, the filter icon, the "..." trigger, and the "+" add-column trigger)
share a "ghost button" style: uncolored and borderless until hovered,
focused, or — for "..."/"+" — while their own dropdown is open, at which
point a light grey rounded background appears (see
[Architecture notes](#architecture-notes)).

- **Sorting** is single-column in v1. A sortable column's menu shows "Sort
  ascending"/"Sort descending"; whichever direction is currently active is
  shown as "Sorted ascending"/"Sorted descending" with a small **×** to
  clear it, while the *other* direction remains a clickable action to switch
  to. Choosing a sort on one column replaces any previous sort on another.
  The active column also shows an ascending/descending icon next to its
  title — clicking that icon directly is a shortcut that flips the
  direction without opening the menu. `SortDescriptor` carries the property
  name and direction.
- **Filtering** is per-column: a filterable column's menu has a "Filter by
  values…" item that switches the same dropdown into an editor with an
  operator dropdown (`Contains`, `Equals`, `NotEquals`, `StartsWith`,
  `GreaterThan`, `LessThan`, `IsEmpty`, `IsNotEmpty`) and a value box, plus
  Apply/Clear. A column with an active filter shows a filter icon next to
  its title — clicking it is a shortcut that opens the "..." menu directly
  to that editor (pre-filled with the current operator/value), skipping the
  action list. Multiple active filters (across different columns) combine
  with **AND** semantics. `FilterDescriptor.Value` is always a plain,
  culture-invariant `string` — `InMemoryDataProvider` converts it to the
  property's actual type internally (via `TypeConverter`, falling back to an
  ordinal string comparison if conversion fails) so e.g. a numeric or date
  filter compares numerically/chronologically rather than lexically.
- **Grouping** is single-column in v1: a groupable column's menu has a
  "Group by values" item, shown as "Stop grouping" (with a checkmark) when
  that column is the active group-by — clicking it again ungroups. Choosing
  a different column's "Group by values" replaces the previous one. The
  active group-by column also shows a small "rows" icon next to its title,
  distinct from the sort indicator. It's valid — and common, matching
  GitHub's own behavior — to group by a column that isn't itself visible,
  since the group header already shows that value.
- **Pipeline order**: filter → sort → group → page. If you also sort by the
  column you're grouping by, groups come out in that sorted order "for
  free," since `InMemoryDataProvider`'s grouping preserves whatever order the
  sequence was already in.

## Column customization

- **Show/hide and reorder** each have their own dedicated place, both
  calling the same `GridState` methods under the hood:
  - Each column's own "..." menu has "Hide field" (disabled when it's the
    only remaining visible column — the table always keeps at least one)
    and "Move left"/"Move right"/"Move to start"/"Move to end", calling
    `SetColumnVisible`/`MoveColumn` for just that column. These four are
    disabled/enabled, and "Move left"/"Move right" choose their swap target,
    based on the nearest *visible* neighbor — not the column's raw position
    in `GridState.Columns`, which still includes hidden columns. A
    hidden column sitting between two visible ones (or before/after every
    remaining visible column) would otherwise let you click "Move left" and
    see nothing happen, since swapping with a hidden neighbor doesn't change
    the *visible* column order at all.
  - The **+** button at the end of the header row (matching GitHub's own
    "add field" placement) opens a panel listing *every* column — including
    hidden ones — each with just a visibility checkbox, calling
    `SetColumnVisible`. This is the only way to re-show a column you've
    hidden, since a hidden column's own header (and so its own "..." menu)
    is gone. It has no reorder controls of its own — that's what each
    column's "..." menu is for.
  Reorder is deliberately **not** drag-and-drop on the column headers
  themselves — that would conflict with the header's resize-drag handle
  occupying the same element.
- **Resize** by dragging a column's right edge, with a live width preview
  that follows the cursor. This is backed by JS interop (see
  [Architecture notes](#architecture-notes)): the drag itself, and the live
  preview, are both tracked entirely in JS by directly rewriting each row's
  `grid-template-columns`; only the final width, on release, round-trips
  into Blazor to commit into `GridState`.

## Row selection

Set `EnableRowSelection="true"` to add a checkbox column with select-all in
the header. Selection is exposed as `@bind-SelectedKeys`
(`IReadOnlySet<string>`), independent of `GridState`.

Provide `RowKey` (`Func<TItem, string>`) to control how a row's identity is
computed:

- **Required, and validated at render time, when `DataProvider` is set.**
  Without it, selection would be keyed by object identity — which silently
  breaks the moment a provider-backed reload deserializes new instances for
  the same logical rows.
- **Optional for the `Items` path**, where it defaults to an identity-based
  key stable for an object instance's lifetime. This fallback requires
  `TItem` to be a reference type; if you use a value type (a `record
  struct`, say) as `TItem`, always supply `RowKey` explicitly, since boxing a
  struct on every access would otherwise produce a different identity each
  time and silently break selection.

Selection persists across sort/filter/group changes. The header checkbox is
tri-state (checked/unchecked/indeterminate) reflecting only the *currently
visible* rows — "select all N across remote pages" is out of scope for v1
(see below).

## Sticky header

`EnableStickyHeader` (defaults to `true`) makes the column header row stick
to the top of its scroll container while scrolling, with a drop shadow that
fades in once it's actually stuck (not just because it's positioned sticky —
see [Architecture notes](#architecture-notes) for how "stuck" is detected).
Set it to `false` for a header that scrolls away normally with the rest of
the table.

**This only visibly sticks to the page if the table's hosting container has
no `overflow` set (or is bounded-height with its own `overflow-y: auto`) —
see [Known limitations](#known-limitations-v1) for the CSS reason a
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
    --cg-header-bg: #f6f8fa;
    --cg-row-hover-bg: #f6f8fa;
    --cg-row-selected-bg: #ddf4ff;
    --cg-text-color: #1f2328;
    --cg-muted-text-color: #59636e;
    --cg-accent-color: #0969da;
    --cg-font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Helvetica, Arial, sans-serif;
    --cg-font-size: 14px;
    --cg-row-height: 40px;
    --cg-cell-padding-x: 12px;
    --cg-group-header-padding-y: 6px;
    --cg-radius: 6px;

    /* Floating dropdown chrome only — see below. */
    --cg-popover-bg: #fff;
    --cg-popover-text-color: #1f2328;
    --cg-popover-muted-text-color: #59636e;
    --cg-popover-hover-bg: #f6f8fa;
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
[Installation](#installation) and [Architecture
notes](#architecture-notes) for why that, along with horizontal scrolling,
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
ordinary rows) and switches from `--cg-border-color` to the full-strength
`--cg-text-color` whenever any sort, filter, or grouping is active
(`ColonnadeGrid.razor.cs`'s `HasActiveSortFilterOrGroup` adds an
`cg-header-row-active` class) — a lightweight visual cue that the view is
currently modified from its default, without needing a separate "active
filters" badge or banner.

The four `--cg-popover-*` tokens exist as a **separate** set from the
table's own tokens (rather than `ColumnMenu`/`ColumnsMenu`/`FilterPopover`
just reusing `--cg-text-color`/`--cg-row-hover-bg`/etc.) so a host app can
re-theme the table and its dropdowns independently of each other — override
only `--cg-popover-*` to change just the dropdowns, or only the table's own
tokens to leave dropdowns as-is.

Structural CSS (grid layout, spacing, borders-vs-background split) lives
alongside each component in its own `.razor.css` file — see
[Architecture notes](#architecture-notes) for why it's split that way.

## Architecture notes

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
  [Installation](#installation)'s quick-start snippet and the sample app's
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
  browser and measuring layout (see [Running the
  tests](#running-the-tests) — bUnit doesn't do layout, so this class of bug
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
  [Running the tests](#running-the-tests)). The fix: `findScrollParent`
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
  caught this (see [Running the tests](#running-the-tests) for this same
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
  bUnit doesn't have) — see [Running the tests](#running-the-tests).

## Known limitations (v1)

These are deliberate v1 scope boundaries, not accidents — flagged here so
they're a documented decision rather than a surprise:

- **No pager/infinite-scroll UI.** `DataRequest.Skip`/`Take` exist for a
  provider to use, but the table itself doesn't paginate — it renders
  whatever a provider returns in one page. **When grouped, `Skip`/`Take` are
  ignored entirely** and every matching item is expected back, since
  grouping and paging together (cursor-per-group, "does `Take` mean items or
  groups?") is a materially harder feature than v1 takes on.
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

## Running the tests

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
  directions, grouping, paging, and the "`Take` ignored when grouped" rule.
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
[Architecture notes](#architecture-notes). Any change to layout-affecting
CSS (row/column sizing in particular) should additionally be spot-checked in
a real browser — e.g. by loading the sample app and measuring
`getBoundingClientRect()` on a few rows with visibly different content
lengths, not just glancing at it.

## Extending ColonnadeGrid

- **Custom cell rendering**: give a `<GridColumn>` a `<CellTemplate
  Context="item">...</CellTemplate>` — it receives the row item and can
  render anything (badges, links, nested components).
- **Custom header rendering**: `<HeaderTemplate>...</HeaderTemplate>`.
- **Custom filter operators or value types**: not directly extensible in
  v1's fixed `FilterOperator` enum; if you need something bespoke, implement
  your own `IDataProvider<TItem>` and interpret `FilterDescriptor` however
  you like (nothing requires you to use `InMemoryDataProvider`'s
  interpretation of it).
- **A remote/server-side data source**: implement `IDataProvider<TItem>` —
  see [Data sources](#data-sources-items-vs-idataprovidertitem) above and
  `SimulatedRemoteDataProvider` in the sample app.

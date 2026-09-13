# Getting started

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
no outer border and doesn't scroll itself (see [Architecture](architecture.md)),
so wrap it in a container that supplies
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

Next: [Columns](columns.md) for everything a column can do, or
[Data sources](data-sources.md) to back the grid with your own data source.

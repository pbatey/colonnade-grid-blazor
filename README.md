# ColonnadeGrid

A Blazor data grid with saved views — show/hide, reorder, resize, sort,
filter, and group columns, all in serializable state.

```razor
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
```

## Features

- Column show/hide and reorder, via the **Columns** menu
- Column resize, by dragging a header's right edge
- Single-column sort (click a sortable header; cycles ascending/descending/none)
- Per-column filtering with editors matched to the column's type: value
  checklists, number and duration ranges, relative date presets and date ranges,
  or text operators — with choices and limits taken from the data
- Single-column grouping with collapsible, counted group headers
- Row selection with a tri-state select-all header checkbox
- Optional paging (`EnablePaging`) with a built-in pager; grouped views can list
  groups from their counts first and page each group separately
- Works with a plain in-memory list (`Items`) **or** a pluggable
  `IDataProvider<TItem>` for server-side paging/sort/filter/group
- Self-contained, CSS-isolated styling (no Bootstrap/Tailwind dependency),
  themeable via CSS custom properties (including your own dark theme)

## Repository layout

```
src/ColonnadeGrid/           the component library (Razor Class Library, net10.0)
tests/ColonnadeGrid.Tests/   xUnit + bUnit test suite
samples/ColonnadeGrid.Demo/  a runnable Blazor WebAssembly demo app
samples/ColonnadeGrid.LargeData/  100,000 rows in Postgres behind an ASP.NET Core API, with server-side sort/filter/group and paging
docs/                        the developer guide (see below)
```

## Documentation

- [Getting started](docs/getting-started.md) — installation and a quick start
- [Columns](docs/columns.md) — column reference, customization, and custom rendering
- [Sorting, filtering, grouping, and view state](docs/view-state.md)
- [Filtering](docs/filtering.md) — the filter editors and how their limits follow the data
- [Row selection](docs/row-selection.md)
- [Paging](docs/paging.md) — the pager, and paging each group separately
- [Styling](docs/styling.md) — sticky header, compact mode, and theming
- [Data sources](docs/data-sources.md) — `Items` vs. your own `IDataProvider<TItem>`
- [Working with large data sets](docs/large-data.md)
- [Known limitations](docs/known-limitations.md), [Architecture](docs/architecture.md), and [Testing](docs/testing.md)

## Running it

To run the sample app:

```bash
dotnet run --project samples/ColonnadeGrid.Demo
```

To run the tests:

```bash
dotnet test tests/ColonnadeGrid.Tests/ColonnadeGrid.Tests.csproj
```

## License

[MIT](LICENSE)

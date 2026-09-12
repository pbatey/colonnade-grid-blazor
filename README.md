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
- Per-column filtering (Contains, Equals, StartsWith, GreaterThan/LessThan, IsEmpty/IsNotEmpty, ...)
- Single-column grouping with collapsible, counted group headers
- Row selection with a tri-state select-all header checkbox
- Works with a plain in-memory list (`Items`) **or** a pluggable
  `IDataProvider<TItem>` for server-side paging/sort/filter/group
- Self-contained, CSS-isolated styling (no Bootstrap/Tailwind dependency),
  themeable via CSS custom properties, with a built-in dark-mode variant

## Repository layout

```
src/ColonnadeGrid/           the component library (Razor Class Library, net10.0)
tests/ColonnadeGrid.Tests/   xUnit + bUnit test suite
samples/ColonnadeGrid.Demo/  a runnable Blazor WebAssembly demo app
docs/developer-guide.md      full developer guide (see below)
```

## Getting started

See **[docs/developer-guide.md](docs/developer-guide.md)** for installation,
a quick start, the full column/API reference, how to implement a custom
`IDataProvider<TItem>`, theming, architecture notes, and known v1
limitations.

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

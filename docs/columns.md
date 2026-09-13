# Columns

## Column reference

Each `<GridColumn Field="..." ...>` inside a table's `<Columns>` content
supports:

| Parameter | Type | Default | Purpose |
|---|---|---|---|
| `Field` | `Expression<Func<TItem, TProp>>` | *(required)* | The property to read. Must be a simple property access (`x => x.Name`), not a computed expression — see [Known limitations](known-limitations.md). |
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

## Custom cell and header rendering

- **Custom cell rendering**: give a `<GridColumn>` a `<CellTemplate
  Context="item">...</CellTemplate>` — it receives the row item and can
  render anything (badges, links, nested components).
- **Custom header rendering**: `<HeaderTemplate>...</HeaderTemplate>`.

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
  [Architecture](architecture.md)): the drag itself, and the live
  preview, are both tracked entirely in JS by directly rewriting each row's
  `grid-template-columns`; only the final width, on release, round-trips
  into Blazor to commit into `GridState`.

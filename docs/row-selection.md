# Row selection

Set `EnableRowSelection="true"` to add a checkbox column with select-all in
the header. Selection is exposed as `@bind-SelectedKeys`
(`IReadOnlySet<string>`), independent of `GridState`.

## Clickable rows

Set `OnRowClick` (`EventCallback<TItem>`) to make each body row act as a
button — for example, to open a detail page for that row:

```razor
<ColonnadeGrid TItem="Call"
               Items="calls"
               OnRowClick="OpenCall">
    ...
</ColonnadeGrid>

@code {
    private void OpenCall(Call row) => Nav.NavigateTo($"/calls/{row.Id}");
}
```

When set, every body row gets a pointer cursor, `role="button"` with
`tabindex="0"` so it's keyboard-focusable, and activates on Enter/Space as well
as click. The hovered or keyboard-focused row shows a colored accent bar down
its left edge (the theme's `--cg-accent-color`) marking it as openable.

Clicks that land on the built-in selection checkbox toggle selection without
raising `OnRowClick` — the grid already stops those from reaching the row.

### Interactive content inside a clickable row

A click on the row bubbles up from whatever was clicked, so any interactive
element **you** render in a cell (a link, button, menu, checkbox, etc.) will
*also* trigger `OnRowClick` unless you stop the event. Add
`@onclick:stopPropagation="true"` to that element so its own action runs
without unexpectedly invoking the row's:

```razor
<GridColumn TItem="Call" Field="(Call c) => c.Id" Title="Actions">
    <CellTemplate Context="row">
        @* Without stopPropagation, clicking Delete would also open the row. *@
        <button @onclick="() => Delete(row)"
                @onclick:stopPropagation="true">
            Delete
        </button>
    </CellTemplate>
</GridColumn>
```

The same applies to an `<a>` link, a nested checkbox, or any other control in a
cell. If a cell has no interactive element of its own, nothing extra is needed.
Where practical, prefer letting the row itself be the single click target and
keeping cells plain text.

Provide `RowKey` (`Func<TItem, string>`) to control how a row's identity is
computed:

- **Required, and validated at render time, when `DataProvider` is set.**
  Without it, selection would be keyed by object identity — which silently
  breaks the moment a provider-backed reload deserializes new instances for
  the same logical rows.
- **Optional for the `Items` path**, where each object instance gets its own
  key for as long as it lives. That needs `TItem` to be a reference type: with
  a value type (a `record struct`, say), every boxed copy is a different
  object, so the grid requires `RowKey` whenever `EnableRowSelection` is set
  and throws at render time without it.

Selection persists across sort/filter/group changes. The header checkbox is
tri-state (checked/unchecked/indeterminate) reflecting only the *currently
visible* rows — "select all N across remote pages" is out of scope for v1
(see [Known limitations](known-limitations.md)).

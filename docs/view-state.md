# Sorting, filtering, grouping, and view state

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
[Architecture](architecture.md)).

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
  values…" item that switches the same dropdown into a filter editor chosen
  from the column's type — a checklist of values, a number or duration range,
  date presets or a date range, or an operator and a text value (see
  [Filtering](filtering.md)) — plus Apply/Clear. A column with an active filter shows a filter icon next to
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

## `GridState`: view configuration and persistence

`GridState` is the table's "view configuration": column order,
visibility, and width; the active sort; active filters; the group-by column;
and which groups are collapsed. It is **not** where row selection lives (see
[Row selection](row-selection.md)) — selection is ephemeral interaction state,
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

Passing a different `State` later — like the saved view above, which is read
after the grid has already loaded — reloads the rows when its sort, filters,
or group-by differ from what's showing. The current page isn't reset; if it no
longer exists, the grid shows the last page. The grid compares `State` with
the last value you passed, not with its own current state, so if you pass
`State` without binding `StateChanged`, re-rendering your page with the same
instance won't undo the user's changes; pass a new instance to replace them.

A saved state can predate columns you've since added to the markup. The grid
appends any declared column the state doesn't list, visible, and raises the
completed state through `StateChanged`. Columns the state lists but the markup
no longer declares keep their settings, in case they're only rendered some of
the time.

If you don't bind `State` at all, the table manages it internally: on first
render, once its columns are known, it builds a default state (all columns
visible, in declaration order, unsorted/unfiltered/ungrouped) and uses that.

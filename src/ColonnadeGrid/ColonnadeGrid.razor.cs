using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ColonnadeGrid.Abstractions;
using ColonnadeGrid.Internal;
using ColonnadeGrid.Models;
using ColonnadeGrid.Providers;

namespace ColonnadeGrid;

public partial class ColonnadeGrid<TItem>
{
    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    /// <summary>The row data, for the common in-memory case. Mutually exclusive with <see cref="DataProvider"/> — exactly one must be set.</summary>
    [Parameter]
    public IEnumerable<TItem>? Items { get; set; }

    /// <summary>A pluggable data source (e.g. for server-side paging/sort/filter/group). Mutually exclusive with <see cref="Items"/> — exactly one must be set.</summary>
    [Parameter]
    public IDataProvider<TItem>? DataProvider { get; set; }

    /// <summary>The table's column declarations (<c>&lt;GridColumn&gt;</c> elements).</summary>
    [Parameter, EditorRequired]
    public RenderFragment Columns { get; set; } = default!;

    /// <summary>The table's view configuration (columns, sort, filters, group-by). Supports two-way binding via <c>@bind-State</c>.</summary>
    [Parameter]
    public GridState? State { get; set; }

    /// <summary>Raised when <see cref="State"/> changes.</summary>
    [Parameter]
    public EventCallback<GridState> StateChanged { get; set; }

    /// <summary>Whether a selection checkbox column is shown.</summary>
    [Parameter]
    public bool EnableRowSelection { get; set; }

    /// <summary>Whether the header row sticks to the top of its scroll container while scrolling. Defaults to <c>true</c>.</summary>
    [Parameter]
    public bool EnableStickyHeader { get; set; } = true;

    /// <summary>Reduces row height and cell padding to fit more rows on screen.</summary>
    [Parameter]
    public bool CompactMode { get; set; }

    /// <summary>
    /// Produces a stable string key for a row, used for selection. Required
    /// (and validated) when <see cref="DataProvider"/> is set, since
    /// object-identity-based selection silently breaks across reloads for
    /// provider-backed data. Optional for the <see cref="Items"/> path, where
    /// it defaults to an identity-based key.
    /// </summary>
    [Parameter]
    public Func<TItem, string>? RowKey { get; set; }

    /// <summary>The currently selected row keys (see <see cref="RowKey"/>). Supports two-way binding via <c>@bind-SelectedKeys</c>.</summary>
    [Parameter]
    public IReadOnlySet<string>? SelectedKeys { get; set; }

    /// <summary>Raised when <see cref="SelectedKeys"/> changes.</summary>
    [Parameter]
    public EventCallback<IReadOnlySet<string>> SelectedKeysChanged { get; set; }

    private readonly GridContext<TItem> _context = new();

    private GridState? _state;
    private IReadOnlySet<string> _selectedKeys = new HashSet<string>();
    private IDataProvider<TItem>? _effectiveProvider;
    private IEnumerable<TItem>? _lastItems;
    private DataResponse<TItem>? _data;
    private CancellationTokenSource? _loadCts;
    private bool _isLoading;
    private bool _initialized;
    private bool _columnsMenuOpen;
    private string? _openColumnMenuId;
    private bool _openColumnMenuOpensToFilterView;
    private ElementReference _gridRef;
    private ElementReference _stickySentinelRef;
    private ElementReference _headerRowRef;
    private IJSObjectReference? _module;
    private IJSObjectReference? _resizeHandle;
    private IJSObjectReference? _stickyShadowHandle;
    private DotNetObjectReference<ColonnadeGrid<TItem>>? _selfRef;

    private IReadOnlyList<GridColumnBase<TItem>> VisibleColumns
    {
        get
        {
            if (_state is null)
            {
                return [];
            }

            var byId = _context.Columns.ToDictionary(c => c.Id);
            var result = new List<GridColumnBase<TItem>>();
            foreach (var columnState in _state.Columns)
            {
                if (columnState.Visible && byId.TryGetValue(columnState.Id, out var column))
                {
                    result.Add(column);
                }
            }

            return result;
        }
    }

    private IReadOnlyList<GridColumnBase<TItem>> GroupableColumns =>
        _context.Columns.Where(c => c.Groupable).ToList();

    /// <summary>Whether this column has an active sort, filter, or is the current group-by column — drives that column's own header cell "active" underline (see .cg-header-cell-active).</summary>
    private bool IsColumnActive(GridColumnBase<TItem> column) =>
        GetActiveSortDirection(column) is not null
        || GetActiveFilter(column) is not null
        || _state?.GroupByPropertyName == column.PropertyName;

    protected override async Task OnParametersSetAsync()
    {
        ValidateParameters();

        var providerChanged = UpdateEffectiveProvider();

        if (State is not null && !ReferenceEquals(State, _state))
        {
            _state = State;
        }

        if (SelectedKeys is not null && !ReferenceEquals(SelectedKeys, _selectedKeys))
        {
            _selectedKeys = SelectedKeys;
        }

        if (_initialized && providerChanged)
        {
            await LoadDataAsync();
        }
    }

    private void ValidateParameters()
    {
        if (Items is null && DataProvider is null)
        {
            throw new InvalidOperationException(
                $"{nameof(ColonnadeGrid<TItem>)} requires either '{nameof(Items)}' or '{nameof(DataProvider)}' to be set.");
        }

        if (Items is not null && DataProvider is not null)
        {
            throw new InvalidOperationException(
                $"{nameof(ColonnadeGrid<TItem>)} cannot have both '{nameof(Items)}' and '{nameof(DataProvider)}' set — choose one.");
        }

        if (DataProvider is not null && RowKey is null)
        {
            throw new InvalidOperationException(
                $"'{nameof(RowKey)}' is required when '{nameof(DataProvider)}' is set: object-identity-based " +
                "selection silently breaks across reloads for provider-backed data.");
        }
    }

    /// <summary>Rebuilds the in-memory provider when <see cref="Items"/>'s reference changes, or swaps in a new <see cref="DataProvider"/>. Returns whether the effective provider changed.</summary>
    private bool UpdateEffectiveProvider()
    {
        if (DataProvider is not null)
        {
            if (ReferenceEquals(_effectiveProvider, DataProvider))
            {
                return false;
            }

            _effectiveProvider = DataProvider;
            return true;
        }

        if (ReferenceEquals(_lastItems, Items))
        {
            return false;
        }

        _lastItems = Items;
        _effectiveProvider = new InMemoryDataProvider<TItem>(Items!);
        return true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _state ??= State ?? GridState.Create(_context.Columns.Select(c => c.Id));
            if (StateChanged.HasDelegate && !ReferenceEquals(_state, State))
            {
                await StateChanged.InvokeAsync(_state);
            }

            await LoadDataAsync();
            _initialized = true;
            StateHasChanged();

            // JS interop is a progressive enhancement (resize drag tracking,
            // select-all indeterminate state) — the table is fully usable
            // without it, so a null/failed module load (e.g. no JS engine
            // available, as in a bUnit test host) degrades gracefully rather
            // than throwing.
            try
            {
                _module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/ColonnadeGrid/colonnadeGrid.js");
                if (_module is not null)
                {
                    _selfRef = DotNetObjectReference.Create(this);
                    _resizeHandle = await _module.InvokeAsync<IJSObjectReference>("initResize", _gridRef, _selfRef);
                    // Always attached, regardless of EnableStickyHeader's
                    // current value: that parameter only gates the CSS (see
                    // .cg-header-row-sticky/.cg-header-row-stuck), so
                    // toggling it at runtime — even starting disabled and
                    // enabling it later — works correctly without needing
                    // to tear down/recreate this observer to match.
                    _stickyShadowHandle = await _module.InvokeAsync<IJSObjectReference>(
                        "initStickyHeaderShadow", _stickySentinelRef, _headerRowRef);
                }
            }
            catch (JSException)
            {
            }
        }

        if (_module is not null && EnableRowSelection)
        {
            try
            {
                await _module.InvokeVoidAsync("syncIndeterminate", _gridRef);
            }
            catch (JSException)
            {
            }
        }
    }

    private async Task LoadDataAsync()
    {
        if (_state is null || _effectiveProvider is null)
        {
            return;
        }

        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        _isLoading = true;
        StateHasChanged();

        var request = new DataRequest(0, int.MaxValue, _state.Sort, _state.Filters, _state.GroupByPropertyName);

        try
        {
            var response = await _effectiveProvider.GetDataAsync(request, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            _data = response;
        }
        finally
        {
            if (!cts.IsCancellationRequested)
            {
                _isLoading = false;
            }
        }
    }

    private async Task SetStateAsync(GridState newState, bool reload)
    {
        if (ReferenceEquals(newState, _state))
        {
            return;
        }

        _state = newState;

        if (StateChanged.HasDelegate)
        {
            await StateChanged.InvokeAsync(newState);
        }

        if (reload)
        {
            await LoadDataAsync();
        }

        StateHasChanged();
    }

    private async Task SetSelectedKeysAsync(IReadOnlySet<string> updated)
    {
        _selectedKeys = updated;
        if (SelectedKeysChanged.HasDelegate)
        {
            await SelectedKeysChanged.InvokeAsync(updated);
        }

        StateHasChanged();
    }

    private string GetRowKey(TItem item)
    {
        if (RowKey is not null)
        {
            return RowKey(item);
        }

        // In-memory ("Items") convenience path only (validated by ValidateParameters):
        // falls back to an identity-based key, stable for a given object
        // instance's lifetime. This requires TItem to be a reference type —
        // a value-type TItem must supply RowKey explicitly, since boxing a
        // struct on every call would otherwise produce a different identity
        // hash each time and silently break selection.
        return item is null ? "" : RuntimeHelpers.GetHashCode(item).ToString(CultureInfo.InvariantCulture);
    }

    private bool IsSelected(TItem item) => _selectedKeys.Contains(GetRowKey(item));

    private Task ToggleRowSelectionAsync(TItem item)
    {
        var key = GetRowKey(item);
        var updated = new HashSet<string>(_selectedKeys);
        if (!updated.Remove(key))
        {
            updated.Add(key);
        }

        return SetSelectedKeysAsync(updated);
    }

    private (bool AllSelected, bool SomeSelected) GetHeaderCheckboxState()
    {
        var visibleKeys = (_data?.Items ?? []).Select(GetRowKey).ToList();
        if (visibleKeys.Count == 0)
        {
            return (false, false);
        }

        var selectedCount = visibleKeys.Count(_selectedKeys.Contains);
        return (selectedCount == visibleKeys.Count, selectedCount > 0 && selectedCount < visibleKeys.Count);
    }

    private Task ToggleSelectAllVisibleAsync()
    {
        var visibleKeys = (_data?.Items ?? []).Select(GetRowKey).ToHashSet();
        var (allSelected, _) = GetHeaderCheckboxState();

        var updated = new HashSet<string>(_selectedKeys);
        if (allSelected)
        {
            updated.ExceptWith(visibleKeys);
        }
        else
        {
            updated.UnionWith(visibleKeys);
        }

        return SetSelectedKeysAsync(updated);
    }

    private string GetAriaSort(GridColumnBase<TItem> column)
    {
        if (_state?.Sort?.PropertyName != column.PropertyName)
        {
            return "none";
        }

        return _state.Sort.Direction switch
        {
            SortDirection.Ascending => "ascending",
            SortDirection.Descending => "descending",
            _ => "none"
        };
    }

    /// <summary>This column's current sort direction, or <c>null</c> if it isn't the active sort column.</summary>
    private SortDirection? GetActiveSortDirection(GridColumnBase<TItem> column)
    {
        if (_state?.Sort?.PropertyName != column.PropertyName)
        {
            return null;
        }

        return _state.Sort.Direction == SortDirection.None ? null : _state.Sort.Direction;
    }

    private void ToggleColumnMenu(string columnId)
    {
        if (_openColumnMenuId == columnId)
        {
            _openColumnMenuId = null;
            return;
        }

        // Only one dropdown is ever open at a time: opening a column's "..."
        // menu while the "+" columns menu is open (or vice versa, in
        // ToggleColumnsMenu below) must close the other one too, not just
        // stack on top of it — the click-away backdrop already handles
        // closing everything when the user clicks *outside* both, but a
        // click that lands directly on a different trigger button never
        // reaches the backdrop, so it needs this explicit handling instead.
        _columnsMenuOpen = false;
        _openColumnMenuId = columnId;
        _openColumnMenuOpensToFilterView = false;
    }

    /// <summary>Opens the given column's "..." menu directly to its "Filter by values…" view — the filter-indicator icon's shortcut, mirroring <see cref="ToggleSortDirectionAsync"/> for sorting.</summary>
    private void OpenColumnMenuToFilter(string columnId)
    {
        _columnsMenuOpen = false;
        _openColumnMenuId = columnId;
        _openColumnMenuOpensToFilterView = true;
    }

    /// <summary>The active filter for the given column, if any.</summary>
    private FilterDescriptor? GetActiveFilter(GridColumnBase<TItem> column) =>
        _state?.Filters.FirstOrDefault(f => f.PropertyName == column.PropertyName);

    /// <summary>Flips the given (already-active) sort column between Ascending and Descending — a quick alternative to the "..." menu's Sort ascending/descending items.</summary>
    private Task ToggleSortDirectionAsync(GridColumnBase<TItem> column)
    {
        var next = GetActiveSortDirection(column) == SortDirection.Ascending
            ? SortDirection.Descending
            : SortDirection.Ascending;
        return SetStateAsync(_state!.SetSort(new SortDescriptor(column.PropertyName, next)), reload: true);
    }

    private Task OnSortChangedAsync(SortDescriptor? sort) =>
        SetStateAsync(_state!.SetSort(sort), reload: true);

    private Task OnFilterAppliedAsync(FilterDescriptor filter) =>
        SetStateAsync(_state!.SetFilter(filter), reload: true);

    private Task OnFilterClearedAsync(string propertyName) =>
        SetStateAsync(_state!.RemoveFilter(propertyName), reload: true);

    /// <summary>Toggles grouping by the given property: clears it if already the active group-by column, otherwise makes it the (single) active group-by column.</summary>
    private Task OnGroupByToggledAsync(string propertyName) =>
        SetStateAsync(_state!.SetGroupBy(_state.GroupByPropertyName == propertyName ? null : propertyName), reload: true);

    private Task OnToggleGroupAsync(string groupKey) =>
        SetStateAsync(_state!.ToggleGroupCollapsed(groupKey), reload: false);

    private void ToggleColumnsMenu()
    {
        if (_columnsMenuOpen)
        {
            _columnsMenuOpen = false;
            return;
        }

        // See ToggleColumnMenu's comment: only one dropdown is open at a
        // time, so opening this one must close any per-column "..." menu.
        _openColumnMenuId = null;
        _columnsMenuOpen = true;
    }

    /// <summary>Closes whichever dropdown (a per-column "..." menu, or the "+" columns menu) is currently open — the backdrop's click-away handler.</summary>
    private void CloseAllMenus()
    {
        _openColumnMenuId = null;
        _columnsMenuOpen = false;
    }

    private Task OnColumnVisibilityChangedAsync((string ColumnId, bool Visible) change) =>
        SetStateAsync(_state!.SetColumnVisible(change.ColumnId, change.Visible), reload: false);

    private Task OnHideColumnAsync(string columnId) =>
        SetStateAsync(_state!.SetColumnVisible(columnId, false), reload: false);

    private Task OnMoveColumnAsync((string ColumnId, int NewIndex) move)
    {
        if (move.NewIndex < 0 || move.NewIndex >= _state!.Columns.Count)
        {
            return Task.CompletedTask;
        }

        return SetStateAsync(_state.MoveColumn(move.ColumnId, move.NewIndex), reload: false);
    }

    /// <summary>Invoked from JS once a column-resize drag ends.</summary>
    [JSInvokable]
    public Task OnColumnResizedAsync(string columnId, double width) =>
        _state is null ? Task.CompletedTask : SetStateAsync(_state.SetColumnWidth(columnId, width), reload: false);

    /// <summary>
    /// Builds the shared <c>grid-template-columns</c> track list. Header,
    /// body, and group-header rows all use this exact same track list —
    /// including the trailing 40px track for the header's "+" add-column
    /// button, which body/group rows also reserve (as an empty cell) even
    /// though they don't show anything in it. This has to be unconditional:
    /// when a row is wider than its own content (via .cg-row's
    /// width:max-content/min-width:100%), each `1fr` track's resolved pixel
    /// width depends on how many *other* tracks are competing for the same
    /// row width — so a header row with one extra fixed-width track than
    /// body rows would resolve its `1fr` columns to a different width than
    /// the body's `1fr` columns of the exact same row width, visibly
    /// misaligning every column. Giving every row the identical track list
    /// is what keeps them pixel-for-pixel aligned.
    /// </summary>
    private string BuildGridTemplateColumns()
    {
        var tracks = new List<string>();
        if (EnableRowSelection)
        {
            tracks.Add("40px");
        }

        var anyFlexibleColumn = false;
        foreach (var column in VisibleColumns)
        {
            var width = _state?.FindColumn(column.Id)?.Width;
            if (width is { } w)
            {
                tracks.Add($"{w.ToString(CultureInfo.InvariantCulture)}px");
            }
            else
            {
                anyFlexibleColumn = true;
                tracks.Add("minmax(140px, 1fr)");
            }
        }

        // Ordinarily a fixed 40px: with at least one `1fr` data column above,
        // that column already absorbs any leftover row width, so the
        // trailing track just needs to be exactly wide enough for the "+"
        // button. But once every column has an explicit fixed width (the
        // user has resized them all), there's no `1fr` track left to soak up
        // the remainder between the tracks' total width and the row's own
        // (100%-of-container) width — leaving a gap past the last column
        // with no cell, and so no border, drawn across it (the header row's
        // own border-bottom was intentionally moved to per-cell borders — see
        // .cg-header-cell/.cg-select-cell/.cg-add-column-cell in
        // ColonnadeGrid.razor.css — so nothing else fills that gap). Making
        // this track `minmax(40px, 1fr)` only in that all-fixed-widths case
        // lets the add-column cell itself absorb the remainder, so its own
        // border-bottom reaches the row's true right edge.
        tracks.Add(anyFlexibleColumn ? "40px" : "minmax(40px, 1fr)");

        return $"grid-template-columns: {string.Join(' ', tracks)};";
    }

    public async ValueTask DisposeAsync()
    {
        _loadCts?.Cancel();

        if (_resizeHandle is not null)
        {
            try
            {
                await _resizeHandle.InvokeVoidAsync("dispose");
            }
            catch (JSDisconnectedException)
            {
                // Circuit/runtime already gone; nothing to clean up on the JS side.
            }

            await _resizeHandle.DisposeAsync();
        }

        if (_stickyShadowHandle is not null)
        {
            try
            {
                await _stickyShadowHandle.InvokeVoidAsync("dispose");
            }
            catch (JSDisconnectedException)
            {
            }

            await _stickyShadowHandle.DisposeAsync();
        }

        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }

        _selfRef?.Dispose();
    }
}

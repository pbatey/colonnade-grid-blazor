using System.Globalization;
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

    /// <summary>The default for <see cref="PageSize"/>.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>The default for <see cref="PageSizeOptions"/>.</summary>
    public static IReadOnlyList<int> DefaultPageSizeOptions { get; } = [25, 50, 100];

    /// <summary>
    /// Shows a pager below the rows and loads one page at a time: requests to
    /// the data source carry the current page's <see cref="DataRequest.Skip"/>/<see cref="DataRequest.Take"/>
    /// instead of asking for every row. Applies to grouped views too.
    /// </summary>
    [Parameter]
    public bool EnablePaging { get; set; }

    /// <summary>Rows per page when <see cref="EnablePaging"/> is set; must be at least 1. Supports two-way binding via <c>@bind-PageSize</c>.</summary>
    [Parameter]
    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>Raised when the user picks a different page size.</summary>
    [Parameter]
    public EventCallback<int> PageSizeChanged { get; set; }

    /// <summary>
    /// The zero-based current page when <see cref="EnablePaging"/> is set.
    /// Supports two-way binding via <c>@bind-PageIndex</c>. The grid returns to
    /// the first page when the sort, filters, or group-by change, and to the
    /// last page when this one no longer exists.
    /// </summary>
    [Parameter]
    public int PageIndex { get; set; }

    /// <summary>Raised when the current page changes.</summary>
    [Parameter]
    public EventCallback<int> PageIndexChanged { get; set; }

    /// <summary>The choices in the pager's "Rows per page" selector. <c>null</c> or empty hides the selector.</summary>
    [Parameter]
    public IReadOnlyList<int>? PageSizeOptions { get; set; } = DefaultPageSizeOptions;

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

    /// <summary>
    /// Raised with the exception when the data source fails to load rows, groups,
    /// or a column's filter stats. The grid shows the failure itself either way;
    /// use this to log it.
    /// </summary>
    [Parameter]
    public EventCallback<Exception> OnLoadError { get; set; }

    /// <summary>
    /// Turns a data source failure into the message shown to the user. Defaults
    /// to the exception's <see cref="Exception.Message"/>, which can reveal more
    /// than a user should see.
    /// </summary>
    [Parameter]
    public Func<Exception, string>? FormatLoadError { get; set; }

    private readonly GridContext<TItem> _context = new();

    private GridState? _state;
    private GridState? _lastStateParameter;
    private string? _loadError;
    private IReadOnlySet<string> _selectedKeys = new HashSet<string>();
    private IDataProvider<TItem>? _effectiveProvider;
    private IEnumerable<TItem>? _lastItems;
    private DataResponse<TItem>? _data;
    private CancellationTokenSource? _loadCts;
    private int _pageIndex;
    private int _pageSize = DefaultPageSize;
    // The page size the host first configured. Kept so the "Rows per page"
    // selector always offers it, even after the user picks a different size —
    // otherwise a configured size that isn't in PageSizeOptions (e.g. a host
    // PageSize of 15 with the default [25,50,100] options) would appear once
    // and then vanish, leaving no way back to it.
    private int? _initialPageSize;
    private int _lastPageIndexParameter;
    private int _lastPageSizeParameter = DefaultPageSize;
    private bool _lastEnablePaging;
    private bool _isLoading;
    private bool _initialized;
    private bool _disposed;
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
        var pageIndexParameterChanged = PageIndex != _lastPageIndexParameter;
        var pagingChanged = UpdatePagingFromParameters();
        var (queryChanged, groupExpansionChanged) = UpdateStateFromParameter();

        if (SelectedKeys is not null && !ReferenceEquals(SelectedKeys, _selectedKeys))
        {
            _selectedKeys = SelectedKeys;
        }

        var groupPagingChanged = UpdateGroupPagingFromParameters();

        if (!_initialized)
        {
            return;
        }

        // A new sort, filter, or group-by returns to the first page, as it does
        // from the menus (see SetStateAsync) — unless the host chose a page in
        // this same update, e.g. restoring both a view and a page from a URL.
        if (queryChanged && EnablePaging && !pageIndexParameterChanged)
        {
            await SetPageIndexAsync(0);
        }

        if (providerChanged || pagingChanged || groupPagingChanged || queryChanged)
        {
            await LoadDataAsync();
        }
        else if (groupExpansionChanged && IsPerGroupPaging)
        {
            await LoadExpandedGroupPagesAsync();
        }
    }

    /// <summary>
    /// Applies a host-driven <see cref="State"/>, e.g. a saved view restored
    /// after the grid first loaded. Like the paging parameters, it's compared
    /// against its own previous value, not the grid's current state: a host that
    /// passes <c>State</c> without binding <c>StateChanged</c> keeps passing the
    /// same instance on every re-render, and that mustn't undo the user's
    /// changes. Returns whether the new state asks for different rows, and
    /// whether it expands or collapses different groups.
    /// </summary>
    private (bool QueryChanged, bool GroupExpansionChanged) UpdateStateFromParameter()
    {
        if (State is null || ReferenceEquals(State, _lastStateParameter))
        {
            return (false, false);
        }

        _lastStateParameter = State;
        if (ReferenceEquals(State, _state))
        {
            return (false, false);
        }

        var previous = _state;
        _state = State;
        return previous is null
            ? (false, false)
            : (!previous.HasSameQueryAs(State), !previous.HasSameGroupExpansionAs(State));
    }

    /// <summary>
    /// Applies host-driven changes to <see cref="EnablePaging"/>, <see cref="PageSize"/>,
    /// and <see cref="PageIndex"/>. Each is compared against its own previous
    /// parameter value, not the grid's current page state: a host that doesn't
    /// bind <c>PageIndex</c> keeps passing its initial value on every re-render,
    /// and that mustn't send the user back to that page. Returns whether the
    /// change needs a reload.
    /// </summary>
    private bool UpdatePagingFromParameters()
    {
        _initialPageSize ??= PageSize;

        var reload = false;

        if (EnablePaging != _lastEnablePaging)
        {
            _lastEnablePaging = EnablePaging;
            reload = true;
        }

        if (PageSize != _lastPageSizeParameter)
        {
            _lastPageSizeParameter = PageSize;
            if (PageSize != _pageSize)
            {
                _pageSize = PageSize;
                reload |= EnablePaging;
            }
        }

        if (PageIndex != _lastPageIndexParameter)
        {
            _lastPageIndexParameter = PageIndex;
            var pageIndex = Math.Max(0, PageIndex);
            if (pageIndex != _pageIndex)
            {
                _pageIndex = pageIndex;
                reload |= EnablePaging;
            }
        }

        return reload;
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

        if (EnableRowSelection && RowKey is null && typeof(TItem).IsValueType)
        {
            throw new InvalidOperationException(
                $"'{nameof(RowKey)}' is required for row selection when the item type ('{typeof(TItem).Name}') is a " +
                "value type: every boxed copy is a different object, so an identity-based key can't track a row.");
        }

        if (EnablePaging && PageSize < 1)
        {
            throw new InvalidOperationException(
                $"'{nameof(PageSize)}' must be at least 1 when '{nameof(EnablePaging)}' is set (was {PageSize}).");
        }

        if (EnablePaging && (GroupPageSize < 1 || GroupsPerLoad < 1 || MaxGroupPagesPerRequest < 1 || GroupRowBudget < 0))
        {
            throw new InvalidOperationException(
                $"When '{nameof(EnablePaging)}' is set, '{nameof(GroupPageSize)}', '{nameof(GroupsPerLoad)}', and " +
                $"'{nameof(MaxGroupPagesPerRequest)}' must be at least 1 and '{nameof(GroupRowBudget)}' can't be negative " +
                $"(were {GroupPageSize}, {GroupsPerLoad}, {MaxGroupPagesPerRequest}, and {GroupRowBudget?.ToString() ?? "null"}).");
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
        // Columns register while rendering, so this is the first point they're
        // all known. A state from the host can predate some of them (a view saved
        // before a column was added), and a column can be added to the markup later.
        var columnIds = _context.Columns.Select(c => c.Id);
        if (!firstRender && _initialized && _state is not null
            && _state.AddMissingColumns(columnIds) is var completed && !ReferenceEquals(completed, _state))
        {
            await SetStateAsync(completed, reload: false);
        }

        if (firstRender)
        {
            _state = (_state ?? GridState.Create([])).AddMissingColumns(columnIds);
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
            if (!_disposed)
            {
                await InitJsAsync();
            }
        }

        if (_module is not null && EnableRowSelection && !_disposed)
        {
            try
            {
                await _module.InvokeVoidAsync("syncIndeterminate", _gridRef);
            }
            catch (JSException)
            {
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }

    private async Task InitJsAsync()
    {
        try
        {
            // A local, not the field: DisposeAsync can run during any await
            // here and clear the fields.
            var module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/ColonnadeGrid/colonnadeGrid.js");
            _module = module;
            if (module is not null && !_disposed)
            {
                _selfRef = DotNetObjectReference.Create(this);
                _resizeHandle = await module.InvokeAsync<IJSObjectReference>("initResize", _gridRef, _selfRef);
            }

            // Always attached, regardless of EnableStickyHeader's current
            // value: that parameter only gates the CSS (see
            // .cg-header-row-sticky/.cg-header-row-stuck), so toggling it at
            // runtime works without tearing down/recreating this observer.
            if (module is not null && !_disposed)
            {
                _stickyShadowHandle = await module.InvokeAsync<IJSObjectReference>(
                    "initStickyHeaderShadow", _stickySentinelRef, _headerRowRef);
            }
        }
        catch (JSException)
        {
        }
        catch (JSDisconnectedException)
        {
        }

        // Disposed while this was awaiting: DisposeAsync ran before these
        // existed, so their document listeners would otherwise never be removed.
        if (_disposed)
        {
            await DisposeJsObjectsAsync();
        }
    }

    private async Task LoadDataAsync()
    {
        if (_state is null || _effectiveProvider is null)
        {
            return;
        }

        if (IsPerGroupPaging)
        {
            await LoadGroupsAsync();
            return;
        }

        ClearGroupSlots();
        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        _loadError = null;
        _isLoading = true;
        StateHasChanged();

        var request = EnablePaging
            ? new DataRequest((int)Math.Min((long)_pageIndex * _pageSize, int.MaxValue), _pageSize,
                _state.Sort, _state.Filters, _state.GroupByPropertyName)
            : new DataRequest(0, int.MaxValue, _state.Sort, _state.Filters, _state.GroupByPropertyName);

        DataResponse<TItem> response;
        try
        {
            response = await _effectiveProvider.GetDataAsync(request, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // A newer load cancelled this one (see _loadCts above), and its
            // result is being discarded anyway. Letting the exception escape
            // would silently abandon the rest of whichever caller awaited this
            // load — for the first-render load, that's the remaining
            // initialization in OnAfterRenderAsync — since Blazor treats a
            // cancelled task as a non-error rather than reporting it.
            return;
        }
        catch (Exception ex)
        {
            // Shown in place of the rows, with a retry button, rather than
            // thrown: an exception escaping a component ends a Blazor Server circuit.
            var message = await ReportLoadErrorAsync(ex);
            if (!cts.IsCancellationRequested)
            {
                _loadError = message;
                _data = null;
            }

            return;
        }
        finally
        {
            if (!cts.IsCancellationRequested)
            {
                _isLoading = false;
            }
        }

        if (cts.IsCancellationRequested)
        {
            return;
        }

        _data = response;

        // The current page can stop existing (a smaller data set, or a host
        // passing a PageIndex past the end): show the last page instead of an
        // empty one.
        if (EnablePaging && response.Items.Count == 0 && response.TotalCount > 0)
        {
            var lastPageIndex = (response.TotalCount - 1) / _pageSize;
            if (lastPageIndex < _pageIndex)
            {
                await SetPageIndexAsync(lastPageIndex);
                await LoadDataAsync();
            }
        }
    }

    private Task RetryLoadAsync() => LoadDataAsync();

    /// <summary>Reports a data source failure to <see cref="OnLoadError"/> and returns the message to show for it.</summary>
    private async Task<string> ReportLoadErrorAsync(Exception exception)
    {
        if (OnLoadError.HasDelegate)
        {
            await OnLoadError.InvokeAsync(exception);
        }

        return FormatLoadError?.Invoke(exception) ?? exception.Message;
    }

    private async Task SetPageIndexAsync(int pageIndex)
    {
        if (pageIndex == _pageIndex)
        {
            return;
        }

        _pageIndex = pageIndex;
        if (PageIndexChanged.HasDelegate)
        {
            await PageIndexChanged.InvokeAsync(pageIndex);
        }
    }

    private async Task OnPageRequestedAsync(int pageIndex)
    {
        var lastPageIndex = Math.Max(0, ((_data?.TotalCount ?? 0) - 1) / _pageSize);
        pageIndex = Math.Clamp(pageIndex, 0, lastPageIndex);
        if (pageIndex == _pageIndex)
        {
            return;
        }

        await SetPageIndexAsync(pageIndex);
        await LoadDataAsync();
        StateHasChanged();
    }

    private async Task OnPageSizeRequestedAsync(int pageSize)
    {
        if (pageSize < 1 || pageSize == _pageSize)
        {
            return;
        }

        // Stay on whichever page now contains the first row that was showing.
        var firstRowIndex = (long)_pageIndex * _pageSize;
        _pageSize = pageSize;
        if (PageSizeChanged.HasDelegate)
        {
            await PageSizeChanged.InvokeAsync(pageSize);
        }

        await SetPageIndexAsync((int)(firstRowIndex / pageSize));
        await LoadDataAsync();
        StateHasChanged();
    }

    /// <summary>
    /// The page-size choices shown in the pager: the configured
    /// <see cref="PageSizeOptions"/> plus the host's initially-configured
    /// <see cref="PageSize"/>, so that size stays selectable even after the
    /// user switches away from it. The pager additionally includes the current
    /// size; sorting/de-duplication happens there.
    /// </summary>
    private IReadOnlyList<int>? EffectivePageSizeOptions =>
        PageSizeOptions is null
            ? null
            : (_initialPageSize is { } initial
                ? PageSizeOptions.Append(initial).ToList()
                : PageSizeOptions);

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
            // Every reloading change (sort, filter, group-by) changes which
            // rows land on which page, so the current page number is meaningless.
            if (EnablePaging)
            {
                await SetPageIndexAsync(0);
            }

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

        // In-memory ("Items") convenience path only, and only for reference
        // types (both validated by ValidateParameters): a key per object instance.
        return item is null ? "" : IdentityRowKeys.For(item);
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
        var visibleKeys = LoadedItems.Select(GetRowKey).ToList();
        if (visibleKeys.Count == 0)
        {
            return (false, false);
        }

        var selectedCount = visibleKeys.Count(_selectedKeys.Contains);
        return (selectedCount == visibleKeys.Count, selectedCount > 0 && selectedCount < visibleKeys.Count);
    }

    private Task ToggleSelectAllVisibleAsync()
    {
        var visibleKeys = LoadedItems.Select(GetRowKey).ToHashSet();
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

    /// <summary>
    /// Stats for a column's filter editor, over the rows matching every
    /// <em>other</em> column's filter — so the editor's limits follow the rest of
    /// the view without collapsing to its own current filter. Returns <c>null</c>
    /// when the data source doesn't implement <see cref="IColumnStatsProvider{TItem}"/>.
    /// </summary>
    private async Task<ColumnStats?> LoadColumnStatsAsync(GridColumnBase<TItem> column, CancellationToken cancellationToken)
    {
        if (_effectiveProvider is not IColumnStatsProvider<TItem> provider || _state is null)
        {
            return null;
        }

        var request = new ColumnStatsRequest(
            column.PropertyName,
            _state.Filters.Where(f => f.PropertyName != column.PropertyName).ToList(),
            IncludeValueCounts: column.EffectiveFilterKind == FilterKind.Values);
        try
        {
            return await provider.GetColumnStatsAsync(request, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // The filter editor shows the failure; the host still hears about it.
            await ReportLoadErrorAsync(ex);
            throw;
        }
    }

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
    /// Builds the <c>grid-template-columns</c> track list shared by every row —
    /// header, body, and group header — including the trailing track for the
    /// header's "+" button, which the other rows leave empty. Rows with
    /// different track lists resolve their <c>1fr</c> tracks to different
    /// widths and misalign (see docs/architecture.md).
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

        // 40px for the "+" button, unless every column has a fixed width: then
        // no `1fr` column absorbs the leftover row width, so this track does,
        // carrying its bottom border to the row's right edge.
        tracks.Add(anyFlexibleColumn ? "40px" : "minmax(40px, 1fr)");

        return $"grid-template-columns: {string.Join(' ', tracks)};";
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _loadCts?.Cancel();
        await DisposeJsObjectsAsync();
    }

    /// <summary>
    /// Removes the JS listeners and releases the JS references. Every field is
    /// cleared before the first await, so a second call running meanwhile
    /// (DisposeAsync racing first-render setup) never releases one twice.
    /// </summary>
    private async Task DisposeJsObjectsAsync()
    {
        var resizeHandle = _resizeHandle;
        var stickyShadowHandle = _stickyShadowHandle;
        var module = _module;
        var selfRef = _selfRef;
        _resizeHandle = null;
        _stickyShadowHandle = null;
        _module = null;
        _selfRef = null;

        await DisposeJsHandleAsync(resizeHandle);
        await DisposeJsHandleAsync(stickyShadowHandle);
        if (module is not null)
        {
            try
            {
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }

        selfRef?.Dispose();
    }

    /// <summary>Calls a handle's own <c>dispose()</c>, removing its listeners, then releases the reference.</summary>
    private static async Task DisposeJsHandleAsync(IJSObjectReference? handle)
    {
        if (handle is null)
        {
            return;
        }

        try
        {
            await handle.InvokeVoidAsync("dispose");
            await handle.DisposeAsync();
        }
        catch (JSException)
        {
        }
        catch (JSDisconnectedException)
        {
            // Circuit/runtime already gone; nothing to clean up on the JS side.
        }
    }
}

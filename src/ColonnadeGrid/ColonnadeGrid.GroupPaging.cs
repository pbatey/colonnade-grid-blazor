using Microsoft.AspNetCore.Components;
using ColonnadeGrid.Abstractions;
using ColonnadeGrid.Models;

namespace ColonnadeGrid;

// Per-group paging: used instead of the flat pager when EnablePaging is set,
// the view is grouped, and the data source implements IGroupedDataProvider.
// Group headers load first, from their row counts; each expanded group then
// loads and pages its own rows, and only as many groups start expanded as
// fit in the row budget.
public partial class ColonnadeGrid<TItem>
{
    /// <summary>The default for <see cref="GroupPageSize"/>.</summary>
    public const int DefaultGroupPageSize = 10;

    /// <summary>The default for <see cref="GroupsPerLoad"/>.</summary>
    public const int DefaultGroupsPerLoad = 50;

    /// <summary>
    /// Rows per page inside each group when groups are paged separately (see
    /// <see cref="IGroupedDataProvider{TItem}"/>). Must be at least 1.
    /// </summary>
    [Parameter]
    public int GroupPageSize { get; set; } = DefaultGroupPageSize;

    /// <summary>
    /// How many rows groups may load when they first appear, which bounds how
    /// much the browser holds. Going through the groups in order, each starts
    /// expanded while its first page still fits in the budget; the first group
    /// that doesn't fit, and every group after it, starts collapsed. The first
    /// group always starts expanded. Groups the user expands or collapses keep
    /// that choice and don't change how any other group starts. Defaults to
    /// <see cref="PageSize"/>, so a grouped view holds about as many rows as an
    /// ungrouped page.
    /// </summary>
    [Parameter]
    public int? GroupRowBudget { get; set; }

    /// <summary>
    /// How many groups to list at a time when groups are paged separately; a
    /// "Show more" button below the last group loads the next batch. Must be at least 1.
    /// </summary>
    [Parameter]
    public int GroupsPerLoad { get; set; } = DefaultGroupsPerLoad;

    /// <summary>The default for <see cref="MaxGroupPagesPerRequest"/>.</summary>
    public const int DefaultMaxGroupPagesPerRequest = 50;

    /// <summary>
    /// The most group pages to ask for in one
    /// <see cref="IGroupedDataProvider{TItem}.GetGroupPagesAsync"/> call. When more
    /// expanded groups need rows at once, the grid splits them across calls.
    /// Lower it to stay within a data source's own limit. Must be at least 1.
    /// </summary>
    [Parameter]
    public int MaxGroupPagesPerRequest { get; set; } = DefaultMaxGroupPagesPerRequest;

    /// <summary>A listed group and whatever of its rows are loaded.</summary>
    private sealed class GroupSlot(GroupSummary summary)
    {
        public GroupSummary Summary { get; } = summary;

        /// <summary>The group's row count: from the group list at first, then refreshed by each page load.</summary>
        public int Count { get; set; } = summary.Count;

        public int PageIndex { get; set; }

        /// <summary>The current page's rows, or <c>null</c> when collapsed, loading, or failed.</summary>
        public IReadOnlyList<TItem>? Items { get; set; }

        public bool IsLoading { get; set; }

        public string? Error { get; set; }

        /// <summary>Identifies the latest load for this group, so a slower, superseded response is ignored.</summary>
        public int LoadVersion { get; set; }

        /// <summary>Cancels this group's own page load (not a shared multi-group load).</summary>
        public CancellationTokenSource? PageCts { get; set; }
    }

    private List<GroupSlot>? _groupSlots;
    private int _totalGroupCount;
    private int _groupedTotalCount;
    private bool _isLoadingMoreGroups;
    private string? _showMoreGroupsError;
    private int _groupLoadVersion;
    private (int PageSize, int? RowBudget, int PerLoad) _lastGroupPagingParameters =
        (DefaultGroupPageSize, null, DefaultGroupsPerLoad);

    private bool IsPerGroupPaging =>
        EnablePaging && _state?.GroupByPropertyName is not null && _effectiveProvider is IGroupedDataProvider<TItem>;

    /// <summary>The rows currently loaded, which is what select-all covers.</summary>
    private IEnumerable<TItem> LoadedItems =>
        IsPerGroupPaging && _groupSlots is not null
            ? _groupSlots.Where(slot => slot.Items is not null).SelectMany(slot => slot.Items!)
            : _data?.Items ?? [];

    /// <summary>Returns whether a host-driven change to the group paging parameters needs a reload.</summary>
    private bool UpdateGroupPagingFromParameters()
    {
        var current = (GroupPageSize, GroupRowBudget, GroupsPerLoad);
        if (current == _lastGroupPagingParameters)
        {
            return false;
        }

        _lastGroupPagingParameters = current;
        return IsPerGroupPaging;
    }

    /// <summary>
    /// Which listed groups are expanded. The row budget decides the default for
    /// each group from the groups' counts and order alone; the user's recorded
    /// choices then override it. Keeping the two independent means expanding or
    /// collapsing one group never changes how another one starts.
    /// </summary>
    private HashSet<string> GetExpandedGroupKeys()
    {
        var expanded = new HashSet<string>();
        if (_groupSlots is null || _state is null)
        {
            return expanded;
        }

        var budget = GroupRowBudget ?? _pageSize;
        var budgetUsed = 0;
        var withinBudget = true;
        for (var i = 0; i < _groupSlots.Count; i++)
        {
            var key = _groupSlots[i].Summary.Key;
            var firstPageRows = Math.Min(_groupSlots[i].Summary.Count, GroupPageSize);

            withinBudget = withinBudget && (i == 0 || budgetUsed + firstPageRows <= budget);
            budgetUsed += firstPageRows;

            var isExpanded = _state.ExpandedGroupKeys.Contains(key)
                || (withinBudget && !_state.CollapsedGroupKeys.Contains(key));
            if (isExpanded)
            {
                expanded.Add(key);
            }
        }

        return expanded;
    }

    /// <summary>Lists the first batch of groups for the current view, then loads rows for the ones that start expanded.</summary>
    private async Task LoadGroupsAsync()
    {
        var provider = (IGroupedDataProvider<TItem>)_effectiveProvider!;
        var state = _state!;

        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        ClearGroupSlots();
        _data = null;
        _loadError = null;
        _showMoreGroupsError = null;
        _isLoading = true;
        StateHasChanged();

        GroupListResponse response;
        try
        {
            response = await provider.GetGroupsAsync(
                new GroupListRequest(state.Sort, state.Filters, state.GroupByPropertyName!, 0, GroupsPerLoad),
                cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            // Shown in place of the groups, with a retry button (see LoadDataAsync).
            var message = await ReportLoadErrorAsync(ex);
            if (!cts.IsCancellationRequested)
            {
                _loadError = message;
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

        _groupSlots = response.Groups.Select(group => new GroupSlot(group)).ToList();
        _totalGroupCount = response.TotalGroupCount;
        _groupedTotalCount = response.TotalCount;
        StateHasChanged();

        await LoadExpandedGroupPagesAsync();
    }

    private void ClearGroupSlots()
    {
        if (_groupSlots is null)
        {
            return;
        }

        foreach (var slot in _groupSlots)
        {
            CancelPageLoad(slot);
        }

        _groupSlots = null;
    }

    /// <summary>
    /// Brings loaded rows in line with which groups are expanded: loads the
    /// current page of every expanded group that has none yet, in one request,
    /// and drops the rows of collapsed groups. Collapsed rows are dropped rather
    /// than kept hidden because the row budget is about what the browser holds.
    /// </summary>
    private async Task LoadExpandedGroupPagesAsync()
    {
        if (_groupSlots is null || _loadCts is null)
        {
            return;
        }

        var expanded = GetExpandedGroupKeys();
        var toLoad = new List<GroupSlot>();
        foreach (var slot in _groupSlots)
        {
            if (!expanded.Contains(slot.Summary.Key))
            {
                CancelPageLoad(slot);
                slot.Items = null;
                slot.IsLoading = false;
                slot.Error = null;
                slot.LoadVersion = ++_groupLoadVersion;
            }
            else if (slot.Items is null && !slot.IsLoading && slot.Error is null)
            {
                toLoad.Add(slot);
            }
        }

        // At most MaxGroupPagesPerRequest groups per call, with the calls made together.
        var cancellationToken = _loadCts.Token;
        await Task.WhenAll(toLoad
            .Chunk(MaxGroupPagesPerRequest)
            .Select(chunk => LoadGroupPagesAsync(chunk, cancellationToken)));
    }

    /// <summary>
    /// Loads each slot's current page in one provider call. A failure is shown
    /// inside the affected groups (with a retry button) rather than thrown, so
    /// one bad load doesn't take down the rest of the grid.
    /// </summary>
    private async Task LoadGroupPagesAsync(IReadOnlyList<GroupSlot> slots, CancellationToken cancellationToken)
    {
        var provider = (IGroupedDataProvider<TItem>)_effectiveProvider!;
        var state = _state!;

        var versions = new Dictionary<GroupSlot, int>();
        foreach (var slot in slots)
        {
            slot.IsLoading = true;
            slot.Error = null;
            slot.Items = null;
            slot.LoadVersion = ++_groupLoadVersion;
            versions[slot] = slot.LoadVersion;
        }

        StateHasChanged();

        var request = new GroupPagesRequest(
            state.Sort,
            state.Filters,
            state.GroupByPropertyName!,
            slots.Select(slot => new GroupPageRequest(
                slot.Summary.Key,
                (int)Math.Min((long)slot.PageIndex * GroupPageSize, int.MaxValue),
                GroupPageSize)).ToList());

        IReadOnlyList<GroupPage<TItem>> pages;
        try
        {
            pages = await provider.GetGroupPagesAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            var message = await ReportLoadErrorAsync(ex);
            foreach (var slot in slots.Where(slot => slot.LoadVersion == versions[slot]))
            {
                slot.IsLoading = false;
                slot.Error = message;
            }

            StateHasChanged();
            return;
        }

        var pagesByKey = pages.GroupBy(page => page.GroupKey).ToDictionary(group => group.Key, group => group.First());
        var movedToLastPage = new List<GroupSlot>();
        foreach (var slot in slots)
        {
            // Collapsed, re-paged, or reloaded since this request started.
            if (slot.LoadVersion != versions[slot])
            {
                continue;
            }

            slot.IsLoading = false;
            if (!pagesByKey.TryGetValue(slot.Summary.Key, out var page))
            {
                slot.Count = 0;
                slot.Items = [];
                continue;
            }

            slot.Count = page.Count;

            // The group shrank since it was listed: show its last page instead of an empty one.
            if (page.Items.Count == 0 && page.Count > 0 && slot.PageIndex > 0)
            {
                slot.PageIndex = (page.Count - 1) / GroupPageSize;
                movedToLastPage.Add(slot);
                continue;
            }

            slot.Items = page.Items;
        }

        StateHasChanged();

        if (movedToLastPage.Count > 0)
        {
            await LoadGroupPagesAsync(movedToLastPage, cancellationToken);
        }
    }

    private async Task OnGroupPageRequestedAsync(GroupSlot slot, int pageIndex)
    {
        var lastPageIndex = Math.Max(0, (slot.Count - 1) / GroupPageSize);
        pageIndex = Math.Clamp(pageIndex, 0, lastPageIndex);
        if (pageIndex == slot.PageIndex || _loadCts is null)
        {
            return;
        }

        slot.PageIndex = pageIndex;
        CancelPageLoad(slot);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_loadCts.Token);
        slot.PageCts = cts;

        await LoadGroupPagesAsync([slot], cts.Token);
    }

    /// <summary>
    /// Cancels a group's own page load and disposes its token source, which is
    /// linked to <c>_loadCts</c> and stays registered with it until disposed.
    /// </summary>
    private static void CancelPageLoad(GroupSlot slot)
    {
        slot.PageCts?.Cancel();
        slot.PageCts?.Dispose();
        slot.PageCts = null;
    }

    private async Task OnShowMoreGroupsAsync()
    {
        if (_groupSlots is null || _loadCts is null || _isLoadingMoreGroups)
        {
            return;
        }

        var provider = (IGroupedDataProvider<TItem>)_effectiveProvider!;
        var state = _state!;
        var cancellationToken = _loadCts.Token;

        _isLoadingMoreGroups = true;
        _showMoreGroupsError = null;
        StateHasChanged();

        GroupListResponse response;
        try
        {
            response = await provider.GetGroupsAsync(
                new GroupListRequest(state.Sort, state.Filters, state.GroupByPropertyName!, _groupSlots.Count, GroupsPerLoad),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            // Shown in the footer; the groups already listed stay, and "Show more" retries.
            _showMoreGroupsError = await ReportLoadErrorAsync(ex);
            return;
        }
        finally
        {
            _isLoadingMoreGroups = false;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        // Skip any group already listed, in case the groups shifted between calls.
        var listedKeys = _groupSlots.Select(slot => slot.Summary.Key).ToHashSet();
        _groupSlots.AddRange(response.Groups.Where(group => listedKeys.Add(group.Key)).Select(group => new GroupSlot(group)));
        _totalGroupCount = response.TotalGroupCount;
        _groupedTotalCount = response.TotalCount;
        StateHasChanged();

        await LoadExpandedGroupPagesAsync();
    }

    private async Task OnToggleGroupAsync(string groupKey)
    {
        if (IsPerGroupPaging && _groupSlots is not null)
        {
            var isExpanded = GetExpandedGroupKeys().Contains(groupKey);
            await SetStateAsync(_state!.SetGroupExpanded(groupKey, !isExpanded), reload: false);
            await LoadExpandedGroupPagesAsync();
            StateHasChanged();
            return;
        }

        await SetStateAsync(_state!.ToggleGroupCollapsed(groupKey), reload: false);
    }

    private async Task RetryGroupAsync(GroupSlot slot)
    {
        slot.Error = null;
        await LoadExpandedGroupPagesAsync();
    }

    /// <summary>Placeholder rows for a loading group: as many as its current page will have, so nothing shifts when the rows arrive.</summary>
    private int GetSkeletonRowCount(GroupSlot slot) =>
        Math.Max(1, Math.Min(GroupPageSize, slot.Count - slot.PageIndex * GroupPageSize));
}

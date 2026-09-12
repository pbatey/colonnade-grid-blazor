using ColonnadeGrid.Models;

namespace ColonnadeGrid.Tests.Models;

public class GridStateTests
{
    private static GridState CreateState() => GridState.Create(["A", "B", "C"]);

    [Fact]
    public void Create_BuildsColumnsInOrder_AllVisible_NoWidth()
    {
        var state = CreateState();

        Assert.Equal(["A", "B", "C"], state.Columns.Select(c => c.Id));
        Assert.All(state.Columns, c => Assert.True(c.Visible));
        Assert.All(state.Columns, c => Assert.Null(c.Width));
        Assert.Null(state.Sort);
        Assert.Empty(state.Filters);
        Assert.Null(state.GroupByPropertyName);
        Assert.Empty(state.CollapsedGroupKeys);
    }

    [Theory]
    [InlineData(0, 2, new[] { "B", "C", "A" })]
    [InlineData(2, 0, new[] { "C", "A", "B" })]
    [InlineData(1, 1, new[] { "A", "B", "C" })]
    [InlineData(0, 1, new[] { "B", "A", "C" })]
    public void MoveColumn_ReordersColumns(int from, int to, string[] expected)
    {
        var state = CreateState();

        var result = state.MoveColumn(from, to);

        Assert.Equal(expected, result.Columns.Select(c => c.Id));
    }

    [Fact]
    public void MoveColumn_SameIndex_ReturnsSameInstance()
    {
        var state = CreateState();

        var result = state.MoveColumn(1, 1);

        Assert.Same(state, result);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(3, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 3)]
    public void MoveColumn_OutOfRangeIndex_Throws(int from, int to)
    {
        var state = CreateState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.MoveColumn(from, to));
    }

    [Fact]
    public void MoveColumn_ById_MovesToTargetIndex()
    {
        var state = CreateState();

        var result = state.MoveColumn("C", 0);

        Assert.Equal(["C", "A", "B"], result.Columns.Select(c => c.Id));
    }

    [Fact]
    public void MoveColumn_UnknownId_Throws()
    {
        var state = CreateState();

        Assert.Throws<ArgumentException>(() => state.MoveColumn("Z", 0));
    }

    [Fact]
    public void MoveColumn_DoesNotMutateOriginalState()
    {
        var state = CreateState();

        state.MoveColumn(0, 2);

        Assert.Equal(["A", "B", "C"], state.Columns.Select(c => c.Id));
    }

    [Fact]
    public void SetColumnWidth_ClampsToMinAndMax()
    {
        var state = CreateState();

        var tooSmall = state.SetColumnWidth("A", 1, minWidth: 40, maxWidth: 400);
        var tooLarge = state.SetColumnWidth("A", 9999, minWidth: 40, maxWidth: 400);
        var withinRange = state.SetColumnWidth("A", 200, minWidth: 40, maxWidth: 400);

        Assert.Equal(40, tooSmall.FindColumn("A")!.Width);
        Assert.Equal(400, tooLarge.FindColumn("A")!.Width);
        Assert.Equal(200, withinRange.FindColumn("A")!.Width);
    }

    [Fact]
    public void SetColumnWidth_OnlyChangesTargetColumn()
    {
        var state = CreateState();

        var result = state.SetColumnWidth("B", 150);

        Assert.Null(result.FindColumn("A")!.Width);
        Assert.Equal(150, result.FindColumn("B")!.Width);
        Assert.Null(result.FindColumn("C")!.Width);
    }

    [Fact]
    public void SetColumnWidth_MinExceedsMax_Throws()
    {
        var state = CreateState();

        Assert.Throws<ArgumentException>(() => state.SetColumnWidth("A", 100, minWidth: 500, maxWidth: 10));
    }

    [Fact]
    public void SetColumnWidth_UnknownColumn_Throws()
    {
        var state = CreateState();

        Assert.Throws<ArgumentException>(() => state.SetColumnWidth("Z", 100));
    }

    [Fact]
    public void SetColumnVisible_TogglesVisibility()
    {
        var state = CreateState();

        var hidden = state.SetColumnVisible("B", false);

        Assert.False(hidden.FindColumn("B")!.Visible);
        Assert.True(hidden.FindColumn("A")!.Visible);
    }

    [Fact]
    public void SetColumnVisible_NoActualChange_ReturnsSameInstance()
    {
        var state = CreateState();

        var result = state.SetColumnVisible("A", true);

        Assert.Same(state, result);
    }

    [Fact]
    public void SetSort_FirstClick_SortsAscending()
    {
        var state = CreateState();

        var result = state.SetSort("A");

        Assert.Equal(new SortDescriptor("A", SortDirection.Ascending), result.Sort);
    }

    [Fact]
    public void SetSort_CyclesNoneAscendingDescendingNone()
    {
        var state = CreateState();

        var ascending = state.SetSort("A");
        var descending = ascending.SetSort("A");
        var none = descending.SetSort("A");
        var ascendingAgain = none.SetSort("A");

        Assert.Equal(SortDirection.Ascending, ascending.Sort!.Direction);
        Assert.Equal(SortDirection.Descending, descending.Sort!.Direction);
        Assert.Null(none.Sort);
        Assert.Equal(SortDirection.Ascending, ascendingAgain.Sort!.Direction);
    }

    [Fact]
    public void SetSort_SwitchingColumn_StartsAtAscending()
    {
        var state = CreateState();

        var descendingOnA = state.SetSort("A").SetSort("A");
        var switchedToB = descendingOnA.SetSort("B");

        Assert.Equal(new SortDescriptor("B", SortDirection.Ascending), switchedToB.Sort);
    }

    [Fact]
    public void SetFilter_AddsFilter()
    {
        var state = CreateState();

        var result = state.SetFilter(new FilterDescriptor("A", FilterOperator.Contains, "foo"));

        var filter = Assert.Single(result.Filters);
        Assert.Equal("A", filter.PropertyName);
        Assert.Equal("foo", filter.Value);
    }

    [Fact]
    public void SetFilter_ReplacesExistingFilterOnSameProperty()
    {
        var state = CreateState()
            .SetFilter(new FilterDescriptor("A", FilterOperator.Contains, "foo"))
            .SetFilter(new FilterDescriptor("A", FilterOperator.StartsWith, "bar"));

        var filter = Assert.Single(state.Filters);
        Assert.Equal(FilterOperator.StartsWith, filter.Operator);
        Assert.Equal("bar", filter.Value);
    }

    [Fact]
    public void SetFilter_EmptyValueForValueRequiringOperator_RemovesFilter()
    {
        var state = CreateState()
            .SetFilter(new FilterDescriptor("A", FilterOperator.Contains, "foo"))
            .SetFilter(new FilterDescriptor("A", FilterOperator.Contains, ""));

        Assert.Empty(state.Filters);
    }

    [Fact]
    public void SetFilter_IsEmptyOperator_KeepsFilterEvenWithNullValue()
    {
        var state = CreateState().SetFilter(new FilterDescriptor("A", FilterOperator.IsEmpty, null));

        var filter = Assert.Single(state.Filters);
        Assert.Equal(FilterOperator.IsEmpty, filter.Operator);
    }

    [Fact]
    public void RemoveFilter_RemovesOnlyMatchingProperty()
    {
        var state = CreateState()
            .SetFilter(new FilterDescriptor("A", FilterOperator.Contains, "foo"))
            .SetFilter(new FilterDescriptor("B", FilterOperator.Contains, "bar"));

        var result = state.RemoveFilter("A");

        var filter = Assert.Single(result.Filters);
        Assert.Equal("B", filter.PropertyName);
    }

    [Fact]
    public void ClearFilters_RemovesAllFilters()
    {
        var state = CreateState()
            .SetFilter(new FilterDescriptor("A", FilterOperator.Contains, "foo"))
            .SetFilter(new FilterDescriptor("B", FilterOperator.Contains, "bar"));

        var result = state.ClearFilters();

        Assert.Empty(result.Filters);
    }

    [Fact]
    public void SetGroupBy_SetsGroupingColumn()
    {
        var state = CreateState();

        var result = state.SetGroupBy("B");

        Assert.Equal("B", result.GroupByPropertyName);
    }

    [Fact]
    public void SetGroupBy_ResetsCollapsedGroupKeys()
    {
        var state = CreateState()
            .SetGroupBy("A")
            .ToggleGroupCollapsed("todo");

        var result = state.SetGroupBy("B");

        Assert.Empty(result.CollapsedGroupKeys);
    }

    [Fact]
    public void ToggleGroupCollapsed_TogglesMembership()
    {
        var state = CreateState();

        var collapsed = state.ToggleGroupCollapsed("todo");
        var expanded = collapsed.ToggleGroupCollapsed("todo");

        Assert.True(collapsed.IsGroupCollapsed("todo"));
        Assert.False(expanded.IsGroupCollapsed("todo"));
    }

    [Fact]
    public void MutationMethods_ReturnNewInstance_NotSameReference()
    {
        var state = CreateState();

        Assert.NotSame(state, state.SetColumnVisible("A", false));
        Assert.NotSame(state, state.SetColumnWidth("A", 100));
        Assert.NotSame(state, state.SetSort("A"));
        Assert.NotSame(state, state.SetFilter(new FilterDescriptor("A", FilterOperator.Contains, "x")));
        Assert.NotSame(state, state.SetGroupBy("A"));
        Assert.NotSame(state, state.ToggleGroupCollapsed("x"));
    }
}

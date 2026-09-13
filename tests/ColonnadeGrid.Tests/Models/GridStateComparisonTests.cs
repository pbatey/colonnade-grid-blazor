using ColonnadeGrid.Models;

namespace ColonnadeGrid.Tests.Models;

/// <summary><see cref="GridState.AddMissingColumns"/> and the comparisons the grid uses to decide whether a new state needs a reload.</summary>
public class GridStateComparisonTests
{
    [Fact]
    public void AddMissingColumns_AppendsMissingIdsVisible_InTheGivenOrder()
    {
        var state = GridState.Create(["B"]).SetColumnWidth("B", 200);

        var result = state.AddMissingColumns(["A", "B", "C"]);

        Assert.Equal([new ColumnState("B", Width: 200), new ColumnState("A"), new ColumnState("C")], result.Columns);
    }

    [Fact]
    public void AddMissingColumns_KeepsListedColumnsThatArentGiven()
    {
        var state = GridState.Create(["A", "Gone"]).SetColumnVisible("Gone", false);

        var result = state.AddMissingColumns(["A", "B"]);

        Assert.Equal(["A", "Gone", "B"], result.Columns.Select(c => c.Id));
        Assert.False(result.FindColumn("Gone")!.Visible);
    }

    [Fact]
    public void AddMissingColumns_NothingMissing_ReturnsSameInstance()
    {
        var state = GridState.Create(["A", "B"]);

        Assert.Same(state, state.AddMissingColumns(["B", "A"]));
    }

    [Fact]
    public void HasSameQueryAs_EqualFiltersInDifferentListInstances_IsTrue()
    {
        var a = GridState.Create(["A"])
            .SetSort(new SortDescriptor("A", SortDirection.Ascending))
            .SetGroupBy("A")
            .SetFilter(new FilterDescriptor("A", FilterOperator.In, null, Values: new List<string> { "x", "y" }));
        var b = GridState.Create(["A"])
            .SetSort(new SortDescriptor("A", SortDirection.Ascending))
            .SetGroupBy("A")
            .SetFilter(new FilterDescriptor("A", FilterOperator.In, null, Values: new List<string> { "x", "y" }));

        Assert.True(a.HasSameQueryAs(b));
    }

    [Fact]
    public void HasSameQueryAs_IgnoresColumnLayoutAndGroupExpansion()
    {
        var a = GridState.Create(["A", "B"]);
        var b = a.SetColumnVisible("B", false).SetColumnWidth("A", 300).SetGroupExpanded("x", true);

        Assert.True(a.HasSameQueryAs(b));
        Assert.False(a.HasSameGroupExpansionAs(b));
    }

    public static TheoryData<GridState> DifferentQueries => new()
    {
        GridState.Create(["A"]).SetSort(new SortDescriptor("A", SortDirection.Descending)),
        GridState.Create(["A"]).SetGroupBy("B"),
        GridState.Create(["A"]).SetFilter(new FilterDescriptor("A", FilterOperator.In, null, Values: ["x"])),
        GridState.Create(["A"]).SetFilter(new FilterDescriptor("A", FilterOperator.In, null, Values: ["x", "z"], IncludeEmpty: true)),
    };

    [Theory]
    [MemberData(nameof(DifferentQueries))]
    public void HasSameQueryAs_DifferentSortGroupOrFilter_IsFalse(GridState other)
    {
        var state = GridState.Create(["A"])
            .SetSort(new SortDescriptor("A", SortDirection.Ascending))
            .SetFilter(new FilterDescriptor("A", FilterOperator.In, null, Values: ["x", "z"]));

        Assert.False(state.HasSameQueryAs(other));
    }
}

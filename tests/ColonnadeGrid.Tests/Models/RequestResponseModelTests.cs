using ColonnadeGrid.Models;
using Microsoft.AspNetCore.Components;

namespace ColonnadeGrid.Tests.Models;

/// <summary>
/// Covers the plain request/response model records: their positional
/// construction, the <c>Sorts</c> list derived from the primary sort, and the
/// shared static defaults.
/// </summary>
public class RequestResponseModelTests
{
    private static readonly SortDescriptor Asc = new("Name", SortDirection.Ascending);
    private static readonly FilterDescriptor Filter =
        new("Status", FilterOperator.Equals, "Open");

    [Fact]
    public void DataRequest_Default_IsEmptyUnpagedRequest()
    {
        var request = DataRequest.Default;

        Assert.Equal(0, request.Skip);
        Assert.Equal(int.MaxValue, request.Take);
        Assert.Null(request.Sort);
        Assert.Empty(request.Filters);
        Assert.Null(request.GroupByPropertyName);
        Assert.Empty(request.Sorts);
    }

    [Fact]
    public void DataRequest_Sorts_DefaultsToPrimarySortWhenPresent()
    {
        var request = new DataRequest(0, 25, Asc, [], null);

        Assert.Equal(new[] { Asc }, request.Sorts);
    }

    [Fact]
    public void DataRequest_Sorts_IsEmptyWhenPrimarySortIsNull()
    {
        var request = new DataRequest(0, 25, null, [], null);

        Assert.Empty(request.Sorts);
    }

    [Fact]
    public void DataRequest_Sorts_CanBeOverriddenForTwoColumnSort()
    {
        var secondary = new SortDescriptor("Created", SortDirection.Descending);
        var request = new DataRequest(0, 25, Asc, [], null) { Sorts = [Asc, secondary] };

        Assert.Equal(new[] { Asc, secondary }, request.Sorts);
    }

    [Fact]
    public void GroupListRequest_Sorts_FollowsPrimarySort()
    {
        var withSort = new GroupListRequest(Asc, [Filter], "Status", 0, 10);
        var withoutSort = new GroupListRequest(null, [], "Status", 0, 10);

        Assert.Equal(new[] { Asc }, withSort.Sorts);
        Assert.Empty(withoutSort.Sorts);
        Assert.Equal("Status", withSort.GroupByPropertyName);
        Assert.Equal(new[] { Filter }, withSort.Filters);
    }

    [Fact]
    public void GroupPagesRequest_Sorts_FollowsPrimarySort()
    {
        var pages = new[] { new GroupPageRequest("Open", 0, 20) };
        var withSort = new GroupPagesRequest(Asc, [Filter], "Status", pages);
        var withoutSort = new GroupPagesRequest(null, [], "Status", pages);

        Assert.Equal(new[] { Asc }, withSort.Sorts);
        Assert.Empty(withoutSort.Sorts);
        Assert.Equal(pages, withSort.Pages);
    }

    [Fact]
    public void DataResponse_Empty_IsEmptyAndUngrouped()
    {
        var response = DataResponse<int>.Empty;

        Assert.Empty(response.Items);
        Assert.Equal(0, response.TotalCount);
        Assert.Null(response.Groups);
    }

    [Fact]
    public void DataResponse_Empty_IsCachedSingleton()
    {
        Assert.Same(DataResponse<int>.Empty, DataResponse<int>.Empty);
    }

    [Fact]
    public void DataResponse_CarriesItemsCountAndGroups()
    {
        var groups = new[] { new DataGroup("Open", "Open", 2, 0, 5) };
        var response = new DataResponse<string>
        {
            Items = new[] { "a", "b" },
            TotalCount = 5,
            Groups = groups
        };

        Assert.Equal(new[] { "a", "b" }, response.Items);
        Assert.Equal(5, response.TotalCount);
        Assert.Equal(groups, response.Groups);
    }

    [Fact]
    public void FilterEditorContext_ExposesConstructorArguments()
    {
        var stats = new ColumnStats("1", "9", 0, 10);
        var context = new FilterEditorContext(
            "Status",
            typeof(string),
            Filter,
            stats,
            EventCallback<FilterDescriptor>.Empty,
            EventCallback.Empty);

        Assert.Equal("Status", context.PropertyName);
        Assert.Equal(typeof(string), context.PropertyType);
        Assert.Same(Filter, context.CurrentFilter);
        Assert.Same(stats, context.Stats);
    }

    [Fact]
    public void FilterEditorContext_AllowsNullableFilterAndStats()
    {
        var context = new FilterEditorContext(
            "Status",
            typeof(int),
            null,
            null,
            EventCallback<FilterDescriptor>.Empty,
            EventCallback.Empty);

        Assert.Null(context.CurrentFilter);
        Assert.Null(context.Stats);
    }

    [Fact]
    public void FilterEditorContext_EqualityUsesValueSemantics()
    {
        var a = new FilterEditorContext("Status", typeof(string), null, null,
            EventCallback<FilterDescriptor>.Empty, EventCallback.Empty);
        var b = new FilterEditorContext("Status", typeof(string), null, null,
            EventCallback<FilterDescriptor>.Empty, EventCallback.Empty);

        Assert.Equal(a, b);
    }
}

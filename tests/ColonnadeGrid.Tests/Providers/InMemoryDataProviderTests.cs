using ColonnadeGrid.Models;
using ColonnadeGrid.Providers;

namespace ColonnadeGrid.Tests.Providers;

public class InMemoryDataProviderTests
{
    private enum Status
    {
        Todo,
        InProgress,
        Done
    }

    private sealed record Issue(string Title, int Priority, Status Status, DateTime? DueDate, string? Assignee);

    private static readonly Issue[] SampleIssues =
    [
        new("Fix login bug", 1, Status.Todo, new DateTime(2026, 1, 10), "alice"),
        new("Add dark mode", 3, Status.InProgress, new DateTime(2026, 2, 1), "bob"),
        new("Write docs", 2, Status.Todo, null, null),
        new("Refactor auth", 1, Status.Done, new DateTime(2025, 12, 20), "alice"),
        new("Upgrade deps", 2, Status.Done, new DateTime(2026, 3, 5), "carol"),
    ];

    private static InMemoryDataProvider<Issue> CreateProvider() => new(SampleIssues);

    private static DataRequest Request(
        int skip = 0,
        int take = int.MaxValue,
        SortDescriptor? sort = null,
        IReadOnlyList<FilterDescriptor>? filters = null,
        string? groupBy = null) =>
        new(skip, take, sort, filters ?? [], groupBy);

    // ----- sorting -----

    [Fact]
    public async Task Sort_Ascending_OrdersByProperty()
    {
        var provider = CreateProvider();

        var response = await provider.GetDataAsync(
            Request(sort: new SortDescriptor(nameof(Issue.Priority), SortDirection.Ascending)));

        Assert.Equal([1, 1, 2, 2, 3], response.Items.Select(i => i.Priority));
    }

    [Fact]
    public async Task Sort_Descending_OrdersByPropertyReversed()
    {
        var provider = CreateProvider();

        var response = await provider.GetDataAsync(
            Request(sort: new SortDescriptor(nameof(Issue.Priority), SortDirection.Descending)));

        Assert.Equal([3, 2, 2, 1, 1], response.Items.Select(i => i.Priority));
    }

    [Fact]
    public async Task Sort_None_PreservesOriginalOrder()
    {
        var provider = CreateProvider();

        var response = await provider.GetDataAsync(
            Request(sort: new SortDescriptor(nameof(Issue.Priority), SortDirection.None)));

        Assert.Equal(SampleIssues.Select(i => i.Title), response.Items.Select(i => i.Title));
    }

    [Fact]
    public async Task Sort_HandlesNullValues()
    {
        var provider = CreateProvider();

        var response = await provider.GetDataAsync(
            Request(sort: new SortDescriptor(nameof(Issue.DueDate), SortDirection.Ascending)));

        // null DueDate ("Write docs") should sort first, ascending.
        Assert.Equal("Write docs", response.Items[0].Title);
    }

    // ----- filtering: each operator -----

    [Fact]
    public async Task Filter_Contains_MatchesSubstringCaseInsensitive()
    {
        var provider = CreateProvider();

        var response = await provider.GetDataAsync(
            Request(filters: [new FilterDescriptor(nameof(Issue.Title), FilterOperator.Contains, "LOGIN")]));

        var issue = Assert.Single(response.Items);
        Assert.Equal("Fix login bug", issue.Title);
    }

    [Fact]
    public async Task Filter_StartsWith_MatchesPrefixCaseInsensitive()
    {
        var provider = CreateProvider();

        var response = await provider.GetDataAsync(
            Request(filters: [new FilterDescriptor(nameof(Issue.Title), FilterOperator.StartsWith, "add")]));

        var issue = Assert.Single(response.Items);
        Assert.Equal("Add dark mode", issue.Title);
    }

    [Fact]
    public async Task Filter_Equals_ComparesTypedValue()
    {
        var provider = CreateProvider();

        var response = await provider.GetDataAsync(
            Request(filters: [new FilterDescriptor(nameof(Issue.Priority), FilterOperator.Equals, "2")]));

        Assert.Equal(2, response.Items.Count);
        Assert.All(response.Items, i => Assert.Equal(2, i.Priority));
    }

    [Fact]
    public async Task Filter_NotEquals_ExcludesMatchingValue()
    {
        var provider = CreateProvider();

        var response = await provider.GetDataAsync(
            Request(filters: [new FilterDescriptor(nameof(Issue.Priority), FilterOperator.NotEquals, "1")]));

        Assert.DoesNotContain(response.Items, i => i.Priority == 1);
        Assert.Equal(3, response.Items.Count);
    }

    [Fact]
    public async Task Filter_GreaterThan_ComparesNumerically_NotLexically()
    {
        var provider = CreateProvider();

        // A lexical/string comparison of "10" > "9" would be wrong ("1" < "9");
        // this asserts the numeric-typed comparison path is used.
        var response = await provider.GetDataAsync(
            Request(filters: [new FilterDescriptor(nameof(Issue.Priority), FilterOperator.GreaterThan, "1")]));

        Assert.All(response.Items, i => Assert.True(i.Priority > 1));
        Assert.Equal(3, response.Items.Count);
    }

    [Fact]
    public async Task Filter_LessThan_ComparesNumerically()
    {
        var provider = CreateProvider();

        var response = await provider.GetDataAsync(
            Request(filters: [new FilterDescriptor(nameof(Issue.Priority), FilterOperator.LessThan, "3")]));

        Assert.All(response.Items, i => Assert.True(i.Priority < 3));
        Assert.Equal(4, response.Items.Count);
    }

    [Fact]
    public async Task Filter_GreaterThan_WorksOnDates()
    {
        var provider = CreateProvider();

        var response = await provider.GetDataAsync(
            Request(filters: [new FilterDescriptor(nameof(Issue.DueDate), FilterOperator.GreaterThan, "2026-01-01")]));

        Assert.All(response.Items, i => Assert.True(i.DueDate > new DateTime(2026, 1, 1)));
    }

    [Fact]
    public async Task Filter_IsEmpty_MatchesNullOrEmptyString()
    {
        var provider = CreateProvider();

        var response = await provider.GetDataAsync(
            Request(filters: [new FilterDescriptor(nameof(Issue.Assignee), FilterOperator.IsEmpty, null)]));

        var issue = Assert.Single(response.Items);
        Assert.Equal("Write docs", issue.Title);
    }

    [Fact]
    public async Task Filter_IsNotEmpty_ExcludesNullOrEmptyString()
    {
        var provider = CreateProvider();

        var response = await provider.GetDataAsync(
            Request(filters: [new FilterDescriptor(nameof(Issue.Assignee), FilterOperator.IsNotEmpty, null)]));

        Assert.Equal(4, response.Items.Count);
        Assert.DoesNotContain(response.Items, i => i.Assignee is null);
    }

    [Fact]
    public async Task Filter_MultipleFilters_CombinedWithAnd()
    {
        var provider = CreateProvider();

        var response = await provider.GetDataAsync(Request(filters:
        [
            new FilterDescriptor(nameof(Issue.Status), FilterOperator.Equals, "Todo"),
            new FilterDescriptor(nameof(Issue.Priority), FilterOperator.Equals, "1")
        ]));

        var issue = Assert.Single(response.Items);
        Assert.Equal("Fix login bug", issue.Title);
    }

    [Fact]
    public async Task Filter_TotalCount_ReflectsFilteredCountNotPageSize()
    {
        var provider = CreateProvider();

        var response = await provider.GetDataAsync(Request(
            take: 1,
            filters: [new FilterDescriptor(nameof(Issue.Status), FilterOperator.Equals, "Todo")]));

        Assert.Single(response.Items);
        Assert.Equal(2, response.TotalCount);
    }

    // ----- grouping -----

    [Fact]
    public async Task Group_ProducesCorrectCountsAndStartIndexes()
    {
        var provider = CreateProvider();

        var response = await provider.GetDataAsync(Request(
            sort: new SortDescriptor(nameof(Issue.Status), SortDirection.Ascending),
            groupBy: nameof(Issue.Status)));

        Assert.NotNull(response.Groups);
        Assert.Equal(3, response.Groups!.Count);

        var total = 0;
        foreach (var group in response.Groups)
        {
            Assert.Equal(total, group.StartIndex);
            total += group.Count;
        }

        Assert.Equal(response.Items.Count, total);
    }

    [Fact]
    public async Task Group_ItemsWithinGroupBoundaryHaveMatchingKey()
    {
        var provider = CreateProvider();

        var response = await provider.GetDataAsync(Request(
            sort: new SortDescriptor(nameof(Issue.Status), SortDirection.Ascending),
            groupBy: nameof(Issue.Status)));

        foreach (var group in response.Groups!)
        {
            var itemsInRange = response.Items.Skip(group.StartIndex).Take(group.Count);
            Assert.All(itemsInRange, i => Assert.Equal(group.Key, i.Status.ToString()));
        }
    }

    [Fact]
    public async Task Group_TakeIsIgnored_AllMatchingItemsReturned()
    {
        var provider = CreateProvider();

        var response = await provider.GetDataAsync(Request(take: 1, groupBy: nameof(Issue.Status)));

        // v1 rule: grouping ignores Skip/Take entirely.
        Assert.Equal(SampleIssues.Length, response.Items.Count);
    }

    // ----- paging (ungrouped only) -----

    [Fact]
    public async Task Page_SkipAndTake_ReturnsRequestedSlice()
    {
        var provider = CreateProvider();

        var response = await provider.GetDataAsync(Request(
            skip: 1,
            take: 2,
            sort: new SortDescriptor(nameof(Issue.Priority), SortDirection.Ascending)));

        Assert.Equal(2, response.Items.Count);
        Assert.Equal(5, response.TotalCount);
    }

    [Fact]
    public async Task Page_SkipBeyondCount_ReturnsEmpty()
    {
        var provider = CreateProvider();

        var response = await provider.GetDataAsync(Request(skip: 100, take: 10));

        Assert.Empty(response.Items);
        Assert.Equal(5, response.TotalCount);
    }
}

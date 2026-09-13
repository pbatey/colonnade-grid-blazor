using Bunit;
using ColonnadeGrid.Abstractions;
using ColonnadeGrid.Models;
using ColonnadeGrid.Tests.TestSupport;

namespace ColonnadeGrid.Tests.Components;

public class FilterEditorTests : BunitContext
{
    public FilterEditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private GridState? _appliedState;

    private IRenderedComponent<FilterGridHost> RenderGrid(IDataProvider<Ticket>? provider = null, params FilterDescriptor[] filters)
    {
        var state = filters.Aggregate(GridState.Create(FilterGridHost.ColumnIds), (s, f) => s.SetFilter(f));
        var cut = Render<FilterGridHost>(p =>
        {
            if (provider is null)
            {
                p.Add(x => x.Items, SampleTickets.Create());
            }
            else
            {
                p.Add(x => x.DataProvider, provider);
            }

            p.Add(x => x.State, state).Add(x => x.StateChanged, (GridState s) => _appliedState = s);
        });
        cut.WaitForState(() => cut.FindAll(".cg-column-menu-button").Count == FilterGridHost.ColumnIds.Length);
        return cut;
    }

    private static void OpenFilter(IRenderedComponent<FilterGridHost> cut, string columnId)
    {
        cut.Find($"[data-column-id='{columnId}'] .cg-column-menu-button").Click();
        cut.FindAll(".cg-column-menu-item").Single(item => item.TextContent.Contains("Filter by values")).Click();
        cut.WaitForState(() => cut.FindAll(".cg-filter-popover").Count == 1);
    }

    private static void Apply(IRenderedComponent<FilterGridHost> cut) =>
        cut.FindAll(".cg-filter-popover-actions button")[0].Click();

    private FilterDescriptor AppliedFilter(string propertyName)
    {
        Assert.NotNull(_appliedState);
        return Assert.Single(_appliedState!.Filters, f => f.PropertyName == propertyName);
    }

    private static List<string> OptionTexts(IRenderedComponent<FilterGridHost> cut) =>
        cut.FindAll(".cg-values-option")
            .Select(o => $"{o.QuerySelector(".cg-values-label")!.TextContent} {o.QuerySelector(".cg-values-count")?.TextContent}".Trim())
            .ToList();

    private static void SetOption(IRenderedComponent<FilterGridHost> cut, string label, bool isChecked) =>
        cut.FindAll(".cg-values-option")
            .Single(o => o.QuerySelector(".cg-values-label")!.TextContent == label)
            .QuerySelector("input")!
            .Change(isChecked);

    // ----- value lists -----

    [Fact]
    public void EnumColumn_ListsItsValuesWithCounts_AndAppliesIn()
    {
        var cut = RenderGrid();
        OpenFilter(cut, "Type");

        cut.WaitForAssertion(() => Assert.Equal(["Bug 2", "Feature 1", "Chore 1", "(empty) 1"], OptionTexts(cut)));
        SetOption(cut, "Feature", false);
        SetOption(cut, "(empty)", false);
        Apply(cut);

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".cg-body-row").Count));
        var filter = AppliedFilter("Type");
        Assert.Equal((FilterOperator.In, false), (filter.Operator, filter.IncludeEmpty));
        Assert.Equal(["Bug", "Chore"], filter.Values);
    }

    [Fact]
    public void ValueList_EverythingSelected_AppliesNoFilter()
    {
        var cut = RenderGrid(null, new FilterDescriptor("Type", FilterOperator.In, null, Values: ["Bug"]));
        OpenFilter(cut, "Type");

        cut.FindAll(".cg-filter-link").Single(b => b.TextContent == "Select all").Click();
        Apply(cut);

        cut.WaitForAssertion(() => Assert.Empty(_appliedState!.Filters));
    }

    [Fact]
    public void ValueList_NothingSelected_DisablesApply()
    {
        var cut = RenderGrid();
        OpenFilter(cut, "Type");

        cut.FindAll(".cg-filter-link").Single(b => b.TextContent == "Select none").Click();

        Assert.True(cut.FindAll(".cg-filter-popover-actions button")[0].HasAttribute("disabled"));
    }

    [Fact]
    public void TextColumnWithValuesKind_ListsDistinctValuesFromTheData()
    {
        var cut = RenderGrid();
        OpenFilter(cut, "Team");

        cut.WaitForAssertion(() => Assert.Equal(["Core 2", "Data 1", "Web 2"], OptionTexts(cut)));
    }

    // ----- number ranges -----

    [Fact]
    public void NumberColumn_SliderSpansTheData_AndAppliesBetween()
    {
        var cut = RenderGrid();
        OpenFilter(cut, "Estimate");

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".cg-range-thumb").Count));
        Assert.Equal(("0.5", "40.25"), (cut.Find(".cg-range-thumb").GetAttribute("min"), cut.Find(".cg-range-thumb").GetAttribute("max")));
        Assert.Equal(["0.5", "40.25"], cut.FindAll(".cg-range-limits span").Select(s => s.TextContent));

        cut.Find("input[aria-label='From']").Change("1");
        cut.Find("input[aria-label='To']").Change("10");
        Apply(cut);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".cg-body-row").Count));
        Assert.Equal(new FilterDescriptor("Estimate", FilterOperator.Between, "1", "10"), AppliedFilter("Estimate") with { Values = null });
    }

    [Fact]
    public void NumberRange_BoundAtTheDataLimit_IsLeftOpen()
    {
        var cut = RenderGrid();
        OpenFilter(cut, "Estimate");

        cut.Find("input[aria-label='From']").Change("0.5");
        cut.Find("input[aria-label='To']").Change("8");
        Apply(cut);

        cut.WaitForAssertion(() => Assert.Equal((null, "8"), (AppliedFilter("Estimate").Value, AppliedFilter("Estimate").ValueTo)));
    }

    [Fact]
    public void NumberRange_FromAboveTo_DisablesApply()
    {
        var cut = RenderGrid();
        OpenFilter(cut, "Estimate");

        cut.Find("input[aria-label='From']").Change("10");
        cut.Find("input[aria-label='To']").Change("2");

        Assert.True(cut.FindAll(".cg-filter-popover-actions button")[0].HasAttribute("disabled"));
    }

    // ----- durations -----

    [Fact]
    public void DurationColumn_LongDurations_UseMonthsWeeksAndDays()
    {
        var cut = RenderGrid();
        OpenFilter(cut, "Spent");

        cut.WaitForAssertion(() => Assert.Equal(["30m", "1mo 2w 1d"], cut.FindAll(".cg-range-limits span").Select(s => s.TextContent)));
        cut.Find("input[aria-label='To days']").Change("2");
        Apply(cut);

        cut.WaitForAssertion(() => Assert.Equal((null, "2.00:00:00"), (AppliedFilter("Spent").Value, AppliedFilter("Spent").ValueTo)));
    }

    [Fact]
    public void DurationColumn_ShortDurations_UseHoursAndMinutes()
    {
        var cut = RenderGrid(null, new FilterDescriptor("Estimate", FilterOperator.Between, null, "1.5"));
        OpenFilter(cut, "Spent");

        cut.WaitForAssertion(() => Assert.Equal(["30m", "2h 15m"], cut.FindAll(".cg-range-limits span").Select(s => s.TextContent)));
        cut.Find("input[aria-label='From hours']").Change("1");
        cut.Find("input[aria-label='From minutes']").Change("5");
        Apply(cut);

        cut.WaitForAssertion(() => Assert.Equal(("01:05:00", null), (AppliedFilter("Spent").Value, AppliedFilter("Spent").ValueTo)));
    }

    // ----- dates -----

    private static List<string> PresetLabels(IRenderedComponent<FilterGridHost> cut) =>
        cut.FindAll(".cg-date-option span").Select(s => s.TextContent).ToList();

    [Fact]
    public void DateColumn_ShowsOnlyPresetsThatChangeTheResult_AndAppliesWithinLast()
    {
        var cut = RenderGrid();
        OpenFilter(cut, "Opened");

        // The data reaches back 800 days, so "Last 5 years" would include everything.
        cut.WaitForAssertion(() => Assert.Equal(
            ["Last 7 days", "Last 30 days", "Last 90 days", "Last 6 months", "Last year", "Last 2 years", "Custom range"],
            PresetLabels(cut)));

        cut.FindAll(".cg-date-option").Single(o => o.TextContent.Contains("Last 90 days")).QuerySelector("input")!.Change(true);
        Apply(cut);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".cg-body-row").Count));
        Assert.Equal((FilterOperator.WithinLast, "P90D"), (AppliedFilter("Opened").Operator, AppliedFilter("Opened").Value));
    }

    [Fact]
    public void DatePresets_FollowTheOtherColumnsFilters()
    {
        // Estimates of 8 and up leave tickets opened 400 and 800 days ago.
        var cut = RenderGrid(null, new FilterDescriptor("Estimate", FilterOperator.Between, "8", null));
        OpenFilter(cut, "Opened");

        cut.WaitForAssertion(() => Assert.Equal(["Last 2 years", "Custom range"], PresetLabels(cut)));
    }

    [Fact]
    public void DateCustomRange_CoversWholeDays()
    {
        var cut = RenderGrid();
        OpenFilter(cut, "Opened");

        cut.FindAll(".cg-date-option").Single(o => o.TextContent.Contains("Custom range")).QuerySelector("input")!.Change(true);
        cut.Find("input[aria-label='From date']").Change("2026-01-01");
        cut.Find("input[aria-label='To date']").Change("2026-01-31");
        Apply(cut);

        cut.WaitForAssertion(() => Assert.Equal(
            (FilterOperator.Between, "2026-01-01T00:00:00.0000000", "2026-01-31T23:59:59.9999999"),
            (AppliedFilter("Opened").Operator, AppliedFilter("Opened").Value, AppliedFilter("Opened").ValueTo)));
    }

    [Fact]
    public void DateEditor_ReopensToTheAppliedPreset()
    {
        var cut = RenderGrid(null, new FilterDescriptor("Opened", FilterOperator.WithinLast, "P30D"));
        OpenFilter(cut, "Opened");

        var checkedOption = cut.FindAll(".cg-date-option").Single(o => o.QuerySelector("input")!.HasAttribute("checked"));
        Assert.Contains("Last 30 days", checkedOption.TextContent);
    }

    // ----- stats -----

    [Fact]
    public void StatsRequest_LeavesOutTheColumnsOwnFilter()
    {
        var provider = new RecordingStatsProvider<Ticket>(SampleTickets.Create());
        var cut = RenderGrid(provider,
            new FilterDescriptor("Estimate", FilterOperator.Between, "1", null),
            new FilterDescriptor("Type", FilterOperator.In, null, Values: ["Bug"]));

        OpenFilter(cut, "Estimate");

        cut.WaitForState(() => provider.StatsRequests.Count == 1);
        var request = provider.StatsRequests[0];
        Assert.Equal(("Estimate", false), (request.PropertyName, request.IncludeValueCounts));
        Assert.Equal(["Type"], request.Filters.Select(f => f.PropertyName));
    }

    [Fact]
    public void WithoutStats_EditorsStillWork_WithoutDataLimits()
    {
        var provider = new RecordingDataProvider<Ticket>(SampleTickets.Create());
        var cut = RenderGrid(provider);

        OpenFilter(cut, "Type");
        Assert.Equal(["Bug", "Feature", "Chore", "(empty)"], OptionTexts(cut));
        cut.Find(".cg-dropdown-backdrop").Click();

        OpenFilter(cut, "Estimate");
        Assert.Empty(cut.FindAll(".cg-range-thumb"));
        Assert.Single(cut.FindAll("input[aria-label='From']"));
        cut.Find(".cg-dropdown-backdrop").Click();

        // A text column set to list values has nothing to list, so it falls back to text.
        OpenFilter(cut, "Team");
        Assert.Single(cut.FindAll(".cg-filter-operator"));
    }
}

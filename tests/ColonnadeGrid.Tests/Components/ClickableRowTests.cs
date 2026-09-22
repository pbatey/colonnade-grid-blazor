using Bunit;
using ColonnadeGrid.Tests.TestSupport;
using Microsoft.AspNetCore.Components;

namespace ColonnadeGrid.Tests.Components;

public class ClickableRowTests : BunitContext
{
    public ClickableRowTests()
    {
        // Match ColonnadeGridTests: JS interop is a progressive enhancement the
        // component tolerates failing, so loose mode lets it no-op in tests.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void WithoutOnRowClick_RowsAreNotClickable()
    {
        var cut = Render<IssueGridHost>(p => p.Add(x => x.Items, SampleIssues.Create()));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        var row = cut.Find(".cg-body-row");
        Assert.DoesNotContain("cg-row-clickable", row.ClassList);
        Assert.Equal("row", row.GetAttribute("role"));
        Assert.False(row.HasAttribute("tabindex"));
    }

    [Fact]
    public void WithOnRowClick_RowsAreMarkedClickableAndFocusable()
    {
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.Create())
            .Add(x => x.OnRowClick, EventCallback.Factory.Create<Issue>(this, _ => { })));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        var row = cut.Find(".cg-body-row");
        Assert.Contains("cg-row-clickable", row.ClassList);
        Assert.Equal("button", row.GetAttribute("role"));
        Assert.Equal("0", row.GetAttribute("tabindex"));
    }

    [Fact]
    public void ClickingRow_RaisesOnRowClickWithThatRowsItem()
    {
        var issues = SampleIssues.Create();
        Issue? clicked = null;
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, issues)
            .Add(x => x.OnRowClick, EventCallback.Factory.Create<Issue>(this, i => clicked = i)));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        // The second body row's item (row order matches the source order with no sort).
        cut.FindAll(".cg-body-row")[1].Click();

        Assert.Same(issues[1], clicked);
    }

    [Fact]
    public void PressingEnterOnRow_RaisesOnRowClick()
    {
        var issues = SampleIssues.Create();
        Issue? clicked = null;
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, issues)
            .Add(x => x.OnRowClick, EventCallback.Factory.Create<Issue>(this, i => clicked = i)));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        cut.FindAll(".cg-body-row")[0].KeyDown("Enter");

        Assert.Same(issues[0], clicked);
    }

    [Fact]
    public void TogglingSelectionCheckbox_ChangesSelectionWithoutRaisingOnRowClick()
    {
        var clicks = 0;
        var selected = new HashSet<string>();
        var cut = Render<IssueGridHost>(p => p
            .Add(x => x.Items, SampleIssues.Create())
            .Add(x => x.EnableRowSelection, true)
            .Add(x => x.RowKey, i => i.Title)
            .Add(x => x.SelectedKeys, selected)
            .Add(x => x.SelectedKeysChanged, EventCallback.Factory.Create<IReadOnlySet<string>>(this, s => selected = new HashSet<string>(s)))
            .Add(x => x.OnRowClick, EventCallback.Factory.Create<Issue>(this, _ => clicks++)));
        cut.WaitForState(() => cut.FindAll(".cg-body-row").Count == 4);

        // Toggling a row's selection checkbox selects the row but must not
        // navigate: the select cell stops clicks from reaching the row handler.
        cut.Find(".cg-body-row .cg-select-cell input[type=checkbox]").Change(true);

        Assert.Single(selected);
        Assert.Equal(0, clicks);
    }
}

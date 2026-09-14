using System.Linq.Expressions;
using ColonnadeGrid.Internal;

namespace ColonnadeGrid.Tests.Internal;

public class ColumnAccessorsTests
{
    private sealed class Owner
    {
        public string Name { get; set; } = "";
    }

    private sealed class Widget
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public Owner Owner { get; set; } = new();
    }

    // A new expression tree on every call, as a host's markup builds on every render.
    private static Expression<Func<Widget, string>> NameField() => w => w.Name;

    [Fact]
    public void DirectMemberAccess_BuiltTwice_ReusesOneCompiledAccessor()
    {
        var first = NameField();
        var second = NameField();
        Assert.NotSame(first, second);

        Assert.Same(ColumnAccessors<Widget, string>.For(first), ColumnAccessors<Widget, string>.For(second));
    }

    [Fact]
    public void ValueTypeMember_ReadsTheValue()
    {
        var accessor = ColumnAccessors<Widget, int>.For(w => w.Count);

        Assert.Equal(3, accessor(new Widget { Count = 3 }));
    }

    [Fact]
    public void NestedMemberAccess_ReadsTheNestedValue_NotTheSameNamedDirectMember()
    {
        var direct = ColumnAccessors<Widget, string>.For(NameField());
        var nested = ColumnAccessors<Widget, string>.For(w => w.Owner.Name);
        var widget = new Widget { Name = "widget", Owner = new Owner { Name = "owner" } };

        Assert.Equal("widget", direct(widget));
        Assert.Equal("owner", nested(widget));
    }

    [Fact]
    public void SameExpressionInstance_ReusesThePreviousAccessor()
    {
        Expression<Func<Widget, string>> field = w => w.Owner.Name;
        var accessor = ColumnAccessors<Widget, string>.For(field);

        Assert.Same(accessor, ColumnAccessors<Widget, string>.For(field, (field, accessor)));
    }
}

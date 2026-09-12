using ColonnadeGrid.Internal;

namespace ColonnadeGrid.Tests.Internal;

public class PropertyNameExtractorTests
{
    private sealed class Widget
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public DateTime? DueDate { get; set; }
    }

    [Fact]
    public void ReferenceTypeProperty_ReturnsMemberName()
    {
        var name = PropertyNameExtractor.GetPropertyName<Widget, string>(w => w.Name);

        Assert.Equal("Name", name);
    }

    [Fact]
    public void ValueTypeProperty_BoxedAsObject_UnwrapsConvertAndReturnsMemberName()
    {
        // Boxing a value-type property into an `object`-typed lambda forces the
        // compiler to wrap the member access in UnaryExpression(Convert) —
        // this is exactly the case PropertyNameExtractor must handle.
        System.Linq.Expressions.Expression<Func<Widget, object>> expr = w => w.Count;

        var name = PropertyNameExtractor.GetPropertyName(expr);

        Assert.Equal("Count", name);
    }

    [Fact]
    public void NullableValueTypeProperty_ReturnsMemberName()
    {
        var name = PropertyNameExtractor.GetPropertyName<Widget, DateTime?>(w => w.DueDate);

        Assert.Equal("DueDate", name);
    }

    [Fact]
    public void NonMemberExpression_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => PropertyNameExtractor.GetPropertyName<Widget, int>(w => w.Count + 1));

        Assert.Contains("simple property access", ex.Message);
    }

    [Fact]
    public void MethodCallExpression_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => PropertyNameExtractor.GetPropertyName<Widget, string>(w => w.Name.ToUpper()));
    }

    [Fact]
    public void ConstantExpression_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => PropertyNameExtractor.GetPropertyName<Widget, int>(w => 42));
    }
}

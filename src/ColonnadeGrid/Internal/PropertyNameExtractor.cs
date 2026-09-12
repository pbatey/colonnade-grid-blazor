using System.Linq.Expressions;
using System.Reflection;

namespace ColonnadeGrid.Internal;

/// <summary>
/// Extracts a stable property/field name from a simple member-access lambda
/// expression (e.g. <c>x => x.Title</c>), used to derive a column's
/// <c>PropertyName</c> from its <c>Field</c> expression.
/// </summary>
internal static class PropertyNameExtractor
{
    /// <summary>
    /// Returns the member name accessed by <paramref name="expression"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="expression"/> is not a simple property/field access.
    /// </exception>
    public static string GetPropertyName<TItem, TProp>(Expression<Func<TItem, TProp>> expression)
    {
        var body = expression.Body;

        // Value-type properties (and any case where TProp differs from the
        // member's exact type) get wrapped by the compiler in a
        // UnaryExpression(Convert) node, e.g. `x => x.Count` typed as
        // Expression<Func<TItem, object>> — unwrap it before inspecting the
        // member access itself.
        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            body = unary.Operand;
        }

        if (body is MemberExpression { Member: PropertyInfo or FieldInfo } member)
        {
            return member.Member.Name;
        }

        throw new ArgumentException(
            $"Field must be a simple property access, e.g. 'x => x.Name'. Got: '{expression}'.",
            nameof(expression));
    }
}

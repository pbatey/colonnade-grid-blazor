using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace ColonnadeGrid.Internal;

/// <summary>
/// Compiled <c>Field</c> expressions for <c>GridColumn</c>. A column's
/// parameters are set on every grid render, each time with a new expression
/// tree built by the host's markup, and compiling one is slow — especially
/// under WebAssembly's interpreter. A direct property or field access
/// (<c>x =&gt; x.Title</c>) is cached by its member, so it's compiled once
/// per member for the app's lifetime.
/// </summary>
internal static class ColumnAccessors<TItem, TProp>
{
    private static readonly ConcurrentDictionary<MemberInfo, Func<TItem, TProp>> ByMember = new();

    /// <summary>Returns the compiled accessor for <paramref name="field"/>, or <paramref name="previous"/>'s when <paramref name="field"/> is the same expression instance.</summary>
    public static Func<TItem, TProp> For(
        Expression<Func<TItem, TProp>> field,
        (Expression<Func<TItem, TProp>> Field, Func<TItem, TProp> Accessor)? previous = null)
    {
        if (previous is { } p && ReferenceEquals(p.Field, field))
        {
            return p.Accessor;
        }

        return GetDirectMember(field) is { } member
            ? ByMember.GetOrAdd(member, _ => field.Compile())
            : field.Compile();
    }

    /// <summary>The member <paramref name="field"/> reads straight off its parameter, or <c>null</c> for anything else (such as <c>x =&gt; x.Nested.Title</c>).</summary>
    private static MemberInfo? GetDirectMember(Expression<Func<TItem, TProp>> field)
    {
        var body = field.Body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
            ? unary.Operand
            : field.Body;

        return body is MemberExpression { Expression: ParameterExpression, Member: PropertyInfo or FieldInfo } member
               && field.Body.Type == typeof(TProp)
            ? member.Member
            : null;
    }
}

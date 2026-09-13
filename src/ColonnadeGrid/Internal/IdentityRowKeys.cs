using System.Globalization;
using System.Runtime.CompilerServices;

namespace ColonnadeGrid.Internal;

/// <summary>
/// Row keys for items without a <c>RowKey</c>: each object instance gets its
/// own key, the same one for as long as it lives. Keys come from a counter
/// rather than <see cref="RuntimeHelpers.GetHashCode(object)"/>, which isn't
/// unique — with a few thousand rows, two would likely share a key, and
/// selecting one would select both. The table holds its items weakly, so it
/// doesn't keep old rows alive.
/// </summary>
internal static class IdentityRowKeys
{
    private static readonly ConditionalWeakTable<object, string> Keys = new();
    private static long _lastKey;

    public static string For(object item) =>
        Keys.GetValue(item, static _ => Interlocked.Increment(ref _lastKey).ToString(CultureInfo.InvariantCulture));
}

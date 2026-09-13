namespace ColonnadeGrid.Models;

/// <summary>Which filter editor a column shows.</summary>
public enum FilterKind
{
    /// <summary>An operator (contains, equals, ...) and a text value.</summary>
    Text,

    /// <summary>A checklist of values to include. The default for enums and booleans.</summary>
    Values,

    /// <summary>A from–to range with a slider. The default for numeric types.</summary>
    Number,

    /// <summary>Relative presets ("last 30 days") or a custom date range. The default for <see cref="DateTime"/>, <see cref="DateTimeOffset"/>, and <see cref="DateOnly"/>.</summary>
    Date,

    /// <summary>A from–to duration range with a slider. The default for <see cref="TimeSpan"/>.</summary>
    Duration
}

/// <summary>Chooses a column's default <see cref="FilterKind"/> from its property type.</summary>
public static class FilterKinds
{
    /// <summary>The default filter editor for a property of type <paramref name="propertyType"/> (nullable or not).</summary>
    public static FilterKind ForType(Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (type.IsEnum || type == typeof(bool))
        {
            return FilterKind.Values;
        }

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly))
        {
            return FilterKind.Date;
        }

        if (type == typeof(TimeSpan))
        {
            return FilterKind.Duration;
        }

        return Type.GetTypeCode(type) switch
        {
            TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32
                or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal
                => FilterKind.Number,
            _ => FilterKind.Text
        };
    }
}

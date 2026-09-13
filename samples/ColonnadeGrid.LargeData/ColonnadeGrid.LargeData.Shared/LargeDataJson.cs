using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColonnadeGrid.LargeData.Shared;

/// <summary>JSON settings shared by the API and client, so enums travel as readable names in both directions.</summary>
public static class LargeDataJson
{
    public static JsonSerializerOptions Options { get; } = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

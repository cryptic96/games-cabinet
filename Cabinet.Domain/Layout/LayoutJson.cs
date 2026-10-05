using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cabinet.Domain.Layout;

/// <summary>Turns a layout into JSON with one fixed set of options so the same layout always gives the same bytes.</summary>
public static class LayoutJson
{
    /// <summary>Web defaults (camelCase names), enums as camelCase strings, absent values omitted, properties in declaration order.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static readonly JsonSerializerOptions IndentedOptions = new(Options) { WriteIndented = true };

    /// <summary>Serialises the layout, optionally indented for reading.</summary>
    public static string Serialize(CabinetLayout layout, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(layout);

        return JsonSerializer.Serialize(layout, indented ? IndentedOptions : Options);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Cabinet.Repository.Storage;

/// <summary>The serializer settings shared by the stored files: camel-case names, enum names only, and no null where none is allowed.</summary>
internal static class StoredJson
{
    /// <summary>Creates the options for a stored file.</summary>
    /// <param name="requiredProperties">The JSON names, by type, of the properties that must be present in the file.</param>
    public static JsonSerializerOptions CreateOptions(IReadOnlyDictionary<Type, string[]> requiredProperties)
    {
        ArgumentNullException.ThrowIfNull(requiredProperties);

        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo => RequireProperties(typeInfo, requiredProperties));

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
            RespectNullableAnnotations = true,
            TypeInfoResolver = resolver,
        };
        options.Converters.Add(new StrictEnumConverterFactory());

        return options;
    }

    private static void RequireProperties(JsonTypeInfo typeInfo, IReadOnlyDictionary<Type, string[]> requiredProperties)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object || !requiredProperties.TryGetValue(typeInfo.Type, out var names))
        {
            return;
        }

        foreach (var property in typeInfo.Properties.Where(property => names.Contains(property.Name, StringComparer.Ordinal)))
        {
            property.IsRequired = true;
        }
    }
}

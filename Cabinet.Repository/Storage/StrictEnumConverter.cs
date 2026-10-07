using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cabinet.Repository.Storage;

/// <summary>
/// Reads and writes enum values as the camel-case names the app writes, and nothing else: an integer, a number in a string,
/// a name in another letter case, a list of names or an unknown name is content the app did not write and is refused.
/// </summary>
internal sealed class StrictEnumConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter?)Activator.CreateInstance(typeof(StrictEnumConverter<>).MakeGenericType(typeToConvert));
}

/// <summary>Converts one enum type to and from its camel-case names.</summary>
/// <typeparam name="T">The enum type.</typeparam>
internal sealed class StrictEnumConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    private static readonly Dictionary<string, T> ByName = Enum.GetValues<T>()
        .ToDictionary(value => JsonNamingPolicy.CamelCase.ConvertName(value.ToString()), value => value, StringComparer.Ordinal);

    private static readonly Dictionary<T, string> ByValue = ByName.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String
            || reader.GetString() is not { } name
            || !ByName.TryGetValue(name, out var value))
        {
            throw new JsonException("The value is not one of the names written for this field.");
        }

        return value;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (!ByValue.TryGetValue(value, out var name))
        {
            throw new JsonException("The value has no name to write.");
        }

        writer.WriteStringValue(name);
    }
}

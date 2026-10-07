using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;

namespace Cabinet.Repository.Bgg;

/// <summary>Raised when an answer is well-formed enough to read but is not a collection answer.</summary>
/// <param name="message">What is wrong, in words that carry no content of the answer.</param>
public sealed class BggAnswerException(string message) : Exception(message);

/// <summary>What one collection answer held.</summary>
/// <param name="TotalItems">The number of items the answer declares, or null when it declares none.</param>
/// <param name="Items">The owned items in the answer.</param>
/// <param name="SkippedItems">How many owned entries were left out because they carried no usable identifier.</param>
public sealed record ParsedCollection(int? TotalItems, IReadOnlyList<SnapshotItem> Items, int SkippedItems);

/// <summary>Reads a BGG collection answer into stored items. Everything in the answer is treated as untrusted.</summary>
public static class BggCollectionParser
{
    /// <summary>The longest title that is kept.</summary>
    public const int MaxTitleLength = 300;

    /// <summary>The longest location that is kept.</summary>
    public const int MaxLocationLength = 200;

    private const int MaxDocumentCharacters = 20_000_000;

    /// <summary>
    /// Reads the answer with DTDs prohibited, no resolver and a size cap. An answer whose root is not the items element
    /// is rejected. Only entries marked as owned are kept; names are read as element text. An entry without a usable
    /// identifier is skipped and counted while the rest of the answer is kept.
    /// </summary>
    /// <param name="body">The answer body.</param>
    /// <param name="kind">The kind every returned item gets; the caller knows it from the request it made.</param>
    /// <param name="includePrivateInfo">Whether to read the private inventory location; when false it is never read.</param>
    /// <exception cref="BggAnswerException">The root is not an items element.</exception>
    /// <exception cref="XmlException">The body is not well-formed XML.</exception>
    public static ParsedCollection Parse(Stream body, ItemKind kind, bool includePrivateInfo)
    {
        ArgumentNullException.ThrowIfNull(body);

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxDocumentCharacters,
            MaxCharactersFromEntities = 0,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };

        using var reader = XmlReader.Create(body, settings);
        var document = XDocument.Load(reader);

        if (document.Root is not { Name.LocalName: "items" } root)
        {
            throw new BggAnswerException("The answer is not a collection.");
        }

        var items = new List<SnapshotItem>();
        var skipped = 0;

        foreach (var entry in root.Elements("item").Where(IsOwned))
        {
            if (TryReadItem(entry, kind, includePrivateInfo) is { } item)
            {
                items.Add(item);
            }
            else
            {
                skipped++;
            }
        }

        return new ParsedCollection(ReadWholeNumber((string?)root.Attribute("totalitems")), items, skipped);
    }

    /// <summary>
    /// Cleans a title the way every stored title is cleaned: control characters (the C0 and C1 ranges and DEL), the line and
    /// paragraph separators and the bidirectional override, embedding and isolate characters are removed, the ends are
    /// trimmed and the length is capped. Nothing else about the title is changed.
    /// </summary>
    /// <param name="title">The title as BGG returned it.</param>
    public static string CleanTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);

        return Cap(RemoveControlCharacters(title).Trim(), MaxTitleLength);
    }

    private static bool IsOwned(XElement item) => (string?)item.Element("status")?.Attribute("own") == "1";

    private static SnapshotItem? TryReadItem(XElement item, ItemKind kind, bool includePrivateInfo)
    {
        if (!long.TryParse((string?)item.Attribute("collid"), NumberStyles.None, CultureInfo.InvariantCulture, out var collectionId)
            || !int.TryParse((string?)item.Attribute("objectid"), NumberStyles.None, CultureInfo.InvariantCulture, out var gameId))
        {
            return null;
        }

        return new SnapshotItem(
            collectionId,
            gameId,
            CleanTitle(item.Element("name")?.Value ?? string.Empty),
            kind,
            ReadWholeNumber((string?)item.Element("yearpublished")),
            ReadDimensions(item),
            includePrivateInfo ? ReadLocation(item) : null);
    }

    private static VersionDimensions? ReadDimensions(XElement item)
    {
        var version = item.Element("version") is { } wrapper ? wrapper.Element("item") ?? wrapper : null;

        if (version is null
            || ReadDecimal(version, "width") is not { } width
            || ReadDecimal(version, "length") is not { } length
            || ReadDecimal(version, "depth") is not { } depth)
        {
            return null;
        }

        return new VersionDimensions(width, length, depth);
    }

    private static double? ReadDecimal(XElement version, string name) =>
        double.TryParse((string?)version.Element(name)?.Attribute("value"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
        && double.IsFinite(value)
            ? value
            : null;

    private static string? ReadLocation(XElement item)
    {
        var text = (string?)item.Element("privateinfo")?.Attribute("inventorylocation");

        if (text is null)
        {
            return null;
        }

        var cleaned = Cap(RemoveControlCharacters(text).Trim(), MaxLocationLength);

        return cleaned.Length == 0 ? null : cleaned;
    }

    private static bool IsInvisibleControl(char character) =>
        char.IsControl(character)
        || character is '\u2028' or '\u2029'
        || character is >= '\u202A' and <= '\u202E'
        || character is >= '\u2066' and <= '\u2069';

    private static string RemoveControlCharacters(string text) =>
        text.Any(IsInvisibleControl) ? string.Concat(text.Where(character => !IsInvisibleControl(character))) : text;

    private static string Cap(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        var length = char.IsHighSurrogate(text[maxLength - 1]) ? maxLength - 1 : maxLength;

        return text[..length].TrimEnd();
    }

    private static int? ReadWholeNumber(string? text) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ? value : null;
}

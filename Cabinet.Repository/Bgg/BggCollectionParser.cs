using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;

namespace Cabinet.Repository.Bgg;

/// <summary>Raised when an answer is well-formed enough to read but is not a collection answer, or an entry in it is unusable.</summary>
/// <param name="message">What is wrong, in words that carry no content of the answer.</param>
public sealed class BggAnswerException(string message) : Exception(message);

/// <summary>What one collection answer held.</summary>
/// <param name="TotalItems">The number of items the answer declares, or null when it declares none.</param>
/// <param name="Items">The owned items in the answer.</param>
public sealed record ParsedCollection(int? TotalItems, IReadOnlyList<SnapshotItem> Items);

/// <summary>Reads a BGG collection answer into stored items. Everything in the answer is treated as untrusted.</summary>
public static class BggCollectionParser
{
    private const int MaxDocumentCharacters = 20_000_000;

    /// <summary>
    /// Reads the answer with DTDs prohibited, no resolver and a size cap. An answer whose root is not the items element
    /// is rejected. Only entries marked as owned are kept; names are read as element text.
    /// </summary>
    /// <param name="body">The answer body.</param>
    /// <param name="kind">The kind every returned item gets; the caller knows it from the request it made.</param>
    /// <exception cref="BggAnswerException">The root is not an items element, or an entry has no usable identifier.</exception>
    /// <exception cref="XmlException">The body is not well-formed XML.</exception>
    public static ParsedCollection Parse(Stream body, ItemKind kind)
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

        var items = root.Elements("item")
            .Where(IsOwned)
            .Select(item => ToSnapshotItem(item, kind))
            .ToList();

        return new ParsedCollection(ReadWholeNumber((string?)root.Attribute("totalitems")), items);
    }

    private static bool IsOwned(XElement item) => (string?)item.Element("status")?.Attribute("own") == "1";

    private static SnapshotItem ToSnapshotItem(XElement item, ItemKind kind)
    {
        if (!long.TryParse((string?)item.Attribute("collid"), NumberStyles.None, CultureInfo.InvariantCulture, out var collectionId)
            || !int.TryParse((string?)item.Attribute("objectid"), NumberStyles.None, CultureInfo.InvariantCulture, out var gameId))
        {
            throw new BggAnswerException("An entry has no usable identifier.");
        }

        var title = item.Element("name")?.Value.Trim() ?? string.Empty;

        return new SnapshotItem(
            collectionId,
            gameId,
            title,
            kind,
            ReadWholeNumber((string?)item.Element("yearpublished")),
            Dimensions: null,
            Location: null);
    }

    private static int? ReadWholeNumber(string? text) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ? value : null;
}

using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Repository.Images;

namespace Cabinet.Repository.Bgg;

/// <summary>What one details answer held.</summary>
/// <param name="Games">The details of every game the answer described, by game identifier.</param>
/// <param name="SkippedItems">How many entries were left out because they carried no usable identifier.</param>
public sealed record ParsedThings(IReadOnlyDictionary<int, GameDetails> Games, int SkippedItems);

/// <summary>Reads a BGG details answer into stored details. Everything in the answer is treated as untrusted.</summary>
public static class BggThingParser
{
    /// <summary>The most designers, mechanics or expanded games kept for one game.</summary>
    public const int MaxListEntries = 20;

    /// <summary>The most family links kept for one game.</summary>
    public const int MaxFamilyEntries = 40;

    private const int MaxDocumentCharacters = 20_000_000;
    private const string DesignerLink = "boardgamedesigner";
    private const string MechanicLink = "boardgamemechanic";
    private const string ExpansionLink = "boardgameexpansion";
    private const string FamilyLinkType = "boardgamefamily";

    /// <summary>
    /// Reads the answer with DTDs prohibited, no resolver and a size cap. An answer whose root is not the items element is
    /// rejected. An entry without a usable identifier is skipped and counted while the rest of the answer is kept. Only the
    /// designer, mechanic, family and inbound expansion links are read; any other link is ignored.
    /// </summary>
    /// <param name="body">The answer body.</param>
    /// <param name="enrichedAtUtc">The time stored on every game's details.</param>
    /// <exception cref="BggAnswerException">The root is not an items element.</exception>
    /// <exception cref="XmlException">The body is not well-formed XML.</exception>
    public static ParsedThings Parse(Stream body, DateTimeOffset enrichedAtUtc)
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
            throw new BggAnswerException("The answer is not a list of games.");
        }

        var games = new Dictionary<int, GameDetails>();
        var skipped = 0;

        foreach (var entry in root.Elements("item"))
        {
            if (!int.TryParse((string?)entry.Attribute("id"), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                skipped++;

                continue;
            }

            games.TryAdd(id, ReadDetails(entry, enrichedAtUtc));
        }

        return new ParsedThings(games, skipped);
    }

    private static GameDetails ReadDetails(XElement item, DateTimeOffset enrichedAtUtc)
    {
        var ratings = item.Element("statistics")?.Element("ratings");

        return new GameDetails(
            enrichedAtUtc,
            ReadCount(item, "minplayers"),
            ReadCount(item, "maxplayers"),
            ReadCount(item, "playingtime"),
            ReadCount(item, "minplaytime"),
            ReadCount(item, "maxplaytime"),
            ReadCount(item, "minage"),
            ReadRating(ratings, "averageweight"),
            ReadRating(ratings, "average"),
            ReadRating(ratings, "bayesaverage"),
            ReadNames(item, DesignerLink),
            ReadNames(item, MechanicLink),
            ReadExpandedGames(item),
            ArtUrl.Canonical(item.Element("image")?.Value?.Trim()),
            Families: ReadFamilies(item),
            DetailsVersion: GameDetails.CurrentDetailsVersion);
    }

    private static IReadOnlyList<FamilyLink> ReadFamilies(XElement item)
    {
        var families = new List<FamilyLink>();

        foreach (var link in item.Elements("link"))
        {
            if ((string?)link.Attribute("type") != FamilyLinkType
                || !int.TryParse((string?)link.Attribute("id"), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                || families.Any(family => family.Id == id))
            {
                continue;
            }

            var name = BggCollectionParser.CleanTitle((string?)link.Attribute("value") ?? string.Empty);

            if (name.Length == 0)
            {
                continue;
            }

            families.Add(new FamilyLink(id, name));

            if (families.Count == MaxFamilyEntries)
            {
                break;
            }
        }

        return families;
    }

    private static int? ReadCount(XElement item, string name) =>
        int.TryParse((string?)item.Element(name)?.Attribute("value"), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
        && value > 0
            ? value
            : null;

    private static double? ReadRating(XElement? ratings, string name) =>
        double.TryParse((string?)ratings?.Element(name)?.Attribute("value"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
        && double.IsFinite(value)
        && value > 0
            ? value
            : null;

    private static IReadOnlyList<string> ReadNames(XElement item, string linkType) =>
        [
            .. item.Elements("link")
                .Where(link => (string?)link.Attribute("type") == linkType)
                .Select(link => BggCollectionParser.CleanTitle((string?)link.Attribute("value") ?? string.Empty))
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Take(MaxListEntries),
        ];

    private static IReadOnlyList<BaseGameRef> ReadExpandedGames(XElement item)
    {
        var references = new List<BaseGameRef>();

        foreach (var link in item.Elements("link"))
        {
            if ((string?)link.Attribute("type") != ExpansionLink
                || (string?)link.Attribute("inbound") != "true"
                || !int.TryParse((string?)link.Attribute("id"), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                || references.Any(reference => reference.BggId == id))
            {
                continue;
            }

            references.Add(new BaseGameRef(id, BggCollectionParser.CleanTitle((string?)link.Attribute("value") ?? string.Empty)));

            if (references.Count == MaxListEntries)
            {
                break;
            }
        }

        return references;
    }
}

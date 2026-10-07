using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace Cabinet.FakeBgg;

/// <summary>The parameters of a collection request that change what the fake answers.</summary>
/// <param name="OwnOnly">Whether only owned entries are wanted.</param>
/// <param name="Subtype">When set, only entries of this subtype are wanted.</param>
/// <param name="ExcludeSubtype">When set, entries of this subtype are left out.</param>
/// <param name="Version">Whether the selected version, with its box dimensions, is wanted.</param>
/// <param name="ShowPrivate">Whether private inventory information is wanted.</param>
/// <param name="Stats">Whether player counts and play times are wanted.</param>
public sealed record CollectionQuery(
    bool OwnOnly,
    string? Subtype,
    string? ExcludeSubtype,
    bool Version,
    bool ShowPrivate,
    bool Stats = true)
{
    /// <summary>The subtype BoardGameGeek gives expansions.</summary>
    public const string ExpansionSubtype = "boardgameexpansion";

    /// <summary>Reads the parameters from the query of an incoming request.</summary>
    /// <param name="query">The request's query values.</param>
    public static CollectionQuery Parse(IQueryCollection query) => FromPairs(query);

    /// <summary>Reads the parameters from a query string, with or without its leading question mark.</summary>
    /// <param name="queryString">The raw query string.</param>
    public static CollectionQuery Parse(string queryString) => FromPairs(QueryHelpers.ParseQuery(queryString));

    private static CollectionQuery FromPairs(IEnumerable<KeyValuePair<string, StringValues>> pairs)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in pairs)
        {
            values[key] = value.ToString();
        }

        return new CollectionQuery(
            OwnOnly: IsOn(values, "own"),
            Subtype: Text(values, "subtype"),
            ExcludeSubtype: Text(values, "excludesubtype"),
            Version: IsOn(values, "version"),
            ShowPrivate: IsOn(values, "showprivate"),
            Stats: IsOn(values, "stats"));
    }

    private static bool IsOn(Dictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value) && value == "1";

    private static string? Text(Dictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value) && value.Length > 0 ? value : null;
}

/// <summary>Writes invented answers in the shape BoardGameGeek's XML API returns, for the fake and for tests.</summary>
public static class BggXml
{
    private const string TermsOfUse = "https://boardgamegeek.com/xmlapi/termsofuse";
    private const string PublishedAt = "Thu, 01 Jan 2026 12:00:00 +0000";
    private const string LastModified = "2026-01-01 12:00:00";
    private const int BigBoxObjectId = 880001;

    /// <summary>The path the fake serves its synthetic pictures under.</summary>
    public const string ArtPath = "/fake-art/";

    private const int BigBoxContainedObjectId = SyntheticBggCollection.FirstObjectId + 3;

    /// <summary>Picks the entries a collection request asks for, the way BoardGameGeek does.</summary>
    /// <param name="items">Every entry in the invented collection.</param>
    /// <param name="query">The request's parameters.</param>
    public static IReadOnlyList<FakeBggItem> Select(IEnumerable<FakeBggItem> items, CollectionQuery query)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(query);

        return items
            .Where(item => !query.OwnOnly || item.Owned)
            .Where(item => query.Subtype != CollectionQuery.ExpansionSubtype || item.IsExpansion)
            .Where(item => query.ExcludeSubtype != CollectionQuery.ExpansionSubtype || !item.IsExpansion)
            .ToList();
    }

    /// <summary>
    /// Writes a collection answer. Expansions are labelled as expansions only when they were asked for by subtype;
    /// the unfiltered request labels them as base games, which is what BoardGameGeek does.
    /// </summary>
    /// <param name="items">Every entry in the invented collection.</param>
    /// <param name="query">The request's parameters.</param>
    /// <param name="totalItemsOverride">A declared total that differs from the number of entries written.</param>
    /// <param name="artOrigin">
    /// The origin picture addresses point at, such as <c>http://127.0.0.1:6190</c>, so the fake's own synthetic pictures
    /// are used; when null the addresses are invented ones on an example host that nothing serves.
    /// </param>
    public static string Collection(IEnumerable<FakeBggItem> items, CollectionQuery query, int? totalItemsOverride = null, string? artOrigin = null)
    {
        var selected = Select(items, query);
        var root = new XElement(
            "items",
            new XAttribute("totalitems", (totalItemsOverride ?? selected.Count).ToString(CultureInfo.InvariantCulture)),
            new XAttribute("termsofuse", TermsOfUse),
            new XAttribute("pubdate", PublishedAt),
            selected.Select(item => CollectionItem(item, query, artOrigin)));
        return Serialize(root);
    }

    /// <summary>Writes the answer BoardGameGeek gives while it prepares a collection.</summary>
    public static string Queued() =>
        Serialize(new XElement(
            "message",
            "Your request for this collection has been accepted and will be processed. Please try again later for access."));

    /// <summary>Writes an error answer; BoardGameGeek sends these with a success status.</summary>
    /// <param name="message">The error text.</param>
    public static string Errors(string message) =>
        Serialize(new XElement("errors", new XElement("error", new XElement("message", message))));

    /// <summary>Writes a web page of the kind a fronting proxy serves in place of the API.</summary>
    public static string CloudflarePage() =>
        "<!DOCTYPE html><html lang=\"en\"><head><title>Just a moment...</title></head>"
        + "<body><h1>Checking your browser before accessing the site.</h1></body></html>";

    /// <summary>Writes a collection answer that stops in the middle of an entry.</summary>
    public static string Malformed() =>
        "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>"
        + $"<items totalitems=\"3\" termsofuse=\"{TermsOfUse}\">"
        + "<item objecttype=\"thing\" objectid=\"100001\" subtype=\"boardgame\" collid=\"5000001\">"
        + "<name sortindex=\"1\">Example Gam";

    /// <summary>
    /// Writes a single-game answer for each requested id: the matching collection entry when there is one,
    /// otherwise an invented game. Every game carries two designers, two mechanics, its play times and its ratings (the ranked
    /// rating is zero for every seventh game id); a base game also lists, without the inbound mark, the expansions the
    /// collection holds for it; an expansion lists the games it expands with the inbound mark; and one game carries an
    /// inbound link of a kind that is not an expansion link.
    /// </summary>
    /// <param name="ids">The requested game ids.</param>
    /// <param name="known">The invented collection, used to give known ids their real titles and links.</param>
    /// <param name="stats">Whether the statistics block is wanted.</param>
    /// <param name="artOrigin">The origin picture addresses point at; when null they are invented addresses on an example host.</param>
    public static string Things(IEnumerable<int> ids, IReadOnlyList<FakeBggItem> known, bool stats, string? artOrigin = null)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(known);

        var byId = known.GroupBy(item => item.ObjectId).ToDictionary(group => group.Key, group => group.First());
        var root = new XElement(
            "items",
            new XAttribute("termsofuse", TermsOfUse),
            ids.Select(id => byId.TryGetValue(id, out var item)
                ? ThingItem(item, stats, byId, artOrigin)
                : ThingItem(new FakeBggItem(id, 0, $"Example Thing {id}", false, true, 2000, null, null), stats, byId, artOrigin)));
        return Serialize(root);
    }

    /// <summary>Returns the id of an entry's selected version; ids follow collection entry order.</summary>
    /// <param name="item">The collection entry.</param>
    public static int VersionId(FakeBggItem item) =>
        SyntheticBggCollection.FirstVersionId + (int)Math.Max(0, item.CollId - SyntheticBggCollection.FirstCollId);

    private static string MainPicture(int objectId, string? artOrigin) =>
        artOrigin is null ? $"https://example.org/images/{objectId}.jpg" : $"{artOrigin}{ArtPath}{Number(objectId)}-main.png";

    private static string MainThumbnail(int objectId, string? artOrigin) =>
        artOrigin is null ? $"https://example.org/thumbnails/{objectId}.jpg" : MainPicture(objectId, artOrigin);

    private static string VersionPicture(int versionId, string? artOrigin) =>
        artOrigin is null ? $"https://example.org/images/version-{versionId}.jpg" : $"{artOrigin}{ArtPath}{Number(versionId)}-version.png";

    private static string VersionThumbnail(int versionId, string? artOrigin) =>
        artOrigin is null ? $"https://example.org/thumbnails/version-{versionId}.jpg" : VersionPicture(versionId, artOrigin);

    private static XElement CollectionItem(FakeBggItem item, CollectionQuery query, string? artOrigin)
    {
        var subtype = item.IsExpansion && query.Subtype == CollectionQuery.ExpansionSubtype
            ? CollectionQuery.ExpansionSubtype
            : "boardgame";
        var element = new XElement(
            "item",
            new XAttribute("objecttype", "thing"),
            new XAttribute("objectid", Number(item.ObjectId)),
            new XAttribute("subtype", subtype),
            new XAttribute("collid", Number(item.CollId)),
            new XElement("name", new XAttribute("sortindex", "1"), item.Title));

        if (item.ObjectId % 3 == 0)
        {
            element.Add(new XElement("originalname", $"Original Example {item.ObjectId}"));
        }

        if (item.Year is { } year)
        {
            element.Add(new XElement("yearpublished", Number(year)));
        }

        element.Add(
            new XElement("image", MainPicture(item.ObjectId, artOrigin)),
            new XElement("thumbnail", MainThumbnail(item.ObjectId, artOrigin)));

        if (query.Stats)
        {
            element.Add(CollectionStats(item));
        }

        element.Add(
            new XElement(
                "status",
                new XAttribute("own", item.Owned ? "1" : "0"),
                new XAttribute("prevowned", "0"),
                new XAttribute("fortrade", "0"),
                new XAttribute("want", "0"),
                new XAttribute("wanttoplay", "0"),
                new XAttribute("wanttobuy", "0"),
                new XAttribute("wishlist", "0"),
                new XAttribute("preordered", "0"),
                new XAttribute("lastmodified", LastModified)),
            new XElement("numplays", "0"));

        if (query.Version && item.Version is { } version)
        {
            element.Add(VersionElement(item, version, artOrigin));
        }

        if (query.ShowPrivate && !string.IsNullOrEmpty(item.Location))
        {
            element.Add(PrivateInfo(item.Location));
        }

        return element;
    }

    private static XElement CollectionStats(FakeBggItem item)
    {
        var minPlayers = 1 + item.ObjectId % 3;
        var maxPlayers = minPlayers + 1 + item.ObjectId % 4;
        var stats = new XElement(
            "stats",
            new XAttribute("minplayers", Number(minPlayers)),
            new XAttribute("maxplayers", Number(maxPlayers)));

        var hasPlayTime = !item.IsExpansion || item.ObjectId % 4 != 0;
        if (hasPlayTime)
        {
            var minutes = 20 + 10 * (item.ObjectId % 6);
            stats.Add(
                new XAttribute("minplaytime", Number(minutes)),
                new XAttribute("maxplaytime", Number(minutes + 30)),
                new XAttribute("playingtime", Number(minutes + 30)));
        }

        stats.Add(
            new XAttribute("numowned", Number(100 + item.ObjectId % 900)),
            new XElement("rating", new XAttribute("value", "N/A")));
        return stats;
    }

    private static XElement VersionElement(FakeBggItem item, FakeVersion version, string? artOrigin) =>
        new(
            "version",
            new XElement(
                "item",
                new XAttribute("type", "boardgameversion"),
                new XAttribute("id", Number(VersionId(item))),
                new XElement("thumbnail", VersionThumbnail(VersionId(item), artOrigin)),
                new XElement("image", VersionPicture(VersionId(item), artOrigin)),
                new XElement(
                    "link",
                    new XAttribute("type", "language"),
                    new XAttribute("id", "2184"),
                    new XAttribute("value", "English")),
                new XElement("name", new XAttribute("type", "primary"), new XAttribute("sortindex", "1"), new XAttribute("value", $"{item.Title} edition")),
                new XElement("yearpublished", new XAttribute("value", Number(item.Year ?? 0))),
                new XElement("productcode", new XAttribute("value", string.Empty)),
                new XElement("width", new XAttribute("value", Decimal(version.Width))),
                new XElement("length", new XAttribute("value", Decimal(version.Length))),
                new XElement("depth", new XAttribute("value", Decimal(version.Depth))),
                new XElement("weight", new XAttribute("value", "0"))));

    private static XElement PrivateInfo(string location) =>
        new(
            "privateinfo",
            new XAttribute("pp_currency", "USD"),
            new XAttribute("pricepaid", "0.00"),
            new XAttribute("cv_currency", "USD"),
            new XAttribute("currvalue", "0.00"),
            new XAttribute("quantity", "1"),
            new XAttribute("acquisitiondate", string.Empty),
            new XAttribute("acquiredfrom", string.Empty),
            new XAttribute("inventorylocation", location));

    private static XElement ThingItem(FakeBggItem item, bool stats, IReadOnlyDictionary<int, FakeBggItem> known, string? artOrigin)
    {
        var minPlayers = 1 + item.ObjectId % 3;
        var playingTime = 30 + 10 * (item.ObjectId % 6);
        var element = new XElement(
            "item",
            new XAttribute("type", item.IsExpansion ? CollectionQuery.ExpansionSubtype : "boardgame"),
            new XAttribute("id", Number(item.ObjectId)),
            new XElement("thumbnail", MainThumbnail(item.ObjectId, artOrigin)),
            new XElement("image", MainPicture(item.ObjectId, artOrigin)),
            new XElement("name", new XAttribute("type", "primary"), new XAttribute("sortindex", "1"), new XAttribute("value", item.Title)),
            new XElement("description", $"An invented description for example game {item.ObjectId}."),
            new XElement("yearpublished", new XAttribute("value", Number(item.Year ?? 0))),
            new XElement("minplayers", new XAttribute("value", Number(minPlayers))),
            new XElement("maxplayers", new XAttribute("value", Number(minPlayers + 1 + item.ObjectId % 4))),
            new XElement("playingtime", new XAttribute("value", Number(playingTime))),
            new XElement("minplaytime", new XAttribute("value", Number(playingTime - 10))),
            new XElement("maxplaytime", new XAttribute("value", Number(playingTime))),
            new XElement("minage", new XAttribute("value", Number(8 + item.ObjectId % 6))));

        element.Add(
            Link("boardgamedesigner", 3000 + item.ObjectId % 5, $"Invented Designer {item.ObjectId % 5 + 1}", inbound: false),
            Link("boardgamedesigner", 3010 + item.ObjectId % 4, $"Invented Designer {item.ObjectId % 4 + 6}", inbound: false),
            Link("boardgamemechanic", 2000 + item.ObjectId % 3, $"Example Mechanic {(char)('A' + item.ObjectId % 3)}", inbound: false),
            Link("boardgamemechanic", 2010 + item.ObjectId % 2, $"Example Mechanic {(char)('D' + item.ObjectId % 2)}", inbound: false));

        foreach (var outbound in known.Values
            .Where(other => other.IsExpansion && (other.BaseObjectId == item.ObjectId || other.AlsoExpands?.Contains(item.ObjectId) == true))
            .OrderBy(other => other.ObjectId))
        {
            element.Add(Link(CollectionQuery.ExpansionSubtype, outbound.ObjectId, outbound.Title, inbound: false));
        }

        if (item.IsExpansion)
        {
            foreach (var baseObjectId in (item.BaseObjectId is { } primary ? new[] { primary } : []).Concat(item.AlsoExpands ?? []))
            {
                element.Add(Link(CollectionQuery.ExpansionSubtype, baseObjectId, BaseTitle(baseObjectId, known), inbound: true));
            }
        }

        if (item.ObjectId == BigBoxContainedObjectId)
        {
            element.Add(Link("boardgamecompilation", BigBoxObjectId, "Example Big Box", inbound: true));
        }

        if (stats)
        {
            var weight = 1.0 + item.ObjectId % 40 / 10.0;
            var bayes = item.ObjectId % 7 == 0 ? 0 : 5.0 + item.ObjectId % 30 / 10.0;
            element.Add(new XElement(
                "statistics",
                new XAttribute("page", "1"),
                new XElement(
                    "ratings",
                    new XElement("usersrated", new XAttribute("value", Number(50 + item.ObjectId % 500))),
                    new XElement("average", new XAttribute("value", Decimal(5.0 + item.ObjectId % 40 / 10.0))),
                    new XElement("bayesaverage", new XAttribute("value", Decimal(bayes))),
                    new XElement("averageweight", new XAttribute("value", Decimal(weight))))));
        }

        return element;
    }

    private static string BaseTitle(int baseObjectId, IReadOnlyDictionary<int, FakeBggItem> known) =>
        known.TryGetValue(baseObjectId, out var item)
            ? item.Title
            : $"Example Game {baseObjectId - SyntheticBggCollection.FirstObjectId + 1}";

    private static XElement Link(string type, int id, string value, bool inbound)
    {
        var link = new XElement("link", new XAttribute("type", type), new XAttribute("id", Number(id)), new XAttribute("value", value));

        if (inbound)
        {
            link.Add(new XAttribute("inbound", "true"));
        }

        return link;
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Decimal(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Serialize(XElement root)
    {
        using var stream = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false,
            Indent = false,
        };

        using (var writer = XmlWriter.Create(stream, settings))
        {
            new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root).Save(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}

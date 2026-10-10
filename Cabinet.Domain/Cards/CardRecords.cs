using System.Text.Json;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;

namespace Cabinet.Domain.Cards;

/// <summary>The cover picture a card shows: one stored size of the picture and how it sits in the box front.</summary>
/// <param name="Url">The path on the site's own origin the file is served from.</param>
/// <param name="Width">The width of the served file in pixels.</param>
/// <param name="Height">The height of the served file in pixels.</param>
/// <param name="Fit">How the picture sits inside the box front.</param>
/// <param name="Edges">The colours along the picture's edges, or null when they are not known.</param>
public sealed record CardCover(string Url, int Width, int Height, ArtFit Fit, ArtEdges? Edges);

/// <summary>A game named on a card: an owned entry the visitor can open, or a game the collection does not hold.</summary>
/// <param name="EntryId">The collection entry of the game, or null when the game is not owned.</param>
/// <param name="GameId">The game identifier.</param>
/// <param name="Title">The title as the source gave it.</param>
/// <param name="Chip">The box colour as a hexadecimal colour for the small chip beside the title, or null when the game is not owned.</param>
public sealed record CardLink(long? EntryId, int GameId, string Title, string? Chip);

/// <summary>
/// Everything the detail card and the screen-reader list show about one owned entry. Absent values are null and are left
/// out of the JSON; the lists are empty when there is nothing to list.
/// </summary>
/// <param name="EntryId">The collection entry the card belongs to.</param>
/// <param name="GameId">The game identifier.</param>
/// <param name="Title">The title, exactly as the source gave it.</param>
/// <param name="Year">The publication year, or null when it is not known.</param>
/// <param name="IsExpansion">Whether the entry is an expansion.</param>
/// <param name="Ratio">The width of the box front divided by its height, to four decimals.</param>
/// <param name="Cover">The cover picture, or null when the game has none.</param>
/// <param name="Colour">The background and text colours taken from the picture, or null when there are none.</param>
/// <param name="ToneIndex">The index of the placeholder colour of the game.</param>
/// <param name="PatternIndex">The index of the placeholder cover pattern of the game.</param>
/// <param name="Location">Where the entry is kept, or null when it is not known.</param>
/// <param name="MinPlayers">The fewest players, or null when unknown.</param>
/// <param name="MaxPlayers">The most players, or null when unknown.</param>
/// <param name="PlayTime">The stated playing time in minutes, or null when unknown.</param>
/// <param name="MinPlayTime">The shortest playing time in minutes, or null when unknown.</param>
/// <param name="MaxPlayTime">The longest playing time in minutes, or null when unknown.</param>
/// <param name="MinAge">The minimum age in years, or null when unknown.</param>
/// <param name="Weight">The complexity rating rounded to one decimal, or null when unknown.</param>
/// <param name="Rating">The average user rating rounded to one decimal, or null when unknown.</param>
/// <param name="Designers">The designers' names in the order the source gave them.</param>
/// <param name="Mechanics">The mechanics' names in the order the source gave them.</param>
/// <param name="Expansions">The owned expansions of this game, in collection order.</param>
/// <param name="Bases">For an expansion, the games it expands: the owned ones when any is owned, otherwise the named ones.</param>
public sealed record CardRecord(
    long EntryId,
    int GameId,
    string Title,
    int? Year,
    bool IsExpansion,
    double Ratio,
    CardCover? Cover,
    PaletteTone? Colour,
    int ToneIndex,
    int PatternIndex,
    string? Location,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayTime,
    int? MinPlayTime,
    int? MaxPlayTime,
    int? MinAge,
    double? Weight,
    double? Rating,
    IReadOnlyList<string> Designers,
    IReadOnlyList<string> Mechanics,
    IReadOnlyList<CardLink> Expansions,
    IReadOnlyList<CardLink> Bases);

/// <summary>The body of the card endpoint: the cards of one collection view and the layout they belong to.</summary>
/// <param name="Layout">The entity tag of the layout of the same collection and profile, so the page can tell the two apart.</param>
/// <param name="Cards">One card per owned entry, in layout item order.</param>
public sealed record CardsDocument(string Layout, IReadOnlyList<CardRecord> Cards);

/// <summary>Builds the card records of a collection: one per owned entry, from stored data only.</summary>
public static class CardRecords
{
    private const int RatioDecimals = 4;
    private const int DesktopCoverPixels = 384;
    private const int PhoneCoverPixels = 224;

    /// <summary>The stored size of the cover a card of this design asks for: twice the width the cover is drawn at.</summary>
    /// <param name="design">The section design of the requested profile.</param>
    public static int CardCoverPixels(SectionDesign design)
    {
        ArgumentNullException.ThrowIfNull(design);

        return string.Equals(design.Name, SectionDesigns.PhoneName, StringComparison.Ordinal) ? PhoneCoverPixels : DesktopCoverPixels;
    }

    /// <summary>Builds one record per item, in item order.</summary>
    /// <param name="items">The items of the collection view.</param>
    /// <param name="snapshot">The stored collection the items came from, or null when there is none.</param>
    /// <param name="design">The section design of the requested profile; it decides the stored size of each cover.</param>
    public static IReadOnlyList<CardRecord> Build(IReadOnlyList<CabinetItem> items, CollectionSnapshot? snapshot, SectionDesign design)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(design);

        return items.Select(item => ToRecord(item, snapshot, design)).ToList();
    }

    /// <summary>Serialises the document with the same options as the layout, so the same cards always give the same bytes.</summary>
    /// <param name="document">The document to serialise.</param>
    public static string Serialize(CardsDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return JsonSerializer.Serialize(document, LayoutJson.Options);
    }

    private static CardRecord ToRecord(CabinetItem item, CollectionSnapshot? snapshot, SectionDesign design)
    {
        _ = snapshot;

        return new CardRecord(
            item.CollectionId,
            item.BggId,
            item.Title,
            null,
            item.Kind == ItemKind.Expansion,
            RatioOf(item.Box),
            CoverOf(item, design),
            item.Colour,
            SpinePalette.ToneFor(item.BggId),
            SpinePalette.PatternFor(item.BggId),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            [],
            []);
    }

    private static double RatioOf(BoxDimensions box) =>
        box.HeightMm <= 0 ? 1 : Math.Round(box.WidthMm / (double)box.HeightMm, RatioDecimals, MidpointRounding.AwayFromZero);

    private static CardCover? CoverOf(CabinetItem item, SectionDesign design)
    {
        if (item.Art is null || item.Art.Variants.Count == 0)
        {
            return null;
        }

        var wanted = CardCoverPixels(design);
        var variant = item.Art.Variants
            .Where(candidate => candidate.Width >= wanted)
            .OrderBy(candidate => candidate.Width)
            .FirstOrDefault()
            ?? item.Art.Variants.OrderByDescending(candidate => candidate.Width).First();

        return new CardCover(
            variant.Url,
            variant.Width,
            variant.Height,
            ArtFitting.Fit(item.Box.WidthMm, item.Box.HeightMm, variant.Width, variant.Height),
            item.Art.Edges);
    }
}

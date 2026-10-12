using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;

namespace Cabinet.Domain.Owner;

/// <summary>Which picture the automatic pick draws for a game, so the page can say which tile Automatic duplicates.</summary>
public enum AutomaticSource
{
    /// <summary>The picture of the owned edition.</summary>
    OwnedEdition,

    /// <summary>The game's main picture.</summary>
    MainPicture,

    /// <summary>No usable picture: the generated cover is drawn.</summary>
    GeneratedCover,
}

/// <summary>Why the automatic pick drew what it did; one of the four reasons the pictures page words for every row.</summary>
public enum AutomaticWhy
{
    /// <summary>The owned edition's picture is drawn.</summary>
    OwnedEdition,

    /// <summary>The main picture is drawn because the owned edition's picture reads as something other than a flat cover.</summary>
    MainBecauseOwnedReads,

    /// <summary>The main picture is drawn because the owned edition has no usable picture.</summary>
    MainBecauseNoOwned,

    /// <summary>No picture is usable, so the generated cover is drawn.</summary>
    GeneratedCover,
}

/// <summary>How many games the owner's worklist holds.</summary>
/// <param name="ToCheck">The candidates the owner has not dealt with yet.</param>
/// <param name="Candidates">Every game worth a look, including those the owner dealt with while that still holds.</param>
/// <param name="Total">Every game.</param>
public sealed record PictureCounts(int ToCheck, int Candidates, int Total);

/// <summary>One of the four tiles of a game on the pictures page.</summary>
/// <param name="Kind">Which choice the tile stands for.</param>
/// <param name="Available">Whether the choice has something behind it; a tile that is not available shows no picture and its radio cannot be picked.</param>
/// <param name="Url">The path of the small stored picture the cabinet would draw, or null when there is none to show.</param>
/// <param name="Width">The width of that picture in pixels, or zero without one.</param>
/// <param name="Height">The height of that picture in pixels, or zero without one.</param>
/// <param name="Verdict">What the picture looks like, or null without a picture.</param>
/// <param name="IsAutomaticPick">Whether this is the picture the tile Automatic duplicates.</param>
/// <param name="Fetching">Whether the owner's link is stored but its picture has not been fetched yet.</param>
/// <param name="LinkAddress">The short form of the owner's link, such as <c>host/.../file.jpg</c>, or null when the tile is not the link tile or has no link.</param>
/// <param name="LinkFullAddress">The whole address of the owner's link, or null when there is none.</param>
public sealed record PictureTile(
    PictureChoice Kind,
    bool Available,
    string? Url,
    int Width,
    int Height,
    ArtVerdictKind? Verdict,
    bool IsAutomaticPick,
    bool Fetching,
    string? LinkAddress,
    string? LinkFullAddress);

/// <summary>One game on the pictures page, with its choice and the four tiles.</summary>
/// <param name="GameId">The game.</param>
/// <param name="Title">The title to show.</param>
/// <param name="Year">The publication year, or null when it is not known.</param>
/// <param name="IsExpansion">Whether the game is an expansion.</param>
/// <param name="Choice">The kind of picture the owner chose; Automatic when the owner chose none.</param>
/// <param name="AutomaticSource">Which picture Automatic draws now.</param>
/// <param name="CoverBackground">The background of the generated cover, which is the game's spine colour when it has no picture.</param>
/// <param name="CoverText">The text colour of the generated cover.</param>
/// <param name="CoverPattern">The index of the generated cover's pattern.</param>
/// <param name="Tiles">The four tiles in the order Automatic, Owned edition, Main picture, Your link.</param>
/// <param name="Look">What the worklist knows about the game: its reasons to be looked at, its marks and whether it is resolved.</param>
/// <param name="Why">Why Automatic drew what it did.</param>
/// <param name="OwnedReadsAs">What the owned edition's picture looks like to the detector, or null when it has no usable picture.</param>
public sealed record PictureRow(
    int GameId,
    string Title,
    int? Year,
    bool IsExpansion,
    PictureChoice Choice,
    AutomaticSource AutomaticSource,
    string CoverBackground,
    string CoverText,
    int CoverPattern,
    IReadOnlyList<PictureTile> Tiles,
    WorthALookResult Look,
    AutomaticWhy Why,
    ArtVerdictKind? OwnedReadsAs)
{
    /// <summary>The tile of one choice.</summary>
    /// <param name="kind">The choice.</param>
    public PictureTile Tile(PictureChoice kind) => Tiles.Single(tile => tile.Kind == kind);
}

/// <summary>Builds what the pictures page shows for every synced game: the four pictures it can be drawn from, as they would look.</summary>
public static class PictureRows
{
    private const int TileWidthPx = 240;
    private const string AddressGap = "/.../";

    /// <summary>
    /// Builds one row per game, ordered by title with case ignored and then by game. Each row is made from what the automatic pick
    /// would draw for the game, never from what the owner chose, so the owner always sees the candidates as they are. A game
    /// listed in several collection entries is one row, made from the entry with the lowest identifier.
    /// </summary>
    /// <param name="snapshot">The stored collection as the sync wrote it.</param>
    /// <param name="rules">The rules that decide what Automatic picks.</param>
    /// <param name="data">What the owner entered.</param>
    /// <param name="now">The moment the rows are built for, which decides whether a link counts as dead.</param>
    public static IReadOnlyList<PictureRow> Build(CollectionSnapshot snapshot, ArtRules rules, OwnerData data, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(data);

        return
        [
            .. SnapshotMapper.Explain(snapshot, rules)
                .GroupBy(trace => trace.Item.GameId)
                .Select(group => group.First())
                .Select(trace => RowOf(trace, snapshot, rules, data, now))
                .OrderBy(row => row.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.GameId),
        ];
    }

    /// <summary>
    /// The games the owner's worklist opens on: the candidates that are not resolved. Games with a mark that asks for attention
    /// come first, a missing or broken picture before a link that no longer works before a picture BGG changed, and the rest
    /// follow; inside each group the games are by title with case ignored and then by game.
    /// </summary>
    /// <param name="rows">The rows of every game.</param>
    public static IReadOnlyList<PictureRow> WorthALookList(IReadOnlyList<PictureRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return
        [
            .. rows
                .Where(row => row.Look.ToCheck)
                .OrderBy(row => row.Look.AttentionRank)
                .ThenBy(row => row.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.GameId),
        ];
    }

    /// <summary>Counts what the worklist holds.</summary>
    /// <param name="rows">The rows of every game.</param>
    public static PictureCounts CountsOf(IReadOnlyList<PictureRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return new PictureCounts(rows.Count(row => row.Look.ToCheck), rows.Count(row => row.Look.Candidate), rows.Count);
    }

    /// <summary>The title to show for an item: the title trimmed, or a name made from the game's number when it is blank.</summary>
    /// <param name="item">The stored item.</param>
    public static string TitleOf(SnapshotItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return string.IsNullOrWhiteSpace(item.Title) ? $"Game {item.GameId}" : item.Title.Trim();
    }

    private static PictureRow RowOf(MappedItemTrace trace, CollectionSnapshot snapshot, ArtRules rules, OwnerData data, DateTimeOffset now)
    {
        var item = trace.Item;
        var setting = data.Games.FirstOrDefault(game => game.GameId == item.GameId)?.Picture;
        var source = trace.Pick switch
        {
            ArtPick.VersionImage => AutomaticSource.OwnedEdition,
            ArtPick.MainImage => AutomaticSource.MainPicture,
            _ => AutomaticSource.GeneratedCover,
        };
        var mainUrl = SnapshotMapper.MainPictureUrl(item, snapshot);
        var owned = PictureTileOf(PictureChoice.OwnedEdition, item.VersionImageUrl is not null, trace.VersionImage, rules, source == AutomaticSource.OwnedEdition);
        var main = PictureTileOf(
            PictureChoice.MainPicture,
            mainUrl is not null,
            trace.MainImage,
            rules,
            source == AutomaticSource.MainPicture);
        var automatic = AutomaticTile(source, owned, main);
        var tone = SpinePalette.Tones[SpinePalette.ToneFor(item.GameId)];

        return new PictureRow(
            item.GameId,
            TitleOf(item),
            item.Year,
            item.Kind == ItemKind.Expansion,
            setting?.Choice ?? PictureChoice.Automatic,
            source,
            tone.Background,
            tone.Text,
            SpinePalette.PatternFor(item.GameId),
            [automatic, owned, main, LinkTileOf(setting?.Link, rules)],
            WorthALook.Evaluate(trace, mainUrl, setting, snapshot.Images ?? new Dictionary<string, ImageRecord>(StringComparer.Ordinal), now),
            WhyOf(source, trace.VersionImage),
            owned.Verdict);
    }

    private static AutomaticWhy WhyOf(AutomaticSource source, ImageRecord? ownedUsable) => source switch
    {
        AutomaticSource.OwnedEdition => AutomaticWhy.OwnedEdition,
        AutomaticSource.MainPicture => ownedUsable is null ? AutomaticWhy.MainBecauseNoOwned : AutomaticWhy.MainBecauseOwnedReads,
        _ => AutomaticWhy.GeneratedCover,
    };

    private static PictureTile AutomaticTile(AutomaticSource source, PictureTile owned, PictureTile main)
    {
        var drawn = source switch
        {
            AutomaticSource.OwnedEdition => owned,
            AutomaticSource.MainPicture => main,
            _ => null,
        };

        return new PictureTile(
            PictureChoice.Automatic,
            true,
            drawn?.Url,
            drawn?.Width ?? 0,
            drawn?.Height ?? 0,
            drawn?.Verdict,
            false,
            false,
            null,
            null);
    }

    private static PictureTile PictureTileOf(PictureChoice kind, bool offered, ImageRecord? usable, ArtRules rules, bool isAutomaticPick)
    {
        var file = usable is null ? null : FileOf(usable);

        return new PictureTile(
            kind,
            offered,
            file is null ? null : $"{ArtFitting.RequestPath}/{file.Name}",
            file?.Width ?? 0,
            file?.Height ?? 0,
            VerdictOf(usable, rules),
            isAutomaticPick,
            false,
            null,
            null);
    }

    private static PictureTile LinkTileOf(PictureLink? link, ArtRules rules)
    {
        if (link is null)
        {
            return new PictureTile(PictureChoice.Link, false, null, 0, 0, null, false, false, null, null);
        }

        var usable = link.Record is { Status: ImageStatus.Ok, Files.Count: > 0 } record ? record : null;
        var file = usable is null ? null : FileOf(usable);

        return new PictureTile(
            PictureChoice.Link,
            true,
            file is null ? null : $"{ArtFitting.RequestPath}/{file.Name}",
            file?.Width ?? 0,
            file?.Height ?? 0,
            VerdictOf(usable, rules),
            false,
            link.Record is null,
            ShortAddress(link.Url),
            link.Url);
    }

    private static ArtVerdictKind? VerdictOf(ImageRecord? record, ArtRules rules) =>
        record?.Features is { } features
            ? ArtVerdictKinds.Of(ArtVerdicts.Classify(features, rules.Thresholds), features)
            : null;

    private static ArtFile? FileOf(ImageRecord record)
    {
        var files = record.Files;

        if (files is null || files.Count == 0)
        {
            return null;
        }

        return files.Where(file => file.Width >= TileWidthPx).OrderBy(file => file.Width).FirstOrDefault()
            ?? files.OrderByDescending(file => file.Width).First();
    }

    private static string? ShortAddress(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var address) || address.Host.Length == 0)
        {
            return null;
        }

        var file = address.Segments.LastOrDefault()?.Trim('/');

        return string.IsNullOrEmpty(file) ? address.Host : address.Host + AddressGap + Uri.UnescapeDataString(file);
    }
}

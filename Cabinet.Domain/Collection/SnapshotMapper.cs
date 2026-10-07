using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Cabinet.Domain.Layout;

namespace Cabinet.Domain.Collection;

/// <summary>Turns the stored collection into the items the layout engine draws, in an order that does not depend on the source.</summary>
public static class SnapshotMapper
{
    private const int VersionLength = 16;

    /// <summary>
    /// Maps every stored item to an item to draw, ordered by collection entry and then by game, so the same collection in
    /// any source order gives the same cabinet. An entry listed twice is one item, and it is the expansion; two entries for
    /// the same game with different entry identifiers stay two items. An expansion carries the games its details say it expands, paired by <see cref="ExpansionPairing"/>; before its details arrive it carries none.
    /// </summary>
    /// <param name="snapshot">The stored collection.</param>
    public static IReadOnlyList<CabinetItem> ToCabinetItems(CollectionSnapshot snapshot) =>
        ToCabinetItems(snapshot, ArtRules.Default);

    /// <summary>
    /// Maps every stored item to an item to draw as <see cref="ToCabinetItems(CollectionSnapshot)"/> does, choosing each
    /// game's picture from the stored measurements with the given rules: the picture of the owned edition when it is a flat
    /// cover, the game's main picture when the owned edition's picture is a photographed box and the main picture is flat,
    /// and the owned edition's picture otherwise. The chosen picture also supplies the colour pair and the edge colours the
    /// item carries; a game with no usable picture carries neither. A chosen picture that is a flat cover may also give the
    /// box its shape; see <see cref="BoxShape"/>.
    /// </summary>
    /// <param name="snapshot">The stored collection.</param>
    /// <param name="rules">The rules that turn stored measurements into a choice.</param>
    public static IReadOnlyList<CabinetItem> ToCabinetItems(CollectionSnapshot snapshot, ArtRules rules)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(rules);

        var images = snapshot.Images ?? new Dictionary<string, ImageRecord>();
        var pairing = ExpansionPairing.Pair(snapshot);

        return
        [
            .. snapshot.Items
                .GroupBy(item => item.CollectionId)
                .Select(entry => entry.FirstOrDefault(item => item.Kind == ItemKind.Expansion) ?? entry.First())
                .OrderBy(item => item.CollectionId)
                .ThenBy(item => item.GameId)
                .Select(item => ToCabinetItem(item, snapshot, images, pairing, rules)),
        ];
    }

    /// <summary>
    /// The address of the picture a game offers besides the one of the owned edition: the main picture its details name,
    /// and until its details arrive, or when they name none, the picture the collection gave for the item.
    /// </summary>
    /// <param name="item">The stored item.</param>
    /// <param name="snapshot">The stored collection, which holds the details.</param>
    public static string? MainPictureUrl(SnapshotItem item, CollectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(snapshot);

        return snapshot.Games is not null && snapshot.Games.TryGetValue(item.GameId, out var details) && details.MainImageUrl is { } main
            ? main
            : item.ImageUrl;
    }

    /// <summary>
    /// The identifier of a collection: the first sixteen lowercase hexadecimal characters of a SHA-256 hash over the mapped
    /// items in order. It changes whenever anything that is drawn changes and not otherwise.
    /// </summary>
    /// <param name="items">The mapped items, in mapped order.</param>
    public static string Version(IReadOnlyList<CabinetItem> items) => Version(items, ArtRules.Default);

    /// <summary>
    /// The identifier of a collection as <see cref="Version(IReadOnlyList{CabinetItem})"/> works it out, with the rules the
    /// items were mapped with in front, so a change of rules changes the identifier. Each item's line also holds its colour
    /// pair and its edge colours.
    /// </summary>
    /// <param name="items">The mapped items, in mapped order.</param>
    /// <param name="rules">The rules the items were mapped with.</param>
    public static string Version(IReadOnlyList<CabinetItem> items, ArtRules rules)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(rules);

        var lines = items
            .Select(item => string.Create(
                CultureInfo.InvariantCulture,
                $"{item.CollectionId}|{item.BggId}|{item.Kind}|{item.Title}|{item.Box.WidthMm}|{item.Box.HeightMm}|{item.Box.DepthMm}|{item.PoseHeightMm}|{VariantUrls(item.Art)}|{ExpansionRefs(item.ExpansionOf)}|{ColourText(item.Colour)}|{EdgeText(item.Art?.Edges)}"))
            .Prepend(rules.Fingerprint);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines)));

        return Convert.ToHexStringLower(hash)[..VersionLength];
    }

    private static CabinetItem ToCabinetItem(
        SnapshotItem item,
        CollectionSnapshot snapshot,
        IReadOnlyDictionary<string, ImageRecord> images,
        IReadOnlyDictionary<long, IReadOnlyList<BaseGameRef>> pairing,
        ArtRules rules)
    {
        var chosen = ChosenRecord(item, snapshot, images, rules);
        var details = snapshot.Games is not null && snapshot.Games.TryGetValue(item.GameId, out var known) ? known : null;
        var shaped = BoxShape.Resolve(item, details, FlatCoverOf(chosen, rules), rules);

        return new CabinetItem(
            item.GameId,
            item.CollectionId,
            item.Title,
            item.Kind,
            shaped.Box,
            pairing.TryGetValue(item.CollectionId, out var expansionOf) && item.Kind == ItemKind.Expansion ? expansionOf : [],
            chosen is null ? null : ArtOf(chosen),
            chosen is not null && SpineColour.IsValidPair(chosen.Colour) ? chosen.Colour : null,
            shaped.PoseHeightMm);
    }

    private static ArtFile? FlatCoverOf(ImageRecord? chosen, ArtRules rules) =>
        VerdictOf(chosen, rules) == ArtVerdict.Flat ? chosen!.Files!.OrderByDescending(file => file.Width).First() : null;

    private static ImageRecord? ChosenRecord(
        SnapshotItem item,
        CollectionSnapshot snapshot,
        IReadOnlyDictionary<string, ImageRecord> images,
        ArtRules rules)
    {
        var version = UsableRecord(item.VersionImageUrl, images);
        var main = UsableRecord(MainPictureUrl(item, snapshot), images);

        return ArtChooser.Choose(VerdictOf(version, rules), VerdictOf(main, rules)) switch
        {
            ArtPick.VersionImage => version,
            ArtPick.MainImage => main,
            _ => null,
        };
    }

    private static ImageRecord? UsableRecord(string? url, IReadOnlyDictionary<string, ImageRecord> images) =>
        url is not null
        && images.TryGetValue(url, out var record)
        && record.Status == ImageStatus.Ok
        && record.Files is { Count: > 0 }
        && record.Features is not null
            ? record
            : null;

    private static ArtVerdict? VerdictOf(ImageRecord? record, ArtRules rules) =>
        record?.Features is { } features ? ArtVerdicts.Classify(features, rules.Thresholds) : null;

    private static ArtImage ArtOf(ImageRecord record) =>
        new(
            [.. record.Files!
                .OrderByDescending(file => file.Width)
                .Select(file => new ArtVariant(file.Width, file.Height, $"{ArtFitting.RequestPath}/{file.Name}"))],
            EdgesOf(record.Edges));

    private static ArtEdges? EdgesOf(ArtEdges? edges) =>
        edges is not null
        && RgbColour.TryParseHex(edges.Top, out _)
        && RgbColour.TryParseHex(edges.Right, out _)
        && RgbColour.TryParseHex(edges.Bottom, out _)
        && RgbColour.TryParseHex(edges.Left, out _)
            ? edges
            : null;

    private static string ColourText(PaletteTone? colour) => colour is null ? string.Empty : $"{colour.Background}:{colour.Text}";

    private static string EdgeText(ArtEdges? edges) => edges is null ? string.Empty : $"{edges.Top}:{edges.Right}:{edges.Bottom}:{edges.Left}";

    private static string ExpansionRefs(IReadOnlyList<BaseGameRef> references) =>
        string.Join(';', references.Select(reference => string.Create(CultureInfo.InvariantCulture, $"{reference.BggId}:{reference.Title}")));

    private static string VariantUrls(ArtImage? art) =>
        art is null ? string.Empty : string.Join(',', art.Variants.Select(variant => variant.Url));

    /// <summary>The box size used for an item whose real size is not known.</summary>
    /// <param name="kind">Whether the item is a standalone game or an expansion.</param>
    public static BoxDimensions DefaultBox(ItemKind kind) => BoxFromVersion.DefaultFor(kind);
}

using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;

namespace Cabinet.Domain.Owner;

/// <summary>Where the size of the box in a preview came from, in the words the owner reads.</summary>
public enum PreviewShapeSource
{
    /// <summary>BGG's own size for the game's box.</summary>
    Bgg,

    /// <summary>The shape of the picture itself, which only a flat cover may give.</summary>
    Picture,

    /// <summary>An estimate, because BGG gave no believable size and the picture may not shape the box.</summary>
    Estimate,
}

/// <summary>
/// What the owner sees of a picture they linked before they use it: the face-out box with the picture and the spine, both at
/// the size the cabinet would draw them, with the verdict of the picture and where the box's shape came from. It is made by
/// the same mapping that draws the cabinet, with the link chosen for this one game, so the preview cannot drift from what
/// every visitor sees once the link is used.
/// </summary>
/// <param name="Title">The game's title.</param>
/// <param name="BoxWidthMm">The width of the face-out box in millimetres.</param>
/// <param name="BoxHeightMm">The height of the face-out box in millimetres.</param>
/// <param name="BoxDepthMm">The depth of the box in millimetres.</param>
/// <param name="Fit">How the picture sits in the box front.</param>
/// <param name="Edges">The colours along the picture's edges that fill what the picture leaves, or null when they are not known.</param>
/// <param name="ArtWidth">The width in pixels of the small picture that is shown.</param>
/// <param name="ArtHeight">The height in pixels of the small picture that is shown.</param>
/// <param name="ArtFileName">The name of the stored file of the small picture that is shown.</param>
/// <param name="ShapeSource">Where the box's size came from.</param>
/// <param name="Verdict">What the picture looks like to the detector.</param>
/// <param name="SpineWidthMm">The width of the spine in millimetres, never under the cabinet's least box thickness.</param>
/// <param name="SpineHeightMm">The height of the spine in millimetres, which is the height of the box.</param>
/// <param name="SpineLabel">The title shortened the way the cabinet shortens it for a spine of this height.</param>
/// <param name="Spine">The background and text colours of the spine.</param>
public sealed record LinkPreview(
    string Title,
    int BoxWidthMm,
    int BoxHeightMm,
    int BoxDepthMm,
    ArtFit Fit,
    ArtEdges? Edges,
    int ArtWidth,
    int ArtHeight,
    string ArtFileName,
    PreviewShapeSource ShapeSource,
    ArtVerdictKind Verdict,
    int SpineWidthMm,
    int SpineHeightMm,
    string SpineLabel,
    PaletteTone Spine)
{
    /// <summary>
    /// Builds the preview of a linked picture for one game. The game is mapped as the cabinet maps it with the link chosen, so
    /// the box, its size source, the colours and the edges are the cabinet's own. Null when the game is not in the collection or
    /// the record cannot be drawn.
    /// </summary>
    /// <param name="snapshot">The stored collection.</param>
    /// <param name="gameId">The game the link is for.</param>
    /// <param name="rules">The rules that turn stored measurements into a choice.</param>
    /// <param name="record">The processed record of the linked picture.</param>
    public static LinkPreview? Build(CollectionSnapshot snapshot, int gameId, ArtRules rules, ImageRecord record)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(record);

        var chosen = new PictureSetting(PictureChoice.Link, true, null, null, new PictureLink(record.SourceUrl, record, 0, null));
        var overlay = OwnerOverlay.From(snapshot, OwnerData.Empty with { Games = [new OwnerGame(gameId, null, null, chosen)] });
        var trace = SnapshotMapper.Explain(snapshot, rules, overlay).FirstOrDefault(candidate => candidate.Item.GameId == gameId);

        if (trace is not { PickedByOwner: true, Mapped.Art: { } art } || record.Features is not { } features || PictureRows.FileOf(record) is not { } file)
        {
            return null;
        }

        var box = trace.Shape.Box;
        var design = SectionDesigns.Desktop;

        return new LinkPreview(
            PictureRows.TitleOf(trace.Item),
            box.WidthMm,
            box.HeightMm,
            box.DepthMm,
            ArtFitting.Fit(box.WidthMm, box.HeightMm, file.Width, file.Height),
            art.Edges,
            file.Width,
            file.Height,
            file.Name,
            ShapeSourceOf(trace.Shape.Source),
            ArtVerdictKinds.Of(ArtVerdicts.Classify(features, rules.Thresholds), features),
            Math.Max(box.DepthMm, design.MinBoxThicknessMm),
            box.HeightMm,
            Layout.SpineLabel.Shorten(trace.Mapped.Title, box.HeightMm / Math.Max(1, design.LabelCharPitchMm)),
            trace.Mapped.Colour ?? SpinePalette.Tones[SpinePalette.ToneFor(gameId)]);
    }

    private static PreviewShapeSource ShapeSourceOf(BoxSource source) => source switch
    {
        BoxSource.RealSize => PreviewShapeSource.Bgg,
        BoxSource.CoverShape => PreviewShapeSource.Picture,
        _ => PreviewShapeSource.Estimate,
    };
}

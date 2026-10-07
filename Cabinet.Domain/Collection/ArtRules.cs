namespace Cabinet.Domain.Collection;

/// <summary>
/// The rules read when the cabinet is built from the stored collection, as opposed to the facts stored with each picture.
/// Changing a rule changes which picture a game shows or how its box is shaped without any picture being fetched again.
/// </summary>
/// <param name="Thresholds">The tuning values that turn the stored measurements of a picture into a verdict.</param>
/// <param name="ShapeMarginPercent">How far, as a percentage of a flat cover's shape, the shape of the real dimensions may differ from it before the front is rebuilt from the cover.</param>
/// <param name="OrientFromCover">Whether a landscape flat cover makes the box front landscape, so its width is the longer side.</param>
public sealed record ArtRules(
    ArtThresholds Thresholds,
    int ShapeMarginPercent = ArtRules.DefaultShapeMarginPercent,
    bool OrientFromCover = true)
{
    /// <summary>The shape margin a cabinet starts with, in percent.</summary>
    public const int DefaultShapeMarginPercent = 12;

    /// <summary>The rules a cabinet starts with.</summary>
    public static ArtRules Default { get; } = new(ArtThresholds.Default);

    /// <summary>Text that differs whenever any rule differs, so a change of rules shows up in the collection version.</summary>
    public string Fingerprint => $"{Thresholds.Fingerprint}#{ShapeMarginPercent}#{OrientFromCover}";
}

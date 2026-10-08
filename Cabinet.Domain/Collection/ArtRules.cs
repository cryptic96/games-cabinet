namespace Cabinet.Domain.Collection;

/// <summary>
/// The rules read when the cabinet is built from the stored collection, as opposed to the facts stored with each picture.
/// Changing a rule changes which picture a game shows or how its box is shaped without any picture being fetched again.
/// </summary>
/// <param name="Thresholds">The tuning values that turn the stored measurements of a picture into a verdict.</param>
/// <param name="ShapeMarginPercent">How far, as a percentage of a flat cover's shape, the shape of the real dimensions may differ from it before the front is rebuilt from the cover.</param>
/// <param name="OrientFromCover">Whether pictures may turn a box front landscape at all, so its width is the longer side: a flat landscape cover always does, an unsure picture only beyond the landscape margin.</param>
/// <param name="UnsureLandscapeMarginPercent">How much wider than tall, as a percentage of its height, an unsure picture must be before it turns a box front landscape without changing its proportions.</param>
public sealed record ArtRules(
    ArtThresholds Thresholds,
    int ShapeMarginPercent = ArtRules.DefaultShapeMarginPercent,
    bool OrientFromCover = true,
    int UnsureLandscapeMarginPercent = ArtRules.DefaultUnsureLandscapeMarginPercent)
{
    /// <summary>The shape margin a cabinet starts with, in percent.</summary>
    public const int DefaultShapeMarginPercent = 12;

    /// <summary>The unsure landscape margin a cabinet starts with, in percent. It is a starting value to be tuned by looking at the cabinet.</summary>
    public const int DefaultUnsureLandscapeMarginPercent = 20;

    /// <summary>The rules a cabinet starts with.</summary>
    public static ArtRules Default { get; } = new(ArtThresholds.Default);

    /// <summary>Text that differs whenever any rule differs, so a change of rules shows up in the collection version.</summary>
    public string Fingerprint => $"{Thresholds.Fingerprint}#{ShapeMarginPercent}#{OrientFromCover}#{UnsureLandscapeMarginPercent}";
}

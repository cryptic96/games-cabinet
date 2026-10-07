using System.Globalization;

namespace Cabinet.Domain.Collection;

/// <summary>
/// What the detector measured on a picture, kept so the verdict can be derived again with different thresholds without
/// downloading anything. Every share is a fraction from 0 to 1.
/// </summary>
/// <param name="BackdropShare">The share of the picture that is plain backdrop around the subject.</param>
/// <param name="Fill">The share of the subject's bounding box that the subject covers.</param>
/// <param name="Corner1">The largest backdrop share among the four corners of the subject's bounding box.</param>
/// <param name="Corner2">The second largest backdrop share among those four corners.</param>
/// <param name="SidesTouched">How many of the picture's four sides the subject touches.</param>
public sealed record ArtFeatures(double BackdropShare, double Fill, double Corner1, double Corner2, int SidesTouched);

/// <summary>
/// The tuning values that turn <see cref="ArtFeatures"/> into a verdict. Every comparison is inclusive: a feature exactly
/// at a threshold meets it.
/// </summary>
/// <param name="FlatMinFill">The least fill a flat cover has.</param>
/// <param name="FlatMaxCorner">The most backdrop a flat cover shows in its emptiest corner.</param>
/// <param name="ThreeDMaxFill">The most fill a photographed 3D box has.</param>
/// <param name="ThreeDMinCorner">The least backdrop a 3D box shows in its second emptiest corner.</param>
/// <param name="DegenerateBackdropShare">The backdrop share from which a picture counts as flat because there is no subject to confuse.</param>
public sealed record ArtThresholds(
    double FlatMinFill,
    double FlatMaxCorner,
    double ThreeDMaxFill,
    double ThreeDMinCorner,
    double DegenerateBackdropShare)
{
    private const double DefaultFlatMinFill = 0.97;
    private const double DefaultFlatMaxCorner = 0.15;
    private const double DefaultThreeDMaxFill = 0.93;
    private const double DefaultThreeDMinCorner = 0.40;
    private const double DefaultDegenerateBackdropShare = 0.97;
    private const string FingerprintSeparator = "|";

    /// <summary>The starting thresholds.</summary>
    public static ArtThresholds Default { get; } = new(
        DefaultFlatMinFill,
        DefaultFlatMaxCorner,
        DefaultThreeDMaxFill,
        DefaultThreeDMinCorner,
        DefaultDegenerateBackdropShare);

    /// <summary>The five values as invariant-culture text, so a change to any threshold changes the text.</summary>
    public string Fingerprint => string.Join(
        FingerprintSeparator,
        new[] { FlatMinFill, FlatMaxCorner, ThreeDMaxFill, ThreeDMinCorner, DegenerateBackdropShare }
            .Select(value => value.ToString("R", CultureInfo.InvariantCulture)));
}

/// <summary>What a picture looks like: a flat cover, a photographed 3D box, or neither clearly.</summary>
public enum ArtVerdict
{
    /// <summary>A flat cover that fills its frame.</summary>
    Flat,

    /// <summary>Neither clearly flat nor clearly a photographed box.</summary>
    Unsure,

    /// <summary>A photographed box standing on a backdrop.</summary>
    ThreeD,
}

/// <summary>Derives a verdict from the stored detector features.</summary>
public static class ArtVerdicts
{
    private const double ScoreFillWeight = 0.5;
    private const double ScoreFillRange = 0.25;
    private const double ScoreCornerWeight = 0.5;
    private const double ScoreCornerRange = 0.7;

    /// <summary>
    /// A picture that is almost all backdrop is flat, since there is nothing to confuse. Otherwise it is flat when it fills
    /// its bounding box and every corner is full, a 3D shot when it leaves much of its bounding box empty and at least two
    /// corners show backdrop, and unsure in between.
    /// </summary>
    public static ArtVerdict Classify(ArtFeatures features, ArtThresholds thresholds)
    {
        if (features.BackdropShare >= thresholds.DegenerateBackdropShare)
        {
            return ArtVerdict.Flat;
        }

        if (features.Fill >= thresholds.FlatMinFill && features.Corner1 <= thresholds.FlatMaxCorner)
        {
            return ArtVerdict.Flat;
        }

        if (features.Fill <= thresholds.ThreeDMaxFill && features.Corner2 >= thresholds.ThreeDMinCorner)
        {
            return ArtVerdict.ThreeD;
        }

        return ArtVerdict.Unsure;
    }

    /// <summary>
    /// A continuous measure from 0 for a flat cover to 1 for a clear 3D shot, for the review sheet. The verdict never
    /// depends on it.
    /// </summary>
    public static double Score(ArtFeatures features) =>
        Math.Clamp(
            (ScoreFillWeight * (1 - features.Fill) / ScoreFillRange) + (ScoreCornerWeight * features.Corner2 / ScoreCornerRange),
            0,
            1);
}

/// <summary>Which picture a game's box is drawn from.</summary>
public enum ArtPick
{
    /// <summary>The picture of the edition the owner has.</summary>
    VersionImage,

    /// <summary>The main picture of the game.</summary>
    MainImage,

    /// <summary>No usable picture: the generated cover is drawn.</summary>
    GeneratedCover,
}

/// <summary>Chooses between the owned edition's picture and the game's main picture.</summary>
public static class ArtChooser
{
    /// <summary>
    /// The owned edition's picture wins when it is flat. When it is not flat, the main picture wins only if it is flat; with
    /// no flat cover anywhere the owned edition's picture is still preferred. A missing, failed or undecodable candidate is
    /// passed as null and is never chosen. With no usable picture the generated cover is drawn.
    /// </summary>
    /// <param name="version">The verdict for the owned edition's picture, or null when it is unusable.</param>
    /// <param name="main">The verdict for the main picture, or null when it is unusable.</param>
    public static ArtPick Choose(ArtVerdict? version, ArtVerdict? main)
    {
        if (version is null)
        {
            return main is null ? ArtPick.GeneratedCover : ArtPick.MainImage;
        }

        if (version == ArtVerdict.Flat)
        {
            return ArtPick.VersionImage;
        }

        return main == ArtVerdict.Flat ? ArtPick.MainImage : ArtPick.VersionImage;
    }
}

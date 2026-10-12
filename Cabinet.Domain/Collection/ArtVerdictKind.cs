namespace Cabinet.Domain.Collection;

/// <summary>
/// The five ways a picture is described to the owner. It refines <see cref="ArtVerdict"/> by splitting the 3D shots into the
/// marked kinds, so every screen that names a verdict uses the same five words.
/// </summary>
public enum ArtVerdictKind
{
    /// <summary>A flat cover that fills its frame.</summary>
    Flat,

    /// <summary>Neither clearly flat nor clearly a photographed box.</summary>
    Unsure,

    /// <summary>A photographed box standing on a backdrop, with no further mark.</summary>
    ThreeD,

    /// <summary>A cut-out product picture whose background is transparent around most of its outer ring.</summary>
    CutOut,

    /// <summary>A box photographed so close that it runs along all four sides.</summary>
    TightCrop,
}

/// <summary>Maps the detector's verdict to the kind the owner reads.</summary>
public static class ArtVerdictKinds
{
    /// <summary>
    /// A flat verdict is flat and an unsure verdict is unsure. A 3D verdict is a cut-out when the picture is marked as one,
    /// otherwise a tight crop when it is marked as one, otherwise a plain 3D shot; a picture with no stored features is a
    /// plain 3D shot.
    /// </summary>
    /// <param name="verdict">The detector's verdict.</param>
    /// <param name="features">What the detector measured, or null when it is not known.</param>
    public static ArtVerdictKind Of(ArtVerdict verdict, ArtFeatures? features) => verdict switch
    {
        ArtVerdict.Flat => ArtVerdictKind.Flat,
        ArtVerdict.ThreeD when features?.CutOut == true => ArtVerdictKind.CutOut,
        ArtVerdict.ThreeD when features?.TightCrop == true => ArtVerdictKind.TightCrop,
        ArtVerdict.ThreeD => ArtVerdictKind.ThreeD,
        _ => ArtVerdictKind.Unsure,
    };
}

namespace Cabinet.Domain.Layout;

/// <summary>
/// The placeholder colour table for generated boxes and the per-game picks from it. A game's tone and pattern come only
/// from its own identifier, so adding a game never recolours another. Every entry pairs a background with one of two
/// text colours, chosen so the text reads at a contrast of at least 4.5 to 1.
/// </summary>
public static class SpinePalette
{
    /// <summary>The number of cover patterns the page can draw.</summary>
    public const int PatternCount = 6;

    private const string LightText = "#ffffff";
    private const string DarkText = "#2a1a10";

    /// <summary>
    /// The tones in order: oxblood, forest, navy, mustard, teal, plum, rust, sky, cream, sage, slate and rose. The page
    /// applies the background and text of the chosen entry as the box colours, so colours taken from real box art can
    /// replace an entry later without any change to the page.
    /// </summary>
    public static IReadOnlyList<PaletteTone> Tones { get; } =
    [
        new("#8a2432", LightText),
        new("#1f5a3a", LightText),
        new("#23407a", LightText),
        new("#e0a82e", DarkText),
        new("#17766f", LightText),
        new("#6a2f7a", LightText),
        new("#b8481a", LightText),
        new("#8fbfe0", DarkText),
        new("#efe3c2", DarkText),
        new("#a3b88f", DarkText),
        new("#434f5e", LightText),
        new("#e09aa5", DarkText),
    ];

    /// <summary>The index into <see cref="Tones"/> for a game, fixed by its identifier alone.</summary>
    public static int ToneFor(int bggId) => StableHash.Bucket(bggId, StableHash.ToneSalt, Tones.Count);

    /// <summary>The cover pattern index, below <see cref="PatternCount"/>, for a game, fixed by its identifier alone.</summary>
    public static int PatternFor(int bggId) => StableHash.Bucket(bggId, StableHash.PatternSalt, PatternCount);
}

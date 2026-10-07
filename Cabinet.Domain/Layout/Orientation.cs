namespace Cabinet.Domain.Layout;

/// <summary>How a box is placed in its cubby.</summary>
public enum BoxPose
{
    /// <summary>Facing out so the whole front shows.</summary>
    Cover,

    /// <summary>Standing upright so only the spine shows.</summary>
    Spine,

    /// <summary>Lying flat with the spine facing out, in a short stack.</summary>
    Flat,
}

/// <summary>A coarse size of a box, taken from its standing height.</summary>
public enum SizeClass
{
    /// <summary>A short box.</summary>
    Small,

    /// <summary>A box of ordinary height.</summary>
    Standard,

    /// <summary>A tall box.</summary>
    Large,
}

/// <summary>
/// Decides how each game is chosen to stand. A decision depends only on that game, the settings and the design, never on
/// the other games, so adding a game never changes the pose it is chosen for. The one exception is the few-games switch,
/// which looks at how many games there are and is accepted as a single global rearrangement. The engine may still lay a
/// game flat when no cubby has room for it in the pose it was chosen for. The choice reads the item's pose height and never
/// the shape its picture gives the drawn box, so changing how a picture is judged never changes how a box stands.
/// </summary>
public static class Orientation
{
    /// <summary>Boxes lower than this stand as small ones, in millimetres.</summary>
    public const int SmallBelowHeightMm = 200;

    /// <summary>Boxes lower than this and not small stand as standard ones; taller boxes are large, in millimetres.</summary>
    public const int StandardBelowHeightMm = 320;

    /// <summary>A box at most this deep may lie flat whatever its size, in millimetres. A starting value for review.</summary>
    public const int FlatDepthLimitMm = 40;

    /// <summary>An expansion at least this deep stands upright beside its base game instead of lying in the stack, in millimetres. A starting value for review.</summary>
    public const int UprightExpansionMinDepthMm = 50;

    /// <summary>The most expansions of one base game that stand upright beside it. A starting value for review.</summary>
    public const int MaxUprightExpansions = 2;

    /// <summary>The chance, in basis points, that an eligible box lies flat instead of standing. A starting value for review.</summary>
    public const int FlatChanceBasisPoints = 5000;

    private const int SmallWeight = 30;
    private const int StandardWeight = 100;
    private const int LargeWeight = 220;
    private const int MaxChanceBasisPoints = 9500;
    private const int BasisPoints = 10000;
    private const int BasisPointsPerPercent = 100;

    /// <summary>
    /// Whether an expansion is thick enough to stand upright beside its base game: its box is at least
    /// <see cref="UprightExpansionMinDepthMm"/> deep. It is judged on the box as owned, before any scaling to the design.
    /// </summary>
    public static bool StandsUpright(CabinetItem expansion)
    {
        ArgumentNullException.ThrowIfNull(expansion);

        return expansion.Box.DepthMm >= UprightExpansionMinDepthMm;
    }

    /// <summary>The size class of a box from its standing height.</summary>
    public static SizeClass SizeClassOf(BoxDimensions box)
    {
        ArgumentNullException.ThrowIfNull(box);

        if (box.HeightMm < SmallBelowHeightMm)
        {
            return SizeClass.Small;
        }

        return box.HeightMm < StandardBelowHeightMm ? SizeClass.Standard : SizeClass.Large;
    }

    /// <summary>
    /// The chance, in basis points, that a box of the size class faces out under the size-weighted strategy: the share
    /// times a fixed weight for the class, capped so some boxes always stand. The weights are constants, so they never
    /// depend on the collection.
    /// </summary>
    public static int CoverChanceBasisPoints(int sharePercent, SizeClass sizeClass)
    {
        var weight = sizeClass switch
        {
            SizeClass.Small => SmallWeight,
            SizeClass.Standard => StandardWeight,
            _ => LargeWeight,
        };

        return Math.Min(MaxChanceBasisPoints, sharePercent * weight);
    }

    /// <summary>
    /// Decides whether the game faces out, stands as a spine or lies flat. When <paramref name="fewGames"/> is true every
    /// box faces out. Otherwise the strategy picks the covers, and a small or thin box that does not face out may lie flat.
    /// </summary>
    public static BoxPose Decide(CabinetItem item, LayoutOptions options, SectionDesign design, bool fewGames)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(design);

        if (fewGames || FacesOut(item, options, design))
        {
            return BoxPose.Cover;
        }

        return CanLieFlat(PoseBox(item)) && StableHash.Bucket(item.BggId, StableHash.FlatSalt, BasisPoints) < FlatChanceBasisPoints
            ? BoxPose.Flat
            : BoxPose.Spine;
    }

    private static BoxDimensions PoseBox(CabinetItem item) =>
        item.PoseHeightMm is { } height ? item.Box with { HeightMm = height } : item.Box;

    private static bool CanLieFlat(BoxDimensions box) =>
        SizeClassOf(box) == SizeClass.Small || box.DepthMm <= FlatDepthLimitMm;

    private static bool FacesOut(CabinetItem item, LayoutOptions options, SectionDesign design)
    {
        if (options.CoverStrategy == CoverStrategy.OversizeOnly)
        {
            return PoseBox(item).HeightMm > design.MaxSpineHeightMm;
        }

        var chance = options.CoverStrategy == CoverStrategy.Random
            ? options.CoverSharePercent * BasisPointsPerPercent
            : CoverChanceBasisPoints(options.CoverSharePercent, SizeClassOf(PoseBox(item)));

        return StableHash.Bucket(item.BggId, StableHash.CoverSalt, BasisPoints) < chance;
    }
}

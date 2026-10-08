using System.Globalization;

namespace Cabinet.Domain.Layout;

/// <summary>How the boxes that face out are chosen.</summary>
public enum CoverStrategy
{
    /// <summary>Larger boxes are much more likely to face out, so the big, striking boxes show their art.</summary>
    SizeWeighted,

    /// <summary>Every box has the same chance to face out, decided from its game identifier alone.</summary>
    Random,

    /// <summary>Exactly the boxes too tall to stand upright in the design face out, whatever the share.</summary>
    OversizeOnly,
}

/// <summary>The settings that tune the look of the cabinet. They are server settings, never visitor controls.</summary>
/// <param name="CoverSharePercent">The share of boxes that face out, from 0 to 100; the size-weighted strategy spreads it unevenly by size.</param>
/// <param name="CoverStrategy">How the boxes that face out are chosen.</param>
/// <param name="ExpansionStackMax">The most expansions drawn in one stack beside a game, from 1 to 20.</param>
/// <param name="FewGamesThreshold">Below this many top-level games every box faces out, from 0 to 100.</param>
/// <param name="LieFlatBeforeNewSection">
/// Whether a game that fits no cubby of the existing sections the way it was chosen to stand may lie flat in the first
/// cubby that can take it lying down, before a new section is opened. Turning it off may add sections.
/// </param>
/// <param name="GroupSeries">
/// Whether the games of one series (a shared BGG series family or a shared title key) are placed together. Turning it off
/// places every game on its own, which gives the arrangement made without any series.
/// </param>
/// <param name="CoverFromExpansions">
/// The fewest owned expansions that make a base game face out, from 0 to 20; 0 turns the rule off. A base game with at
/// least this many owned expansions faces out whatever the strategy and the share.
/// </param>
public sealed record LayoutOptions(
    int CoverSharePercent,
    CoverStrategy CoverStrategy,
    int ExpansionStackMax,
    int FewGamesThreshold,
    bool LieFlatBeforeNewSection = true,
    bool GroupSeries = true,
    int CoverFromExpansions = 2)
{
    private const int MaxShare = 100;
    private const int MaxStack = 20;
    private const int MaxThreshold = 100;
    private const int MaxCoverFromExpansions = 20;
    private const int StrategyShift = 8;
    private const int StackShift = 16;
    private const int ThresholdShift = 32;
    private const int LieFlatShift = 40;
    private const int GroupSeriesShift = 41;
    private const int CoverFromExpansionsShift = 48;

    /// <summary>The committed starting values: a quarter of the boxes face out, size weighted, stacks of six, twelve games to leave the all-covers look, a big box lies flat before a new section opens, the games of a series stand together, and a base game with two owned expansions faces out.</summary>
    public static LayoutOptions Default { get; } = new(25, CoverStrategy.SizeWeighted, 6, 12, true, true, 2);

    /// <summary>Sixteen lowercase hexadecimal digits that change whenever any setting changes.</summary>
    public string Fingerprint => StableHash.Hash(Pack(), StableHash.OptionsSalt).ToString("x16", CultureInfo.InvariantCulture);

    /// <summary>Throws when a setting is outside its range, naming the property.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A setting is out of range.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(CoverSharePercent, 0, nameof(CoverSharePercent));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(CoverSharePercent, MaxShare, nameof(CoverSharePercent));

        if (!Enum.IsDefined(CoverStrategy))
        {
            throw new ArgumentOutOfRangeException(nameof(CoverStrategy), CoverStrategy, "The cover strategy is not a known strategy.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(ExpansionStackMax, 1, nameof(ExpansionStackMax));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ExpansionStackMax, MaxStack, nameof(ExpansionStackMax));
        ArgumentOutOfRangeException.ThrowIfLessThan(FewGamesThreshold, 0, nameof(FewGamesThreshold));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(FewGamesThreshold, MaxThreshold, nameof(FewGamesThreshold));
        ArgumentOutOfRangeException.ThrowIfLessThan(CoverFromExpansions, 0, nameof(CoverFromExpansions));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(CoverFromExpansions, MaxCoverFromExpansions, nameof(CoverFromExpansions));
    }

    private long Pack() =>
        (long)CoverSharePercent
        | ((long)(int)CoverStrategy << StrategyShift)
        | ((long)ExpansionStackMax << StackShift)
        | ((long)FewGamesThreshold << ThresholdShift)
        | ((LieFlatBeforeNewSection ? 1L : 0L) << LieFlatShift)
        | ((GroupSeries ? 1L : 0L) << GroupSeriesShift)
        | ((long)CoverFromExpansions << CoverFromExpansionsShift);
}

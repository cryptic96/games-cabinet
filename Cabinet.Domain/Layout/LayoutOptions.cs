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
public sealed record LayoutOptions(
    int CoverSharePercent,
    CoverStrategy CoverStrategy,
    int ExpansionStackMax,
    int FewGamesThreshold)
{
    private const int MaxShare = 100;
    private const int MaxStack = 20;
    private const int MaxThreshold = 100;
    private const int StrategyShift = 8;
    private const int StackShift = 16;
    private const int ThresholdShift = 32;

    /// <summary>The committed starting values: a quarter of the boxes face out, size weighted, stacks of six, twelve games to leave the all-covers look.</summary>
    public static LayoutOptions Default { get; } = new(25, CoverStrategy.SizeWeighted, 6, 12);

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
    }

    private long Pack() =>
        (long)CoverSharePercent
        | ((long)(int)CoverStrategy << StrategyShift)
        | ((long)ExpansionStackMax << StackShift)
        | ((long)FewGamesThreshold << ThresholdShift);
}

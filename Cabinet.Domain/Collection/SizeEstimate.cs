using Cabinet.Domain.Layout;

namespace Cabinet.Domain.Collection;

/// <summary>A coarse box size estimated from what is known about a game, for games whose real box size is not known.</summary>
public enum BoxSizeClass
{
    /// <summary>A small card-game box.</summary>
    Compact,

    /// <summary>A small box.</summary>
    Small,

    /// <summary>An ordinary box.</summary>
    Standard,

    /// <summary>A large, deep box.</summary>
    Large,

    /// <summary>The biggest and deepest kind of box.</summary>
    ExtraLarge,
}

/// <summary>
/// Estimates how big a game's box is from its weight, playing time and player count. A game scores in half points; the
/// score falls into a band that names a size class. Once a class is stored it only changes when the score has moved clear
/// of that class's band, so small drift in the source data never changes a box, and a change of the model version starts
/// every game over.
/// </summary>
public static class SizeEstimate
{
    /// <summary>The version of the scoring and the class dimensions; a stored class from another version is worked out again.</summary>
    public const int ModelVersion = 1;

    /// <summary>The distance in score a game must move beyond the edge of its stored class's band before the class changes.</summary>
    public const double HysteresisPoints = 0.5;

    /// <summary>The weight below which a game is a light one.</summary>
    public const double LightWeightBelow = 1.5;

    /// <summary>The weight below which a game is a medium-light one.</summary>
    public const double MediumWeightBelow = 2.5;

    /// <summary>The weight below which a game is a medium-heavy one.</summary>
    public const double HeavyWeightBelow = 3.5;

    /// <summary>The playing time, in minutes, up to which a game is short.</summary>
    public const int ShortPlayMinutes = 30;

    /// <summary>The playing time, in minutes, up to which a game is of ordinary length.</summary>
    public const int MediumPlayMinutes = 60;

    /// <summary>The playing time, in minutes, up to which a game is long.</summary>
    public const int LongPlayMinutes = 120;

    /// <summary>The player count up to which a game is a duel.</summary>
    public const int DuelMaxPlayers = 2;

    /// <summary>The player count up to which a game is for a small group.</summary>
    public const int SmallGroupMaxPlayers = 4;

    /// <summary>The score from which a game is not compact.</summary>
    public const double SmallFromScore = 2;

    /// <summary>The score from which a game is not small.</summary>
    public const double StandardFromScore = 4;

    /// <summary>The score from which a game is not standard.</summary>
    public const double LargeFromScore = 6;

    /// <summary>The score from which a game is not large.</summary>
    public const double ExtraLargeFromScore = 7.5;

    private const double UnknownBandScore = 1.5;
    private const double UnknownPlayerScore = 1;
    private const int SecondBandScore = 1;
    private const int FourthBandScore = 3;
    private const int ThirdBandScore = 2;

    /// <summary>
    /// The score of a game: a weight band, a playing time band and a player band added together, in half points. An unknown
    /// input counts as the middle of its band, so a game about which nothing is known scores as a standard one.
    /// </summary>
    /// <param name="details">What is known about the game, or null when nothing is.</param>
    public static double Score(GameDetails? details) =>
        WeightScore(details?.Weight) + PlayTimeScore(PlayTimeOf(details)) + PlayerScore(details?.MaxPlayers);

    /// <summary>The class a score falls into.</summary>
    /// <param name="score">The score of a game.</param>
    public static BoxSizeClass ClassFor(double score) => score switch
    {
        < SmallFromScore => BoxSizeClass.Compact,
        < StandardFromScore => BoxSizeClass.Small,
        < LargeFromScore => BoxSizeClass.Standard,
        < ExtraLargeFromScore => BoxSizeClass.Large,
        _ => BoxSizeClass.ExtraLarge,
    };

    /// <summary>
    /// The class of a game. With no earlier class, or one worked out by another model version, it is the class of the
    /// score. Otherwise the earlier class stays unless the score lies at least <see cref="HysteresisPoints"/> below the
    /// bottom or at least that much above the top of that class's band.
    /// </summary>
    /// <param name="details">What is known about the game, or null when nothing is; null gives null.</param>
    /// <param name="previous">The class stored before, or null when there is none.</param>
    /// <param name="previousModelVersion">The model version the stored class was worked out by, or null when unknown.</param>
    public static BoxSizeClass? Assign(GameDetails? details, BoxSizeClass? previous, int? previousModelVersion)
    {
        if (details is null)
        {
            return null;
        }

        var score = Score(details);

        if (previous is not { } kept || previousModelVersion != ModelVersion)
        {
            return ClassFor(score);
        }

        var (lower, upper) = BandOf(kept);

        return score <= lower - HysteresisPoints || score >= upper + HysteresisPoints ? ClassFor(score) : kept;
    }

    /// <summary>The box a class stands for, for a base game or an expansion.</summary>
    /// <param name="sizeClass">The class.</param>
    /// <param name="kind">Whether the item is a standalone game or an expansion.</param>
    public static BoxDimensions Dimensions(BoxSizeClass sizeClass, ItemKind kind) =>
        kind == ItemKind.Expansion
            ? sizeClass switch
            {
                BoxSizeClass.Compact => new BoxDimensions(90, 130, 20),
                BoxSizeClass.Small => new BoxDimensions(130, 180, 30),
                BoxSizeClass.Standard => new BoxDimensions(200, 260, 40),
                BoxSizeClass.Large => new BoxDimensions(250, 250, 50),
                _ => new BoxDimensions(280, 280, 70),
            }
            : sizeClass switch
            {
                BoxSizeClass.Compact => new BoxDimensions(100, 140, 30),
                BoxSizeClass.Small => new BoxDimensions(150, 200, 45),
                BoxSizeClass.Standard => new BoxDimensions(225, 300, 60),
                BoxSizeClass.Large => new BoxDimensions(300, 300, 75),
                _ => new BoxDimensions(300, 420, 90),
            };

    private static (double Lower, double Upper) BandOf(BoxSizeClass sizeClass) => sizeClass switch
    {
        BoxSizeClass.Compact => (double.NegativeInfinity, SmallFromScore),
        BoxSizeClass.Small => (SmallFromScore, StandardFromScore),
        BoxSizeClass.Standard => (StandardFromScore, LargeFromScore),
        BoxSizeClass.Large => (LargeFromScore, ExtraLargeFromScore),
        _ => (ExtraLargeFromScore, double.PositiveInfinity),
    };

    private static int? PlayTimeOf(GameDetails? details)
    {
        if (details is null)
        {
            return null;
        }

        return new[] { details.PlayingTime, details.MaxPlayTime, details.MinPlayTime }
            .FirstOrDefault(minutes => minutes is > 0);
    }

    private static double WeightScore(double? weight)
    {
        if (weight is not > 0 || !double.IsFinite(weight.Value))
        {
            return UnknownBandScore;
        }

        if (weight < LightWeightBelow)
        {
            return 0;
        }

        if (weight < MediumWeightBelow)
        {
            return SecondBandScore;
        }

        return weight < HeavyWeightBelow ? ThirdBandScore : FourthBandScore;
    }

    private static double PlayTimeScore(int? minutes)
    {
        if (minutes is not > 0)
        {
            return UnknownBandScore;
        }

        if (minutes <= ShortPlayMinutes)
        {
            return 0;
        }

        if (minutes <= MediumPlayMinutes)
        {
            return SecondBandScore;
        }

        return minutes <= LongPlayMinutes ? ThirdBandScore : FourthBandScore;
    }

    private static double PlayerScore(int? maxPlayers)
    {
        if (maxPlayers is not > 0)
        {
            return UnknownPlayerScore;
        }

        if (maxPlayers <= DuelMaxPlayers)
        {
            return 0;
        }

        return maxPlayers <= SmallGroupMaxPlayers ? SecondBandScore : ThirdBandScore;
    }
}

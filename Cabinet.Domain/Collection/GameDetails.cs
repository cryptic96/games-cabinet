using Cabinet.Domain.Layout;

namespace Cabinet.Domain.Collection;

/// <summary>
/// What the details call of the source knows about one game. Every count and rating is null when the source gave none or
/// gave a value that cannot be true (zero, negative, not a number). The lists keep the order the source gave them,
/// without duplicates and capped in length.
/// </summary>
/// <param name="EnrichedAtUtc">When these details were read.</param>
/// <param name="MinPlayers">The fewest players, or null when unknown.</param>
/// <param name="MaxPlayers">The most players, or null when unknown.</param>
/// <param name="PlayingTime">The stated playing time in minutes, or null when unknown.</param>
/// <param name="MinPlayTime">The shortest playing time in minutes, or null when unknown.</param>
/// <param name="MaxPlayTime">The longest playing time in minutes, or null when unknown.</param>
/// <param name="MinAge">The minimum age in years, or null when unknown.</param>
/// <param name="Weight">The average complexity rating, or null when unknown.</param>
/// <param name="Average">The average user rating, or null when unknown.</param>
/// <param name="BayesAverage">The ranked (Bayesian average) user rating, or null when unknown.</param>
/// <param name="Designers">The names of the designers.</param>
/// <param name="Mechanics">The names of the mechanics.</param>
/// <param name="ExpandsGames">The games this game is an expansion of; empty for a game that expands nothing.</param>
/// <param name="MainImageUrl">The canonical address of the game's main picture, or null when the source gave none.</param>
/// <param name="EstimatedSize">The box size class worked out from these details, or null when it has not been worked out.</param>
/// <param name="EstimateModelVersion">The version of the estimate that produced <paramref name="EstimatedSize"/>, or null when there is none.</param>
public sealed record GameDetails(
    DateTimeOffset EnrichedAtUtc,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayingTime,
    int? MinPlayTime,
    int? MaxPlayTime,
    int? MinAge,
    double? Weight,
    double? Average,
    double? BayesAverage,
    IReadOnlyList<string> Designers,
    IReadOnlyList<string> Mechanics,
    IReadOnlyList<BaseGameRef> ExpandsGames,
    string? MainImageUrl,
    BoxSizeClass? EstimatedSize = null,
    int? EstimateModelVersion = null)
{
    /// <summary>Whether another set of details says exactly the same, comparing the lists by their contents.</summary>
    /// <param name="other">The details to compare with; null is never the same.</param>
    public bool SameAs(GameDetails? other) =>
        other is not null
        && EnrichedAtUtc == other.EnrichedAtUtc
        && MinPlayers == other.MinPlayers
        && MaxPlayers == other.MaxPlayers
        && PlayingTime == other.PlayingTime
        && MinPlayTime == other.MinPlayTime
        && MaxPlayTime == other.MaxPlayTime
        && MinAge == other.MinAge
        && Weight == other.Weight
        && Average == other.Average
        && BayesAverage == other.BayesAverage
        && MainImageUrl == other.MainImageUrl
        && EstimatedSize == other.EstimatedSize
        && EstimateModelVersion == other.EstimateModelVersion
        && Designers.SequenceEqual(other.Designers)
        && Mechanics.SequenceEqual(other.Mechanics)
        && ExpandsGames.SequenceEqual(other.ExpandsGames);
}

/// <summary>The answer of a details source: the details of the games it knows or the reason there are none.</summary>
public abstract record EnrichmentFetchResult
{
    /// <summary>The source answered.</summary>
    /// <param name="Games">The details by game identifier; a requested game the source did not describe is absent.</param>
    public sealed record Fetched(IReadOnlyDictionary<int, GameDetails> Games) : EnrichmentFetchResult;

    /// <summary>The source could not provide the details.</summary>
    /// <param name="Failure">Why not.</param>
    public sealed record Failed(SyncFailure Failure) : EnrichmentFetchResult;
}

/// <summary>Reads the details of games from wherever they live.</summary>
public interface IEnrichmentSource
{
    /// <summary>Fetches the details of up to <see cref="EnrichmentPlanner.MaxIdsPerRequest"/> games, or says why it could not.</summary>
    /// <param name="gameIds">The games to describe.</param>
    /// <param name="cancellationToken">Cancels the fetch.</param>
    Task<EnrichmentFetchResult> FetchDetailsAsync(IReadOnlyList<int> gameIds, CancellationToken cancellationToken);
}

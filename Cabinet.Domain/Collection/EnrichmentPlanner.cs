namespace Cabinet.Domain.Collection;

/// <summary>How much of the details work one sync run may do.</summary>
/// <param name="MaxThingRequestsPerRun">The most details calls one run makes.</param>
/// <param name="RefreshBatchesPerRun">The most calls one run spends refreshing games whose details are already known.</param>
/// <param name="RefreshAfter">How old known details must be before they are refreshed.</param>
public sealed record EnrichmentOptions(int MaxThingRequestsPerRun, int RefreshBatchesPerRun, TimeSpan RefreshAfter)
{
    /// <summary>At most 25 calls a run, one of them a refresh, and a refresh after a week.</summary>
    public static EnrichmentOptions Default { get; } = new(25, 1, TimeSpan.FromDays(7));
}

/// <summary>Decides which games to ask the source about in a run, new games first and then the longest-known ones.</summary>
public static class EnrichmentPlanner
{
    /// <summary>The most games a single details call may name.</summary>
    public const int MaxIdsPerRequest = 20;

    /// <summary>
    /// Plans the calls of one run. Games without details come first, in collection order; then the games whose details were
    /// read when the step read less than it does now (their <see cref="GameDetails.DetailsVersion"/> is not
    /// <see cref="GameDetails.CurrentDetailsVersion"/>), in collection order, which do not count against the refresh
    /// calls; then, at most <see cref="EnrichmentOptions.RefreshBatchesPerRun"/> calls' worth of the other games whose
    /// details are at least <see cref="EnrichmentOptions.RefreshAfter"/> old, the oldest first and the lower game identifier
    /// first among equals. No more than <see cref="EnrichmentOptions.MaxThingRequestsPerRun"/> calls are planned in all. The same input always
    /// gives the same plan.
    /// </summary>
    /// <param name="items">The owned items.</param>
    /// <param name="games">The details known so far, by game identifier; null when none are known.</param>
    /// <param name="now">The current time.</param>
    /// <param name="options">The limits of a run.</param>
    public static IReadOnlyList<IReadOnlyList<int>> Plan(
        IReadOnlyList<SnapshotItem> items,
        IReadOnlyDictionary<int, GameDetails>? games,
        DateTimeOffset now,
        EnrichmentOptions options)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(options);

        var known = games ?? new Dictionary<int, GameDetails>();
        var ids = items
            .OrderBy(item => item.CollectionId)
            .ThenBy(item => item.GameId)
            .Select(item => item.GameId)
            .Distinct()
            .ToList();

        var fresh = ids.Where(id => !known.ContainsKey(id));
        var outdated = ids.Where(id => known.TryGetValue(id, out var details) && details.DetailsVersion != GameDetails.CurrentDetailsVersion).ToHashSet();
        var stale = ids
            .Where(id => !outdated.Contains(id) && known.TryGetValue(id, out var details) && now - details.EnrichedAtUtc >= options.RefreshAfter)
            .OrderBy(id => known[id].EnrichedAtUtc)
            .ThenBy(id => id);

        IEnumerable<IReadOnlyList<int>> batches = fresh
            .Chunk(MaxIdsPerRequest)
            .Select(batch => (IReadOnlyList<int>)batch)
            .Concat(ids.Where(outdated.Contains).Chunk(MaxIdsPerRequest).Select(batch => (IReadOnlyList<int>)batch))
            .Concat(stale.Chunk(MaxIdsPerRequest).Take(Math.Max(0, options.RefreshBatchesPerRun)).Select(batch => (IReadOnlyList<int>)batch));

        return [.. batches.Take(Math.Max(0, options.MaxThingRequestsPerRun))];
    }
}

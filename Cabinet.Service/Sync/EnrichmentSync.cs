using Cabinet.Domain.Collection;

namespace Cabinet.Service.Sync;

/// <summary>How the details step of a sync run ended.</summary>
/// <param name="Requests">How many details calls were sent.</param>
/// <param name="Enriched">How many games had their details replaced.</param>
/// <param name="Failure">Why the step stopped early, or null when it did not.</param>
public sealed record EnrichmentSyncResult(int Requests, int Enriched, SyncFailure? Failure);

/// <summary>
/// The details step of a sync run. It asks the source about the games <see cref="EnrichmentPlanner"/> names, one call at a
/// time, and commits after every answer so details show on the next redraw while later calls are still to come. It starts
/// no call after the deadline and stops at the first failed call, keeping the details already known; the next run asks
/// again. A game the answer leaves out keeps its previous details. It logs the failure category and counts only.
/// </summary>
/// <param name="sourceFactory">Creates the source for each run, so a pooled connection never outlives its handler's lifetime.</param>
/// <param name="options">The limits of a run.</param>
/// <param name="time">The clock the deadline and the age of known details are measured on.</param>
/// <param name="logger">Receives the failure category and the counts.</param>
public sealed class EnrichmentSync(
    Func<IEnrichmentSource> sourceFactory,
    EnrichmentOptions options,
    TimeProvider time,
    ILogger<EnrichmentSync> logger)
{
    /// <summary>Fetches the details that are due, within the per-run limits and the deadline.</summary>
    /// <param name="snapshot">The stored collection with the details known so far.</param>
    /// <param name="deadline">The moment after which no further call starts.</param>
    /// <param name="commit">Stores a snapshot and shows it; called after every answer that brought details.</param>
    /// <param name="cancellationToken">Cancels the step.</param>
    public async Task<EnrichmentSyncResult> RunAsync(
        CollectionSnapshot snapshot,
        DateTimeOffset deadline,
        Func<CollectionSnapshot, Task> commit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(commit);

        var batches = EnrichmentPlanner.Plan(snapshot.Items, snapshot.Games, time.GetUtcNow(), options);
        var games = new Dictionary<int, GameDetails>(snapshot.Games ?? new Dictionary<int, GameDetails>());
        var requests = 0;
        var enriched = 0;
        SyncFailure? failure = null;
        IEnrichmentSource? source = null;

        foreach (var batch in batches)
        {
            if (time.GetUtcNow() >= deadline)
            {
                break;
            }

            source ??= sourceFactory();
            requests++;

            var answer = await source.FetchDetailsAsync(batch, cancellationToken);

            if (answer is EnrichmentFetchResult.Failed failed)
            {
                failure = failed.Failure;
                logger.LogWarning("BGG details failed: {Failure}", failed.Failure);

                break;
            }

            var fetched = (EnrichmentFetchResult.Fetched)answer;

            foreach (var (id, details) in fetched.Games)
            {
                games.TryGetValue(id, out var previous);
                games[id] = details with
                {
                    EstimatedSize = SizeEstimate.Assign(details, previous?.EstimatedSize, previous?.EstimateModelVersion),
                    EstimateModelVersion = SizeEstimate.ModelVersion,
                };
                enriched++;
            }

            if (fetched.Games.Count > 0)
            {
                snapshot = snapshot with { Games = new Dictionary<int, GameDetails>(games) };
                await commit(snapshot);
            }
        }

        logger.LogInformation("Game details: {Requests} calls, {Enriched} games described.", requests, enriched);

        return new EnrichmentSyncResult(requests, enriched, failure);
    }
}

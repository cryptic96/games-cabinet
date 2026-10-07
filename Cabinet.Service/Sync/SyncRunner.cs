using Cabinet.Domain.Collection;
using Cabinet.Service.Collection;

namespace Cabinet.Service.Sync;

/// <summary>How one sync run ended.</summary>
/// <param name="Result">What happened to the collection.</param>
/// <param name="Failure">Why the run failed; <see cref="SyncFailure.None"/> unless the result is a failure.</param>
/// <param name="HeldBack">The collection the run set aside, when the result is held back; otherwise null.</param>
public sealed record SyncRunResult(SyncResult Result, SyncFailure Failure, HeldBackRecord? HeldBack = null);

/// <summary>
/// Runs one sync: fetch the owned collection, let the shrink guard judge it, and when it is accepted and differs from the
/// shown one, store it and then show it. The stored copy is written first, so a restart never shows something older than
/// what visitors saw. A collection the guard holds back is neither stored nor shown. It logs the failure category only,
/// never an address, an answer or a title.
/// </summary>
public sealed class SyncRunner
{
    private readonly Func<ICollectionSource> _sourceFactory;
    private readonly ISnapshotStore _snapshots;
    private readonly CollectionStore _collection;
    private readonly TimeProvider _time;
    private readonly ILogger<SyncRunner> _logger;

    /// <summary>Creates the runner.</summary>
    /// <param name="sourceFactory">Creates the source for each run, so a pooled connection never outlives its handler's lifetime.</param>
    /// <param name="snapshots">Stores the collection.</param>
    /// <param name="collection">Holds the collection visitors see.</param>
    /// <param name="time">The clock that stamps a captured collection.</param>
    /// <param name="logger">Receives the failure category.</param>
    public SyncRunner(
        Func<ICollectionSource> sourceFactory,
        ISnapshotStore snapshots,
        CollectionStore collection,
        TimeProvider time,
        ILogger<SyncRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(sourceFactory);
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);

        _sourceFactory = sourceFactory;
        _snapshots = snapshots;
        _collection = collection;
        _time = time;
        _logger = logger;
    }

    /// <summary>Fetches the collection and applies it when the guard accepts it and it changed.</summary>
    /// <param name="previousHeldBack">The collection the previous sync set aside, or null when none is waiting for confirmation.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    public async Task<SyncRunResult> RunAsync(HeldBackRecord? previousHeldBack, CancellationToken cancellationToken)
    {
        var fetched = await _sourceFactory().FetchOwnedAsync(cancellationToken);

        if (fetched is not CollectionFetchResult.Fetched collection)
        {
            var failure = ((CollectionFetchResult.Failed)fetched).Failure;
            _logger.LogWarning("BGG sync failed: {Failure}", failure);

            return new SyncRunResult(SyncResult.Failed, failure);
        }

        var entryIds = collection.Items.Select(item => item.CollectionId).Distinct().ToList();

        if (ShrinkGuard.Evaluate(_collection.Current.Items.Count, entryIds, previousHeldBack) is GuardDecision.HeldBack held)
        {
            _logger.LogWarning("BGG sync held back a collection: {Kind}", held.Kind);

            return new SyncRunResult(
                SyncResult.HeldBack,
                SyncFailure.None,
                new HeldBackRecord(held.Kind, held.Fingerprint, held.Count, _time.GetUtcNow()));
        }

        var snapshot = new CollectionSnapshot(CollectionSnapshot.CurrentSchemaVersion, _time.GetUtcNow(), collection.Items);
        var next = CollectionState.FromSnapshot(snapshot);

        if (next.Version == _collection.Current.Version)
        {
            return new SyncRunResult(SyncResult.Unchanged, SyncFailure.None);
        }

        _snapshots.Save(snapshot);
        _collection.Replace(next);

        return new SyncRunResult(SyncResult.Changed, SyncFailure.None);
    }
}

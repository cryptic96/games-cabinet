using Cabinet.Domain.Collection;
using Cabinet.Repository.Images;
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
/// what visitors saw. A collection the guard holds back is neither stored nor shown. When a stored collection exists but
/// could not be read, each run tries to read it again first; while it still cannot be read, the run does not treat itself as
/// the first sync, so a stored file is never replaced by an empty answer or by one answer that nothing confirms. Once the
/// collection is stored and shown, the run fetches the game details that are due and then the box pictures that are due,
/// within a time limit of its own, and stores the records of how each went; details or a picture that go wrong never fail
/// the run. It logs the failure category only, never an address, an answer or a title.
/// </summary>
public sealed class SyncRunner
{
    /// <summary>How long after a run began it may still start a picture download.</summary>
    public static readonly TimeSpan ExtrasDeadline = TimeSpan.FromMinutes(6);

    private readonly Func<ICollectionSource> _sourceFactory;
    private readonly ISnapshotStore _snapshots;
    private readonly CollectionStore _collection;
    private readonly TimeProvider _time;
    private readonly ILogger<SyncRunner> _logger;
    private readonly ArtSync? _art;
    private readonly ArtCache? _cache;
    private readonly ImageOptions? _images;
    private readonly EnrichmentSync? _enrichment;
    private CollectionSnapshot? _stored;

    /// <summary>Creates the runner.</summary>
    /// <param name="sourceFactory">Creates the source for each run, so a pooled connection never outlives its handler's lifetime.</param>
    /// <param name="snapshots">Stores the collection.</param>
    /// <param name="collection">Holds the collection visitors see.</param>
    /// <param name="time">The clock that stamps a captured collection.</param>
    /// <param name="logger">Receives the failure category.</param>
    /// <param name="art">Fetches the box pictures; null leaves the run without a picture step.</param>
    /// <param name="cache">The stored pictures, pruned after the run; null leaves them alone.</param>
    /// <param name="images">The picture rules; the picture step needs them.</param>
    /// <param name="enrichment">Fetches the game details; null leaves the run without a details step.</param>
    public SyncRunner(
        Func<ICollectionSource> sourceFactory,
        ISnapshotStore snapshots,
        CollectionStore collection,
        TimeProvider time,
        ILogger<SyncRunner> logger,
        ArtSync? art = null,
        ArtCache? cache = null,
        ImageOptions? images = null,
        EnrichmentSync? enrichment = null)
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
        _art = art;
        _cache = cache;
        _images = images;
        _enrichment = enrichment;
    }

    /// <summary>Fetches the collection and applies it when the guard accepts it and it changed, then fetches the box pictures that are due.</summary>
    /// <param name="previousHeldBack">The collection the previous sync set aside, or null when none is waiting for confirmation.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    public async Task<SyncRunResult> RunAsync(HeldBackRecord? previousHeldBack, CancellationToken cancellationToken)
    {
        var started = _time.GetUtcNow();
        var fetched = await _sourceFactory().FetchOwnedAsync(cancellationToken);

        if (fetched is not CollectionFetchResult.Fetched collection)
        {
            var failure = ((CollectionFetchResult.Failed)fetched).Failure;
            _logger.LogWarning("BGG sync failed: {Failure}", failure);

            return new SyncRunResult(SyncResult.Failed, failure);
        }

        var entryIds = collection.Items.Select(item => item.CollectionId).Distinct().ToList();

        var unreadable = StoredCollectionStillUnreadable();

        if (ShrinkGuard.Evaluate(_collection.Current.Items.Count, entryIds, previousHeldBack, unreadable) is GuardDecision.HeldBack held)
        {
            _logger.LogWarning("BGG sync held back a collection: {Kind}", held.Kind);

            return new SyncRunResult(
                SyncResult.HeldBack,
                SyncFailure.None,
                new HeldBackRecord(held.Kind, held.Fingerprint, held.Count, _time.GetUtcNow()));
        }

        var versionAtStart = _collection.Current.Version;
        var baseline = _stored ?? _collection.Current.Snapshot;
        var snapshot = new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            _time.GetUtcNow(),
            collection.Items,
            StillNamedImages(baseline, collection.Items),
            StillOwnedGames(baseline, collection.Items));

        if (ContentChanged(baseline, snapshot))
        {
            Commit(snapshot);
        }

        var enriched = await FetchDetailsAsync(snapshot, started + ExtrasDeadline, cancellationToken);
        await FetchPicturesAsync(enriched, started + ExtrasDeadline, cancellationToken);

        return new SyncRunResult(
            _collection.Current.Version == versionAtStart ? SyncResult.Unchanged : SyncResult.Changed,
            SyncFailure.None);
    }

    private static Dictionary<string, ImageRecord>? StillNamedImages(CollectionSnapshot? baseline, IReadOnlyList<SnapshotItem> items)
    {
        if (baseline?.Images is not { Count: > 0 } known)
        {
            return null;
        }

        var named = items
            .SelectMany(item => new[] { item.VersionImageUrl, item.ImageUrl })
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        var kept = known
            .Where(pair => named.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        return kept.Count == 0 ? null : kept;
    }

    private static Dictionary<int, GameDetails>? StillOwnedGames(CollectionSnapshot? baseline, IReadOnlyList<SnapshotItem> items)
    {
        if (baseline?.Games is not { Count: > 0 } known)
        {
            return null;
        }

        var owned = items.Select(item => item.GameId).ToHashSet();
        var kept = known.Where(pair => owned.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);

        return kept.Count == 0 ? null : kept;
    }

    private static bool ContentChanged(CollectionSnapshot? baseline, CollectionSnapshot next)
    {
        if (baseline is null || !baseline.Items.SequenceEqual(next.Items) || !SameGames(baseline.Games, next.Games))
        {
            return true;
        }

        var before = baseline.Images ?? new Dictionary<string, ImageRecord>();
        var after = next.Images ?? new Dictionary<string, ImageRecord>();

        return before.Count != after.Count
            || before.Any(pair => !after.TryGetValue(pair.Key, out var record) || !SameRecord(pair.Value, record));
    }

    private static bool SameGames(IReadOnlyDictionary<int, GameDetails>? left, IReadOnlyDictionary<int, GameDetails>? right)
    {
        var before = left ?? new Dictionary<int, GameDetails>();
        var after = right ?? new Dictionary<int, GameDetails>();

        return before.Count == after.Count
            && before.All(pair => after.TryGetValue(pair.Key, out var details) && pair.Value.SameAs(details));
    }

    private static bool SameRecord(ImageRecord left, ImageRecord right) =>
        left.SourceUrl == right.SourceUrl
        && left.Status == right.Status
        && left.AttemptedAtUtc == right.AttemptedAtUtc
        && (left.Files ?? []).SequenceEqual(right.Files ?? []);

    private void Commit(CollectionSnapshot snapshot)
    {
        _snapshots.Save(snapshot);
        _stored = snapshot;

        var next = CollectionState.FromSnapshot(snapshot);

        if (next.Version != _collection.Current.Version)
        {
            _collection.Replace(next);
        }
    }

    /// <summary>Fetches the game details that are due and returns the collection with every answer applied, or the one it was given when nothing changed.</summary>
    private async Task<CollectionSnapshot> FetchDetailsAsync(CollectionSnapshot snapshot, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        if (_enrichment is null)
        {
            return snapshot;
        }

        var latest = snapshot;

        try
        {
            await _enrichment.RunAsync(
                snapshot,
                deadline,
                stored =>
                {
                    Commit(stored);
                    latest = stored;

                    return Task.CompletedTask;
                },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning("Game details stopped early: {ExceptionType}", exception.GetType().Name);
        }

        return latest;
    }

    private async Task FetchPicturesAsync(CollectionSnapshot snapshot, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        if (_art is null)
        {
            return;
        }

        try
        {
            var result = await _art.RunAsync(
                snapshot,
                deadline,
                stored =>
                {
                    Commit(stored);

                    return Task.CompletedTask;
                },
                cancellationToken);

            Prune(result.Snapshot);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning("Box pictures stopped early: {ExceptionType}", exception.GetType().Name);
        }
    }

    private void Prune(CollectionSnapshot snapshot)
    {
        if (_cache is null || _images is null)
        {
            return;
        }

        var referenced = (snapshot.Images ?? new Dictionary<string, ImageRecord>())
            .Values
            .SelectMany(record => record.Files ?? [])
            .Select(file => file.Name)
            .ToHashSet(StringComparer.Ordinal);

        try
        {
            var deleted = _cache.Prune(referenced, _images.PruneGrace, _time.GetUtcNow());

            if (deleted > 0)
            {
                _logger.LogInformation("Box pictures: {Deleted} unused files removed.", deleted);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("Box pictures could not be cleaned up: {ExceptionType}", exception.GetType().Name);
        }
    }

    private bool StoredCollectionStillUnreadable()
    {
        if (!_snapshots.HasUnreadStoredCollection)
        {
            return false;
        }

        var stored = _snapshots.Load();

        if (stored is null)
        {
            return _snapshots.HasUnreadStoredCollection;
        }

        _stored = stored;
        _collection.Replace(CollectionState.FromSnapshot(stored));

        return false;
    }
}

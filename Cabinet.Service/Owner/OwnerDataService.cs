using System.Security.Cryptography;
using Cabinet.Domain.Owner;

namespace Cabinet.Service.Owner;

/// <summary>How an attempt to change the owner data ended.</summary>
public enum OwnerOutcome
{
    /// <summary>The change was stored.</summary>
    Saved,

    /// <summary>The change had nothing to do, so nothing was stored.</summary>
    NoChange,

    /// <summary>The writer's copy was older than the stored data, so nothing was stored.</summary>
    OutOfDate,

    /// <summary>The change broke a rule or could not be stored, so nothing was stored.</summary>
    Refused,

    /// <summary>The stored file cannot be read, so every write is refused and the file is left alone.</summary>
    Unreadable,
}

/// <summary>The answer to an attempt to change the owner data.</summary>
/// <param name="Outcome">How the attempt ended.</param>
/// <param name="Data">The owner data after the attempt; null when the stored file is unreadable.</param>
/// <param name="StatusKey">A short key the page turns into the words it shows the owner.</param>
/// <param name="CanUndo">Whether the latest change can still be undone: nothing was written after it.</param>
public sealed record OwnerResult(OwnerOutcome Outcome, OwnerData? Data, string StatusKey, bool CanUndo = false);

/// <summary>
/// The only code that changes the owner data. Writes are one at a time, each one is checked against the revision the writer
/// worked from, and each saved change is logged for the owner, stored atomically and announced so the collection shown to
/// visitors is rebuilt. Before a write an earlier copy of the file is kept when the newest one is more than an hour old.
/// The housekeeping the sync triggers, stamping and forgetting games that left the collection, goes through the same lock
/// but is not an owner change. It writes nothing to the application log but counts and categories.
/// </summary>
public sealed class OwnerDataService
{
    /// <summary>How old the newest earlier copy of the file must be before a write keeps another one.</summary>
    public static readonly TimeSpan VersionInterval = TimeSpan.FromHours(1);

    /// <summary>How long a game may be missing from accepted collections before its owner data is removed.</summary>
    public static readonly TimeSpan AbsentAfter = OwnerExpiry.AbsentAfter;

    private readonly IOwnerDataStore _store;
    private readonly TimeProvider _time;
    private readonly ILogger<OwnerDataService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private OwnerDataState _state;
    private UndoSlot? _undo;

    /// <summary>Creates the service and reads the stored owner data once.</summary>
    /// <param name="store">Keeps the owner data.</param>
    /// <param name="time">Stamps each save.</param>
    /// <param name="logger">Receives categories and counts, never a name or a title.</param>
    public OwnerDataService(IOwnerDataStore store, TimeProvider time, ILogger<OwnerDataService> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _time = time;
        _logger = logger;
        _state = store.Load();
    }

    /// <summary>Raised after a change was stored, with the new data already readable through <see cref="State"/>.</summary>
    public event Action? Changed;

    /// <summary>What the stored owner data is right now.</summary>
    public OwnerDataState State => Volatile.Read(ref _state);

    /// <summary>The revision of the data now, or zero while it cannot be read.</summary>
    public long Revision => State is OwnerDataState.Ready ready ? ready.Data.Revision : 0;

    /// <summary>The revision of the change that <see cref="UndoAsync"/> would revert, or null when there is nothing to undo.</summary>
    public long? UndoableRevision => _undo is { } slot && State is OwnerDataState.Ready ready && ready.Data.Revision == slot.Revision
        ? slot.Revision
        : null;

    /// <summary>Makes a fresh identifier for a location.</summary>
    public static string NewId() => RandomNumberGenerator.GetHexString(8, lowercase: true);

    /// <summary>Applies one change to the stored data, alone and in turn with every other change.</summary>
    /// <param name="expectedRevision">The revision the writer worked from; a stored revision that differs answers out of date.</param>
    /// <param name="change">Works out the new data from the stored data.</param>
    /// <param name="cancellationToken">Cancels the wait for a turn.</param>
    public async Task<OwnerResult> MutateAsync(
        long expectedRevision,
        Func<OwnerData, OwnerChange> change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (State is not OwnerDataState.Ready ready)
            {
                return new OwnerResult(OwnerOutcome.Unreadable, null, "unreadable");
            }

            if (ready.Data.Revision != expectedRevision)
            {
                return new OwnerResult(OwnerOutcome.OutOfDate, ready.Data, "out-of-date", UndoableRevision is not null);
            }

            var edit = change(ready.Data);

            if (edit.IsNone)
            {
                return new OwnerResult(OwnerOutcome.NoChange, ready.Data, edit.StatusKey, UndoableRevision is not null);
            }

            if (!edit.Changed)
            {
                return new OwnerResult(OwnerOutcome.Refused, ready.Data, edit.StatusKey, UndoableRevision is not null);
            }

            return Save(ready.Data, edit);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Takes back the latest change, and only that: it works while nothing was written after the change, once. The previous
    /// data is stored again as a new revision, which also drops the change's log line and its place in the change count.
    /// </summary>
    /// <param name="revisionOfChange">The revision the change produced, which the writer was shown when it made the change.</param>
    /// <param name="cancellationToken">Cancels the wait for a turn.</param>
    public async Task<OwnerResult> UndoAsync(long revisionOfChange, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (State is not OwnerDataState.Ready ready)
            {
                return new OwnerResult(OwnerOutcome.Unreadable, null, "unreadable");
            }

            if (_undo is not { } slot || slot.Revision != revisionOfChange || ready.Data.Revision != revisionOfChange)
            {
                return new OwnerResult(OwnerOutcome.OutOfDate, ready.Data, "cant-undo", UndoableRevision is not null);
            }

            var now = _time.GetUtcNow();
            var restored = slot.Previous with { Revision = ready.Data.Revision + 1, SavedAtUtc = now };

            try
            {
                KeepEarlierVersionWhenDue(now);
                _store.Save(restored);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning("The owner data could not be saved: {ExceptionType}", exception.GetType().Name);

                return new OwnerResult(OwnerOutcome.Refused, ready.Data, "save-failed", UndoableRevision is not null);
            }

            _undo = null;
            Volatile.Write(ref _state, new OwnerDataState.Ready(restored));
            _logger.LogInformation("Owner data change taken back: revision {Revision}.", restored.Revision);
            Changed?.Invoke();

            return new OwnerResult(OwnerOutcome.Saved, restored, "undone");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Applies a change the system makes on its own, alone and in turn with the owner's changes. It adds no log line, does not
    /// count as a change since the last download, does not move the revision and does not announce itself, because it never
    /// changes what visitors see; it does end the chance to undo, so an undo can never bring back what it removed. It does
    /// nothing while the stored file cannot be read, and a failed save only leaves the data as it was.
    /// </summary>
    /// <param name="change">Works out the new data from the stored data, or returns null when there is nothing to change.</param>
    /// <param name="cancellationToken">Cancels the wait for a turn.</param>
    public async Task MutateSystemAsync(Func<OwnerData, OwnerData?> change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (State is not OwnerDataState.Ready ready || change(ready.Data) is not { } next)
            {
                return;
            }

            try
            {
                KeepEarlierVersionWhenDue(_time.GetUtcNow());
                _store.Save(next);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning("The owner data could not be saved: {ExceptionType}", exception.GetType().Name);

                return;
            }

            _undo = null;
            Volatile.Write(ref _state, new OwnerDataState.Ready(next));
            _logger.LogInformation("Owner data tidied: revision {Revision}, {Games} games with decisions.", next.Revision, next.Games.Count);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Applies the thirty-day rule after an accepted collection: a game the collection no longer lists is stamped with the
    /// time of that first collection, a game that is listed again loses its stamp and keeps its decisions, and a game whose
    /// stamp is thirty days old is forgotten.
    /// </summary>
    /// <param name="listedGameIds">Every game the accepted collection lists, base games and expansions.</param>
    /// <param name="cancellationToken">Cancels the wait for a turn.</param>
    public Task ReconcileAsync(IReadOnlySet<int> listedGameIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(listedGameIds);

        return MutateSystemAsync(data => OwnerExpiry.Reconcile(data, listedGameIds, _time.GetUtcNow()), cancellationToken);
    }

    private void KeepEarlierVersionWhenDue(DateTimeOffset now)
    {
        try
        {
            if (_store.NewestEarlierVersionAt() is not { } newest || now - newest > VersionInterval)
            {
                _store.KeepEarlierVersion(now);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("An earlier version of the owner data could not be kept: {ExceptionType}", exception.GetType().Name);
        }
    }

    private OwnerResult Save(OwnerData current, OwnerChange edit)
    {
        var now = _time.GetUtcNow();
        var changes = edit.LogText is null
            ? edit.Data.Changes
            : (IReadOnlyList<ChangeEntry>)[new ChangeEntry(now, edit.LogText), .. edit.Data.Changes.Take(OwnerData.MaxStoredChanges - 1)];
        var next = edit.Data with
        {
            Revision = current.Revision + 1,
            SavedAtUtc = now,
            Changes = changes,
            ChangesSinceDownload = current.ChangesSinceDownload + 1,
        };

        try
        {
            KeepEarlierVersionWhenDue(now);
            _store.Save(next);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("The owner data could not be saved: {ExceptionType}", exception.GetType().Name);

            return new OwnerResult(OwnerOutcome.Refused, current, "save-failed", UndoableRevision is not null);
        }

        _undo = new UndoSlot(next.Revision, current);
        Volatile.Write(ref _state, new OwnerDataState.Ready(next));
        _logger.LogInformation("Owner data saved: revision {Revision}, {Locations} locations, {Games} games with decisions.", next.Revision, next.Locations.Count, next.Games.Count);
        Changed?.Invoke();

        return new OwnerResult(OwnerOutcome.Saved, next, edit.StatusKey, CanUndo: true);
    }

    private sealed record UndoSlot(long Revision, OwnerData Previous);
}

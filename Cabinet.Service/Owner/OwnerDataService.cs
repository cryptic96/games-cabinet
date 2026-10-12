using System.Security.Cryptography;
using Cabinet.Domain.Owner;
using Cabinet.Repository.Storage;

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

/// <summary>Where a document that replaces the owner data came from.</summary>
public enum RestoreSource
{
    /// <summary>A file the owner downloaded earlier and chose.</summary>
    File,

    /// <summary>One of the earlier versions the server keeps.</summary>
    EarlierVersion,
}

/// <summary>A checked document that waits for the owner to confirm the restore.</summary>
/// <param name="Data">The document that passed the check.</param>
/// <param name="FormatVersion">The stored format the file was written in.</param>
/// <param name="Source">Where the document came from.</param>
public sealed record RestoreCandidate(OwnerData Data, int FormatVersion, RestoreSource Source);

/// <summary>The file of a backup the owner downloads.</summary>
/// <param name="FileName">The name the file is saved under.</param>
/// <param name="Content">The readable JSON document.</param>
public sealed record OwnerDownload(string FileName, byte[] Content);

/// <summary>What the owner pages need to know about the backup: how stale it is, and whether there is anything to back up.</summary>
/// <param name="Readable">Whether the stored owner data can be read; nothing is offered while it cannot.</param>
/// <param name="HasData">Whether the owner has entered anything yet.</param>
/// <param name="ChangesSinceDownload">How many changes were saved since the last download, counted in full.</param>
/// <param name="LastDownloadAtUtc">When the owner last downloaded a backup, or null when never.</param>
public sealed record BackupState(bool Readable, bool HasData, int ChangesSinceDownload, DateTimeOffset? LastDownloadAtUtc)
{
    /// <summary>Whether the reminder shows: there is something that exists only on the server and was not downloaded.</summary>
    public bool ShowsBanner => Readable && ChangesSinceDownload > 0;
}

/// <summary>Tells the art clean-up which stored pictures the owner's data still needs.</summary>
public interface IOwnerArtReferences
{
    /// <summary>
    /// The file names of every stored picture the owner's linked pictures refer to, whichever picture each game is drawn from
    /// now; null while the stored owner data cannot be read, which means the answer is unknown and nothing may be removed.
    /// </summary>
    IReadOnlySet<string>? ReferencedArtFiles();
}

/// <summary>
/// The only code that changes the owner data. Writes are one at a time, each one is checked against the revision the writer
/// worked from, and each saved change is logged for the owner, stored atomically and announced so the collection shown to
/// visitors is rebuilt. Before a write an earlier copy of the file is kept when the newest one is more than an hour old.
/// The housekeeping the sync triggers, stamping and forgetting games that left the collection, goes through the same lock
/// but is not an owner change. It writes nothing to the application log but counts and categories.
/// </summary>
public sealed class OwnerDataService : IOwnerArtReferences
{
    /// <summary>How old the newest earlier copy of the file must be before a write keeps another one.</summary>
    public static readonly TimeSpan VersionInterval = TimeSpan.FromHours(1);

    /// <summary>How long a game may be missing from accepted collections before its owner data is removed.</summary>
    public static readonly TimeSpan AbsentAfter = OwnerExpiry.AbsentAfter;

    private const string RestoredFromFileLog = "Restored from file";
    private const string RestoredEarlierVersionLog = "Restored an earlier version";

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

    /// <summary>Raised when something asks for the pictures the owner linked to be fetched, so the background work wakes up before its next turn.</summary>
    public event Action? LinkWorkRequested;

    /// <summary>What the stored owner data is right now.</summary>
    public OwnerDataState State => Volatile.Read(ref _state);

    /// <summary>The revision of the data now, or zero while it cannot be read.</summary>
    public long Revision => State is OwnerDataState.Ready ready ? ready.Data.Revision : 0;

    /// <summary>The revision of the change that <see cref="UndoAsync"/> would revert, or null when there is nothing to undo.</summary>
    public long? UndoableRevision => _undo is { } slot && State is OwnerDataState.Ready ready && ready.Data.Revision == slot.Revision
        ? slot.Revision
        : null;

    /// <summary>What the owner pages show about the backup right now.</summary>
    public BackupState Backup => State is OwnerDataState.Ready ready
        ? new BackupState(
            Readable: true,
            HasData: ready.Data.Revision > 0 || ready.Data.Locations.Count > 0 || ready.Data.Games.Count > 0 || ready.Data.Changes.Count > 0,
            ready.Data.ChangesSinceDownload,
            ready.Data.LastDownloadAtUtc)
        : new BackupState(Readable: false, HasData: false, 0, null);

    /// <summary>Asks the background work to look for linked pictures that have no usable stored copy now instead of at its next turn.</summary>
    public void RequestLinkWork() => LinkWorkRequested?.Invoke();

    /// <inheritdoc />
    public IReadOnlySet<string>? ReferencedArtFiles() =>
        State is OwnerDataState.Ready ready
            ? ready.Data.Games
                .SelectMany(game => game.Picture?.Link?.Record?.Files ?? [])
                .Select(file => file.Name)
                .ToHashSet(StringComparer.Ordinal)
            : null;

    /// <summary>Makes a fresh identifier for a location.</summary>
    public static string NewId() => RandomNumberGenerator.GetHexString(OwnerLocation.IdLength, lowercase: true);

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
    /// count as a change since the last download and does not move the revision; it does end the chance to undo, so an undo
    /// can never bring back what it removed. It does not announce itself unless asked to, because the housekeeping never
    /// changes what visitors see; a change that does, such as the stored copy of a linked picture, asks to be announced so the
    /// collection visitors see is built again. It does nothing while the stored file cannot be read, and a failed save only
    /// leaves the data as it was.
    /// </summary>
    /// <param name="change">Works out the new data from the stored data, or returns null when there is nothing to change.</param>
    /// <param name="cancellationToken">Cancels the wait for a turn.</param>
    /// <param name="announce">Whether to raise <see cref="Changed"/> after the change was stored.</param>
    public async Task MutateSystemAsync(Func<OwnerData, OwnerData?> change, CancellationToken cancellationToken, bool announce = false)
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

            if (announce)
            {
                Changed?.Invoke();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Makes the backup the owner downloads and records that it was made: the last download becomes now and no change is left
    /// since it. It is a system write, so it adds no log line and does not move the revision, and it ends the chance to undo.
    /// The file carries those values as they are after the download. Nothing is recorded when the file cannot be stored, so
    /// the owner is not told a backup exists that the server does not know about.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait for a turn.</param>
    /// <returns>The file, or null while the stored owner data cannot be read or the record of the download could not be stored.</returns>
    public async Task<OwnerDownload?> RecordDownloadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (State is not OwnerDataState.Ready ready)
            {
                return null;
            }

            var now = _time.GetUtcNow();
            var next = ready.Data with { LastDownloadAtUtc = now, ChangesSinceDownload = 0 };
            var content = OwnerDataExport.Write(next, now);

            try
            {
                KeepEarlierVersionWhenDue(now);
                _store.Save(next);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning("The owner data could not be saved: {ExceptionType}", exception.GetType().Name);

                return null;
            }

            _undo = null;
            Volatile.Write(ref _state, new OwnerDataState.Ready(next));
            _logger.LogInformation("Owner data downloaded: revision {Revision}, {Changes} log lines.", next.Revision, next.Changes.Count);

            return new OwnerDownload(OwnerDataExport.FileName(now), content);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Replaces all the owner data with a document that passed the restore check. There is no merging. The data that is stored
    /// now, readable or not, is kept as an earlier version first, and when that cannot be done nothing is replaced. The
    /// document is stored under a revision higher than both its own and the one it replaces, with one log line that does not
    /// count toward the download reminder; the reminder's own state comes from the document. A game in the document that the
    /// collection does not list gets its thirty days from now, and one it does list loses any stamp. The chance to undo ends, the
    /// link pictures are asked for again and the change is announced.
    /// </summary>
    /// <param name="document">The checked document, whose links carry no fetched record.</param>
    /// <param name="source">Where the document came from, for the log line.</param>
    /// <param name="ownedGameIds">Every game the collection lists now, or null while there is no collection yet, which stamps no game.</param>
    /// <param name="cancellationToken">Cancels the wait for a turn.</param>
    public async Task<OwnerResult> RestoreAsync(
        OwnerData document,
        RestoreSource source,
        IReadOnlySet<int>? ownedGameIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            var current = State as OwnerDataState.Ready;
            var now = _time.GetUtcNow();
            var next = Restored(document, current?.Data.Revision ?? 0, source, ownedGameIds, now);

            try
            {
                _store.KeepEarlierVersion(now);
                _store.Save(next);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning("The owner data could not be restored: {ExceptionType}", exception.GetType().Name);

                return new OwnerResult(OwnerOutcome.Refused, current?.Data, "save-failed", UndoableRevision is not null);
            }

            _undo = null;
            Volatile.Write(ref _state, new OwnerDataState.Ready(next));
            _logger.LogInformation("Owner data restored: revision {Revision}, {Locations} locations, {Games} games with decisions.", next.Revision, next.Locations.Count, next.Games.Count);
            Changed?.Invoke();
            RequestLinkWork();

            return new OwnerResult(OwnerOutcome.Saved, next, "restored");
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

    private static OwnerData Restored(
        OwnerData document,
        long currentRevision,
        RestoreSource source,
        IReadOnlySet<int>? ownedGameIds,
        DateTimeOffset now)
    {
        var line = new ChangeEntry(now, source == RestoreSource.File ? RestoredFromFileLog : RestoredEarlierVersionLog);
        var games = document.Games
            .Select(game => ownedGameIds is null ? game : game with { AbsentSinceUtc = ownedGameIds.Contains(game.GameId) ? null : now })
            .OrderBy(game => game.GameId);

        return document with
        {
            Revision = Math.Max(document.Revision, currentRevision) + 1,
            SavedAtUtc = now,
            Games = [.. games],
            Changes = [line, .. document.Changes.Take(OwnerData.MaxStoredChanges - 1)],
        };
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

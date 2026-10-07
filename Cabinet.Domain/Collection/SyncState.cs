namespace Cabinet.Domain.Collection;

/// <summary>What kind of odd answer a sync set aside instead of applying.</summary>
public enum HeldBackKind
{
    /// <summary>The source reported no owned items although a collection is already shown.</summary>
    Empty,

    /// <summary>The source reported far fewer items than the collection already shown.</summary>
    Shrunk,

    /// <summary>The stored collection could not be read, so nothing is known to compare the answer with; it waits for a second identical answer.</summary>
    Unverified,
}

/// <summary>An answer a sync did not apply because it looked wrong, remembered so the same answer is not held back twice in a row.</summary>
/// <param name="Kind">What looked wrong.</param>
/// <param name="Fingerprint">A short identifier of the held-back answer.</param>
/// <param name="Count">How many items the held-back answer held.</param>
/// <param name="DetectedUtc">When the answer was held back.</param>
public sealed record HeldBackRecord(HeldBackKind Kind, string Fingerprint, int Count, DateTimeOffset DetectedUtc);

/// <summary>
/// The bookkeeping of the sync that must survive a restart: when it last started and finished, how the last run ended, and
/// the shared window during which a visitor's request for a sync is refused.
/// </summary>
/// <param name="SchemaVersion">The version of the stored shape; a reader refuses a version newer than it knows.</param>
/// <param name="LastStartedUtc">When the most recent sync was accepted, or null before any.</param>
/// <param name="LastFinishedUtc">When the most recent sync ended, or null before any has.</param>
/// <param name="LastSuccessUtc">When a sync last ended with the collection changed or confirmed unchanged.</param>
/// <param name="LastResult">How the most recent sync ended, or null before any has.</param>
/// <param name="LastFailure">Why the most recent sync failed; none unless it failed.</param>
/// <param name="ConsecutiveFailures">How many syncs in a row have failed.</param>
/// <param name="CooldownEndsUtc">When the shared window ends: a request for a sync is refused before this moment.</param>
/// <param name="HeldBack">The answer the most recent sync set aside, or null when nothing is held back.</param>
public sealed record SyncState(
    int SchemaVersion,
    DateTimeOffset? LastStartedUtc,
    DateTimeOffset? LastFinishedUtc,
    DateTimeOffset? LastSuccessUtc,
    SyncResult? LastResult,
    SyncFailure LastFailure,
    int ConsecutiveFailures,
    DateTimeOffset? CooldownEndsUtc,
    HeldBackRecord? HeldBack)
{
    /// <summary>The stored shape this build writes and understands.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The state of an installation that has never synced.</summary>
    public static SyncState Initial { get; } = new(
        CurrentSchemaVersion,
        null,
        null,
        null,
        null,
        SyncFailure.None,
        0,
        null,
        null);
}

/// <summary>Keeps the sync bookkeeping so a restart does not reset the shared window.</summary>
public interface ISyncStateStore
{
    /// <summary>Reads the stored state, or returns the initial state when there is none that can be used.</summary>
    SyncState Load();

    /// <summary>Stores the state so that a reader sees either the previous copy or this one, never a mixture.</summary>
    /// <param name="state">The state to store.</param>
    void Save(SyncState state);
}

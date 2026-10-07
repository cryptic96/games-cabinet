using System.Text.Json;
using Cabinet.Service.Collection;

namespace Cabinet.Service.Sync;

/// <summary>
/// What a visitor's page may know about the sync: when the collection was last synced, whether a sync is running, when the
/// button may be used again, and a four-value summary of the last run. It never carries a failure category, a count, the
/// username or the token.
/// </summary>
/// <param name="ServerTimeUtc">The server clock when the status was read, so a page can correct for a wrong clock of its own.</param>
/// <param name="SnapshotVersion">The identifier of the collection version shown, or null before the first sync.</param>
/// <param name="LastSyncedUtc">When the collection was last confirmed to match BGG, or null before the first sync.</param>
/// <param name="Running">Whether a sync is in progress.</param>
/// <param name="CooldownEndsUtc">When the sync button may be used again, or null when it may be used now.</param>
/// <param name="HeldBack">Whether the last answer from BGG was set aside as suspicious.</param>
/// <param name="StaleAfterSeconds">How old the collection may get before a page treats it as out of date.</param>
/// <param name="LastResult">changed, unchanged, failed or heldBack, or null before any run has ended.</param>
/// <param name="LastResultAtUtc">When the last run ended, or null before any has.</param>
public sealed record CabinetStatus(
    DateTimeOffset ServerTimeUtc,
    string? SnapshotVersion,
    DateTimeOffset? LastSyncedUtc,
    bool Running,
    DateTimeOffset? CooldownEndsUtc,
    bool HeldBack,
    int StaleAfterSeconds,
    string? LastResult,
    DateTimeOffset? LastResultAtUtc);

/// <summary>Builds the status from the sync bookkeeping and the collection that is shown.</summary>
/// <param name="coordinator">Holds the bookkeeping and knows whether a run is in progress.</param>
/// <param name="store">Holds the collection that is shown.</param>
/// <param name="options">The timing rules.</param>
/// <param name="time">The clock the status is stamped with.</param>
public sealed class SyncStatusService(SyncCoordinator coordinator, CollectionStore store, SyncOptions options, TimeProvider time)
{
    /// <summary>Reads the status as it stands now.</summary>
    public CabinetStatus Current()
    {
        var now = time.GetUtcNow();
        var state = coordinator.State;
        var collection = store.Current;
        var cooldownEnds = state.CooldownEndsUtc is { } until && until > now ? until : (DateTimeOffset?)null;

        return new CabinetStatus(
            now,
            collection.Version,
            state.LastSuccessUtc ?? collection.CapturedAtUtc,
            coordinator.IsRunning,
            cooldownEnds,
            state.HeldBack is not null,
            (int)options.StaleAfter.TotalSeconds,
            state.LastResult is { } result ? JsonNamingPolicy.CamelCase.ConvertName(result.ToString()) : null,
            state.LastFinishedUtc);
    }
}

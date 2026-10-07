using Cabinet.Domain.Collection;
using Cabinet.Repository.Bgg;
using Cabinet.Repository.Storage;
using Cabinet.Service.Collection;

namespace Cabinet.Service.Sync;

/// <summary>
/// Prepares the sync before the server listens: it removes temporary files an interrupted write left behind and loads the
/// stored collection into the store, so a restart shows the same cabinet right away. It also says once whether the BGG
/// settings are usable; a missing username or token is a warning rather than a failure, so a release without them still
/// starts, serves the being-filled page and stays healthy.
/// </summary>
/// <param name="storage">The directory the stored collection lives in.</param>
/// <param name="snapshots">The stored collection.</param>
/// <param name="collection">The store visitors read from.</param>
/// <param name="options">The BGG settings; only whether they are complete is looked at, never their values.</param>
/// <param name="artRules">The rules that choose each game's picture.</param>
/// <param name="logger">Receives the start-up warnings.</param>
public sealed class SyncStartup(
    StorageDirectory storage,
    ISnapshotStore snapshots,
    CollectionStore collection,
    BggOptions options,
    ArtRules artRules,
    ILogger<SyncStartup> logger) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.IsConfigured)
        {
            logger.LogWarning("BGG username or token is not configured; syncs are skipped until both are set.");
        }

        if (options.BaseUriOverrideIgnored)
        {
            logger.LogWarning("Bgg:BaseUri is ignored outside Development.");
        }

        AtomicJsonFile.RemoveStrayTemporaryFiles(storage.Path);

        var stored = snapshots.Load();

        if (stored is not null)
        {
            collection.Replace(CollectionState.FromSnapshot(stored, artRules));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

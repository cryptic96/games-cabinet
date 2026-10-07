using Cabinet.Domain.Collection;
using Cabinet.Service.Collection;

namespace Cabinet.Service.Sync;

/// <summary>
/// Prepares the sync before the server listens: it loads the stored collection into the store, so a restart shows the
/// same cabinet right away.
/// </summary>
/// <param name="snapshots">The stored collection.</param>
/// <param name="collection">The store visitors read from.</param>
public sealed class SyncStartup(ISnapshotStore snapshots, CollectionStore collection) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var stored = snapshots.Load();

        if (stored is not null)
        {
            collection.Replace(CollectionState.FromSnapshot(stored));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

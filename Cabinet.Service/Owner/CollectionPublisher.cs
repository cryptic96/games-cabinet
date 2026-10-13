using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;
using Cabinet.Service.Collection;

namespace Cabinet.Service.Owner;

/// <summary>
/// The only code that builds the collection view visitors read and puts it in the store: the sync, the start-up load, the
/// retry of an unreadable stored collection and every owner change all come through here, one at a time, so a sync commit
/// and an owner write can never overwrite each other's result. It holds the last synced collection as the sync gave it and
/// applies the owner data on top whenever either one changes; the synced collection itself is never changed.
/// </summary>
public sealed class CollectionPublisher
{
    private readonly Lock _gate = new();
    private readonly CollectionStore _store;
    private readonly ArtRules _rules;
    private readonly OwnerDataService? _owner;
    private CollectionSnapshot? _raw;

    /// <summary>Creates the publisher and starts following the owner data.</summary>
    /// <param name="store">The store visitors read from.</param>
    /// <param name="rules">The rules that choose each game's picture.</param>
    /// <param name="owner">The owner data; when null the synced collection is shown as it is, which only a test without the owner tools does.</param>
    public CollectionPublisher(CollectionStore store, ArtRules rules, OwnerDataService? owner = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rules);

        _store = store;
        _rules = rules;
        _owner = owner;

        if (owner is not null)
        {
            owner.Changed += Republish;
        }
    }

    /// <summary>Builds the view of a synced collection with the owner data on top and shows it when it differs from the one shown.</summary>
    /// <param name="snapshot">The synced collection, exactly as the sync stored it.</param>
    /// <param name="reconcile">
    /// Whether this collection also starts and stops the thirty-day clocks of the owner data. Only a collection BGG just returned
    /// and the sync accepted may do that; a collection read back from disk may be old, and the time the server was off would
    /// count against games the owner has since added again.
    /// </param>
    /// <returns>True when the view changed.</returns>
    /// <remarks>
    /// The housekeeping runs after the lock is released, because a save holds the owner data's turn and then asks for this
    /// lock, and it waits for its turn, which is at most one small file write.
    /// </remarks>
    public bool Publish(CollectionSnapshot snapshot, bool reconcile = true)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        bool changed;

        lock (_gate)
        {
            _raw = snapshot;
            changed = Replace(snapshot);
        }

        if (reconcile)
        {
            _owner?.ReconcileAsync(snapshot.Items.Select(item => item.GameId).ToHashSet(), CancellationToken.None).GetAwaiter().GetResult();
        }

        return changed;
    }

    /// <summary>Builds the view again from the last synced collection with the owner data as it is now. It does nothing before any collection was published.</summary>
    public void Republish()
    {
        lock (_gate)
        {
            if (_raw is { } snapshot)
            {
                Replace(snapshot);
            }
        }
    }

    private bool Replace(CollectionSnapshot snapshot)
    {
        var next = _owner is null
            ? CollectionState.FromSnapshot(snapshot, _rules)
            : CollectionState.FromSnapshot(snapshot, _rules, OwnerOverlay.From(snapshot, ReadyData()));

        if (next.Version == _store.Current.Version)
        {
            return false;
        }

        _store.Replace(next);

        return true;
    }

    private OwnerData? ReadyData() => _owner?.State is OwnerDataState.Ready ready ? ready.Data : null;
}

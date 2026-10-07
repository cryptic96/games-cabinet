using Cabinet.Domain.Collection;

namespace Cabinet.UnitTests.Sync;

/// <summary>A sync state store that keeps its state in memory, so a test can seed it and read back what was saved.</summary>
public sealed class InMemorySyncStateStore : ISyncStateStore
{
    private readonly object _gate = new();
    private SyncState _state;

    /// <summary>Creates the store holding a state.</summary>
    /// <param name="state">The state to start with; the initial state when omitted.</param>
    public InMemorySyncStateStore(SyncState? state = null) => _state = state ?? SyncState.Initial;

    /// <summary>How many times the state was saved.</summary>
    public int SaveCount { get; private set; }

    /// <summary>The state as last saved or seeded.</summary>
    public SyncState Stored
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <inheritdoc />
    public SyncState Load() => Stored;

    /// <inheritdoc />
    public void Save(SyncState state)
    {
        lock (_gate)
        {
            _state = state;
            SaveCount++;
        }
    }
}

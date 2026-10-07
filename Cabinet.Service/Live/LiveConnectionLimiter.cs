using System.Collections.Concurrent;

namespace Cabinet.Service.Live;

/// <summary>
/// Keeps the number of open live connections within the configured cap, for the whole site rather than per visitor, so a
/// flood cannot hold more memory and sockets than the host can spare.
/// </summary>
/// <param name="options">The live settings.</param>
public sealed class LiveConnectionLimiter(LiveOptions options)
{
    private readonly ConcurrentDictionary<string, byte> _admitted = new(StringComparer.Ordinal);
    private int _count;

    /// <summary>How many connections are admitted now.</summary>
    public int Count => Volatile.Read(ref _count);

    /// <summary>Admits a connection when there is room.</summary>
    /// <param name="connectionId">The identifier of the connection asking to stay open.</param>
    /// <returns>True when the connection may stay open; false when the cap is reached.</returns>
    public bool TryAdmit(string connectionId)
    {
        ArgumentNullException.ThrowIfNull(connectionId);

        if (Interlocked.Increment(ref _count) > options.MaxConnections)
        {
            Interlocked.Decrement(ref _count);

            return false;
        }

        if (_admitted.TryAdd(connectionId, 0))
        {
            return true;
        }

        Interlocked.Decrement(ref _count);

        return true;
    }

    /// <summary>Frees the place of a connection that has closed. A connection that was never admitted frees nothing.</summary>
    /// <param name="connectionId">The identifier of the connection that closed.</param>
    public void Release(string connectionId)
    {
        ArgumentNullException.ThrowIfNull(connectionId);

        if (_admitted.TryRemove(connectionId, out _))
        {
            Interlocked.Decrement(ref _count);
        }
    }
}

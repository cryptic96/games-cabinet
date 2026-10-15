namespace Cabinet.Service.Live;

/// <summary>
/// Keeps the number of open live connections within the configured cap, for the whole site rather than per visitor, so a
/// flood cannot hold more memory and sockets than the host can spare.
/// </summary>
/// <param name="options">The live settings.</param>
public sealed class LiveConnectionLimiter(LiveOptions options)
{
    private readonly Lock _gate = new();
    private readonly HashSet<string> _admitted = new(StringComparer.Ordinal);

    /// <summary>How many connections are admitted now.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _admitted.Count;
            }
        }
    }

    /// <summary>
    /// Admits a connection when there is room. A connection that already holds a place stays admitted and is never turned away,
    /// even when the cap is reached, and it does not take a second place.
    /// </summary>
    /// <param name="connectionId">The identifier of the connection asking to stay open.</param>
    /// <returns>True when the connection may stay open; false when it holds no place and the cap is reached.</returns>
    public bool TryAdmit(string connectionId)
    {
        ArgumentNullException.ThrowIfNull(connectionId);

        lock (_gate)
        {
            return _admitted.Contains(connectionId) || (_admitted.Count < options.MaxConnections && _admitted.Add(connectionId));
        }
    }

    /// <summary>
    /// Tells whether a connection holds a place now. Only the tests that wait for a connection to be admitted or refused call
    /// it; the site itself acts on the answer of <see cref="TryAdmit"/>.
    /// </summary>
    /// <param name="connectionId">The identifier of the connection to look up.</param>
    /// <returns>True when the connection was admitted and has not closed.</returns>
    public bool IsAdmitted(string connectionId)
    {
        ArgumentNullException.ThrowIfNull(connectionId);

        lock (_gate)
        {
            return _admitted.Contains(connectionId);
        }
    }

    /// <summary>Frees the place of a connection that has closed. A connection that was never admitted frees nothing.</summary>
    /// <param name="connectionId">The identifier of the connection that closed.</param>
    public void Release(string connectionId)
    {
        ArgumentNullException.ThrowIfNull(connectionId);

        lock (_gate)
        {
            _admitted.Remove(connectionId);
        }
    }
}

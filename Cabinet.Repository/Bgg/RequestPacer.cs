namespace Cabinet.Repository.Bgg;

/// <summary>Spaces requests out: one turn at a time, a minimum gap after the previous one finished.</summary>
public interface IRequestPacer
{
    /// <summary>
    /// Waits until it is this caller's turn and the gap since the previous request has passed. The turn lasts until the
    /// returned lease is disposed, so the gap is measured from the end of the previous request.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    Task<IDisposable> WaitTurnAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The pacer every BGG request goes through. It is shared, so even concurrent callers keep at least the gap between two
/// requests, and a gap configured below the minimum is raised to it.
/// </summary>
public sealed class RequestPacer : IRequestPacer
{
    private readonly SemaphoreSlim _turn = new(1, 1);
    private readonly TimeSpan _gap;
    private readonly TimeProvider _time;
    private long? _lastEndTimestamp;

    /// <summary>Creates a pacer.</summary>
    /// <param name="gap">The least time between two requests; raised to the API minimum when it is shorter.</param>
    /// <param name="time">The clock the gap is measured with, read through its monotonic timestamp so a change of the wall clock never lengthens a wait.</param>
    public RequestPacer(TimeSpan gap, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        _gap = gap < BggOptions.MinimumRequestGap ? BggOptions.MinimumRequestGap : gap;
        _time = time;
    }

    /// <inheritdoc />
    public async Task<IDisposable> WaitTurnAsync(CancellationToken cancellationToken)
    {
        await _turn.WaitAsync(cancellationToken);

        try
        {
            if (_lastEndTimestamp is { } lastEndTimestamp)
            {
                var wait = _gap - _time.GetElapsedTime(lastEndTimestamp);

                if (wait > _gap)
                {
                    wait = _gap;
                }

                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, _time, cancellationToken);
                }
            }
        }
        catch
        {
            _turn.Release();
            throw;
        }

        return new Lease(this);
    }

    private void End()
    {
        _lastEndTimestamp = _time.GetTimestamp();
        _turn.Release();
    }

    private sealed class Lease(RequestPacer owner) : IDisposable
    {
        private int _ended;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _ended, 1) == 0)
            {
                owner.End();
            }
        }
    }
}

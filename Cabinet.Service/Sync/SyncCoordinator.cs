using System.Threading.Channels;

namespace Cabinet.Service.Sync;

/// <summary>What asked for a sync.</summary>
public enum SyncTrigger
{
    /// <summary>A visitor pressed the sync button.</summary>
    Manual,

    /// <summary>The periodic timer fired.</summary>
    Scheduled,

    /// <summary>The app just started.</summary>
    Startup,
}

/// <summary>The answer to a request for a sync.</summary>
public abstract record SyncRequestResult
{
    /// <summary>The request was accepted and a sync will run.</summary>
    public sealed record Started : SyncRequestResult;

    /// <summary>A sync is already running or about to run, so this request was not queued.</summary>
    public sealed record AlreadyRunning : SyncRequestResult;

    /// <summary>The previous sync was too recent.</summary>
    /// <param name="Until">When the next sync may start.</param>
    public sealed record CoolingDown(DateTimeOffset Until) : SyncRequestResult;
}

/// <summary>
/// Lets only one sync exist at a time. A request is accepted only when none is running; the accepted request travels over a
/// channel with room for one, and the single worker that reads it reports back when the run has finished.
/// </summary>
public sealed class SyncCoordinator
{
    private readonly Channel<SyncTrigger> _channel = Channel.CreateBounded<SyncTrigger>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    private readonly object _gate = new();
    private bool _running;
    private SyncRunResult? _lastResult;

    /// <summary>Whether a sync has been accepted and has not finished yet.</summary>
    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _running;
            }
        }
    }

    /// <summary>How the most recent sync ended, or null before any has finished.</summary>
    public SyncRunResult? LastResult
    {
        get
        {
            lock (_gate)
            {
                return _lastResult;
            }
        }
    }

    /// <summary>The accepted requests, for the one worker that runs them.</summary>
    public ChannelReader<SyncTrigger> Requests => _channel.Reader;

    /// <summary>Asks for a sync. Nothing is queued while one is running.</summary>
    /// <param name="trigger">What asked.</param>
    public SyncRequestResult TryRequest(SyncTrigger trigger)
    {
        lock (_gate)
        {
            if (_running)
            {
                return new SyncRequestResult.AlreadyRunning();
            }

            _running = true;

            if (!_channel.Writer.TryWrite(trigger))
            {
                _running = false;

                return new SyncRequestResult.AlreadyRunning();
            }

            return new SyncRequestResult.Started();
        }
    }

    /// <summary>Records that the accepted sync has finished, so the next request is accepted.</summary>
    /// <param name="result">How the sync ended.</param>
    public void Complete(SyncRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_gate)
        {
            _lastResult = result;
            _running = false;
        }
    }
}

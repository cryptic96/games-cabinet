using System.Threading.Channels;
using Cabinet.Domain.Collection;

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
/// Lets only one sync exist at a time and keeps the shared window. A request is accepted only when none is running; every
/// accepted request opens a window of the manual cooldown, written to disk before the request is queued, during which a
/// visitor's request is refused. The accepted request travels over a channel with room for one, and the single worker that
/// reads it reports back when the run has finished. It also notices a rejected token: after
/// <see cref="RejectionsBeforeBackoff"/> unauthorized runs in a row the timed syncs are paused to about one a day, which
/// ends with the next successful run or a restart of the service. A visitor's request is never affected.
/// </summary>
public sealed class SyncCoordinator
{
    /// <summary>How many runs in a row BGG must refuse the token on before the timed syncs slow down.</summary>
    public const int RejectionsBeforeBackoff = 3;

    /// <summary>How long timed syncs are spaced out while the token is being refused.</summary>
    public static readonly TimeSpan BackoffSpacing = TimeSpan.FromHours(24);

    private readonly Channel<SyncTrigger> _channel = Channel.CreateBounded<SyncTrigger>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    private readonly ISyncStateStore _stateStore;
    private readonly SyncOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<SyncCoordinator>? _logger;
    private readonly object _gate = new();
    private SyncState _state;
    private bool _running;
    private SyncRunResult? _lastResult;
    private int _rejectionsInARow;

    /// <summary>Creates the coordinator and loads the stored bookkeeping, so a restart keeps the shared window.</summary>
    /// <param name="stateStore">Keeps the bookkeeping on disk.</param>
    /// <param name="options">The timing rules.</param>
    /// <param name="time">The clock every window is measured on.</param>
    /// <param name="logger">Receives a line when the bookkeeping cannot be written after a run; optional.</param>
    public SyncCoordinator(ISyncStateStore stateStore, SyncOptions options, TimeProvider time, ILogger<SyncCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);

        _stateStore = stateStore;
        _options = options;
        _time = time;
        _logger = logger;
        _state = stateStore.Load();
    }

    /// <summary>The bookkeeping as it stands now.</summary>
    public SyncState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

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

    /// <summary>
    /// When the timed syncs may run again while BGG keeps refusing the token, or null when they are not paused. The count of
    /// refusals is kept in memory only, so a restart of the service clears the pause. The moment is about
    /// <see cref="BackoffSpacing"/> after the last start, less half an interval, so the tick nearest to a day later is the one
    /// that runs.
    /// </summary>
    public DateTimeOffset? TimedSyncsResumeAtUtc
    {
        get
        {
            lock (_gate)
            {
                if (_rejectionsInARow < RejectionsBeforeBackoff
                    || _options.Interval >= BackoffSpacing
                    || _state.LastStartedUtc is not { } started)
                {
                    return null;
                }

                return started + BackoffSpacing - (_options.Interval / 2);
            }
        }
    }

    /// <summary>The accepted requests, for the one worker that runs them.</summary>
    public ChannelReader<SyncTrigger> Requests => _channel.Reader;

    /// <summary>
    /// Asks for a sync. Nothing is queued while one is running, and a visitor's request is refused inside the shared window.
    /// Every accepted request, whatever asked, opens a new window that is stored before the request is queued.
    /// </summary>
    /// <param name="trigger">What asked.</param>
    /// <exception cref="IOException">The window could not be stored; nothing was queued.</exception>
    public SyncRequestResult TryRequest(SyncTrigger trigger)
    {
        lock (_gate)
        {
            if (_running)
            {
                return new SyncRequestResult.AlreadyRunning();
            }

            var now = _time.GetUtcNow();

            if (trigger == SyncTrigger.Manual && _state.CooldownEndsUtc is { } until && now < until)
            {
                return new SyncRequestResult.CoolingDown(until);
            }

            var opened = _state with { LastStartedUtc = now, CooldownEndsUtc = now + _options.ManualCooldown };
            _stateStore.Save(opened);
            _state = opened;
            _running = true;

            if (!_channel.Writer.TryWrite(trigger))
            {
                _running = false;

                return new SyncRequestResult.AlreadyRunning();
            }

            return new SyncRequestResult.Started();
        }
    }

    /// <summary>
    /// Records that the accepted sync has finished, so the next request is accepted. A held-back result stores the held-back
    /// record and does not move the last success; an accepted result clears the record; a failure keeps it.
    /// </summary>
    /// <param name="result">How the sync ended.</param>
    public void Complete(SyncRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_gate)
        {
            CountRejection(result);

            try
            {
                var now = _time.GetUtcNow();
                var succeeded = result.Result is SyncResult.Changed or SyncResult.Unchanged;
                var failures = succeeded ? 0 : _state.ConsecutiveFailures + (result.Result == SyncResult.Failed ? 1 : 0);

                _state = _state with
                {
                    LastFinishedUtc = now,
                    LastResult = result.Result,
                    LastFailure = result.Failure,
                    ConsecutiveFailures = failures,
                    LastSuccessUtc = succeeded ? now : _state.LastSuccessUtc,
                    HeldBack = succeeded ? null : result.Result == SyncResult.HeldBack ? result.HeldBack ?? _state.HeldBack : _state.HeldBack,
                };
                _lastResult = result;
                _stateStore.Save(_state);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger?.LogWarning("The sync state could not be stored after a run: {ExceptionType}", exception.GetType().Name);
            }
            finally
            {
                _running = false;
            }
        }
    }

    private void CountRejection(SyncRunResult result)
    {
        if (result.Result != SyncResult.Failed || result.Failure != SyncFailure.Unauthorized)
        {
            _rejectionsInARow = 0;

            return;
        }

        _rejectionsInARow++;

        if (_rejectionsInARow == RejectionsBeforeBackoff)
        {
            _logger?.LogWarning("BGG keeps refusing the token; timed syncs now run about once a day until a sync succeeds or the service restarts.");
        }
    }
}

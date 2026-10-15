using Cabinet.Service.Live;
using Cabinet.Service.Sync;

namespace Cabinet.Service.Owner;

/// <summary>
/// Tells open pages that what visitors see changed after the owner saved, the same way they are told after a sync, but once per
/// burst of saves instead of once per save: a few seconds after the last save, and never more than a ceiling after the first
/// save nobody was told about yet. What visitors see is the collection and the look. Nothing is sent when both are the ones the
/// pages were last told of.
/// What is sent is the public status only, never any owner data.
/// </summary>
public sealed class OwnerChangeNotifier : IHostedService, IDisposable
{
    /// <summary>How long after the latest save the notification is sent when no further save follows.</summary>
    public static readonly TimeSpan QuietPeriod = TimeSpan.FromSeconds(3);

    /// <summary>The longest a save may wait for its notification while saves keep coming.</summary>
    public static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(15);

    private readonly OwnerDataService _owner;
    private readonly SyncStatusService _status;
    private readonly ILiveNotifier _live;
    private readonly TimeProvider _time;
    private readonly ILogger<OwnerChangeNotifier> _logger;
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _stopping = new();
    private ITimer? _timer;
    private DateTimeOffset? _firstUnannouncedAt;
    private Announced? _lastSent;
    private bool _closed;

    /// <summary>Creates the notifier; it does nothing until the host starts it.</summary>
    /// <param name="owner">Raises an event after every stored owner change.</param>
    /// <param name="status">Reads the status that is sent, which carries the collection version and the look.</param>
    /// <param name="live">Sends the status to every open page.</param>
    /// <param name="time">The clock the waits run on.</param>
    /// <param name="logger">Receives the type of a failed send, never its message.</param>
    public OwnerChangeNotifier(
        OwnerDataService owner,
        SyncStatusService status,
        ILiveNotifier live,
        TimeProvider time,
        ILogger<OwnerChangeNotifier> logger)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);

        _owner = owner;
        _status = status;
        _live = live;
        _time = time;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _lastSent = Announced.Of(_status.Current());
            _timer = _time.CreateTimer(OnTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        _owner.Changed += OnChanged;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Shut(dispose: false);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose() => Shut(dispose: true);

    private void OnChanged()
    {
        lock (_gate)
        {
            if (_timer is null)
            {
                return;
            }

            var now = _time.GetUtcNow();
            var first = _firstUnannouncedAt ??= now;
            var untilCeiling = first + Ceiling - now;
            var wait = untilCeiling < QuietPeriod ? untilCeiling : QuietPeriod;

            _timer.Change(wait > TimeSpan.Zero ? wait : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnTimer(object? state)
    {
        lock (_gate)
        {
            _firstUnannouncedAt = null;
        }

        _ = AnnounceAsync();
    }

    private async Task AnnounceAsync()
    {
        Announced? previous;
        CabinetStatus status;

        lock (_gate)
        {
            status = _status.Current();
            var now = Announced.Of(status);

            if (now == _lastSent)
            {
                return;
            }

            previous = _lastSent;
            _lastSent = now;
        }

        try
        {
            await _live.PublishAsync(status, _stopping.Token);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Telling open pages about an owner change failed: {ExceptionType}", exception.GetType().Name);

            lock (_gate)
            {
                _lastSent = previous;
            }
        }
    }

    private void Shut(bool dispose)
    {
        _owner.Changed -= OnChanged;

        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _timer?.Dispose();
            _timer = null;
            _firstUnannouncedAt = null;
            _stopping.Cancel();
            _closed = dispose;

            if (dispose)
            {
                _stopping.Dispose();
            }
        }
    }

    /// <summary>What the pages were last told about what visitors see: the collection version and the look.</summary>
    /// <param name="SnapshotVersion">The identifier of the collection version shown, or null before the first sync.</param>
    /// <param name="Look">The look visitors see.</param>
    private sealed record Announced(string? SnapshotVersion, LookStatus Look)
    {
        /// <summary>Reads what a status tells about what visitors see.</summary>
        /// <param name="status">The status to read.</param>
        public static Announced Of(CabinetStatus status) => new(status.SnapshotVersion, status.Look);
    }
}

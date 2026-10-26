using System.Threading.RateLimiting;

namespace Cabinet.Service.Limits;

/// <summary>
/// Counts new live connections per client address and for the whole site in fixed windows, and admits a request only when
/// both budgets have room. The check and the spending happen together under one lock, so a request that is refused by either
/// budget spends nothing from the other: an address already over its own window takes nothing from the site's budget, and a
/// site that is out of budget takes nothing from the visitor's own. An address's window starts at its first admitted request
/// and the site's at the first admitted request of the site; both are checked against the clock when asked, so nothing holds
/// a timer and an address's window is dropped once it has expired.
/// </summary>
internal sealed class LiveConnectWindowLimiter : PartitionedRateLimiter<HttpContext>
{
    private readonly ClientLimitOptions _options;
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private readonly Dictionary<string, CountedWindow> _addresses = new(StringComparer.Ordinal);
    private CountedWindow? _site;
    private long _lastSweep;

    /// <summary>Creates the limiter.</summary>
    /// <param name="options">The budgets and the window they are counted in.</param>
    /// <param name="clock">The clock the windows are measured on.</param>
    public LiveConnectWindowLimiter(ClientLimitOptions options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        _options = options;
        _clock = clock;
        _lastSweep = clock.GetTimestamp();
    }

    /// <inheritdoc />
    public override RateLimiterStatistics? GetStatistics(HttpContext resource) => null;

    /// <inheritdoc />
    protected override RateLimitLease AttemptAcquireCore(HttpContext resource, int permitCount)
    {
        if (!LiveConnectWindow.CountsAsNewConnection(resource.Request))
        {
            return Lease.Admitted;
        }

        var key = ClientAddress.PartitionKey(resource);

        lock (_gate)
        {
            var now = _clock.GetTimestamp();
            Sweep(now);

            var site = CurrentWindow(_site, now);
            var address = _addresses.TryGetValue(key, out var existing) ? CurrentWindow(existing, now) : null;
            var siteWait = site is not null && site.Count >= _options.LiveConnectsSiteWide ? RemainingOf(site, now) : (TimeSpan?)null;
            var addressWait = address is not null && address.Count >= _options.LiveConnectsPerWindow ? RemainingOf(address, now) : (TimeSpan?)null;

            if (siteWait is not null || addressWait is not null)
            {
                return Lease.Refused(Longest(siteWait, addressWait));
            }

            _site = site ?? new CountedWindow(now);
            _site.Count++;
            address ??= new CountedWindow(now);
            address.Count++;
            _addresses[key] = address;

            return Lease.Admitted;
        }
    }

    /// <inheritdoc />
    protected override ValueTask<RateLimitLease> AcquireAsyncCore(HttpContext resource, int permitCount, CancellationToken cancellationToken) =>
        ValueTask.FromResult(AttemptAcquireCore(resource, permitCount));

    private CountedWindow? CurrentWindow(CountedWindow? window, long now) =>
        window is not null && _clock.GetElapsedTime(window.Start, now) < _options.LiveWindow ? window : null;

    private TimeSpan RemainingOf(CountedWindow window, long now) =>
        _options.LiveWindow - _clock.GetElapsedTime(window.Start, now);

    private void Sweep(long now)
    {
        if (_clock.GetElapsedTime(_lastSweep, now) < _options.LiveWindow)
        {
            return;
        }

        _lastSweep = now;

        foreach (var key in _addresses.Where(pair => CurrentWindow(pair.Value, now) is null).Select(pair => pair.Key).ToList())
        {
            _addresses.Remove(key);
        }
    }

    private static TimeSpan Longest(TimeSpan? first, TimeSpan? second) =>
        first is { } a && second is { } b ? (a > b ? a : b) : first ?? second ?? TimeSpan.Zero;

    private sealed class CountedWindow(long start)
    {
        public long Start { get; } = start;

        public int Count { get; set; }
    }

    private sealed class Lease : RateLimitLease
    {
        public static readonly Lease Admitted = new(isAcquired: true, retryAfter: null);

        private readonly TimeSpan? _retryAfter;

        private Lease(bool isAcquired, TimeSpan? retryAfter)
        {
            IsAcquired = isAcquired;
            _retryAfter = retryAfter;
        }

        public override bool IsAcquired { get; }

        public override IEnumerable<string> MetadataNames => _retryAfter is null ? [] : [MetadataName.RetryAfter.Name];

        public static Lease Refused(TimeSpan retryAfter) => new(isAcquired: false, retryAfter);

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (_retryAfter is { } wait && metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = wait;

                return true;
            }

            metadata = null;

            return false;
        }
    }
}

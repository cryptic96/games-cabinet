using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests;

/// <summary>
/// A clock that hands everything to a fake clock and counts the timers created on it, so a test can tell that code which
/// waits on a timer has armed it before the test moves time forward, instead of sleeping and hoping.
/// </summary>
/// <param name="inner">The fake clock that keeps the time and fires the timers.</param>
public sealed class TimerCountingClock(FakeTimeProvider inner) : TimeProvider
{
    private int _timerCount;

    /// <summary>How many timers have been created on this clock, including those since disposed.</summary>
    public int TimerCount => Volatile.Read(ref _timerCount);

    /// <inheritdoc />
    public override long TimestampFrequency => inner.TimestampFrequency;

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();

    /// <inheritdoc />
    public override long GetTimestamp() => inner.GetTimestamp();

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = inner.CreateTimer(callback, state, dueTime, period);
        Interlocked.Increment(ref _timerCount);

        return timer;
    }

    /// <summary>Waits, within a bound, until at least <paramref name="count"/> timers have been created.</summary>
    /// <param name="count">The number of timers to wait for.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <exception cref="TimeoutException">The timers were not created within ten seconds.</exception>
    public async Task WaitForTimersAsync(int count, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (TimerCount < count)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Only {TimerCount} of {count} timers were created within ten seconds.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
        }
    }
}

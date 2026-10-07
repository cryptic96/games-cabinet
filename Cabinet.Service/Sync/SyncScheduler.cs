using Cabinet.Service.Collection;

namespace Cabinet.Service.Sync;

/// <summary>
/// Asks for a sync on its own: once shortly after a cold start when the collection is missing or stale, and then every
/// interval. It asks the same way a visitor's button does, so a request that arrives while a run is in progress queues
/// nothing, and it never starts a sync right after the last start, so a crash loop cannot hammer BGG. The interval timer
/// is armed only after the start-up delay, so a long delay can never leave a tick pending beside the start-up request.
/// While BGG keeps refusing the token the ticks are skipped until the coordinator says the timed syncs may resume, which
/// spaces them to about one a day.
/// </summary>
/// <param name="coordinator">Accepts or refuses each request.</param>
/// <param name="collection">Tells whether a collection has been synced yet.</param>
/// <param name="options">The timing rules.</param>
/// <param name="time">The clock every delay and tick is measured on.</param>
/// <param name="logger">Receives the type of an unexpected exception, never its message.</param>
public sealed class SyncScheduler(
    SyncCoordinator coordinator,
    CollectionStore collection,
    SyncOptions options,
    TimeProvider time,
    ILogger<SyncScheduler> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.BackgroundEnabled)
        {
            return;
        }

        try
        {
            if (ShouldRunAtStartup())
            {
                await Task.Delay(StartupDelay(), time, stoppingToken);
                Request(SyncTrigger.Startup);
            }

            using var timer = new PeriodicTimer(options.Interval, time);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (coordinator.TimedSyncsResumeAtUtc is { } resumeAt && time.GetUtcNow() < resumeAt)
                {
                    continue;
                }

                Request(SyncTrigger.Scheduled);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
    }

    private bool ShouldRunAtStartup()
    {
        var now = time.GetUtcNow();
        var state = coordinator.State;
        var lastSynced = state.LastSuccessUtc ?? collection.Current.CapturedAtUtc;
        var stale = lastSynced is null || now - lastSynced.Value >= options.Interval;
        var startedRecently = state.LastStartedUtc is { } started && now - started < SyncOptions.MinimumInterval;

        return stale && !startedRecently;
    }

    private TimeSpan StartupDelay()
    {
        var spread = options.StartupJitterMax - SyncOptions.StartupJitterMin;

        return SyncOptions.StartupJitterMin + (spread > TimeSpan.Zero ? spread * Random.Shared.NextDouble() : TimeSpan.Zero);
    }

    private void Request(SyncTrigger trigger)
    {
        try
        {
            coordinator.TryRequest(trigger);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("A {Trigger} sync could not be started: {ExceptionType}", trigger, exception.GetType().Name);
        }
    }
}

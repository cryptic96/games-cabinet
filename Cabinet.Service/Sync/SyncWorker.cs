using Cabinet.Domain.Collection;
using Cabinet.Service.Live;

namespace Cabinet.Service.Sync;

/// <summary>
/// The one consumer of sync requests. It runs each accepted request to the end, with a limit on how long a run may take (measured on the injected clock),
/// and always reports back, so a failed or stuck run never blocks the next request. A run that the limit ends is recorded
/// as a timeout; a run that a service stop interrupts is not recorded at all, because it did not fail.
/// </summary>
/// <param name="coordinator">Supplies the accepted requests and learns when a run has finished.</param>
/// <param name="runner">Runs one sync.</param>
/// <param name="status">Reads the status that is sent to open pages.</param>
/// <param name="live">Tells open pages when a run starts and when it ends.</param>
/// <param name="time">The clock the whole-run limit runs on.</param>
/// <param name="logger">Receives the type of an unexpected exception, never its message.</param>
public sealed class SyncWorker(
    SyncCoordinator coordinator,
    SyncRunner runner,
    SyncStatusService status,
    ILiveNotifier live,
    TimeProvider time,
    ILogger<SyncWorker> logger) : BackgroundService
{
    /// <summary>The longest one whole run may take before it is ended and recorded as a timeout.</summary>
    public static readonly TimeSpan RunLimit = TimeSpan.FromMinutes(10);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var _ in coordinator.Requests.ReadAllAsync(stoppingToken))
            {
                await live.PublishAsync(status.Current(), stoppingToken);
                coordinator.Complete(await RunOnceAsync(stoppingToken));
                await live.PublishAsync(status.Current(), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
    }

    private async Task<SyncRunResult> RunOnceAsync(CancellationToken stoppingToken)
    {
        using var limit = new CancellationTokenSource(RunLimit, time);
        using var stoppedOrLimited = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, limit.Token);

        try
        {
            return await runner.RunAsync(coordinator.State.HeldBack, stoppedOrLimited.Token);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            return new SyncRunResult(SyncResult.Failed, SyncFailure.Timeout);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError("BGG sync stopped unexpectedly: {ExceptionType}", exception.GetType().Name);

            return new SyncRunResult(SyncResult.Failed, SyncFailure.Unavailable);
        }
    }
}

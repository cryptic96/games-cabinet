using Cabinet.Domain.Collection;

namespace Cabinet.Service.Sync;

/// <summary>
/// The one consumer of sync requests. It runs each accepted request to the end, with a limit on how long a run may take,
/// and always reports back, so a failed or stuck run never blocks the next request.
/// </summary>
/// <param name="coordinator">Supplies the accepted requests and learns when a run has finished.</param>
/// <param name="runner">Runs one sync.</param>
/// <param name="logger">Receives the type of an unexpected exception, never its message.</param>
public sealed class SyncWorker(SyncCoordinator coordinator, SyncRunner runner, ILogger<SyncWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RunLimit = TimeSpan.FromMinutes(10);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var _ in coordinator.Requests.ReadAllAsync(stoppingToken))
            {
                coordinator.Complete(await RunOnceAsync(stoppingToken));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
    }

    private async Task<SyncRunResult> RunOnceAsync(CancellationToken stoppingToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        limit.CancelAfter(RunLimit);

        try
        {
            return await runner.RunAsync(limit.Token);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            return new SyncRunResult(SyncResult.Failed, SyncFailure.Timeout);
        }
        catch (OperationCanceledException)
        {
            return new SyncRunResult(SyncResult.Failed, SyncFailure.Unavailable);
        }
        catch (Exception exception)
        {
            logger.LogError("BGG sync stopped unexpectedly: {ExceptionType}", exception.GetType().Name);

            return new SyncRunResult(SyncResult.Failed, SyncFailure.Unavailable);
        }
    }
}

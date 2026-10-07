using Cabinet.Service.Sync;
using Microsoft.AspNetCore.SignalR;

namespace Cabinet.Service.Live;

/// <summary>Tells every connected page that the sync status changed. The sync code only knows this seam, never the hub.</summary>
public interface ILiveNotifier
{
    /// <summary>Sends the status to every connected page. It never throws: a page that cannot be reached is not the sync's problem.</summary>
    /// <param name="status">The status to send.</param>
    /// <param name="cancellationToken">Stops the send when the app shuts down.</param>
    Task PublishAsync(CabinetStatus status, CancellationToken cancellationToken);
}

/// <summary>
/// Sends the status over the live hub and swallows any failure, logging only the type of the exception. A broadcast that
/// takes longer than <see cref="TimeLimit"/>, for example because one connection cannot take more data, is given up on, so
/// a slow page never delays the sync that announced itself.
/// </summary>
/// <param name="hub">The hub context that reaches every connected page.</param>
/// <param name="logger">Receives the type name of a failed broadcast, never its message.</param>
/// <param name="time">The clock the time limit is measured on; the system clock when omitted.</param>
public sealed class HubLiveNotifier(
    IHubContext<CabinetHub, ICabinetClient> hub,
    ILogger<HubLiveNotifier> logger,
    TimeProvider? time = null) : ILiveNotifier
{
    /// <summary>How long a broadcast may take before it is given up on.</summary>
    public static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(5);

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <inheritdoc />
    public async Task PublishAsync(CabinetStatus status, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(status);

        try
        {
            await hub.Clients.All.StatusChanged(status).WaitAsync(TimeLimit, _time, cancellationToken);
        }
        catch (TimeoutException)
        {
            logger.LogWarning("Live broadcast was given up on after {Seconds} seconds.", (int)TimeLimit.TotalSeconds);
        }
        catch (Exception exception)
        {
            logger.LogWarning("Live broadcast failed: {ExceptionType}", exception.GetType().Name);
        }
    }
}

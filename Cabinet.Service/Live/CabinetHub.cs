using Cabinet.Service.Sync;
using Microsoft.AspNetCore.SignalR;

namespace Cabinet.Service.Live;

/// <summary>What the server tells a connected page.</summary>
public interface ICabinetClient
{
    /// <summary>Tells the page the sync status changed, with the same content the status endpoint answers with.</summary>
    /// <param name="status">The status as it stands now.</param>
    Task StatusChanged(CabinetStatus status);
}

/// <summary>
/// The live channel pages listen on. It only sends: it declares no method a client can call, so anything a client sends is
/// answered with a "method does not exist" error and can never cause a sync or a request to BGG. It only counts
/// connections, and refuses one beyond the cap.
/// </summary>
/// <param name="limiter">Decides whether another connection may be held open.</param>
public sealed class CabinetHub(LiveConnectionLimiter limiter) : Hub<ICabinetClient>
{
    /// <inheritdoc />
    public override Task OnConnectedAsync()
    {
        if (!limiter.TryAdmit(Context.ConnectionId))
        {
            Context.Abort();
        }

        return base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        limiter.Release(Context.ConnectionId);

        return base.OnDisconnectedAsync(exception);
    }
}

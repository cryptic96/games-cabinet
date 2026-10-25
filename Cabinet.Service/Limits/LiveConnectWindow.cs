using System.Threading.RateLimiting;
using Cabinet.Service.Live;

namespace Cabinet.Service.Limits;

/// <summary>
/// Counts the new live connections each client address asks for, in a fixed window, whichever way the connection is asked
/// for. A connection that was negotiated is counted once, on the negotiate request; the connect request that follows carries
/// the identifier the negotiation issued and is not counted again, so the budget is a number of connections. A connect
/// request with no identifier is a client that skipped negotiation, which a WebSocket client may do, and it is counted
/// itself, so skipping the negotiation does not buy an unlimited rate of new connections. A connect request with an
/// identifier that was never issued is turned away by the hub without opening anything. Every other request on the live
/// route, which in practice is the messages a Server-Sent Events page posts back and its close request, is not counted.
/// </summary>
public static class LiveConnectWindow
{
    private const string NegotiateSuffix = "/negotiate";
    private const string ConnectionIdKey = "id";
    private const string NotCounted = "not-counted";

    /// <summary>Whether the request asks the live route for a new connection and so spends a place in the window.</summary>
    /// <param name="request">The request to classify.</param>
    public static bool CountsAsNewConnection(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Path.StartsWithSegments(LiveEndpoints.Route, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (request.Path.Value?.EndsWith(NegotiateSuffix, StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        return HttpMethods.IsGet(request.Method)
            && string.IsNullOrEmpty(request.Query[ConnectionIdKey].ToString());
    }

    /// <summary>
    /// Builds the limiter that counts new live connections per client address. It replenishes lazily, so an idle address
    /// holds no timer and its window is dropped once it has expired.
    /// </summary>
    /// <param name="options">The budget and the window it is counted in.</param>
    public static PartitionedRateLimiter<HttpContext> CreateLimiter(ClientLimitOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return PartitionedRateLimiter.Create<HttpContext, string>(context =>
            CountsAsNewConnection(context.Request)
                ? RateLimitPartition.GetFixedWindowLimiter(
                    $"connect|{ClientAddress.PartitionKey(context)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.LiveConnectsPerWindow,
                        Window = options.LiveWindow,
                        QueueLimit = 0,
                        AutoReplenishment = false,
                    })
                : RateLimitPartition.GetNoLimiter(NotCounted));
    }
}

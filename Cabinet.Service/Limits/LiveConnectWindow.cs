using System.Threading.RateLimiting;
using Cabinet.Service.Live;

namespace Cabinet.Service.Limits;

/// <summary>
/// Counts the new live connections each client address asks for, and the whole site together, in fixed windows, whichever way the connection is asked
/// for. A connection that was negotiated is counted once, on the negotiate request, in whatever spelling of its path routing
/// accepts (a trailing slash and letter case make no difference); the connect request that follows carries the identifier the
/// negotiation issued and is not counted again, so the budget is a number of connections. Any other request on the live route
/// that carries no connection identifier is a client that skipped negotiation, which a WebSocket client may do, or one the hub
/// turns away without opening anything; it is counted too, so no way of asking buys an unlimited rate of new connections.
/// Every request that carries an identifier, which in practice is the connect request after a negotiation and the messages a
/// Server-Sent Events page posts back with its close request, is not counted. The site-wide window is a backstop on the same
/// requests: it holds however many addresses a flood comes from. A request refused by either window spends nothing of the other.
/// </summary>
public static class LiveConnectWindow
{
    private const string NegotiateSuffix = "/negotiate";
    private const string ConnectionIdKey = "id";

    /// <summary>Whether the request asks the live route for a new connection and so spends a place in the window.</summary>
    /// <param name="request">The request to classify.</param>
    public static bool CountsAsNewConnection(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Path.StartsWithSegments(LiveEndpoints.Route, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = (request.Path.Value ?? string.Empty).TrimEnd('/');

        if (path.EndsWith(NegotiateSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.IsNullOrEmpty(request.Query[ConnectionIdKey].ToString());
    }

    /// <summary>
    /// Builds the limiter that counts new live connections per client address and site-wide. A refusal by either budget
    /// spends nothing from the other, and both are measured on the clock when asked, so an idle address holds no timer and
    /// its window is dropped once it has expired.
    /// </summary>
    /// <param name="options">The budgets and the window they are counted in.</param>
    /// <param name="clock">The clock the windows are measured on; the system clock when absent.</param>
    public static PartitionedRateLimiter<HttpContext> CreateLimiter(ClientLimitOptions options, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new LiveConnectWindowLimiter(options, clock ?? TimeProvider.System);
    }
}

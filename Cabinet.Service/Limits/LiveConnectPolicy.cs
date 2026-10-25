using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Cabinet.Service.Limits;

/// <summary>
/// Limits how many live connections one client address may hold open at once. The request that holds a connection open for
/// its whole life (the WebSocket upgrade, or the Server-Sent Events stream) is counted by a concurrency limiter per address,
/// whose place is kept until the connection ends; counting there covers both transports and refuses the one over the cap
/// with a plain 429 before any upgrade or handshake takes place. Every other request, which in practice is the negotiate
/// request and the messages a Server-Sent Events page posts back (a ping every fifteen seconds), is not limited here, so a
/// group of pages on the fallback transport is never refused for staying connected. How fast an address may ask for new
/// connections is counted separately, by <see cref="LiveConnectWindow"/>. A refusal is answered quietly: no body, no log line.
/// </summary>
/// <param name="options">The per-address budgets and the window the new connections are counted in.</param>
public sealed class LiveConnectPolicy(ClientLimitOptions options) : IRateLimiterPolicy<string>
{
    private const int SmallestRetryAfterSeconds = 1;

    /// <inheritdoc />
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; } = (context, _) => RejectAsync(context, options.LiveWindow);

    /// <inheritdoc />
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var request = httpContext.Request;

        if (HttpMethods.IsGet(request.Method))
        {
            return RateLimitPartition.GetConcurrencyLimiter(
                $"stream|{ClientAddress.PartitionKey(httpContext)}",
                _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = options.LiveConnectionsPerClient,
                    QueueLimit = 0,
                });
        }

        return RateLimitPartition.GetNoLimiter("other");
    }

    /// <summary>Answers a refused live request quietly: 429, a whole-second Retry-After and no-store, with no body.</summary>
    /// <param name="context">The refused request.</param>
    /// <param name="window">The wait to report when the limiter named none.</param>
    public static ValueTask RejectAsync(OnRejectedContext context, TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(context);

        var response = context.HttpContext.Response;
        var wait = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? retryAfter
            : window;
        var seconds = Math.Max(SmallestRetryAfterSeconds, (int)Math.Ceiling(wait.TotalSeconds));

        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.Headers.CacheControl = "no-store";
        response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);

        return ValueTask.CompletedTask;
    }
}

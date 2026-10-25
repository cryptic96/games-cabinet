using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Cabinet.Service.Limits;

/// <summary>
/// Limits what one client address may do with the live channel, in three parts decided by the kind of request. The request
/// that asks for a connection (negotiate) is counted in a fixed window per address, so reconnect churn is slowed. The
/// request that holds a connection open for its whole life (the WebSocket upgrade, or the Server-Sent Events stream) is
/// counted by a concurrency limiter per address, whose place is kept until the connection ends; counting there covers both
/// transports and refuses the one over the cap with a plain 429 before any upgrade or handshake takes place. Every other
/// request, which in practice is the messages a Server-Sent Events page posts back (a ping every fifteen seconds) and its
/// close request, is not limited at all, so a group of pages on the fallback transport is never refused for staying
/// connected. A refusal is answered quietly: no body, no log line.
/// </summary>
/// <param name="options">The per-address budgets and the window the new connections are counted in.</param>
public sealed class LiveConnectPolicy(ClientLimitOptions options) : IRateLimiterPolicy<string>
{
    private const int SmallestRetryAfterSeconds = 1;
    private const string NegotiateSuffix = "/negotiate";

    /// <inheritdoc />
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; } = (context, _) => RejectAsync(context, options.LiveWindow);

    /// <inheritdoc />
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var request = httpContext.Request;
        var client = ClientAddress.PartitionKey(httpContext);

        if (request.Path.Value?.EndsWith(NegotiateSuffix, StringComparison.OrdinalIgnoreCase) == true)
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                $"negotiate|{client}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.LiveConnectsPerWindow,
                    Window = options.LiveWindow,
                    QueueLimit = 0,
                });
        }

        if (HttpMethods.IsGet(request.Method))
        {
            return RateLimitPartition.GetConcurrencyLimiter(
                $"stream|{client}",
                _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = options.LiveConnectionsPerClient,
                    QueueLimit = 0,
                });
        }

        return RateLimitPartition.GetNoLimiter("send");
    }

    private static ValueTask RejectAsync(OnRejectedContext context, TimeSpan window)
    {
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

using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Cabinet.Service.Limits;

/// <summary>
/// Counts presses of the sync button per client address in a fixed window. A refused press is answered the way the shared
/// window answers one: 429 with a whole-second Retry-After and the outcome cooldown, but with no status object, so the page
/// reads the Retry-After header and shows its existing sentence and button countdown. The refusal writes nothing to the
/// log, because a flood would turn per-refusal lines into noise. The windows replenish lazily rather than on a timer of their
/// own, so an address that has stopped pressing holds no timer and its window is dropped once it has expired.
/// </summary>
/// <param name="options">The press budget and the window it is counted in.</param>
public sealed class SyncPressPolicy(ClientLimitOptions options) : IRateLimiterPolicy<string>
{
    private const int SmallestRetryAfterSeconds = 1;

    /// <inheritdoc />
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; } = (context, cancellationToken) => RejectAsync(context, options.SyncWindow, cancellationToken);

    /// <inheritdoc />
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return RateLimitPartition.GetFixedWindowLimiter(
            ClientAddress.PartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.SyncPressesPerWindow,
                Window = options.SyncWindow,
                QueueLimit = 0,
                AutoReplenishment = false,
            });
    }

    private static ValueTask RejectAsync(OnRejectedContext context, TimeSpan window, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        var wait = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? retryAfter
            : window;
        var seconds = Math.Max(SmallestRetryAfterSeconds, (int)Math.Ceiling(wait.TotalSeconds));

        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.Headers.CacheControl = "no-store";
        response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);

        return new ValueTask(response.WriteAsJsonAsync(new { outcome = "cooldown" }, cancellationToken));
    }
}

using System.Net;

namespace Cabinet.Service.Owner;

/// <summary>Marks an endpoint or page that exists only on the owner port.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class OwnerOnlyAttribute : Attribute;

/// <summary>
/// The owner port and the client ranges allowed on it, read once at start-up. The owner tools have no password: where the
/// request comes from is the only lock, so the listener port and the client address resolved through the trusted proxy are
/// the only inputs, never the Host header or anything else a client can write.
/// </summary>
/// <param name="Port">The owner listener's port, or null when the owner tools are switched off.</param>
/// <param name="Ranges">The client address ranges allowed on the owner port.</param>
public sealed record OwnerAccessOptions(int? Port, IReadOnlyList<System.Net.IPNetwork> Ranges)
{
    /// <summary>Whether the owner tools are switched on.</summary>
    public bool Enabled => Port is not null;

    /// <summary>Whether the request arrived on the owner listener.</summary>
    /// <param name="context">The request.</param>
    public bool IsOwnerPort(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Port is { } port && context.Connection.LocalPort == port;
    }

    /// <summary>Whether the client address, as the forwarded-headers middleware resolved it, lies in a configured range.</summary>
    /// <param name="client">The resolved client address, or null when there is none.</param>
    public bool Allows(IPAddress? client)
    {
        if (client is null)
        {
            return false;
        }

        var address = client.IsIPv4MappedToIPv6 ? client.MapToIPv4() : client;

        return Ranges.Any(range => range.Contains(address));
    }
}

/// <summary>Reads the owner access settings and builds the two walls that keep the owner tools off the public port and off foreign addresses.</summary>
public static class OwnerAccess
{
    private const string UrlKey = "Kestrel:Endpoints:Owner:Url";

    private const string RangesKey = "Owner:AllowedRanges";

    private const string KnownProxiesKey = "ReverseProxy:KnownProxies";

    private const string OwnerSegment = "owner";

    /// <summary>
    /// Reads the owner endpoint and its allowed ranges. With no owner endpoint configured the owner tools are off and no range
    /// is needed. With one configured it must carry an explicit port, at least one valid range and at least one known proxy,
    /// otherwise the proxy's own address would pass for the client and the range check would let everyone in.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    public static OwnerAccessOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var url = configuration[UrlKey];
        if (string.IsNullOrWhiteSpace(url))
        {
            return new OwnerAccessOptions(null, []);
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttp
            || uri.IsDefaultPort)
        {
            throw new InvalidOperationException($"{UrlKey} must be an absolute http URL with an explicit port.");
        }

        var ranges = ParseRanges(configuration);

        var proxies = configuration.GetSection(KnownProxiesKey).Get<string[]>() ?? [];
        if (!proxies.Any(proxy => !string.IsNullOrWhiteSpace(proxy)))
        {
            throw new InvalidOperationException(
                $"{KnownProxiesKey} needs at least one entry when {UrlKey} is set, so client addresses can be trusted.");
        }

        return new OwnerAccessOptions(uri.Port, ranges);
    }

    /// <summary>
    /// Wall two: on the owner port only clients inside the configured ranges get through. Everyone else gets a bare refusal
    /// with no site around it. Place it straight after the forwarded-headers middleware so the address it judges is the client's.
    /// </summary>
    /// <param name="app">The pipeline.</param>
    public static IApplicationBuilder UseOwnerClientRange(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            var options = context.RequestServices.GetRequiredService<OwnerAccessOptions>();

            if (options.IsOwnerPort(context) && !options.Allows(context.Connection.RemoteIpAddress))
            {
                context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Cabinet.Service.Owner")
                    .LogWarning("Owner request refused: the client is outside the allowed ranges.");

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "text/plain; charset=utf-8";
                context.Response.Headers.CacheControl = "no-store";
                await context.Response.WriteAsync(OwnerStrings.NotAvailable);

                return;
            }

            await next(context);
        });
    }

    /// <summary>
    /// Answers an empty 404 on every port but the owner port for any path whose first segment is the owner segment, before
    /// static files and box art can answer it. It is a second, independent check beside the endpoint marker.
    /// </summary>
    /// <param name="app">The pipeline.</param>
    public static IApplicationBuilder UseOwnerPathGuard(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            var options = context.RequestServices.GetRequiredService<OwnerAccessOptions>();

            if (!options.IsOwnerPort(context) && StartsWithOwnerSegment(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;

                return;
            }

            await next(context);
        });
    }

    /// <summary>
    /// Wall one: an endpoint marked owner-only answers an empty 404 when it is reached on any port but the owner port. Place it
    /// straight after routing so the endpoint is known.
    /// </summary>
    /// <param name="app">The pipeline.</param>
    public static IApplicationBuilder UseOwnerOnlyRoutes(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            var options = context.RequestServices.GetRequiredService<OwnerAccessOptions>();
            var ownerOnly = context.GetEndpoint()?.Metadata.GetMetadata<OwnerOnlyAttribute>() is not null;

            if (ownerOnly && !options.IsOwnerPort(context))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;

                return;
            }

            await next(context);
        });
    }

    private static bool StartsWithOwnerSegment(PathString path)
    {
        var first = (path.Value ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

        return first is not null && first.Equals(OwnerSegment, StringComparison.OrdinalIgnoreCase);
    }

    private static List<System.Net.IPNetwork> ParseRanges(IConfiguration configuration)
    {
        var entries = configuration.GetSection(RangesKey).Get<string[]>() ?? [];
        var ranges = new List<System.Net.IPNetwork>();

        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            if (!System.Net.IPNetwork.TryParse(entry.Trim(), out var network)
                || network.PrefixLength == 0
                || network.BaseAddress.IsIPv4MappedToIPv6)
            {
                throw new InvalidOperationException($"{RangesKey}:{index} is not a usable address range.");
            }

            ranges.Add(network);
        }

        if (ranges.Count == 0)
        {
            throw new InvalidOperationException($"{RangesKey} needs at least one entry when {UrlKey} is set.");
        }

        return ranges;
    }
}

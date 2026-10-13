using System.Net;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using System.Text.RegularExpressions;

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
/// <param name="Proxies">The configured known proxies; on the owner port only a connection from one of them is believed.</param>
public sealed record OwnerAccessOptions(int? Port, IReadOnlyList<System.Net.IPNetwork> Ranges, IReadOnlyList<IPAddress>? Proxies = null)
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

        return Ranges.Any(range => range.Contains(Normalised(client)));
    }

    /// <summary>Whether an address is one of the configured known proxies. With none configured, or no address, it is false.</summary>
    /// <param name="peer">The address of the connection's other end, or null when there is none.</param>
    public bool IsKnownProxy(IPAddress? peer) =>
        peer is not null && (Proxies ?? []).Any(proxy => Normalised(proxy).Equals(Normalised(peer)));

    internal static IPAddress Normalised(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}

/// <summary>Reads the owner access settings and builds the two walls that keep the owner tools off the public port and off foreign addresses.</summary>
public static partial class OwnerAccess
{
    private const string UrlKey = "Kestrel:Endpoints:Owner:Url";

    private const string RangesKey = "Owner:AllowedRanges";

    private const string KnownProxiesKey = "ReverseProxy:KnownProxies";

    private const string WebUrlKey = "Kestrel:Endpoints:Web:Url";

    private const string OpsUrlKey = "Kestrel:Endpoints:Ops:Url";

    private const string OwnerSegment = "owner";

    private const string PeerKey = "Cabinet.ConnectionPeer";

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

        RejectSharedPort(configuration, uri.Port, WebUrlKey);
        RejectSharedPort(configuration, uri.Port, OpsUrlKey);

        var ranges = ParseRanges(configuration);

        var proxies = configuration.GetSection(KnownProxiesKey).Get<string[]>() ?? [];
        if (!proxies.Any(proxy => !string.IsNullOrWhiteSpace(proxy)))
        {
            throw new InvalidOperationException(
                $"{KnownProxiesKey} needs at least one entry when {UrlKey} is set, so client addresses can be trusted.");
        }

        var proxyAddresses = proxies.Where(proxy => IPAddress.TryParse(proxy, out _)).Select(proxy => IPAddress.Parse(proxy)).ToList();

        return new OwnerAccessOptions(uri.Port, ranges, proxyAddresses);
    }

    /// <summary>
    /// Remembers the address of the connection's other end before the forwarded-headers middleware may replace it, so the owner
    /// wall can tell whether the request really came from the known proxy. Place it straight before the forwarded-headers
    /// middleware.
    /// </summary>
    /// <param name="app">The pipeline.</param>
    public static IApplicationBuilder UseConnectionPeer(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use((context, next) =>
        {
            context.Items[PeerKey] = context.Connection.RemoteIpAddress;

            return next(context);
        });
    }

    /// <summary>
    /// Wall two: on the owner port only a request that came through a configured known proxy and carries a client address
    /// inside the configured ranges gets through; everyone else gets a bare refusal with no site around it. In production a
    /// request whose client address is the proxy's own, because no forwarded address was applied, is refused as well. Place it
    /// straight after the forwarded-headers middleware so the address it judges is the client's.
    /// </summary>
    /// <param name="app">The pipeline.</param>
    public static IApplicationBuilder UseOwnerClientRange(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            var options = context.RequestServices.GetRequiredService<OwnerAccessOptions>();

            if (options.IsOwnerPort(context) && !IsAllowed(context, options))
            {
                context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Cabinet.Service.Owner")
                    .LogWarning("Owner request refused: the client is not an allowed address behind the known proxy.");

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "text/plain; charset=utf-8";
                context.Response.Headers.CacheControl = "no-store";
                await context.Response.WriteAsync(OwnerStrings.NotAvailable);

                return;
            }

            await next(context);
        });
    }

    private static bool IsAllowed(HttpContext context, OwnerAccessOptions options)
    {
        var peer = context.Items.TryGetValue(PeerKey, out var recorded) ? recorded as IPAddress : null;
        var client = context.Connection.RemoteIpAddress;

        if (!options.IsKnownProxy(peer) || !options.Allows(client))
        {
            return false;
        }

        var production = context.RequestServices.GetRequiredService<IHostEnvironment>().IsProduction();

        return !(production && peer is not null && client is not null && OwnerAccessOptions.Normalised(peer).Equals(OwnerAccessOptions.Normalised(client)));
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
    /// Wall one: an endpoint marked owner-only answers an empty 404 when it is reached on any port but the owner port. On the
    /// owner port a body that declares itself larger than the endpoint's size limit is refused with 413 before any filter or
    /// handler runs. Place it straight after routing so the endpoint is known.
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

            var limit = context.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize;
            if (ownerOnly && limit is { } maxBytes && context.Request.ContentLength > maxBytes)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;

                return;
            }

            await next(context);
        });
    }

    /// <summary>
    /// Marks the owner's own scripts and stylesheet, which sit in the shared static folders, as owner-only, so the owner-only
    /// wall answers an empty 404 for them on every port but the owner port. Fingerprinted copies are marked too, because the
    /// mark is set on every endpoint whose route names an owner asset.
    /// </summary>
    /// <typeparam name="TBuilder">The kind of endpoint convention builder.</typeparam>
    /// <param name="builder">The builder of the static asset endpoints.</param>
    public static TBuilder MarkOwnerAssets<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Add(endpoint =>
        {
            if (endpoint is RouteEndpointBuilder route && IsOwnerAsset(route.RoutePattern.RawText))
            {
                route.Metadata.Add(new OwnerOnlyAttribute());
            }
        });

        return builder;
    }

    /// <summary>Whether a route names a script or stylesheet that only the owner pages use.</summary>
    /// <param name="route">The route text of a static asset, with or without a leading slash.</param>
    public static bool IsOwnerAsset(string? route)
    {
        var trimmed = (route ?? string.Empty).TrimStart('/');

        return trimmed.StartsWith("js/owner-", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("css/owner.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWithOwnerSegment(PathString path)
    {
        var first = (path.Value ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

        return first is not null && first.Equals(OwnerSegment, StringComparison.OrdinalIgnoreCase);
    }

    private static void RejectSharedPort(IConfiguration configuration, int ownerPort, string otherKey)
    {
        var other = configuration[otherKey];
        if (string.IsNullOrWhiteSpace(other))
        {
            return;
        }

        var match = TrailingPort().Match(other.Trim());
        if (match.Success && int.TryParse(match.Groups[1].Value, out var port) && port == ownerPort)
        {
            throw new InvalidOperationException($"{UrlKey} must not use the same port as {otherKey}.");
        }
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

            if (!TryParseRange(entry.Trim(), out var network))
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

    /// <summary>
    /// Reads an address with an optional prefix length. A bare address is a single host, host bits are cleared, and the forms
    /// that would let everyone in or that name the same hosts twice are refused: prefix length 0, any address in the
    /// IPv4-mapped form, and shortened or numeric IPv4 spellings.
    /// </summary>
    private static bool TryParseRange(string entry, out System.Net.IPNetwork network)
    {
        network = default;

        var slash = entry.IndexOf('/', StringComparison.Ordinal);
        var addressText = slash < 0 ? entry : entry[..slash];

        if (!IsPlainAddressSpelling(addressText) || !IPAddress.TryParse(addressText, out var address) || address.IsIPv4MappedToIPv6)
        {
            return false;
        }

        var maxPrefix = address.GetAddressBytes().Length * 8;
        var prefix = maxPrefix;
        if (slash >= 0
            && !(int.TryParse(entry[(slash + 1)..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out prefix)
                && prefix > 0
                && prefix <= maxPrefix))
        {
            return false;
        }

        network = new System.Net.IPNetwork(ClearHostBits(address, prefix), prefix);

        return true;
    }

    private static bool IsPlainAddressSpelling(string text)
    {
        if (text.Contains(':', StringComparison.Ordinal))
        {
            return !text.Contains('%', StringComparison.Ordinal);
        }

        return text.Count(character => character == '.') == 3 && text.All(character => char.IsAsciiDigit(character) || character == '.');
    }

    private static IPAddress ClearHostBits(IPAddress address, int prefix)
    {
        var bytes = address.GetAddressBytes();
        for (var index = 0; index < bytes.Length; index++)
        {
            var kept = Math.Clamp(prefix - (index * 8), 0, 8);
            var mask = kept == 0 ? 0 : (byte)(0xFF << (8 - kept));
            bytes[index] &= (byte)mask;
        }

        return new IPAddress(bytes);
    }

    [GeneratedRegex(@":(\d+)/?$")]
    private static partial Regex TrailingPort();
}

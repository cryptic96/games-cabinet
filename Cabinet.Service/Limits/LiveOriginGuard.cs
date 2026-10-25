using Cabinet.Service.Live;

namespace Cabinet.Service.Limits;

/// <summary>
/// Admits only the site's own pages to the live channel. A request that names an Origin must name the same host and port as
/// the request's Host. Host filtering limits Host to the hostnames in the server env file, so this admits exactly the site's
/// own public and owner pages and no second list of allowed origins can drift from the first. A request with no Origin, which
/// is what a same-origin Server-Sent Events stream and a non-browser client send, is allowed. SignalR has no origin option of
/// its own, and the WebSocket middleware's allowed-origins option covers WebSockets only, never Server-Sent Events, which is
/// why the check lives here. The guard runs before the rate limiter, so a page on another site can neither connect nor spend a
/// visitor's connection budget.
/// </summary>
public static class LiveOriginGuard
{
    private const int HttpsPort = 443;
    private const int HttpPort = 80;

    /// <summary>Whether the request's Origin, when it has one, is the site itself.</summary>
    /// <param name="request">The request to check.</param>
    public static bool IsAllowed(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Headers.TryGetValue("Origin", out var origins) || origins.Count == 0)
        {
            return true;
        }

        if (origins.Count > 1
            || !Uri.TryCreate(origins[0], UriKind.Absolute, out var origin)
            || (origin.Scheme != Uri.UriSchemeHttp && origin.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        var port = request.Host.Port ?? (request.IsHttps ? HttpsPort : HttpPort);

        return string.Equals(origin.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase)
            && origin.Port == port;
    }

    /// <summary>Answers 403 with no body to a request on the live route whose Origin is not the site itself.</summary>
    /// <param name="app">The pipeline to add to.</param>
    public static IApplicationBuilder UseLiveOriginGuard(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(LiveEndpoints.Route, StringComparison.OrdinalIgnoreCase)
                && !IsAllowed(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;

                return;
            }

            await next(context);
        });
    }
}

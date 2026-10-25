namespace Cabinet.Service.Hosting;

/// <summary>
/// Sends the site's security headers with every response the pipeline produces. The Content-Security-Policy allows only
/// the site's own origin: the page, its stylesheets, its scripts and its data all come from the site itself, with no
/// inline styles, inline scripts, event attributes or data URLs, so the policy never allows inline code or eval. The
/// policy lives here rather than in the reverse proxy, so it is tested together with the page it protects and a change
/// that needs a looser policy fails a test. The other headers close the remaining browser-side doors: nosniff stops a
/// browser from guessing a content type for box art or data, the referrer policy keeps the full address from leaking to
/// the linked BoardGameGeek credit, the frame option repeats the policy's frame ancestors for older browsers, the
/// permissions policy switches off device features the cabinet never uses, and the cross-origin opener and resource
/// policies keep other sites' windows and pages away from the site's responses. No cross-origin embedder policy is sent
/// because nothing here needs cross-origin isolation. Strict transport security is deliberately left to the reverse
/// proxy that terminates TLS, since the app only ever sees plain HTTP behind it.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>The content security policy sent with every response.</summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'";

    /// <summary>Tells browsers to trust the declared content type.</summary>
    public const string ContentTypeOptions = "nosniff";

    /// <summary>Sends only the origin, never the path, to other sites.</summary>
    public const string ReferrerPolicy = "strict-origin-when-cross-origin";

    /// <summary>Refuses to be framed by any page.</summary>
    public const string FrameOptions = "DENY";

    /// <summary>Switches off the browser features the cabinet does not use.</summary>
    public const string PermissionsPolicy =
        "accelerometer=(), camera=(), display-capture=(), geolocation=(), gyroscope=(), hid=(), magnetometer=(), microphone=(), midi=(), payment=(), serial=(), usb=(), xr-spatial-tracking=()";

    /// <summary>Keeps the site in its own browsing context group.</summary>
    public const string CrossOriginOpenerPolicy = "same-origin";

    /// <summary>Lets only the site's own pages load its responses.</summary>
    public const string CrossOriginResourcePolicy = "same-origin";

    /// <summary>
    /// Adds the security headers to every response the rest of the pipeline produces. The headers are assigned, so an
    /// endpoint that sets the same header itself replaces the value instead of adding a second one.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            headers.XContentTypeOptions = ContentTypeOptions;
            headers["Referrer-Policy"] = ReferrerPolicy;
            headers.XFrameOptions = FrameOptions;
            headers["Permissions-Policy"] = PermissionsPolicy;
            headers["Cross-Origin-Opener-Policy"] = CrossOriginOpenerPolicy;
            headers["Cross-Origin-Resource-Policy"] = CrossOriginResourcePolicy;

            await next(context);
        });
    }
}

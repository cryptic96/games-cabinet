namespace Cabinet.Service.Hosting;

/// <summary>
/// Sends the site's Content-Security-Policy with every response. The page, its stylesheets, its scripts and its data all
/// come from the site itself, with no inline styles, inline scripts, event attributes or data URLs, so the policy allows
/// only the site's own origin and never inline code or eval. The policy lives here rather than in the reverse proxy, so
/// it is tested together with the page it protects and a change that needs a looser policy fails a test.
/// </summary>
public static class ContentSecurityPolicy
{
    /// <summary>The policy sent with every response.</summary>
    public const string Policy =
        "default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'";

    /// <summary>Adds the policy header to every response the rest of the pipeline produces.</summary>
    public static IApplicationBuilder UseContentSecurityPolicy(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            context.Response.Headers.ContentSecurityPolicy = Policy;

            await next(context);
        });
    }
}

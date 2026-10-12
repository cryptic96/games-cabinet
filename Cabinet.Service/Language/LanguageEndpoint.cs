using Cabinet.Service.Prototype;

namespace Cabinet.Service.Language;

/// <summary>
/// The endpoint the language toggle points at. It remembers the visitor's choice in one functional cookie and sends the visitor
/// back to the plain page, so the language never appears in the address. Only the two known codes are accepted, and the
/// redirect target is fixed apart from a sample name the catalog itself returns, so nothing the visitor typed reaches the
/// cookie or the redirect.
/// </summary>
public static class LanguageEndpoint
{
    /// <summary>The address the toggle items start with.</summary>
    public const string RouteBase = "/language";

    /// <summary>The route that switches the language.</summary>
    public const string Route = RouteBase + "/{code}";

    private static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(365);

    /// <summary>Maps the language route; an unknown code answers 404 and sets nothing.</summary>
    public static IEndpointRouteBuilder MapCabinetLanguage(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(Route, Handle);

        return endpoints;
    }

    /// <summary>
    /// Answers one switch. A known code sets the cookie with the lowercase code and answers 303 to the plain page, or to the
    /// plain page with the sample name when the catalog honours the requested sample. The answer is never cached.
    /// </summary>
    /// <param name="code">The requested language code; only <c>en</c> and <c>nl</c> are accepted, in any casing.</param>
    /// <param name="sample">The sample the visitor was looking at; ignored unless the catalog honours it.</param>
    /// <param name="context">The request, whose services supply the sample catalog.</param>
    public static IResult Handle(string code, string? sample, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!SiteLanguage.TryGet(code, out var language))
        {
            return Results.NotFound();
        }

        context.Response.Cookies.Append(
            SiteLanguage.CookieName,
            language.Code,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                Path = "/",
                MaxAge = CookieLifetime,
            });

        var catalog = context.RequestServices.GetRequiredService<SampleCatalog>();
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Location = catalog.TryResolve(sample, out var sampleName)
            ? $"/?sample={Uri.EscapeDataString(sampleName)}"
            : "/";

        return Results.StatusCode(StatusCodes.Status303SeeOther);
    }
}

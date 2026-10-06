using Cabinet.Domain.Layout;
using Cabinet.Service.Prototype;
using Microsoft.Net.Http.Headers;

namespace Cabinet.Service.Layout;

/// <summary>
/// The undocumented endpoint the cabinet page reads. It only builds layouts for sample and profile names on fixed
/// allowlists, so a visitor cannot ask for an arbitrary collection size, and it sends no cross-origin headers.
/// </summary>
public static class LayoutEndpoint
{
    /// <summary>The route the cabinet page fetches.</summary>
    public const string Route = "/cabinet/layout";

    /// <summary>
    /// Reads and validates the Layout settings now and registers them as a singleton, so a bad value stops the app at
    /// startup instead of failing the first request.
    /// </summary>
    public static IServiceCollection AddCabinetLayout(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return services
            .AddSingleton(LayoutSettings.FromConfiguration(configuration))
            .AddSingleton(SampleCatalog.FromConfiguration(configuration))
            .AddSingleton<LayoutCache>();
    }

    /// <summary>Maps the layout route; it answers 404 while the prototype is off and for unknown sample or profile names.</summary>
    public static IEndpointRouteBuilder MapCabinetLayout(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(Route, Handle);

        return endpoints;
    }

    /// <summary>
    /// Answers one layout request. The query is checked against the allowlists only, which costs nothing; the sample is
    /// generated and laid out only when the layout cache does not hold it yet, so a repeated request or a revalidation
    /// answered with 304 never rebuilds anything.
    /// </summary>
    /// <param name="sample">The requested sample name.</param>
    /// <param name="profile">The requested screen profile.</param>
    /// <param name="context">The request, whose services supply the catalog and the layout cache.</param>
    public static IResult Handle(string? sample, string? profile, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var catalog = context.RequestServices.GetRequiredService<SampleCatalog>();

        if (!catalog.Enabled || !catalog.IsKnown(sample) || !SectionDesigns.TryGet(profile, out var design))
        {
            return Results.NotFound();
        }

        var cached = context.RequestServices.GetRequiredService<LayoutCache>().Get(sample!, design);

        context.Response.Headers.ETag = cached.ETag;
        context.Response.Headers.CacheControl = "no-cache";

        return MatchesIfNoneMatch(context.Request, cached.ETag)
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : Results.Content(cached.Json, "application/json");
    }

    private static bool MatchesIfNoneMatch(HttpRequest request, string eTag)
    {
        var requested = request.GetTypedHeaders().IfNoneMatch;

        return requested.Any(candidate =>
            candidate.Equals(EntityTagHeaderValue.Any) || candidate.Compare(new EntityTagHeaderValue(eTag), useStrongComparison: false));
    }
}

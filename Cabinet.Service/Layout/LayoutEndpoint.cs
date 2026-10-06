using Cabinet.Domain.Layout;
using Cabinet.Service.Collection;
using Cabinet.Service.Prototype;
using Microsoft.Net.Http.Headers;

namespace Cabinet.Service.Layout;

/// <summary>
/// The undocumented endpoint the cabinet page reads. It serves the synced collection's layout, and an invented
/// collection only when the catalog honours the requested sample. Profile and sample names are checked against fixed
/// allowlists, so a visitor cannot ask for an arbitrary collection size, and it sends no cross-origin headers.
/// </summary>
public static class LayoutEndpoint
{
    /// <summary>The route the cabinet page fetches.</summary>
    public const string Route = "/cabinet/layout";

    /// <summary>
    /// Reads and validates the Layout settings now and registers them as a singleton, so a bad value stops the app at
    /// startup instead of failing the first request. It also registers the sample catalog, the sample layout cache and the
    /// store that holds the synced collection.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The configuration the Layout and Prototype settings are read from.</param>
    /// <param name="environment">The hosting environment the app runs in.</param>
    public static IServiceCollection AddCabinetLayout(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        return services
            .AddSingleton(LayoutSettings.FromConfiguration(configuration))
            .AddSingleton(SampleCatalog.FromConfiguration(configuration))
            .AddSingleton<LayoutCache>()
            .AddSingleton<CollectionStore>();
    }

    /// <summary>Maps the layout route; it answers 404 for an unknown or missing profile name.</summary>
    public static IEndpointRouteBuilder MapCabinetLayout(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(Route, Handle);

        return endpoints;
    }

    /// <summary>
    /// Answers one layout request. The query is checked against the allowlists only, which costs nothing. A sample the
    /// catalog honours is laid out from the sample cache; anything else, including an unknown or ignored sample value,
    /// gets the synced collection's layout, which is built once per collection version and profile. A repeated request or
    /// a revalidation answered with 304 never rebuilds anything, and the sample value is never echoed.
    /// </summary>
    /// <param name="sample">The requested sample name; ignored unless the catalog honours it.</param>
    /// <param name="profile">The requested screen profile.</param>
    /// <param name="context">The request, whose services supply the catalog and the layout cache.</param>
    public static IResult Handle(string? sample, string? profile, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!SectionDesigns.TryGet(profile, out var design))
        {
            return Results.NotFound();
        }

        var services = context.RequestServices;
        var cached = services.GetRequiredService<SampleCatalog>().TryResolve(sample, out var sampleName)
            ? services.GetRequiredService<LayoutCache>().Get(sampleName, design)
            : services.GetRequiredService<CollectionStore>().Current.LayoutFor(design, services.GetRequiredService<LayoutOptions>());

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

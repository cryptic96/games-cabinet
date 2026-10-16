using Cabinet.Domain.Layout;
using Cabinet.Service.Collection;
using Cabinet.Service.Hosting;
using Cabinet.Service.Prototype;

namespace Cabinet.Service.Layout;

/// <summary>
/// The undocumented endpoint the cabinet page reads to show one cabinet per storage location. It carries only what the page
/// draws: the geometry and palette the single cabinet already carries, and each location's identifier and name. It reads the
/// collection the server already holds and never asks the source of the collection for anything. Profile and sample names are
/// checked against fixed allowlists, and it sends no cross-origin headers.
/// </summary>
public static class LocationLayoutEndpoint
{
    /// <summary>The route the cabinet page fetches.</summary>
    public const string Route = "/cabinet/layout-by-location";

    /// <summary>
    /// Answers one request. The query is checked against the allowlists only. A sample the catalog honours is answered from the
    /// sample cache; anything else, including an unknown or ignored sample value, gets the synced collection's cabinets, which
    /// are built once per collection version and profile. A revalidation answered with 304 never rebuilds anything, and the
    /// sample value is never echoed.
    /// </summary>
    /// <param name="sample">The requested sample name; ignored unless the catalog honours it.</param>
    /// <param name="profile">The requested screen profile.</param>
    /// <param name="context">The request, whose services supply the catalog, the layout cache and the collection.</param>
    public static IResult Handle(string? sample, string? profile, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!SectionDesigns.TryGet(profile, out var design))
        {
            return Results.NotFound();
        }

        var services = context.RequestServices;
        var cached = services.GetRequiredService<SampleCatalog>().TryResolve(sample, out var sampleName)
            ? services.GetRequiredService<LayoutCache>().GetByLocation(sampleName, design)
            : services.GetRequiredService<CollectionStore>().Current.LocationLayoutsFor(design, services.GetRequiredService<LayoutOptions>());

        context.Response.Headers.ETag = cached.ETag;
        context.Response.Headers.CacheControl = "no-cache";

        return EntityTags.MatchesIfNoneMatch(context.Request, cached.ETag)
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : Results.Content(cached.Json, "application/json");
    }
}

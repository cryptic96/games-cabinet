using Cabinet.Domain.Cards;
using Cabinet.Domain.Layout;
using Cabinet.Service.Collection;
using Cabinet.Service.Prototype;
using Microsoft.Net.Http.Headers;

namespace Cabinet.Service.Cards;

/// <summary>
/// The undocumented endpoint the detail card and the games list read. It serves the cards of the synced collection, and
/// of an invented collection only when the catalog honours the requested sample. Profile and sample names are checked
/// against fixed allowlists, only the fields the page shows are sent, and it sends no cross-origin headers.
/// </summary>
public static class CardsEndpoint
{
    /// <summary>The route the cabinet page fetches.</summary>
    public const string Route = "/cabinet/cards";

    /// <summary>
    /// Answers one cards request. The query is checked against the allowlists only. A sample the catalog honours is
    /// answered from the sample cache; anything else, including an unknown or ignored sample value, gets the synced
    /// collection's cards. A revalidation answered with 304 never rebuilds anything, and the sample value is never echoed.
    /// </summary>
    /// <param name="sample">The requested sample name; ignored unless the catalog honours it.</param>
    /// <param name="profile">The requested screen profile.</param>
    /// <param name="context">The request, whose services supply the catalog and the caches.</param>
    public static IResult Handle(string? sample, string? profile, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!SectionDesigns.TryGet(profile, out var design))
        {
            return Results.NotFound();
        }

        var services = context.RequestServices;
        var cached = services.GetRequiredService<SampleCatalog>().TryResolve(sample, out var sampleName)
            ? services.GetRequiredService<CardCache>().Get(sampleName, design)
            : SyncedCards(services, design);

        context.Response.Headers.ETag = cached.ETag;
        context.Response.Headers.CacheControl = "no-cache";

        return MatchesIfNoneMatch(context.Request, cached.ETag)
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : Results.Content(cached.Json, "application/json");
    }

    private static CachedCards SyncedCards(IServiceProvider services, SectionDesign design)
    {
        var current = services.GetRequiredService<CollectionStore>().Current;
        var layout = current.LayoutFor(design, services.GetRequiredService<LayoutOptions>());

        return CachedCards.From(new CardsDocument(layout.ETag, []));
    }

    private static bool MatchesIfNoneMatch(HttpRequest request, string eTag)
    {
        var requested = request.GetTypedHeaders().IfNoneMatch;

        return requested.Any(candidate =>
            candidate.Equals(EntityTagHeaderValue.Any) || candidate.Compare(new EntityTagHeaderValue(eTag), useStrongComparison: false));
    }
}

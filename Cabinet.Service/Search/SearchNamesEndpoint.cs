using Cabinet.Service.Collection;
using Cabinet.Service.Hosting;
using Cabinet.Service.Prototype;

namespace Cabinet.Service.Search;

/// <summary>
/// The undocumented endpoint the page's search reads the other titles of the owned games from. It serves the synced
/// collection's names, and an invented collection's only when the catalog honours the requested sample. Only the titles the
/// search needs are sent, keyed by game, for owned games only; it never reaches the source of the data, and it sends no
/// cross-origin headers.
/// </summary>
public static class SearchNamesEndpoint
{
    /// <summary>The route the cabinet page fetches on first use of the search field.</summary>
    public const string Route = "/cabinet/search-names";

    /// <summary>
    /// Answers one names request. A sample the catalog honours is answered from the sample cache; anything else, including an
    /// unknown or ignored sample value, gets the synced collection's names, an empty list before the first sync. A revalidation
    /// answered with 304 never rebuilds anything, and the sample value is never echoed.
    /// </summary>
    /// <param name="sample">The requested sample name; ignored unless the catalog honours it.</param>
    /// <param name="context">The request, whose services supply the catalog and the caches.</param>
    public static IResult Handle(string? sample, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var services = context.RequestServices;
        var cached = services.GetRequiredService<SampleCatalog>().TryResolve(sample, out var sampleName)
            ? services.GetRequiredService<SearchNamesCache>().Get(sampleName)
            : services.GetRequiredService<CollectionStore>().Current.NamesIndex;

        context.Response.Headers.ETag = cached.ETag;
        context.Response.Headers.CacheControl = "no-cache";

        return EntityTags.MatchesIfNoneMatch(context.Request, cached.ETag)
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : Results.Content(cached.Json, "application/json");
    }
}

using Microsoft.Net.Http.Headers;

namespace Cabinet.Service.Hosting;

/// <summary>Decides whether a visitor's cached copy of a data route is still the current one.</summary>
internal static class EntityTags
{
    /// <summary>
    /// Whether the request's If-None-Match header names the given tag or the wildcard. The comparison is weak, so a tag the
    /// browser sent back with a weak marker still matches; a malformed header value is ignored and never echoed.
    /// </summary>
    /// <param name="request">The request to read the header from.</param>
    /// <param name="eTag">The current entity tag of the response, quoted.</param>
    public static bool MatchesIfNoneMatch(HttpRequest request, string eTag)
    {
        var requested = request.GetTypedHeaders().IfNoneMatch;

        return requested.Any(candidate =>
            candidate.Equals(EntityTagHeaderValue.Any) || candidate.Compare(new EntityTagHeaderValue(eTag), useStrongComparison: false));
    }
}

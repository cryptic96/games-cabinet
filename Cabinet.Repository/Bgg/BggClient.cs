using System.Net;
using System.Net.Http.Headers;
using System.Xml;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;

namespace Cabinet.Repository.Bgg;

/// <summary>
/// Reads the owner's owned collection from the BGG XML API in two calls, because the unfiltered call labels expansions as
/// base games: base games with expansions excluded, then expansions. Every call goes through the shared pacer. The client
/// never logs: a request address carries the username and an answer carries the owner's data.
/// </summary>
/// <param name="http">The client to send with; its base address is the API address and it never follows redirects.</param>
/// <param name="options">The username, the contact address and the private-information switch.</param>
/// <param name="pacer">Spaces the calls out.</param>
public sealed class BggClient(HttpClient http, BggOptions options, IRequestPacer pacer) : ICollectionSource
{
    /// <inheritdoc />
    public async Task<CollectionFetchResult> FetchOwnedAsync(CancellationToken cancellationToken)
    {
        if (!options.IsConfigured)
        {
            return new CollectionFetchResult.Failed(SyncFailure.NotConfigured);
        }

        var baseGames = await GetCollectionAsync("excludesubtype=boardgameexpansion", ItemKind.Base, cancellationToken);

        if (baseGames is not CollectionFetchResult.Fetched baseFetched)
        {
            return baseGames;
        }

        var expansions = await GetCollectionAsync("subtype=boardgameexpansion", ItemKind.Expansion, cancellationToken);

        return expansions is CollectionFetchResult.Fetched expansionFetched
            ? new CollectionFetchResult.Fetched([.. baseFetched.Items, .. expansionFetched.Items])
            : expansions;
    }

    private async Task<CollectionFetchResult> GetCollectionAsync(string subtypeFilter, ItemKind kind, CancellationToken cancellationToken)
    {
        var privateInfo = options.IncludePrivateInfo ? "&showprivate=1" : string.Empty;
        var relative = $"collection?username={Uri.EscapeDataString(options.Username!)}&own=1&{subtypeFilter}&version=1{privateInfo}";

        try
        {
            using var lease = await pacer.WaitTurnAsync(cancellationToken);
            using var request = CreateRequest(relative);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return new CollectionFetchResult.Failed(SyncFailure.Unavailable);
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            var parsed = BggCollectionParser.Parse(body, kind);

            return parsed.TotalItems is { } total && total != parsed.Items.Count
                ? new CollectionFetchResult.Failed(SyncFailure.BadAnswer)
                : new CollectionFetchResult.Fetched(parsed.Items);
        }
        catch (Exception exception) when (exception is BggAnswerException or XmlException)
        {
            return new CollectionFetchResult.Failed(SyncFailure.BadAnswer);
        }
        catch (HttpRequestException)
        {
            return new CollectionFetchResult.Failed(SyncFailure.Unavailable);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new CollectionFetchResult.Failed(SyncFailure.Timeout);
        }
    }

    private HttpRequestMessage CreateRequest(string relative)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relative);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        request.Headers.TryAddWithoutValidation("User-Agent", BggTransport.UserAgent(options));

        return request;
    }
}

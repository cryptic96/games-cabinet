using System.Net;
using System.Net.Http.Headers;
using System.Xml;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Microsoft.Extensions.Logging;

namespace Cabinet.Repository.Bgg;

/// <summary>
/// Reads the owner's owned collection from the BGG XML API in two calls, because the unfiltered call labels expansions as
/// base games: base games with expansions excluded, then expansions. Every call goes through the shared pacer. A queued
/// answer is polled on a slow, bounded schedule, and a whole sync never sends more than <see cref="MaxRequestsPerSync"/>
/// requests. Whatever goes wrong ends the fetch with a failure category and never with a partial collection; the one
/// exception is the caller cancelling, which propagates so the caller can tell a stop from a slow answer. A collection
/// answer must declare its total, and the total must equal the entries read plus the entries left out for lacking an
/// identifier. The client logs a count at most: a request address carries the username and an answer carries the owner's data.
/// </summary>
public sealed class BggClient : ICollectionSource
{
    /// <summary>The most requests one sync may send, however many of them are answered as queued.</summary>
    public const int MaxRequestsPerSync = 16;

    /// <summary>How long to wait before each poll of a queued answer; the schedule's length is the number of polls allowed.</summary>
    public static readonly IReadOnlyList<TimeSpan> QueuedWaits =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(30),
    ];

    private readonly HttpClient _http;
    private readonly BggOptions _options;
    private readonly IRequestPacer _pacer;
    private readonly TimeProvider _time;
    private readonly IReadOnlyList<TimeSpan> _queuedWaits;
    private readonly ILogger<BggClient>? _logger;

    /// <summary>Creates the client.</summary>
    /// <param name="http">The client to send with; its base address is the API address and it never follows redirects.</param>
    /// <param name="options">The username, the contact address and the private-information switch.</param>
    /// <param name="pacer">Spaces the calls out.</param>
    /// <param name="time">The clock the waits between polls of a queued answer run on.</param>
    /// <param name="queuedWaits">The waits before each poll of a queued answer; <see cref="QueuedWaits"/> when omitted.</param>
    /// <param name="logger">Receives the number of entries left out of an answer, never a title or an identifier; optional.</param>
    public BggClient(
        HttpClient http,
        BggOptions options,
        IRequestPacer pacer,
        TimeProvider time,
        IReadOnlyList<TimeSpan>? queuedWaits = null,
        ILogger<BggClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(pacer);
        ArgumentNullException.ThrowIfNull(time);

        _http = http;
        _options = options;
        _pacer = pacer;
        _time = time;
        _queuedWaits = queuedWaits ?? QueuedWaits;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CollectionFetchResult> FetchOwnedAsync(CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            return new CollectionFetchResult.Failed(SyncFailure.NotConfigured);
        }

        var budget = new RequestBudget();
        var baseGames = await GetCollectionAsync("excludesubtype=boardgameexpansion", ItemKind.Base, budget, cancellationToken);

        if (baseGames is not CollectionFetchResult.Fetched baseFetched)
        {
            return baseGames;
        }

        var expansions = await GetCollectionAsync("subtype=boardgameexpansion", ItemKind.Expansion, budget, cancellationToken);

        return expansions is CollectionFetchResult.Fetched expansionFetched
            ? new CollectionFetchResult.Fetched(MergeByCollectionId(baseFetched.Items, expansionFetched.Items))
            : expansions;
    }

    /// <summary>
    /// Joins the two answers. An entry that appears in both is one item, and it is the expansion, because the unfiltered
    /// labelling is the one that is wrong. Two entries for the same game with different entry identifiers stay two items.
    /// </summary>
    private static IReadOnlyList<SnapshotItem> MergeByCollectionId(IReadOnlyList<SnapshotItem> baseGames, IReadOnlyList<SnapshotItem> expansions)
    {
        var expansionIds = expansions.Select(item => item.CollectionId).ToHashSet();

        return [.. baseGames.Where(item => !expansionIds.Contains(item.CollectionId)), .. expansions];
    }

    private static CollectionFetchResult Failed(SyncFailure failure) => new CollectionFetchResult.Failed(failure);

    private static bool IsXml(HttpResponseMessage response) =>
        response.Content.Headers.ContentType?.MediaType is "text/xml" or "application/xml";

    private async Task<CollectionFetchResult> GetCollectionAsync(
        string subtypeFilter,
        ItemKind kind,
        RequestBudget budget,
        CancellationToken cancellationToken)
    {
        var privateInfo = _options.IncludePrivateInfo ? "&showprivate=1" : string.Empty;
        var relative = $"collection?username={Uri.EscapeDataString(_options.Username!)}&own=1&{subtypeFilter}&version=1{privateInfo}";

        try
        {
            for (var poll = 0; ; poll++)
            {
                if (!budget.TryTake())
                {
                    return Failed(SyncFailure.Queued);
                }

                if (await RequestOnceAsync(relative, kind, cancellationToken) is { } answer)
                {
                    return answer;
                }

                if (poll >= _queuedWaits.Count || budget.IsSpent)
                {
                    return Failed(SyncFailure.Queued);
                }

                await Task.Delay(_queuedWaits[poll], _time, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is BggAnswerException or XmlException)
        {
            return Failed(SyncFailure.BadAnswer);
        }
        catch (HttpRequestException)
        {
            return Failed(SyncFailure.Unavailable);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed(SyncFailure.Timeout);
        }
    }

    /// <summary>
    /// Sends one request in its own turn and classifies the answer. It returns null when the answer is the queued one, so the
    /// caller can wait and ask again; otherwise it returns the collection or the failure the answer amounts to. The turn ends
    /// when this method returns, so a wait between polls is never held inside a turn.
    /// </summary>
    private async Task<CollectionFetchResult?> RequestOnceAsync(string relative, ItemKind kind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var lease = await _pacer.WaitTurnAsync(cancellationToken);
        using var request = CreateRequest(relative);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.Accepted:
                return null;
            case HttpStatusCode.OK:
                break;
            case HttpStatusCode.Unauthorized:
                return Failed(SyncFailure.Unauthorized);
            case HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable:
                return Failed(SyncFailure.Throttled);
            default:
                return Failed(SyncFailure.Unavailable);
        }

        if (!IsXml(response))
        {
            return Failed(SyncFailure.BadAnswer);
        }

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        var parsed = BggCollectionParser.Parse(body, kind, _options.IncludePrivateInfo);

        if (parsed.SkippedItems > 0)
        {
            _logger?.LogWarning("A BGG answer held {SkippedCount} owned entries without a usable identifier; they were left out.", parsed.SkippedItems);
        }

        return parsed.TotalItems is not { } total || total != parsed.Items.Count + parsed.SkippedItems
            ? Failed(SyncFailure.BadAnswer)
            : new CollectionFetchResult.Fetched(parsed.Items);
    }

    private HttpRequestMessage CreateRequest(string relative)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relative);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        request.Headers.TryAddWithoutValidation("User-Agent", BggTransport.UserAgent(_options));

        return request;
    }

    /// <summary>Counts the requests one sync has sent, shared by both of its calls.</summary>
    private sealed class RequestBudget
    {
        private int _used;

        public bool IsSpent => _used >= MaxRequestsPerSync;

        public bool TryTake()
        {
            if (IsSpent)
            {
                return false;
            }

            _used++;

            return true;
        }
    }
}

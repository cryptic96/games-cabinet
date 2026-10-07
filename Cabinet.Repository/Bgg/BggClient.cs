using System.Net;
using System.Net.Http.Headers;
using System.Xml;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Microsoft.Extensions.Logging;

namespace Cabinet.Repository.Bgg;

/// <summary>
/// Reads the owner's owned collection from the BGG XML API in two calls, because the unfiltered call labels expansions as
/// base games with expansions excluded, then expansions. Every call goes through the shared pacer. A queued answer is
/// polled on a slow, bounded schedule, a transient server error or throttle is retried once per call after a polite wait
/// (at least <see cref="RetryFloor"/> for a throttle that names no wait of its own), and a whole sync never sends more than
/// <see cref="MaxRequestsPerSync"/> requests, retries included. A refusal (401 or 403) is never retried. Whatever goes wrong ends the fetch with a failure category and never with a partial collection; the one
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

    /// <summary>The longest wait a transient answer may ask for and still be retried; a longer ask means the next scheduled sync is the retry.</summary>
    public static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The shortest wait before the retry of a throttle (429 or 503) that carries no usable <c>Retry-After</c>. BGG's own
    /// guidance is a gap of seconds between requests, but a throttle that names no wait is better answered with a longer
    /// pause than with the pacer's gap alone. Other server errors keep the pacer's gap.
    /// </summary>
    public static readonly TimeSpan RetryFloor = TimeSpan.FromSeconds(30);

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
            var retried = false;

            for (var poll = 0; ; )
            {
                if (!budget.TryTake())
                {
                    return Failed(SyncFailure.Queued);
                }

                var attempt = await RequestOnceAsync(relative, kind, cancellationToken);

                if (attempt.Kind == AttemptKind.Finished)
                {
                    return attempt.Result!;
                }

                if (attempt.Kind == AttemptKind.Transient)
                {
                    if (retried || budget.IsSpent || RetryWaitFor(attempt) is not { } retryWait)
                    {
                        return attempt.Result!;
                    }

                    retried = true;
                    await DelayAsync(retryWait, cancellationToken);

                    continue;
                }

                if (poll >= _queuedWaits.Count || budget.IsSpent)
                {
                    return Failed(SyncFailure.Queued);
                }

                await Task.Delay(_queuedWaits[poll++], _time, cancellationToken);
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
    /// Sends one request in its own turn and classifies the answer: queued, so the caller can wait and ask again; transient,
    /// so the caller may retry once; or finished with the collection or the failure the answer amounts to. The turn ends
    /// when this method returns, so a wait between requests is never held inside a turn.
    /// </summary>
    private async Task<Attempt> RequestOnceAsync(string relative, ItemKind kind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var lease = await _pacer.WaitTurnAsync(cancellationToken);
        using var request = CreateRequest(relative);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.Accepted:
                return new Attempt(AttemptKind.Queued, null, null);
            case HttpStatusCode.OK:
                break;
            case HttpStatusCode.Unauthorized:
                return Finished(Failed(SyncFailure.Unauthorized));
            case HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable:
                return Transient(SyncFailure.Throttled, response);
            case >= HttpStatusCode.InternalServerError:
                return Transient(SyncFailure.Unavailable, response);
            default:
                return Finished(Failed(SyncFailure.Unavailable));
        }

        if (!IsXml(response))
        {
            return Finished(Failed(SyncFailure.BadAnswer));
        }

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        var parsed = BggCollectionParser.Parse(body, kind, _options.IncludePrivateInfo);

        if (parsed.SkippedItems > 0)
        {
            _logger?.LogWarning("A BGG answer held {SkippedCount} owned entries without a usable identifier; they were left out.", parsed.SkippedItems);
        }

        return Finished(parsed.TotalItems is not { } total || total != parsed.Items.Count + parsed.SkippedItems
            ? Failed(SyncFailure.BadAnswer)
            : new CollectionFetchResult.Fetched(parsed.Items));
    }

    private static Attempt Finished(CollectionFetchResult result) => new(AttemptKind.Finished, result, null);

    private Attempt Transient(SyncFailure failure, HttpResponseMessage response) =>
        new(AttemptKind.Transient, Failed(failure), ReadRetryAfter(response));

    private TimeSpan? ReadRetryAfter(HttpResponseMessage response) =>
        response.Headers.RetryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date - _time.GetUtcNow(),
            _ => null,
        };

    /// <summary>
    /// The wait before the one retry, on top of the pacer's own gap, or null when the answer asked for more than the cap and
    /// the next scheduled sync is the retry. A throttle that names no wait waits <see cref="RetryFloor"/>; a throttle that
    /// names one waits that long, whatever it is up to the cap; any other transient answer adds nothing, because the pacer
    /// already leaves at least its gap after the failed request.
    /// </summary>
    private static TimeSpan? RetryWaitFor(Attempt attempt)
    {
        if (attempt.RetryAfter is not { } asked)
        {
            return attempt.Result is CollectionFetchResult.Failed { Failure: SyncFailure.Throttled } ? RetryFloor : TimeSpan.Zero;
        }

        if (asked > MaxRetryAfter)
        {
            return null;
        }

        return asked > TimeSpan.Zero ? asked : TimeSpan.Zero;
    }

    private async Task DelayAsync(TimeSpan wait, CancellationToken cancellationToken)
    {
        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, _time, cancellationToken);
        }
    }

    private HttpRequestMessage CreateRequest(string relative)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relative);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        request.Headers.TryAddWithoutValidation("User-Agent", BggTransport.UserAgent(_options));

        return request;
    }

    private enum AttemptKind
    {
        Queued,
        Transient,
        Finished,
    }

    /// <summary>How one request was answered: the kind, the result when there is one, and the wait a transient answer asked for.</summary>
    private readonly record struct Attempt(AttemptKind Kind, CollectionFetchResult? Result, TimeSpan? RetryAfter);

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

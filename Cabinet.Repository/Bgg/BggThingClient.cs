using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Xml;
using Cabinet.Domain.Collection;

namespace Cabinet.Repository.Bgg;

/// <summary>
/// Reads the details of owned games from the BGG XML API, up to <see cref="EnrichmentPlanner.MaxIdsPerRequest"/> games per
/// call. Every call goes through the shared pacer, carries nothing but the game identifiers and the statistics switch, and
/// is retried at most once after a throttle or a server error, with the same waits the collection client uses. A refusal is
/// never retried. Whatever goes wrong ends the call with a failure category. The client never logs: a request address names
/// games and an answer carries BGG's data.
/// </summary>
public sealed class BggThingClient : IEnrichmentSource
{
    private readonly HttpClient _http;
    private readonly BggOptions _options;
    private readonly IRequestPacer _pacer;
    private readonly TimeProvider _time;

    /// <summary>Creates the client.</summary>
    /// <param name="http">The client to send with; its base address is the API address and it never follows redirects.</param>
    /// <param name="options">The credentials and contact address; the call needs them to be configured.</param>
    /// <param name="pacer">Spaces the calls out; the same pacer the collection client uses.</param>
    /// <param name="time">The clock the wait before a retry runs on.</param>
    public BggThingClient(HttpClient http, BggOptions options, IRequestPacer pacer, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(pacer);
        ArgumentNullException.ThrowIfNull(time);

        _http = http;
        _options = options;
        _pacer = pacer;
        _time = time;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentOutOfRangeException">No identifier is given, or more than <see cref="EnrichmentPlanner.MaxIdsPerRequest"/>.</exception>
    public async Task<EnrichmentFetchResult> FetchDetailsAsync(IReadOnlyList<int> gameIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gameIds);
        ArgumentOutOfRangeException.ThrowIfZero(gameIds.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(gameIds.Count, EnrichmentPlanner.MaxIdsPerRequest);

        if (!_options.IsConfigured)
        {
            return new EnrichmentFetchResult.Failed(SyncFailure.NotConfigured);
        }

        var relative = $"thing?id={string.Join(',', gameIds.Select(id => id.ToString(CultureInfo.InvariantCulture)))}&stats=1";

        try
        {
            var first = await RequestOnceAsync(relative, gameIds, cancellationToken);

            if (first.Result is not EnrichmentFetchResult.Failed || !first.Transient || first.RetryWait is not { } wait)
            {
                return first.Result;
            }

            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, _time, cancellationToken);
            }

            return (await RequestOnceAsync(relative, gameIds, cancellationToken)).Result;
        }
        catch (Exception exception) when (exception is BggAnswerException or XmlException)
        {
            return new EnrichmentFetchResult.Failed(SyncFailure.BadAnswer);
        }
        catch (HttpRequestException)
        {
            return new EnrichmentFetchResult.Failed(SyncFailure.Unavailable);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new EnrichmentFetchResult.Failed(SyncFailure.Timeout);
        }
    }

    private async Task<Attempt> RequestOnceAsync(string relative, IReadOnlyList<int> gameIds, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var lease = await _pacer.WaitTurnAsync(cancellationToken);
        using var request = CreateRequest(relative);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:
                break;
            case HttpStatusCode.Accepted:
                return Done(SyncFailure.Queued);
            case HttpStatusCode.Unauthorized:
                return Done(SyncFailure.Unauthorized);
            case HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable:
                return Transient(SyncFailure.Throttled, response);
            case >= HttpStatusCode.InternalServerError:
                return Transient(SyncFailure.Unavailable, response);
            default:
                return Done(SyncFailure.Unavailable);
        }

        if (response.Content.Headers.ContentType?.MediaType is not ("text/xml" or "application/xml"))
        {
            return Done(SyncFailure.BadAnswer);
        }

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        var parsed = BggThingParser.Parse(body, _time.GetUtcNow());
        var requested = gameIds.ToHashSet();
        var games = parsed.Games
            .Where(pair => requested.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        return new Attempt(new EnrichmentFetchResult.Fetched(games), false, null);
    }

    private static Attempt Done(SyncFailure failure) => new(new EnrichmentFetchResult.Failed(failure), false, null);

    private Attempt Transient(SyncFailure failure, HttpResponseMessage response) =>
        new(new EnrichmentFetchResult.Failed(failure), true, RetryWaitFor(failure, ReadRetryAfter(response)));

    private TimeSpan? ReadRetryAfter(HttpResponseMessage response) =>
        response.Headers.RetryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date - _time.GetUtcNow(),
            _ => null,
        };

    private static TimeSpan? RetryWaitFor(SyncFailure failure, TimeSpan? asked)
    {
        if (asked is not { } wait)
        {
            return failure == SyncFailure.Throttled ? BggClient.RetryFloor : TimeSpan.Zero;
        }

        if (wait > BggClient.MaxRetryAfter)
        {
            return null;
        }

        return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
    }

    private HttpRequestMessage CreateRequest(string relative)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relative);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        request.Headers.TryAddWithoutValidation("User-Agent", BggTransport.UserAgent(_options));

        return request;
    }

    /// <summary>How one request was answered: the result, whether it was transient, and the wait a retry takes or null for none.</summary>
    private readonly record struct Attempt(EnrichmentFetchResult Result, bool Transient, TimeSpan? RetryWait);
}

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Cabinet.FakeBgg.Testing;

/// <summary>One scripted answer: what a test wants the next request to receive.</summary>
/// <param name="Status">The HTTP status code.</param>
/// <param name="ContentType">The content type header value, or empty for none.</param>
/// <param name="Body">The response body.</param>
/// <param name="Delay">How long to hold the answer back; no delay when zero.</param>
/// <param name="Headers">Extra response headers.</param>
public sealed record ScriptedResponse(
    HttpStatusCode Status,
    string ContentType,
    string Body,
    TimeSpan Delay = default,
    IReadOnlyDictionary<string, string>? Headers = null)
{
    /// <summary>An XML answer.</summary>
    /// <param name="body">The XML text.</param>
    /// <param name="status">The HTTP status code.</param>
    public static ScriptedResponse Xml(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status, "text/xml; charset=utf-8", body);

    /// <summary>An answer with a status and no body.</summary>
    /// <param name="status">The HTTP status code.</param>
    public static ScriptedResponse Empty(HttpStatusCode status) => new(status, "text/html; charset=utf-8", string.Empty);
}

/// <summary>What one request carried, kept so a test can prove where credentials went.</summary>
/// <param name="Uri">The full request address.</param>
/// <param name="AuthorizationScheme">The scheme of the Authorization header, or null when there was none.</param>
/// <param name="AuthorizationParameter">The credentials of the Authorization header, or null when there was none.</param>
/// <param name="UserAgent">The User-Agent header text, or null when there was none.</param>
/// <param name="At">When the request arrived, by the handler's time provider.</param>
public sealed record RecordedRequest(
    Uri Uri,
    string? AuthorizationScheme,
    string? AuthorizationParameter,
    string? UserAgent,
    DateTimeOffset At);

/// <summary>
/// A transport for tests that answers from a script instead of the network and remembers every request it was given.
/// </summary>
public sealed class ScriptedBggHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, ScriptedResponse> _script;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentQueue<ScriptedResponse> _queue = new();
    private readonly List<RecordedRequest> _requests = [];
    private readonly object _gate = new();

    /// <summary>Creates a handler that answers with the responses added through <see cref="Enqueue"/>, in order.</summary>
    /// <param name="timeProvider">Used for delays and request times; the system clock when omitted.</param>
    public ScriptedBggHandler(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _script = _ => DequeueOrFail();
    }

    /// <summary>Creates a handler that asks a function for each answer.</summary>
    /// <param name="script">Produces the answer for a request.</param>
    /// <param name="timeProvider">Used for delays and request times; the system clock when omitted.</param>
    public ScriptedBggHandler(Func<HttpRequestMessage, ScriptedResponse> script, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(script);

        _script = script;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Every request received so far, oldest first.</summary>
    public IReadOnlyList<RecordedRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>
    /// Creates a handler that answers collection and single-game requests the way the fake's normal scenario does,
    /// from the given invented entries.
    /// </summary>
    /// <param name="items">The invented collection.</param>
    /// <param name="timeProvider">Used for delays and request times; the system clock when omitted.</param>
    public static ScriptedBggHandler ForCollection(IReadOnlyList<FakeBggItem> items, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        return new ScriptedBggHandler(request => AnswerLikeTheFake(request, items), timeProvider);
    }

    /// <summary>Adds answers to the end of the queue.</summary>
    /// <param name="responses">The answers, in the order they should be given.</param>
    public ScriptedBggHandler Enqueue(params ScriptedResponse[] responses)
    {
        foreach (var response in responses)
        {
            _queue.Enqueue(response);
        }

        return this;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Record(request);
        var scripted = _script(request);
        if (scripted.Delay > TimeSpan.Zero)
        {
            await Task.Delay(scripted.Delay, _timeProvider, cancellationToken);
        }

        return ToMessage(scripted, request);
    }

    private static ScriptedResponse AnswerLikeTheFake(HttpRequestMessage request, IReadOnlyList<FakeBggItem> items)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("The request has no address.");
        if (uri.AbsolutePath.EndsWith("/collection", StringComparison.Ordinal))
        {
            return ScriptedResponse.Xml(BggXml.Collection(items, CollectionQuery.Parse(uri.Query)));
        }

        if (uri.AbsolutePath.EndsWith("/thing", StringComparison.Ordinal))
        {
            var query = QueryHelpers.ParseQuery(uri.Query);
            var ids = query["id"].ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(id => int.Parse(id, CultureInfo.InvariantCulture));
            return ScriptedResponse.Xml(BggXml.Things(ids, items, query["stats"].ToString() == "1"));
        }

        return ScriptedResponse.Empty(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage ToMessage(ScriptedResponse scripted, HttpRequestMessage request)
    {
        var message = new HttpResponseMessage(scripted.Status)
        {
            Content = new StringContent(scripted.Body, Encoding.UTF8),
            RequestMessage = request,
        };

        message.Content.Headers.ContentType = scripted.ContentType.Length == 0
            ? null
            : MediaTypeHeaderValue.Parse(scripted.ContentType);

        foreach (var (name, value) in scripted.Headers ?? new Dictionary<string, string>())
        {
            if (!message.Headers.TryAddWithoutValidation(name, value))
            {
                message.Content.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return message;
    }

    private ScriptedResponse DequeueOrFail() =>
        _queue.TryDequeue(out var response)
            ? response
            : throw new InvalidOperationException("No scripted BGG answer is left for this request; add one with Enqueue.");

    private void Record(HttpRequestMessage request)
    {
        var userAgent = request.Headers.UserAgent.ToString();
        var recorded = new RecordedRequest(
            request.RequestUri ?? throw new InvalidOperationException("The request has no address."),
            request.Headers.Authorization?.Scheme,
            request.Headers.Authorization?.Parameter,
            userAgent.Length == 0 ? null : userAgent,
            _timeProvider.GetUtcNow());
        lock (_gate)
        {
            _requests.Add(recorded);
        }
    }
}

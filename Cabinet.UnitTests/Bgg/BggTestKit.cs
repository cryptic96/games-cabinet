using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.Repository.Bgg;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Bgg;

/// <summary>A pacer for tests that do not care about spacing: every turn is immediate.</summary>
public sealed class ImmediatePacer : IRequestPacer
{
    /// <inheritdoc />
    public Task<IDisposable> WaitTurnAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IDisposable>(new Turn());

    private sealed class Turn : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

/// <summary>Builds the clients, options and invented answers the BGG client tests share.</summary>
public static class BggTestKit
{
    /// <summary>The invented username every test client is configured with.</summary>
    public const string Username = "sentinel-user-name";

    /// <summary>The invented token every test client is configured with.</summary>
    public const string Token = "sentinel-token-value";

    /// <summary>The settings of a configured client that talks to the real host name, so the pinned request rules apply.</summary>
    public static BggOptions Options() =>
        new(BggOptions.DefaultBaseUri, Username, Token, null, BggOptions.MinimumRequestGap, false, "0.0.0-test");

    /// <summary>Creates a client over a handler.</summary>
    /// <param name="handler">Answers the requests.</param>
    /// <param name="time">The clock the waits between polls run on; the system clock when omitted.</param>
    /// <param name="pacer">Spaces the requests; an immediate pacer when omitted.</param>
    /// <param name="timeout">The HTTP client's own timeout; the default when omitted.</param>
    /// <param name="queuedWaits">The waits between polls of a queued answer; the committed schedule when omitted.</param>
    public static BggClient Client(
        HttpMessageHandler handler,
        TimeProvider? time = null,
        IRequestPacer? pacer = null,
        TimeSpan? timeout = null,
        IReadOnlyList<TimeSpan>? queuedWaits = null)
    {
        var http = new HttpClient(handler) { BaseAddress = BggOptions.DefaultBaseUri };

        if (timeout is { } limit)
        {
            http.Timeout = limit;
        }

        return new BggClient(http, Options(), pacer ?? new ImmediatePacer(), time ?? TimeProvider.System, queuedWaits);
    }

    /// <summary>
    /// Runs a whole fetch on a fake clock that is moved forward a second at a time, so a retry's wait costs a few
    /// milliseconds of real time. Fails if the fetch has not finished within ten seconds of real time.
    /// </summary>
    /// <param name="handler">Answers the requests.</param>
    public static async Task<CollectionFetchResult> FetchOnFakeClockAsync(HttpMessageHandler handler)
    {
        var clock = new FakeTimeProvider();
        var fetch = Client(handler, clock).FetchOwnedAsync(TestContext.Current.CancellationToken);
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (!fetch.IsCompleted)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The fetch did not finish within ten seconds of real time.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(2), TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        return await fetch;
    }

    /// <summary>Whether a request is for the call that excludes expansions, which is the first call of a sync.</summary>
    /// <param name="request">The request.</param>
    public static bool IsBaseCall(HttpRequestMessage request) =>
        request.RequestUri!.Query.Contains("excludesubtype=boardgameexpansion", StringComparison.Ordinal);

    /// <summary>The answer a healthy BGG gives to a collection request, built from invented entries.</summary>
    /// <param name="items">The invented entries.</param>
    /// <param name="request">The request being answered.</param>
    public static ScriptedResponse Good(IReadOnlyList<FakeBggItem> items, HttpRequestMessage request) =>
        ScriptedResponse.Xml(BggXml.Collection(items, CollectionQuery.Parse(request.RequestUri!.Query)));
}

using System.Net;
using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.Repository.Bgg;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Bgg;

/// <summary>
/// Proves the details client asks only the way it should, stays within one call and one retry, spaces its calls through the
/// shared pacer and ends every kind of bad answer with the right failure category.
/// </summary>
[Trait("Category", "Enrichment")]
public sealed class BggThingClientTests
{
    private static readonly IReadOnlyList<FakeBggItem> Items = SyntheticBggCollection.Create(5);
    private static readonly int[] Ids = [100001, 100002, 100003];

    [Fact]
    public async Task The_request_asks_for_the_named_games_with_statistics_and_nothing_else()
    {
        var handler = ScriptedBggHandler.ForCollection(Items);

        var result = await Client(handler).FetchDetailsAsync(Ids, TestContext.Current.CancellationToken);

        result.Should().BeOfType<EnrichmentFetchResult.Fetched>().Which.Games.Keys.Should().BeEquivalentTo(Ids);
        var request = handler.Requests.Should().ContainSingle().Which;
        request.Uri.AbsolutePath.Should().Be("/xmlapi2/thing");
        request.Uri.Query.Should().Be("?id=100001,100002,100003&stats=1");
        request.UserAgent.Should().StartWith("GamesCabinet/");
        request.AuthorizationParameter.Should().BeNull("the transport under test has no authorisation step of its own");
    }

    [Fact]
    public async Task A_game_the_answer_does_not_name_among_those_asked_for_is_not_returned()
    {
        var handler = new ScriptedBggHandler(_ => ScriptedResponse.Xml(BggXml.Things([100001, 100002, 999999], Items, stats: true)));

        var result = await Client(handler).FetchDetailsAsync([100001, 100002], TestContext.Current.CancellationToken);

        result.Should().BeOfType<EnrichmentFetchResult.Fetched>().Which.Games.Keys.Should().BeEquivalentTo([100001, 100002]);
    }

    [Fact]
    public async Task Twenty_games_make_one_call_and_twenty_one_or_none_are_refused_before_any_request()
    {
        var handler = ScriptedBggHandler.ForCollection(Items);
        var client = Client(handler);

        (await client.FetchDetailsAsync([.. Enumerable.Range(1, 20)], TestContext.Current.CancellationToken)).Should().BeOfType<EnrichmentFetchResult.Fetched>();
        var tooMany = () => client.FetchDetailsAsync([.. Enumerable.Range(1, 21)], TestContext.Current.CancellationToken);
        var none = () => client.FetchDetailsAsync([], TestContext.Current.CancellationToken);

        await tooMany.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await none.Should().ThrowAsync<ArgumentOutOfRangeException>();
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task A_client_that_is_not_configured_sends_nothing()
    {
        var handler = ScriptedBggHandler.ForCollection(Items);
        var http = new HttpClient(handler) { BaseAddress = BggOptions.DefaultBaseUri };
        var client = new BggThingClient(http, BggTestKit.Options() with { Token = null }, new ImmediatePacer(), TimeProvider.System);

        var result = await client.FetchDetailsAsync(Ids, TestContext.Current.CancellationToken);

        result.Should().BeOfType<EnrichmentFetchResult.Failed>().Which.Failure.Should().Be(SyncFailure.NotConfigured);
        handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task A_transient_answer_is_retried_once_and_the_call_succeeds_when_the_retry_does(HttpStatusCode transient)
    {
        var clock = new FakeTimeProvider();
        var calls = 0;
        var handler = new ScriptedBggHandler(
            request => calls++ == 0 ? ScriptedResponse.Empty(transient) : Healthy(request),
            clock);

        var result = await Drive(Client(handler, clock).FetchDetailsAsync(Ids, TestContext.Current.CancellationToken), clock);

        result.Should().BeOfType<EnrichmentFetchResult.Fetched>();
        handler.Requests.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, SyncFailure.Throttled)]
    [InlineData(HttpStatusCode.TooManyRequests, SyncFailure.Throttled)]
    [InlineData(HttpStatusCode.BadGateway, SyncFailure.Unavailable)]
    public async Task A_transient_answer_that_repeats_is_not_retried_a_second_time(HttpStatusCode transient, SyncFailure expected)
    {
        var clock = new FakeTimeProvider();
        var handler = new ScriptedBggHandler(_ => ScriptedResponse.Empty(transient), clock);

        var result = await Drive(Client(handler, clock).FetchDetailsAsync(Ids, TestContext.Current.CancellationToken), clock);

        result.Should().BeOfType<EnrichmentFetchResult.Failed>().Which.Failure.Should().Be(expected);
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_throttle_with_no_wait_of_its_own_waits_the_retry_floor_before_the_retry()
    {
        var clock = new FakeTimeProvider();
        var calls = 0;
        var handler = new ScriptedBggHandler(
            request => calls++ == 0 ? ScriptedResponse.Empty(HttpStatusCode.TooManyRequests) : Healthy(request),
            clock);

        await Drive(Client(handler, clock).FetchDetailsAsync(Ids, TestContext.Current.CancellationToken), clock);

        (handler.Requests[1].At - handler.Requests[0].At).Should().BeGreaterThanOrEqualTo(BggClient.RetryFloor);
    }

    [Fact]
    public async Task A_throttle_that_asks_for_longer_than_the_cap_is_not_retried()
    {
        var clock = new FakeTimeProvider();
        var handler = new ScriptedBggHandler(
            _ => ScriptedResponse.Empty(HttpStatusCode.TooManyRequests) with { Headers = new Dictionary<string, string> { ["Retry-After"] = "600" } },
            clock);

        var result = await Drive(Client(handler, clock).FetchDetailsAsync(Ids, TestContext.Current.CancellationToken), clock);

        result.Should().BeOfType<EnrichmentFetchResult.Failed>().Which.Failure.Should().Be(SyncFailure.Throttled);
        handler.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, SyncFailure.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, SyncFailure.Unavailable)]
    [InlineData(HttpStatusCode.NotFound, SyncFailure.Unavailable)]
    [InlineData(HttpStatusCode.Found, SyncFailure.Unavailable)]
    [InlineData(HttpStatusCode.Accepted, SyncFailure.Queued)]
    public async Task A_refusal_or_a_client_error_is_never_retried(HttpStatusCode refusal, SyncFailure expected)
    {
        var handler = new ScriptedBggHandler(_ => ScriptedResponse.Empty(refusal));

        var result = await Client(handler).FetchDetailsAsync(Ids, TestContext.Current.CancellationToken);

        result.Should().BeOfType<EnrichmentFetchResult.Failed>().Which.Failure.Should().Be(expected);
        handler.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData("html-with-success-status")]
    [InlineData("errors-document")]
    [InlineData("wrong-root")]
    [InlineData("malformed-xml")]
    [InlineData("document-type-with-entity")]
    public async Task A_bad_body_with_a_success_status_is_a_bad_answer(string answer)
    {
        var handler = new ScriptedBggHandler(_ => answer switch
        {
            "html-with-success-status" => new ScriptedResponse(HttpStatusCode.OK, "text/html", BggXml.CloudflarePage()),
            "errors-document" => ScriptedResponse.Xml(BggXml.Errors("Rate limit exceeded")),
            "wrong-root" => ScriptedResponse.Xml("<?xml version=\"1.0\"?><message>hello</message>"),
            "malformed-xml" => ScriptedResponse.Xml(BggXml.Malformed()),
            _ => ScriptedResponse.Xml("<?xml version=\"1.0\"?><!DOCTYPE items [<!ENTITY owned \"1\">]><items></items>"),
        });

        var result = await Client(handler).FetchDetailsAsync(Ids, TestContext.Current.CancellationToken);

        result.Should().BeOfType<EnrichmentFetchResult.Failed>().Which.Failure.Should().Be(SyncFailure.BadAnswer);
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task A_call_that_cannot_connect_is_unavailable_and_one_that_times_out_is_a_timeout()
    {
        var unreachable = new ScriptedBggHandler(_ => throw new HttpRequestException("unreachable"));
        var slow = new ScriptedBggHandler(_ => throw new TaskCanceledException("timed out"));

        (await Client(unreachable).FetchDetailsAsync(Ids, TestContext.Current.CancellationToken))
            .Should().BeOfType<EnrichmentFetchResult.Failed>().Which.Failure.Should().Be(SyncFailure.Unavailable);
        (await Client(slow).FetchDetailsAsync(Ids, TestContext.Current.CancellationToken))
            .Should().BeOfType<EnrichmentFetchResult.Failed>().Which.Failure.Should().Be(SyncFailure.Timeout);
    }

    [Fact]
    public async Task Every_attempt_takes_its_own_turn_on_the_shared_pacer()
    {
        var clock = new FakeTimeProvider();
        var pacer = new CountingPacer();
        var calls = 0;
        var handler = new ScriptedBggHandler(
            request => calls++ == 0 ? ScriptedResponse.Empty(HttpStatusCode.BadGateway) : Healthy(request),
            clock);
        var http = new HttpClient(handler) { BaseAddress = BggOptions.DefaultBaseUri };
        var client = new BggThingClient(http, BggTestKit.Options(), pacer, clock);

        await Drive(client.FetchDetailsAsync(Ids, TestContext.Current.CancellationToken), clock);

        pacer.Turns.Should().Be(2);
    }

    private static ScriptedResponse Healthy(HttpRequestMessage request) =>
        ScriptedResponse.Xml(BggXml.Things(Ids, Items, request.RequestUri!.Query.Contains("stats=1", StringComparison.Ordinal)));

    private static BggThingClient Client(HttpMessageHandler handler, TimeProvider? time = null) =>
        new(new HttpClient(handler) { BaseAddress = BggOptions.DefaultBaseUri }, BggTestKit.Options(), new ImmediatePacer(), time ?? TimeProvider.System);

    private static async Task<EnrichmentFetchResult> Drive(Task<EnrichmentFetchResult> call, FakeTimeProvider clock)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (!call.IsCompleted)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The call did not finish within ten seconds of real time.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(2), TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        return await call;
    }

    private sealed class CountingPacer : IRequestPacer
    {
        private int _turns;

        public int Turns => _turns;

        public Task<IDisposable> WaitTurnAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _turns);

            return Task.FromResult<IDisposable>(new Turn());
        }

        private sealed class Turn : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}

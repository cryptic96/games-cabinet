using System.Globalization;
using System.Net;
using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.Repository.Bgg;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Bgg;

/// <summary>
/// Proves a transient server error or throttle is retried at most once per call, only through the shared pacer, within the
/// request budget and with the wait the answer asks for, and that a refusal is never retried.
/// </summary>
[Trait("Category", "Bgg")]
public class BggRetryTests
{
    private static readonly IReadOnlyList<FakeBggItem> Items = SyntheticBggCollection.Create(5);

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task A_transient_answer_is_retried_once_and_the_fetch_succeeds_when_the_retry_does(HttpStatusCode transient)
    {
        var clock = new FakeTimeProvider();
        var baseCalls = 0;
        var handler = new ScriptedBggHandler(
            request => BggTestKit.IsBaseCall(request) && baseCalls++ == 0 ? ScriptedResponse.Empty(transient) : BggTestKit.Good(Items, request),
            clock);

        var result = await Drive(Client(handler, clock).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        result.Should().BeOfType<CollectionFetchResult.Fetched>();
        handler.Requests.Should().HaveCount(3);
    }

    [Fact]
    public async Task Each_call_has_its_own_single_retry()
    {
        var clock = new FakeTimeProvider();
        var seen = new HashSet<string>();
        var handler = new ScriptedBggHandler(
            request => seen.Add(request.RequestUri!.Query) ? ScriptedResponse.Empty(HttpStatusCode.BadGateway) : BggTestKit.Good(Items, request),
            clock);

        var result = await Drive(Client(handler, clock).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        result.Should().BeOfType<CollectionFetchResult.Fetched>();
        handler.Requests.Should().HaveCount(4);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, SyncFailure.Throttled)]
    [InlineData(HttpStatusCode.ServiceUnavailable, SyncFailure.Throttled)]
    [InlineData(HttpStatusCode.BadGateway, SyncFailure.Unavailable)]
    public async Task A_transient_answer_that_repeats_is_not_retried_a_second_time(HttpStatusCode transient, SyncFailure expected)
    {
        var clock = new FakeTimeProvider();
        var handler = new ScriptedBggHandler(_ => ScriptedResponse.Empty(transient), clock);

        var result = await Drive(Client(handler, clock).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        result.Should().BeOfType<CollectionFetchResult.Failed>().Which.Failure.Should().Be(expected);
        handler.Requests.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Found)]
    public async Task A_refusal_or_a_client_error_is_never_retried(HttpStatusCode refusal)
    {
        var clock = new FakeTimeProvider();
        var handler = new ScriptedBggHandler(_ => ScriptedResponse.Empty(refusal), clock);

        await Drive(Client(handler, clock).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task The_retry_waits_its_turn_at_the_pacer_like_any_other_request()
    {
        var clock = new FakeTimeProvider();
        var pacer = new RequestPacer(BggOptions.MinimumRequestGap, clock);
        var baseCalls = 0;
        var handler = new ScriptedBggHandler(
            request => BggTestKit.IsBaseCall(request) && baseCalls++ == 0 ? ScriptedResponse.Empty(HttpStatusCode.BadGateway) : BggTestKit.Good(Items, request),
            clock);

        await Drive(Client(handler, clock, pacer).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        var starts = handler.Requests.Select(request => request.At).ToList();
        starts.Should().HaveCount(3);
        for (var index = 1; index < starts.Count; index++)
        {
            (starts[index] - starts[index - 1]).Should().BeGreaterThanOrEqualTo(BggOptions.MinimumRequestGap);
        }
    }

    [Fact]
    public async Task A_retry_after_in_seconds_is_waited_out_before_the_retry()
    {
        var clock = new FakeTimeProvider();
        var baseCalls = 0;
        var handler = new ScriptedBggHandler(
            request => BggTestKit.IsBaseCall(request) && baseCalls++ == 0 ? Throttled("20") : BggTestKit.Good(Items, request),
            clock);

        await Drive(Client(handler, clock).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        (handler.Requests[1].At - handler.Requests[0].At).Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public async Task A_retry_after_as_a_date_is_waited_out_before_the_retry()
    {
        var clock = new FakeTimeProvider();
        var baseCalls = 0;
        var handler = new ScriptedBggHandler(
            request => BggTestKit.IsBaseCall(request) && baseCalls++ == 0
                ? Throttled(clock.GetUtcNow().AddSeconds(30).ToString("R", CultureInfo.InvariantCulture))
                : BggTestKit.Good(Items, request),
            clock);

        await Drive(Client(handler, clock).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        (handler.Requests[1].At - handler.Requests[0].At).Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task A_retry_after_of_zero_still_leaves_the_pacers_gap_before_the_retry()
    {
        var clock = new FakeTimeProvider();
        var pacer = new RequestPacer(BggOptions.MinimumRequestGap, clock);
        var baseCalls = 0;
        var handler = new ScriptedBggHandler(
            request => BggTestKit.IsBaseCall(request) && baseCalls++ == 0 ? Throttled("0") : BggTestKit.Good(Items, request),
            clock);

        await Drive(Client(handler, clock, pacer).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        (handler.Requests[1].At - handler.Requests[0].At).Should().BeGreaterThanOrEqualTo(BggOptions.MinimumRequestGap);
    }

    [Theory]
    [InlineData("61")]
    [InlineData("3600")]
    public async Task A_retry_after_beyond_the_cap_means_no_retry_and_the_next_scheduled_sync_is_the_retry(string seconds)
    {
        var clock = new FakeTimeProvider();
        var handler = new ScriptedBggHandler(_ => Throttled(seconds), clock);

        var result = await Drive(Client(handler, clock).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        result.Should().BeOfType<CollectionFetchResult.Failed>().Which.Failure.Should().Be(SyncFailure.Throttled);
        handler.Requests.Should().ContainSingle();
        BggClient.MaxRetryAfter.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task A_retry_after_exactly_at_the_cap_is_honoured_with_a_retry()
    {
        var clock = new FakeTimeProvider();
        var baseCalls = 0;
        var handler = new ScriptedBggHandler(
            request => BggTestKit.IsBaseCall(request) && baseCalls++ == 0 ? Throttled("60") : BggTestKit.Good(Items, request),
            clock);

        var result = await Drive(Client(handler, clock).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        result.Should().BeOfType<CollectionFetchResult.Fetched>();
        (handler.Requests[1].At - handler.Requests[0].At).Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task A_retry_counts_against_the_request_budget_of_the_sync()
    {
        var clock = new FakeTimeProvider();
        var longSchedule = Enumerable.Repeat(TimeSpan.FromSeconds(5), 40).ToList();
        var calls = 0;
        var handler = new ScriptedBggHandler(_ => ++calls == BggClient.MaxRequestsPerSync - 1 ? ScriptedResponse.Empty(HttpStatusCode.BadGateway) : Queued(), clock);

        var result = await Drive(Client(handler, clock, queuedWaits: longSchedule).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        result.Should().BeOfType<CollectionFetchResult.Failed>().Which.Failure.Should().Be(SyncFailure.Queued);
        handler.Requests.Should().HaveCount(BggClient.MaxRequestsPerSync);
    }

    [Fact]
    public async Task A_transient_answer_on_the_last_request_of_the_budget_is_not_retried()
    {
        var clock = new FakeTimeProvider();
        var longSchedule = Enumerable.Repeat(TimeSpan.FromSeconds(5), 40).ToList();
        var calls = 0;
        var handler = new ScriptedBggHandler(
            _ => ++calls == BggClient.MaxRequestsPerSync ? ScriptedResponse.Empty(HttpStatusCode.ServiceUnavailable) : Queued(),
            clock);

        var result = await Drive(Client(handler, clock, queuedWaits: longSchedule).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        result.Should().BeOfType<CollectionFetchResult.Failed>().Which.Failure.Should().Be(SyncFailure.Throttled);
        handler.Requests.Should().HaveCount(BggClient.MaxRequestsPerSync);
    }

    [Fact]
    public async Task A_retry_after_a_queued_answer_is_still_allowed_once()
    {
        var clock = new FakeTimeProvider();
        var baseCalls = 0;
        var handler = new ScriptedBggHandler(
            request => !BggTestKit.IsBaseCall(request)
                ? BggTestKit.Good(Items, request)
                : baseCalls++ switch
                {
                    0 => Queued(),
                    1 => ScriptedResponse.Empty(HttpStatusCode.ServiceUnavailable),
                    _ => BggTestKit.Good(Items, request),
                },
            clock);

        var result = await Drive(Client(handler, clock).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        result.Should().BeOfType<CollectionFetchResult.Fetched>();
        handler.Requests.Should().HaveCount(4);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task A_throttle_without_a_retry_after_waits_the_floor_before_the_retry(HttpStatusCode throttle)
    {
        var clock = new FakeTimeProvider();
        var baseCalls = 0;
        var handler = new ScriptedBggHandler(
            request => BggTestKit.IsBaseCall(request) && baseCalls++ == 0 ? ScriptedResponse.Empty(throttle) : BggTestKit.Good(Items, request),
            clock);

        var result = await Drive(Client(handler, clock).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        result.Should().BeOfType<CollectionFetchResult.Fetched>();
        BggClient.RetryFloor.Should().Be(TimeSpan.FromSeconds(30));
        (handler.Requests[1].At - handler.Requests[0].At).Should().BeGreaterThanOrEqualTo(BggClient.RetryFloor);
    }

    [Fact]
    public async Task The_retry_of_a_throttle_does_not_start_before_the_floor_has_passed()
    {
        var clock = new FakeTimeProvider();
        var baseCalls = 0;
        var handler = new ScriptedBggHandler(
            request => BggTestKit.IsBaseCall(request) && baseCalls++ == 0 ? ScriptedResponse.Empty(HttpStatusCode.TooManyRequests) : BggTestKit.Good(Items, request),
            clock);
        var fetch = Client(handler, clock).FetchOwnedAsync(TestContext.Current.CancellationToken);
        await WaitForRequests(handler, 1);

        clock.Advance(BggClient.RetryFloor - TimeSpan.FromSeconds(1));
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        handler.Requests.Should().ContainSingle();
        await Drive(fetch, clock);
        handler.Requests.Should().HaveCount(3);
    }

    [Fact]
    public async Task A_retry_after_that_cannot_be_read_counts_as_none_and_the_floor_applies()
    {
        var clock = new FakeTimeProvider();
        var baseCalls = 0;
        var handler = new ScriptedBggHandler(
            request => BggTestKit.IsBaseCall(request) && baseCalls++ == 0 ? Throttled("soon") : BggTestKit.Good(Items, request),
            clock);

        await Drive(Client(handler, clock).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        (handler.Requests[1].At - handler.Requests[0].At).Should().BeGreaterThanOrEqualTo(BggClient.RetryFloor);
    }

    [Fact]
    public async Task A_retry_after_below_the_floor_is_honoured_as_asked_and_not_raised()
    {
        var clock = new FakeTimeProvider();
        var baseCalls = 0;
        var handler = new ScriptedBggHandler(
            request => BggTestKit.IsBaseCall(request) && baseCalls++ == 0 ? Throttled("5") : BggTestKit.Good(Items, request),
            clock);

        await Drive(Client(handler, clock).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        (handler.Requests[1].At - handler.Requests[0].At).Should().BeLessThan(BggClient.RetryFloor);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task Another_server_error_keeps_the_pacers_gap_and_does_not_wait_the_floor(HttpStatusCode serverError)
    {
        var clock = new FakeTimeProvider();
        var baseCalls = 0;
        var handler = new ScriptedBggHandler(
            request => BggTestKit.IsBaseCall(request) && baseCalls++ == 0 ? ScriptedResponse.Empty(serverError) : BggTestKit.Good(Items, request),
            clock);

        await Drive(Client(handler, clock).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        (handler.Requests[1].At - handler.Requests[0].At).Should().BeLessThan(BggClient.RetryFloor);
    }

    [Fact]
    public async Task A_throttle_that_repeats_after_the_floor_is_still_not_retried_a_second_time()
    {
        var clock = new FakeTimeProvider();
        var handler = new ScriptedBggHandler(_ => ScriptedResponse.Empty(HttpStatusCode.TooManyRequests), clock);

        var result = await Drive(Client(handler, clock).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        result.Should().BeOfType<CollectionFetchResult.Failed>().Which.Failure.Should().Be(SyncFailure.Throttled);
        handler.Requests.Should().HaveCount(2);
        (handler.Requests[1].At - handler.Requests[0].At).Should().BeGreaterThanOrEqualTo(BggClient.RetryFloor);
    }

    [Fact]
    public void The_longest_waits_the_client_can_ask_for_fit_inside_the_whole_run_limit_with_room_to_spare()
    {
        var polling = BggClient.QueuedWaits.Aggregate(TimeSpan.Zero, (sum, wait) => sum + wait) * 2;
        var retrying = (BggClient.RetryFloor > BggClient.MaxRetryAfter ? BggClient.RetryFloor : BggClient.MaxRetryAfter) * 2;
        var pacing = BggOptions.MinimumRequestGap * BggClient.MaxRequestsPerSync;

        (polling + retrying + pacing).Should().BeLessThan(Cabinet.Service.Sync.SyncWorker.RunLimit / 1.2);
    }

    private static async Task WaitForRequests(ScriptedBggHandler handler, int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (handler.Requests.Count < count)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The request was not sent within ten seconds of real time.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(2), TestContext.Current.CancellationToken);
        }
    }

    private static BggClient Client(
        HttpMessageHandler handler,
        FakeTimeProvider clock,
        IRequestPacer? pacer = null,
        IReadOnlyList<TimeSpan>? queuedWaits = null) =>
        BggTestKit.Client(handler, clock, pacer, queuedWaits: queuedWaits);

    private static ScriptedResponse Throttled(string retryAfter) =>
        ScriptedResponse.Empty(HttpStatusCode.TooManyRequests) with
        {
            Headers = new Dictionary<string, string> { ["Retry-After"] = retryAfter },
        };

    private static ScriptedResponse Queued() => ScriptedResponse.Xml(BggXml.Queued(), HttpStatusCode.Accepted);

    private static async Task<T> Drive<T>(Task<T> task, FakeTimeProvider clock)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (!task.IsCompleted)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The fetch did not finish within ten seconds of real time.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(2), TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        return await task;
    }
}

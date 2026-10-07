using System.Net;
using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.Repository.Bgg;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Bgg;

/// <summary>Proves a queued answer is polled slowly, through the shared pacer, and never more often than the budget allows.</summary>
[Trait("Category", "Bgg")]
public class QueuedAnswerTests
{
    private static readonly IReadOnlyList<FakeBggItem> Items = SyntheticBggCollection.Create(5);

    [Fact]
    public async Task Two_queued_answers_then_the_collection_succeed_with_three_requests_for_that_call()
    {
        var clock = new FakeTimeProvider();
        var baseCalls = 0;
        var handler = new ScriptedBggHandler(
            request => BggTestKit.IsBaseCall(request) && baseCalls++ < 2 ? Queued() : BggTestKit.Good(Items, request),
            clock);

        var result = await Drive(BggTestKit.Client(handler, clock).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        result.Should().BeOfType<CollectionFetchResult.Fetched>();
        handler.Requests.Count(request => request.Uri.Query.Contains("excludesubtype", StringComparison.Ordinal)).Should().Be(3);
        handler.Requests.Should().HaveCount(4);
    }

    [Fact]
    public async Task A_queued_answer_that_never_clears_ends_as_queued_after_one_request_and_one_poll_per_scheduled_wait()
    {
        var clock = new FakeTimeProvider();
        var handler = new ScriptedBggHandler(_ => Queued(), clock);

        var result = await Drive(BggTestKit.Client(handler, clock).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        result.Should().BeOfType<CollectionFetchResult.Failed>().Which.Failure.Should().Be(SyncFailure.Queued);
        handler.Requests.Should().HaveCount(1 + BggClient.QueuedWaits.Count);
    }

    [Fact]
    public void The_committed_schedule_is_five_ten_twenty_then_thirty_seconds_with_six_polls()
    {
        BggClient.QueuedWaits.Select(wait => (int)wait.TotalSeconds).Should().Equal(5, 10, 20, 30, 30, 30);
    }

    [Fact]
    public async Task With_the_real_pacer_every_request_starts_five_seconds_after_the_previous_and_each_poll_waits_its_turn()
    {
        var clock = new FakeTimeProvider();
        var pacer = new RequestPacer(BggOptions.MinimumRequestGap, clock);
        var baseCalls = 0;
        var handler = new ScriptedBggHandler(
            request => BggTestKit.IsBaseCall(request) && baseCalls++ < 4 ? Queued() : BggTestKit.Good(Items, request),
            clock);

        await Drive(BggTestKit.Client(handler, clock, pacer).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        var starts = handler.Requests.Select(request => request.At).ToList();
        starts.Should().HaveCount(6);
        for (var index = 1; index < starts.Count; index++)
        {
            (starts[index] - starts[index - 1]).Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(5));
        }

        for (var poll = 0; poll < 4; poll++)
        {
            (starts[poll + 1] - starts[poll]).Should().BeGreaterThanOrEqualTo(BggClient.QueuedWaits[poll]);
        }
    }

    [Fact]
    public async Task One_sync_never_sends_more_than_the_request_budget_and_ends_as_queued()
    {
        var clock = new FakeTimeProvider();
        var longSchedule = Enumerable.Repeat(TimeSpan.FromSeconds(5), 40).ToList();
        var handler = new ScriptedBggHandler(_ => Queued(), clock);

        var result = await Drive(
            BggTestKit.Client(handler, clock, queuedWaits: longSchedule).FetchOwnedAsync(TestContext.Current.CancellationToken),
            clock);

        result.Should().BeOfType<CollectionFetchResult.Failed>().Which.Failure.Should().Be(SyncFailure.Queued);
        handler.Requests.Should().HaveCount(BggClient.MaxRequestsPerSync);
    }

    [Fact]
    public async Task The_budget_is_shared_by_both_calls_of_one_sync()
    {
        var clock = new FakeTimeProvider();
        var longSchedule = Enumerable.Repeat(TimeSpan.FromSeconds(5), 40).ToList();
        var baseCalls = 0;
        var handler = new ScriptedBggHandler(
            request => BggTestKit.IsBaseCall(request) && baseCalls++ >= 8 ? BggTestKit.Good(Items, request) : Queued(),
            clock);

        var result = await Drive(
            BggTestKit.Client(handler, clock, queuedWaits: longSchedule).FetchOwnedAsync(TestContext.Current.CancellationToken),
            clock);

        result.Should().BeOfType<CollectionFetchResult.Failed>().Which.Failure.Should().Be(SyncFailure.Queued);
        handler.Requests.Should().HaveCount(BggClient.MaxRequestsPerSync);
    }

    [Fact]
    public async Task A_refusal_while_polling_stops_the_polling_at_once()
    {
        var clock = new FakeTimeProvider();
        var calls = 0;
        var handler = new ScriptedBggHandler(
            _ => calls++ == 0 ? Queued() : ScriptedResponse.Empty(HttpStatusCode.TooManyRequests),
            clock);

        var result = await Drive(BggTestKit.Client(handler, clock).FetchOwnedAsync(TestContext.Current.CancellationToken), clock);

        result.Should().BeOfType<CollectionFetchResult.Failed>().Which.Failure.Should().Be(SyncFailure.Throttled);
        handler.Requests.Should().HaveCount(2);
    }

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

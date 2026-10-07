using Cabinet.Domain.Collection;
using Cabinet.Service.Collection;
using Cabinet.Service.Sync;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Sync;

/// <summary>
/// Proves that a token BGG keeps refusing slows the timed syncs to about one a day, that a successful run or a restart ends
/// that, and that a visitor's request is never held back by it.
/// </summary>
[Trait("Category", "Sync")]
public class RejectedTokenBackoffTests
{
    private static readonly DateTimeOffset Start = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(250);
    private static readonly SyncRunResult Rejected = new(SyncResult.Failed, SyncFailure.Unauthorized);
    private static readonly SyncOptions Options = new(
        true,
        TimeSpan.FromMinutes(60),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromSeconds(120),
        TimeSpan.FromHours(3));

    [Fact]
    public void Timed_syncs_are_not_paused_before_the_third_refusal_in_a_row()
    {
        var clock = new FakeTimeProvider(Start);
        var coordinator = new SyncCoordinator(new InMemorySyncStateStore(), Options, clock);

        RunToTheEnd(coordinator, clock, Rejected);
        RunToTheEnd(coordinator, clock, Rejected);

        coordinator.TimedSyncsResumeAtUtc.Should().BeNull();
    }

    [Fact]
    public void The_third_refusal_in_a_row_pauses_timed_syncs_until_about_a_day_after_the_last_start()
    {
        var clock = new FakeTimeProvider(Start);
        var coordinator = new SyncCoordinator(new InMemorySyncStateStore(), Options, clock);

        RunToTheEnd(coordinator, clock, Rejected);
        RunToTheEnd(coordinator, clock, Rejected);
        RunToTheEnd(coordinator, clock, Rejected);

        coordinator.TimedSyncsResumeAtUtc.Should().Be(coordinator.State.LastStartedUtc + SyncCoordinator.BackoffSpacing - (Options.Interval / 2));
    }

    [Fact]
    public void A_run_that_fails_for_another_reason_ends_the_streak()
    {
        var clock = new FakeTimeProvider(Start);
        var coordinator = new SyncCoordinator(new InMemorySyncStateStore(), Options, clock);

        RunToTheEnd(coordinator, clock, Rejected);
        RunToTheEnd(coordinator, clock, Rejected);
        RunToTheEnd(coordinator, clock, new SyncRunResult(SyncResult.Failed, SyncFailure.Timeout));
        RunToTheEnd(coordinator, clock, Rejected);

        coordinator.TimedSyncsResumeAtUtc.Should().BeNull();
    }

    [Theory]
    [InlineData(SyncResult.Changed)]
    [InlineData(SyncResult.Unchanged)]
    public void A_successful_run_ends_the_pause(SyncResult success)
    {
        var clock = new FakeTimeProvider(Start);
        var coordinator = new SyncCoordinator(new InMemorySyncStateStore(), Options, clock);
        Reject(coordinator, clock, SyncCoordinator.RejectionsBeforeBackoff);
        coordinator.TimedSyncsResumeAtUtc.Should().NotBeNull();
        clock.Advance(Options.ManualCooldown);

        RunToTheEnd(coordinator, clock, new SyncRunResult(success, SyncFailure.None), SyncTrigger.Manual);

        coordinator.TimedSyncsResumeAtUtc.Should().BeNull();
    }

    [Fact]
    public void A_manual_press_is_never_held_back_by_the_pause_and_a_refused_press_moves_the_pause_on()
    {
        var clock = new FakeTimeProvider(Start);
        var coordinator = new SyncCoordinator(new InMemorySyncStateStore(), Options, clock);
        Reject(coordinator, clock, SyncCoordinator.RejectionsBeforeBackoff);
        var firstResume = coordinator.TimedSyncsResumeAtUtc;
        clock.Advance(TimeSpan.FromHours(2));

        coordinator.TryRequest(SyncTrigger.Manual).Should().BeOfType<SyncRequestResult.Started>();
        coordinator.Requests.TryRead(out _);
        coordinator.Complete(Rejected);

        coordinator.TimedSyncsResumeAtUtc.Should().BeAfter(firstResume!.Value);
    }

    [Fact]
    public void A_restart_clears_the_pause_although_the_stored_state_still_records_the_failures()
    {
        var clock = new FakeTimeProvider(Start);
        var store = new InMemorySyncStateStore();
        var coordinator = new SyncCoordinator(store, Options, clock);
        Reject(coordinator, clock, SyncCoordinator.RejectionsBeforeBackoff + 2);
        coordinator.TimedSyncsResumeAtUtc.Should().NotBeNull();

        var restarted = new SyncCoordinator(store, Options, clock);

        store.Stored.ConsecutiveFailures.Should().BeGreaterThanOrEqualTo(SyncCoordinator.RejectionsBeforeBackoff);
        restarted.TimedSyncsResumeAtUtc.Should().BeNull();
    }

    [Fact]
    public void An_interval_of_a_day_or_more_needs_no_pause()
    {
        var clock = new FakeTimeProvider(Start);
        var daily = Options with { Interval = TimeSpan.FromHours(24) };
        var coordinator = new SyncCoordinator(new InMemorySyncStateStore(), daily, clock);

        Reject(coordinator, clock, SyncCoordinator.RejectionsBeforeBackoff);

        coordinator.TimedSyncsResumeAtUtc.Should().BeNull();
    }

    [Fact]
    public void The_pause_is_logged_once_when_it_begins_and_without_any_detail()
    {
        var clock = new FakeTimeProvider(Start);
        var logger = new ListLogger();
        var coordinator = new SyncCoordinator(new InMemorySyncStateStore(), Options, clock, logger);

        Reject(coordinator, clock, SyncCoordinator.RejectionsBeforeBackoff + 2);

        logger.Lines.Should().ContainSingle().Which.Should().Contain("once a day");
    }

    [Fact]
    public async Task The_scheduler_skips_ticks_during_the_pause_and_asks_again_about_a_day_after_the_last_start()
    {
        var fake = new FakeTimeProvider(Start);
        var clock = new TimerCountingClock(fake);
        var state = SyncState.Initial with { LastSuccessUtc = Start };
        var coordinator = new SyncCoordinator(new InMemorySyncStateStore(state), Options, fake);
        Reject(coordinator, fake, SyncCoordinator.RejectionsBeforeBackoff);
        using var scheduler = new SyncScheduler(coordinator, new CollectionStore(), Options, clock, NullLogger<SyncScheduler>.Instance);
        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await clock.WaitForTimersAsync(1, TestContext.Current.CancellationToken);

        fake.Advance(TimeSpan.FromHours(23));
        await AssertNoRequestWithinAsync(coordinator, QuietPeriod, "the pause lasts about a day");
        fake.Advance(TimeSpan.FromHours(1));
        await WaitForRequestAsync(coordinator);

        await scheduler.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Without_refusals_the_scheduler_asks_at_the_first_tick()
    {
        var fake = new FakeTimeProvider(Start);
        var clock = new TimerCountingClock(fake);
        var state = SyncState.Initial with { LastSuccessUtc = Start };
        var coordinator = new SyncCoordinator(new InMemorySyncStateStore(state), Options, fake);
        using var scheduler = new SyncScheduler(coordinator, new CollectionStore(), Options, clock, NullLogger<SyncScheduler>.Instance);
        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await clock.WaitForTimersAsync(1, TestContext.Current.CancellationToken);

        fake.Advance(Options.Interval);
        await WaitForRequestAsync(coordinator);

        await scheduler.StopAsync(TestContext.Current.CancellationToken);
    }

    private static void Reject(SyncCoordinator coordinator, FakeTimeProvider clock, int times)
    {
        for (var run = 0; run < times; run++)
        {
            RunToTheEnd(coordinator, clock, Rejected);
        }
    }

    private static void RunToTheEnd(SyncCoordinator coordinator, FakeTimeProvider clock, SyncRunResult result, SyncTrigger trigger = SyncTrigger.Scheduled)
    {
        coordinator.TryRequest(trigger).Should().BeOfType<SyncRequestResult.Started>();
        coordinator.Requests.TryRead(out _).Should().BeTrue();
        coordinator.Complete(result);
        clock.Advance(TimeSpan.FromMinutes(1));
    }

    private static async Task AssertNoRequestWithinAsync(SyncCoordinator coordinator, TimeSpan period, string because)
    {
        var deadline = DateTime.UtcNow + period;

        while (DateTime.UtcNow < deadline)
        {
            coordinator.Requests.TryRead(out _).Should().BeFalse(because);
            await Task.Delay(TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);
        }
    }

    private static async Task WaitForRequestAsync(SyncCoordinator coordinator)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (!coordinator.Requests.TryRead(out _))
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("No sync was requested within ten seconds.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        }
    }

    private sealed class ListLogger : ILogger<SyncCoordinator>
    {
        private readonly List<string> _lines = [];

        public IReadOnlyList<string> Lines => _lines;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _lines.Add(formatter(state, exception));
    }
}

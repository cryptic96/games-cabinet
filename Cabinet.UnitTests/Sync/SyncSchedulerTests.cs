using Cabinet.Domain.Collection;
using Cabinet.Service.Collection;
using Cabinet.Service.Sync;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Sync;

/// <summary>Proves the scheduler asks for a sync after a cold start and then once per interval, and never in a burst.</summary>
[Trait("Category", "Sync")]
public class SyncSchedulerTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(250);
    private static readonly DateTimeOffset Start = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly SyncOptions Options = new(
        true,
        TimeSpan.FromMinutes(60),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromSeconds(120),
        TimeSpan.FromHours(3));

    [Fact]
    public async Task With_background_syncs_disabled_nothing_is_ever_requested()
    {
        var fake = new FakeTimeProvider(Start);
        var clock = new TimerCountingClock(fake);
        var disabled = Options with { BackgroundEnabled = false };
        var coordinator = Coordinator(fake, SyncState.Initial, disabled);
        using var scheduler = Scheduler(coordinator, clock, disabled);

        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await scheduler.ExecuteTask!.WaitAsync(Bound, TestContext.Current.CancellationToken);
        fake.Advance(TimeSpan.FromHours(5));
        await scheduler.StopAsync(TestContext.Current.CancellationToken);

        clock.TimerCount.Should().Be(0, "a disabled scheduler arms nothing");
        coordinator.Requests.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task Without_a_snapshot_one_startup_request_is_made_after_the_jitter()
    {
        var fake = new FakeTimeProvider(Start);
        var clock = new TimerCountingClock(fake);
        var coordinator = Coordinator(fake, SyncState.Initial, Options);
        using var scheduler = Scheduler(coordinator, clock, Options);

        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await clock.WaitForTimersAsync(1, TestContext.Current.CancellationToken);
        fake.Advance(TimeSpan.FromSeconds(9));
        coordinator.Requests.TryRead(out _).Should().BeFalse("the shortest wait is ten seconds");

        fake.Advance(TimeSpan.FromSeconds(111));
        await WaitForRequestAsync(coordinator);

        coordinator.State.LastStartedUtc.Should().Be(Start + TimeSpan.FromSeconds(120));
        await scheduler.StopAsync(TestContext.Current.CancellationToken);
        coordinator.Requests.TryRead(out _).Should().BeFalse("only one start-up request is made");
    }

    [Fact]
    public async Task The_interval_timer_is_armed_only_after_the_startup_delay_even_when_the_delay_outlasts_the_interval()
    {
        var fake = new FakeTimeProvider(Start);
        var clock = new TimerCountingClock(fake);
        var longJitter = Options with { Interval = SyncOptions.MinimumInterval, StartupJitterMax = TimeSpan.FromHours(1) };
        var coordinator = Coordinator(fake, SyncState.Initial, longJitter);
        using var scheduler = Scheduler(coordinator, clock, longJitter);

        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await clock.WaitForTimersAsync(1, TestContext.Current.CancellationToken);
        await Task.Delay(QuietPeriod, TestContext.Current.CancellationToken);

        clock.TimerCount.Should().Be(1, "only the start-up delay is pending, so no tick can be waiting beside it");

        fake.Advance(TimeSpan.FromHours(1));
        await WaitForRequestAsync(coordinator);
        await clock.WaitForTimersAsync(2, TestContext.Current.CancellationToken);
        await scheduler.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_last_success_younger_than_the_interval_means_no_startup_request()
    {
        var fake = new FakeTimeProvider(Start);
        var clock = new TimerCountingClock(fake);
        var state = SyncState.Initial with { LastSuccessUtc = Start - TimeSpan.FromMinutes(30) };
        var coordinator = Coordinator(fake, state, Options);
        using var scheduler = Scheduler(coordinator, clock, Options);

        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await clock.WaitForTimersAsync(1, TestContext.Current.CancellationToken);
        fake.Advance(TimeSpan.FromSeconds(125));
        await scheduler.StopAsync(TestContext.Current.CancellationToken);

        clock.TimerCount.Should().Be(1, "only the interval timer is armed, with no start-up delay");
        coordinator.Requests.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task A_last_success_older_than_the_interval_means_a_startup_request()
    {
        var fake = new FakeTimeProvider(Start);
        var clock = new TimerCountingClock(fake);
        var state = SyncState.Initial with { LastSuccessUtc = Start - TimeSpan.FromMinutes(90) };
        var coordinator = Coordinator(fake, state, Options);
        using var scheduler = Scheduler(coordinator, clock, Options);

        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await clock.WaitForTimersAsync(1, TestContext.Current.CancellationToken);
        fake.Advance(TimeSpan.FromSeconds(125));

        await WaitForRequestAsync(coordinator);
        await scheduler.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_start_less_than_fifteen_minutes_ago_means_no_startup_request_even_without_a_snapshot()
    {
        var fake = new FakeTimeProvider(Start);
        var clock = new TimerCountingClock(fake);
        var state = SyncState.Initial with { LastStartedUtc = Start - TimeSpan.FromMinutes(14) };
        var coordinator = Coordinator(fake, state, Options);
        using var scheduler = Scheduler(coordinator, clock, Options);

        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await clock.WaitForTimersAsync(1, TestContext.Current.CancellationToken);
        fake.Advance(TimeSpan.FromSeconds(125));
        await scheduler.StopAsync(TestContext.Current.CancellationToken);

        clock.TimerCount.Should().Be(1, "only the interval timer is armed, with no start-up delay");
        coordinator.Requests.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task One_scheduled_request_is_made_per_interval()
    {
        var fake = new FakeTimeProvider(Start);
        var clock = new TimerCountingClock(fake);
        var state = SyncState.Initial with { LastSuccessUtc = Start };
        var coordinator = Coordinator(fake, state, Options);
        using var scheduler = Scheduler(coordinator, clock, Options);
        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await clock.WaitForTimersAsync(1, TestContext.Current.CancellationToken);

        for (var interval = 1; interval <= 3; interval++)
        {
            fake.Advance(Options.Interval);
            await WaitForRequestAsync(coordinator);
            coordinator.State.LastStartedUtc.Should().Be(Start + (interval * Options.Interval));
            coordinator.Complete(new SyncRunResult(SyncResult.Unchanged, SyncFailure.None));
        }

        await scheduler.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_tick_while_a_run_is_in_progress_queues_no_second_request()
    {
        var fake = new FakeTimeProvider(Start);
        var clock = new TimerCountingClock(fake);
        var state = SyncState.Initial with { LastSuccessUtc = Start };
        var coordinator = Coordinator(fake, state, Options);
        using var scheduler = Scheduler(coordinator, clock, Options);
        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await clock.WaitForTimersAsync(1, TestContext.Current.CancellationToken);

        fake.Advance(Options.Interval);
        await WaitForRequestAsync(coordinator);
        fake.Advance(Options.Interval);

        await AssertNoRequestWithinAsync(coordinator, QuietPeriod, "a tick while a run is in progress queues nothing");
        coordinator.IsRunning.Should().BeTrue();
        await scheduler.StopAsync(TestContext.Current.CancellationToken);
    }

    private static SyncCoordinator Coordinator(FakeTimeProvider clock, SyncState state, SyncOptions options) =>
        new(new InMemorySyncStateStore(state), options, clock);

    private static SyncScheduler Scheduler(SyncCoordinator coordinator, TimeProvider clock, SyncOptions options) =>
        new(coordinator, new CollectionStore(), options, clock, NullLogger<SyncScheduler>.Instance);

    /// <summary>
    /// Watches the queue for a bounded real-time period and fails the moment a request appears. The scheduler offers no signal
    /// for having handled a tick it ignores, so the wait cannot end early when nothing happens; it ends early when something does.
    /// </summary>
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
}

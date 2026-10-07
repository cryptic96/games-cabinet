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
        var clock = new FakeTimeProvider(Start);
        var coordinator = Coordinator(clock, SyncState.Initial, Options with { BackgroundEnabled = false });
        using var scheduler = Scheduler(coordinator, clock, Options with { BackgroundEnabled = false });

        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await SettleAsync();
        clock.Advance(TimeSpan.FromHours(5));
        await SettleAsync();

        coordinator.Requests.TryRead(out _).Should().BeFalse();
        await scheduler.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Without_a_snapshot_one_startup_request_is_made_after_the_jitter()
    {
        var clock = new FakeTimeProvider(Start);
        var coordinator = Coordinator(clock, SyncState.Initial, Options);
        using var scheduler = Scheduler(coordinator, clock, Options);

        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await SettleAsync();
        clock.Advance(TimeSpan.FromSeconds(9));
        await SettleAsync();
        coordinator.Requests.TryRead(out _).Should().BeFalse("the shortest wait is ten seconds");

        clock.Advance(TimeSpan.FromSeconds(111));
        await WaitForRequestAsync(coordinator);

        coordinator.State.LastStartedUtc.Should().Be(Start + TimeSpan.FromSeconds(120));
        coordinator.Requests.TryRead(out _).Should().BeFalse("only one start-up request is made");
        await scheduler.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_last_success_younger_than_the_interval_means_no_startup_request()
    {
        var clock = new FakeTimeProvider(Start);
        var state = SyncState.Initial with { LastSuccessUtc = Start - TimeSpan.FromMinutes(30) };
        var coordinator = Coordinator(clock, state, Options);
        using var scheduler = Scheduler(coordinator, clock, Options);

        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await SettleAsync();
        clock.Advance(TimeSpan.FromSeconds(125));
        await SettleAsync();

        coordinator.Requests.TryRead(out _).Should().BeFalse();
        await scheduler.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_last_success_older_than_the_interval_means_a_startup_request()
    {
        var clock = new FakeTimeProvider(Start);
        var state = SyncState.Initial with { LastSuccessUtc = Start - TimeSpan.FromMinutes(90) };
        var coordinator = Coordinator(clock, state, Options);
        using var scheduler = Scheduler(coordinator, clock, Options);

        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await SettleAsync();
        clock.Advance(TimeSpan.FromSeconds(125));

        await WaitForRequestAsync(coordinator);
        await scheduler.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_start_less_than_fifteen_minutes_ago_means_no_startup_request_even_without_a_snapshot()
    {
        var clock = new FakeTimeProvider(Start);
        var state = SyncState.Initial with { LastStartedUtc = Start - TimeSpan.FromMinutes(14) };
        var coordinator = Coordinator(clock, state, Options);
        using var scheduler = Scheduler(coordinator, clock, Options);

        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await SettleAsync();
        clock.Advance(TimeSpan.FromSeconds(125));
        await SettleAsync();

        coordinator.Requests.TryRead(out _).Should().BeFalse();
        await scheduler.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task One_scheduled_request_is_made_per_interval()
    {
        var clock = new FakeTimeProvider(Start);
        var state = SyncState.Initial with { LastSuccessUtc = Start };
        var coordinator = Coordinator(clock, state, Options);
        using var scheduler = Scheduler(coordinator, clock, Options);
        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await SettleAsync();

        for (var interval = 1; interval <= 3; interval++)
        {
            clock.Advance(Options.Interval);
            await WaitForRequestAsync(coordinator);
            coordinator.State.LastStartedUtc.Should().Be(Start + (interval * Options.Interval));
            coordinator.Complete(new SyncRunResult(SyncResult.Unchanged, SyncFailure.None));
        }

        await scheduler.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_tick_while_a_run_is_in_progress_queues_no_second_request()
    {
        var clock = new FakeTimeProvider(Start);
        var state = SyncState.Initial with { LastSuccessUtc = Start };
        var coordinator = Coordinator(clock, state, Options);
        using var scheduler = Scheduler(coordinator, clock, Options);
        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await SettleAsync();

        clock.Advance(Options.Interval);
        await WaitForRequestAsync(coordinator);
        clock.Advance(Options.Interval);
        await SettleAsync();

        coordinator.IsRunning.Should().BeTrue();
        coordinator.Requests.TryRead(out _).Should().BeFalse();
        await scheduler.StopAsync(TestContext.Current.CancellationToken);
    }

    private static SyncCoordinator Coordinator(FakeTimeProvider clock, SyncState state, SyncOptions options) =>
        new(new InMemorySyncStateStore(state), options, clock);

    private static SyncScheduler Scheduler(SyncCoordinator coordinator, FakeTimeProvider clock, SyncOptions options) =>
        new(coordinator, new CollectionStore(), options, clock, NullLogger<SyncScheduler>.Instance);

    /// <summary>Gives the scheduler thread real time to arm its timers or to act on an advanced clock; the fake clock cannot signal that.</summary>
    private static async Task SettleAsync() =>
        await Task.Delay(TimeSpan.FromMilliseconds(150), TestContext.Current.CancellationToken);

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

using Cabinet.Domain.Collection;
using Cabinet.Service.Sync;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Sync;

/// <summary>Proves the shared window opens at every sync start, is decided under one lock and holds at its edges.</summary>
[Trait("Category", "Sync")]
public class SyncCoordinatorTests
{
    private static readonly DateTimeOffset Start = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(10);
    private static readonly SyncOptions Options = new(true, TimeSpan.FromMinutes(60), Cooldown, TimeSpan.FromSeconds(120), TimeSpan.FromHours(3));

    [Fact]
    public void On_a_fresh_install_the_first_manual_press_is_started_and_opens_the_window()
    {
        var store = new InMemorySyncStateStore();
        var coordinator = new SyncCoordinator(store, Options, new FakeTimeProvider(Start));

        var result = coordinator.TryRequest(SyncTrigger.Manual);

        result.Should().BeOfType<SyncRequestResult.Started>();
        store.Stored.LastStartedUtc.Should().Be(Start);
        store.Stored.CooldownEndsUtc.Should().Be(Start + Cooldown);
        coordinator.IsRunning.Should().BeTrue();
        coordinator.Requests.TryRead(out var trigger).Should().BeTrue();
        trigger.Should().Be(SyncTrigger.Manual);
    }

    [Fact]
    public void The_window_is_saved_before_the_request_is_queued()
    {
        var store = new InMemorySyncStateStore();
        var coordinator = new SyncCoordinator(store, Options, new FakeTimeProvider(Start));

        coordinator.TryRequest(SyncTrigger.Manual);

        store.SaveCount.Should().Be(1);
        coordinator.Requests.TryRead(out _).Should().BeTrue();
    }

    [Fact]
    public void A_manual_press_exactly_at_the_end_of_the_window_is_started()
    {
        var clock = new FakeTimeProvider(Start);
        var coordinator = new SyncCoordinator(new InMemorySyncStateStore(WindowEnding(Start)), Options, clock);

        coordinator.TryRequest(SyncTrigger.Manual).Should().BeOfType<SyncRequestResult.Started>();
    }

    [Fact]
    public void A_manual_press_one_second_before_the_end_of_the_window_is_refused_with_the_end()
    {
        var clock = new FakeTimeProvider(Start - TimeSpan.FromSeconds(1));
        var coordinator = new SyncCoordinator(new InMemorySyncStateStore(WindowEnding(Start)), Options, clock);

        var result = coordinator.TryRequest(SyncTrigger.Manual);

        result.Should().Be(new SyncRequestResult.CoolingDown(Start));
        coordinator.IsRunning.Should().BeFalse();
        coordinator.Requests.TryRead(out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(SyncTrigger.Scheduled)]
    [InlineData(SyncTrigger.Startup)]
    public void A_background_request_inside_the_window_is_started_and_moves_the_window(SyncTrigger trigger)
    {
        var clock = new FakeTimeProvider(Start);
        var store = new InMemorySyncStateStore(WindowEnding(Start + TimeSpan.FromMinutes(7)));
        var coordinator = new SyncCoordinator(store, Options, clock);

        coordinator.TryRequest(trigger).Should().BeOfType<SyncRequestResult.Started>();

        store.Stored.LastStartedUtc.Should().Be(Start);
        store.Stored.CooldownEndsUtc.Should().Be(Start + Cooldown);
    }

    [Fact]
    public void A_failed_run_leaves_the_last_success_and_the_window_in_place()
    {
        var clock = new FakeTimeProvider(Start);
        var lastSuccess = Start - TimeSpan.FromHours(2);
        var store = new InMemorySyncStateStore(SyncState.Initial with { LastSuccessUtc = lastSuccess });
        var coordinator = new SyncCoordinator(store, Options, clock);
        coordinator.TryRequest(SyncTrigger.Scheduled);
        clock.Advance(TimeSpan.FromSeconds(30));

        coordinator.Complete(new SyncRunResult(SyncResult.Failed, SyncFailure.Throttled));

        var state = coordinator.State;
        state.LastSuccessUtc.Should().Be(lastSuccess);
        state.LastFinishedUtc.Should().Be(Start + TimeSpan.FromSeconds(30));
        state.LastResult.Should().Be(SyncResult.Failed);
        state.LastFailure.Should().Be(SyncFailure.Throttled);
        state.ConsecutiveFailures.Should().Be(1);
        state.CooldownEndsUtc.Should().Be(Start + Cooldown);
        store.Stored.Should().Be(state);
        coordinator.IsRunning.Should().BeFalse();
    }

    [Theory]
    [InlineData(SyncResult.Changed)]
    [InlineData(SyncResult.Unchanged)]
    public void A_changed_or_unchanged_run_sets_the_last_success_to_its_finish_time_and_clears_the_failures(SyncResult result)
    {
        var clock = new FakeTimeProvider(Start);
        var store = new InMemorySyncStateStore(SyncState.Initial with { ConsecutiveFailures = 4, LastSuccessUtc = Start - TimeSpan.FromDays(1) });
        var coordinator = new SyncCoordinator(store, Options, clock);
        coordinator.TryRequest(SyncTrigger.Scheduled);
        clock.Advance(TimeSpan.FromSeconds(45));

        coordinator.Complete(new SyncRunResult(result, SyncFailure.None));

        coordinator.State.LastSuccessUtc.Should().Be(Start + TimeSpan.FromSeconds(45));
        coordinator.State.ConsecutiveFailures.Should().Be(0);
        coordinator.State.LastResult.Should().Be(result);
    }

    [Fact]
    public void A_held_back_run_does_not_move_the_last_success()
    {
        var clock = new FakeTimeProvider(Start);
        var lastSuccess = Start - TimeSpan.FromHours(1);
        var coordinator = new SyncCoordinator(new InMemorySyncStateStore(SyncState.Initial with { LastSuccessUtc = lastSuccess }), Options, clock);
        coordinator.TryRequest(SyncTrigger.Scheduled);

        coordinator.Complete(new SyncRunResult(SyncResult.HeldBack, SyncFailure.None));

        coordinator.State.LastSuccessUtc.Should().Be(lastSuccess);
        coordinator.State.LastResult.Should().Be(SyncResult.HeldBack);
    }

    [Fact]
    public void A_new_coordinator_over_the_same_store_refuses_a_manual_press_inside_the_window()
    {
        var store = new InMemorySyncStateStore();
        var clock = new FakeTimeProvider(Start);
        var before = new SyncCoordinator(store, Options, clock);
        before.TryRequest(SyncTrigger.Manual);
        before.Complete(new SyncRunResult(SyncResult.Changed, SyncFailure.None));
        clock.Advance(TimeSpan.FromMinutes(3));

        var afterRestart = new SyncCoordinator(store, Options, clock);

        afterRestart.TryRequest(SyncTrigger.Manual).Should().Be(new SyncRequestResult.CoolingDown(Start + Cooldown));
    }

    [Fact]
    public async Task Two_manual_presses_started_together_yield_exactly_one_start()
    {
        var coordinator = new SyncCoordinator(new InMemorySyncStateStore(), Options, new FakeTimeProvider(Start));
        using var barrier = new Barrier(2);

        var results = await Task.WhenAll(
            Task.Run(() => PressAfter(barrier, coordinator), TestContext.Current.CancellationToken),
            Task.Run(() => PressAfter(barrier, coordinator), TestContext.Current.CancellationToken));

        results.OfType<SyncRequestResult.Started>().Should().ContainSingle();
        results.OfType<SyncRequestResult.AlreadyRunning>().Should().ContainSingle();
    }

    [Fact]
    public void A_press_while_running_is_reported_as_running_even_inside_the_window()
    {
        var coordinator = new SyncCoordinator(new InMemorySyncStateStore(), Options, new FakeTimeProvider(Start));
        coordinator.TryRequest(SyncTrigger.Manual);

        coordinator.TryRequest(SyncTrigger.Manual).Should().BeOfType<SyncRequestResult.AlreadyRunning>();
    }

    [Fact]
    public void A_window_that_cannot_be_stored_starts_nothing()
    {
        var coordinator = new SyncCoordinator(new FailingSyncStateStore(failSaves: true), Options, new FakeTimeProvider(Start));

        var act = () => coordinator.TryRequest(SyncTrigger.Manual);

        act.Should().Throw<IOException>();
        coordinator.IsRunning.Should().BeFalse();
        coordinator.Requests.TryRead(out _).Should().BeFalse();
        coordinator.State.CooldownEndsUtc.Should().BeNull();
    }

    [Fact]
    public void A_run_whose_result_cannot_be_stored_still_frees_the_coordinator()
    {
        var store = new FailingSyncStateStore(failSaves: false);
        var coordinator = new SyncCoordinator(store, Options, new FakeTimeProvider(Start));
        coordinator.TryRequest(SyncTrigger.Scheduled);
        store.FailSaves = true;

        coordinator.Complete(new SyncRunResult(SyncResult.Changed, SyncFailure.None));

        coordinator.IsRunning.Should().BeFalse();
        coordinator.State.LastResult.Should().Be(SyncResult.Changed);
    }

    private static SyncRequestResult PressAfter(Barrier barrier, SyncCoordinator coordinator)
    {
        barrier.SignalAndWait();

        return coordinator.TryRequest(SyncTrigger.Manual);
    }

    private static SyncState WindowEnding(DateTimeOffset end) =>
        SyncState.Initial with { LastStartedUtc = end - Cooldown, CooldownEndsUtc = end };

    private sealed class FailingSyncStateStore(bool failSaves) : ISyncStateStore
    {
        public bool FailSaves { get; set; } = failSaves;

        public SyncState Load() => SyncState.Initial;

        public void Save(SyncState state)
        {
            if (FailSaves)
            {
                throw new IOException("The disk is full.");
            }
        }
    }
}

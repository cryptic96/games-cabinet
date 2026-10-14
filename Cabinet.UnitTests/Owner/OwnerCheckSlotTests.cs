using Cabinet.Repository.Images;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Owner;

/// <summary>
/// Proves the two picture fetch slots are independent and each one polite: the pacer of the background slot and the pacer of
/// the owner's own check each keep the configured gap between their own requests, a held turn on one never makes a turn on
/// the other wait, and two turns on the same slot never overlap.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerCheckSlotTests
{
    private static readonly TimeSpan Gap = TimeSpan.FromSeconds(1);

    private static readonly DateTimeOffset Start = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Two_turns_on_the_background_slot_are_at_least_the_gap_apart()
    {
        var clock = new FakeTimeProvider(Start);
        var shared = new ImagePacer(Gap, clock);

        await AssertSecondTurnWaitsForTheGap(shared, clock);
    }

    [Fact]
    public async Task Two_turns_on_the_owner_check_slot_are_at_least_the_gap_apart()
    {
        var clock = new FakeTimeProvider(Start);
        var ownerCheck = new OwnerCheckPacer(Gap, clock);

        await AssertSecondTurnWaitsForTheGap(ownerCheck, clock);
    }

    [Fact]
    public async Task A_turn_on_the_owner_check_slot_is_granted_at_once_while_the_background_slot_holds_its_turn()
    {
        var clock = new FakeTimeProvider(Start);
        var shared = new ImagePacer(Gap, clock);
        var ownerCheck = new OwnerCheckPacer(Gap, clock);
        using var background = await shared.WaitTurnAsync(TestContext.Current.CancellationToken);

        var turn = ownerCheck.WaitTurnAsync(TestContext.Current.CancellationToken);

        turn.IsCompleted.Should().BeTrue();
        (await turn).Dispose();
    }

    [Fact]
    public async Task A_turn_on_the_background_slot_is_granted_at_once_while_the_owner_check_slot_holds_its_turn()
    {
        var clock = new FakeTimeProvider(Start);
        var shared = new ImagePacer(Gap, clock);
        var ownerCheck = new OwnerCheckPacer(Gap, clock);
        using var check = await ownerCheck.WaitTurnAsync(TestContext.Current.CancellationToken);

        var turn = shared.WaitTurnAsync(TestContext.Current.CancellationToken);

        turn.IsCompleted.Should().BeTrue();
        (await turn).Dispose();
    }

    [Fact]
    public async Task A_second_turn_on_the_background_slot_waits_while_the_first_is_held_so_the_worker_and_the_sync_never_overlap()
    {
        var clock = new FakeTimeProvider(Start);
        var shared = new ImagePacer(Gap, clock);
        var worker = await shared.WaitTurnAsync(TestContext.Current.CancellationToken);

        var sync = shared.WaitTurnAsync(TestContext.Current.CancellationToken);
        clock.Advance(Gap * 3);

        sync.IsCompleted.Should().BeFalse();
        worker.Dispose();
        clock.Advance(Gap);
        (await sync.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)).Dispose();
    }

    private static async Task AssertSecondTurnWaitsForTheGap(IImagePacer pacer, FakeTimeProvider clock)
    {
        (await pacer.WaitTurnAsync(TestContext.Current.CancellationToken)).Dispose();

        var second = pacer.WaitTurnAsync(TestContext.Current.CancellationToken);
        clock.Advance(Gap - TimeSpan.FromMilliseconds(1));
        var finishedEarly = second.IsCompleted;
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var lease = await second.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        finishedEarly.Should().BeFalse();
        lease.Dispose();
    }
}

using Cabinet.Repository.Bgg;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Bgg;

/// <summary>Proves every request waits its turn and at least the minimum gap after the previous one finished.</summary>
[Trait("Category", "Bgg")]
public class RequestPacerTests
{
    private static readonly TimeSpan Gap = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task The_first_request_does_not_wait()
    {
        var pacer = new RequestPacer(Gap, new FakeTimeProvider());

        var turn = pacer.WaitTurnAsync(TestContext.Current.CancellationToken);

        await FinishesPromptly(turn);
    }

    [Fact]
    public async Task A_second_request_starts_at_least_five_seconds_after_the_first_finished()
    {
        var clock = new FakeTimeProvider();
        var pacer = new RequestPacer(Gap, clock);
        (await pacer.WaitTurnAsync(TestContext.Current.CancellationToken)).Dispose();

        var second = pacer.WaitTurnAsync(TestContext.Current.CancellationToken);
        await StaysPending(second);
        clock.Advance(TimeSpan.FromSeconds(4));
        await StaysPending(second);
        clock.Advance(TimeSpan.FromSeconds(1));

        await FinishesPromptly(second);
    }

    [Fact]
    public async Task A_gap_below_the_minimum_is_raised_to_five_seconds()
    {
        var clock = new FakeTimeProvider();
        var pacer = new RequestPacer(TimeSpan.FromSeconds(1), clock);
        (await pacer.WaitTurnAsync(TestContext.Current.CancellationToken)).Dispose();

        var second = pacer.WaitTurnAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(1));
        await StaysPending(second);
        clock.Advance(TimeSpan.FromSeconds(4));

        await FinishesPromptly(second);
    }

    [Fact]
    public async Task The_gap_is_measured_from_the_end_of_the_previous_request_not_its_start()
    {
        var clock = new FakeTimeProvider();
        var pacer = new RequestPacer(Gap, clock);
        var first = await pacer.WaitTurnAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(3));
        first.Dispose();

        var second = pacer.WaitTurnAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(4));
        await StaysPending(second);
        clock.Advance(TimeSpan.FromSeconds(1));

        await FinishesPromptly(second);
    }

    [Fact]
    public async Task A_second_request_waits_for_the_first_to_finish_even_when_the_gap_has_passed()
    {
        var clock = new FakeTimeProvider();
        var pacer = new RequestPacer(Gap, clock);
        var first = await pacer.WaitTurnAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(1));

        var second = pacer.WaitTurnAsync(TestContext.Current.CancellationToken);
        await StaysPending(second);
        first.Dispose();
        clock.Advance(Gap);

        await FinishesPromptly(second);
    }

    [Fact]
    public async Task A_caller_that_gives_up_while_waiting_does_not_block_the_next_one()
    {
        var clock = new FakeTimeProvider();
        var pacer = new RequestPacer(Gap, clock);
        (await pacer.WaitTurnAsync(TestContext.Current.CancellationToken)).Dispose();
        using var giveUp = new CancellationTokenSource();
        var abandoned = pacer.WaitTurnAsync(giveUp.Token);
        await StaysPending(abandoned);

        await giveUp.CancelAsync();
        var next = pacer.WaitTurnAsync(TestContext.Current.CancellationToken);
        clock.Advance(Gap);

        await FinishesPromptly(next);
        abandoned.IsCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task A_wall_clock_step_backwards_does_not_lengthen_the_wait()
    {
        var clock = new SteppableWallClock();
        var pacer = new RequestPacer(Gap, clock);
        (await pacer.WaitTurnAsync(TestContext.Current.CancellationToken)).Dispose();
        clock.StepWallClockBack(TimeSpan.FromHours(1));

        var second = pacer.WaitTurnAsync(TestContext.Current.CancellationToken);
        await StaysPending(second);
        clock.Advance(Gap);

        await FinishesPromptly(second);
    }

    private static async Task FinishesPromptly(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);
        }

        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    private static async Task StaysPending(Task task)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        task.IsCompleted.Should().BeFalse();
    }

    private sealed class SteppableWallClock : FakeTimeProvider
    {
        private TimeSpan _stepBack = TimeSpan.Zero;

        public override DateTimeOffset GetUtcNow() => base.GetUtcNow() - _stepBack;

        public void StepWallClockBack(TimeSpan step) => _stepBack += step;
    }
}

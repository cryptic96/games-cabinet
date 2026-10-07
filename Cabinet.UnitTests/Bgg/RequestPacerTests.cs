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
        var (fake, clock) = NewClock();
        var pacer = new RequestPacer(Gap, clock);
        (await pacer.WaitTurnAsync(TestContext.Current.CancellationToken)).Dispose();

        var second = await RequestWhileDelayed(pacer, clock, TestContext.Current.CancellationToken);
        second.IsCompleted.Should().BeFalse();
        fake.Advance(TimeSpan.FromSeconds(4));
        second.IsCompleted.Should().BeFalse();
        fake.Advance(TimeSpan.FromSeconds(1));

        await FinishesPromptly(second);
    }

    [Fact]
    public async Task A_gap_below_the_minimum_is_raised_to_five_seconds()
    {
        var (fake, clock) = NewClock();
        var pacer = new RequestPacer(TimeSpan.FromSeconds(1), clock);
        (await pacer.WaitTurnAsync(TestContext.Current.CancellationToken)).Dispose();

        var second = await RequestWhileDelayed(pacer, clock, TestContext.Current.CancellationToken);
        fake.Advance(TimeSpan.FromSeconds(1));
        second.IsCompleted.Should().BeFalse();
        fake.Advance(TimeSpan.FromSeconds(4));

        await FinishesPromptly(second);
    }

    [Fact]
    public async Task The_gap_is_measured_from_the_end_of_the_previous_request_not_its_start()
    {
        var (fake, clock) = NewClock();
        var pacer = new RequestPacer(Gap, clock);
        var first = await pacer.WaitTurnAsync(TestContext.Current.CancellationToken);
        fake.Advance(TimeSpan.FromSeconds(3));
        first.Dispose();

        var second = await RequestWhileDelayed(pacer, clock, TestContext.Current.CancellationToken);
        fake.Advance(TimeSpan.FromSeconds(4));
        second.IsCompleted.Should().BeFalse();
        fake.Advance(TimeSpan.FromSeconds(1));

        await FinishesPromptly(second);
    }

    [Fact]
    public async Task A_second_request_waits_for_the_first_to_finish_even_when_the_gap_has_passed()
    {
        var (fake, clock) = NewClock();
        var pacer = new RequestPacer(Gap, clock);
        var first = await pacer.WaitTurnAsync(TestContext.Current.CancellationToken);
        fake.Advance(TimeSpan.FromMinutes(1));

        var second = pacer.WaitTurnAsync(TestContext.Current.CancellationToken);
        second.IsCompleted.Should().BeFalse();
        first.Dispose();
        fake.Advance(Gap);

        await FinishesPromptly(second);
    }

    [Fact]
    public async Task A_caller_that_gives_up_while_waiting_does_not_block_the_next_one()
    {
        var (fake, clock) = NewClock();
        var pacer = new RequestPacer(Gap, clock);
        (await pacer.WaitTurnAsync(TestContext.Current.CancellationToken)).Dispose();
        using var giveUp = new CancellationTokenSource();
        var abandoned = await RequestWhileDelayed(pacer, clock, giveUp.Token);
        abandoned.IsCompleted.Should().BeFalse();

        await giveUp.CancelAsync();
        var next = pacer.WaitTurnAsync(TestContext.Current.CancellationToken);
        fake.Advance(Gap);

        await FinishesPromptly(next);
        await FluentActions.Awaiting(() => abandoned).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task A_wall_clock_step_backwards_does_not_lengthen_the_wait()
    {
        var wallClock = new SteppableWallClock();
        var clock = new TimerCountingClock(wallClock);
        var pacer = new RequestPacer(Gap, clock);
        (await pacer.WaitTurnAsync(TestContext.Current.CancellationToken)).Dispose();
        wallClock.StepWallClockBack(TimeSpan.FromHours(1));

        var second = await RequestWhileDelayed(pacer, clock, TestContext.Current.CancellationToken);
        second.IsCompleted.Should().BeFalse();
        wallClock.Advance(Gap);

        await FinishesPromptly(second);
    }

    private static (FakeTimeProvider Fake, TimerCountingClock Clock) NewClock()
    {
        var fake = new FakeTimeProvider();

        return (fake, new TimerCountingClock(fake));
    }

    /// <summary>
    /// Asks for a turn and returns once the pacer has armed its delay timer on the clock, so what follows is a
    /// deterministic comparison of the fake time against that timer rather than a guess about scheduling.
    /// </summary>
    private static async Task<Task<IDisposable>> RequestWhileDelayed(
        RequestPacer pacer,
        TimerCountingClock clock,
        CancellationToken cancellationToken)
    {
        var timersBefore = clock.TimerCount;
        var turn = pacer.WaitTurnAsync(cancellationToken);
        await clock.WaitForTimersAsync(timersBefore + 1, TestContext.Current.CancellationToken);

        return turn;
    }

    private static async Task FinishesPromptly(Task task)
    {
        await task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    private sealed class SteppableWallClock : FakeTimeProvider
    {
        private TimeSpan _stepBack = TimeSpan.Zero;

        public override DateTimeOffset GetUtcNow() => base.GetUtcNow() - _stepBack;

        public void StepWallClockBack(TimeSpan step) => _stepBack += step;
    }
}

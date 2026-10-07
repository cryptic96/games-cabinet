using Cabinet.Domain.Collection;
using FluentAssertions;

namespace Cabinet.UnitTests.Sync;

/// <summary>Proves the guard, told that the stored collection could not be read, accepts nothing empty and nothing unconfirmed.</summary>
[Trait("Category", "Sync")]
public class ShrinkGuardUnreadableTests
{
    private static readonly DateTimeOffset Detected = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_empty_answer_is_held_back_as_empty_every_time()
    {
        var previous = new HeldBackRecord(HeldBackKind.Empty, ShrinkGuard.Fingerprint([]), 0, Detected);

        ShrinkGuard.Evaluate(0, [], null, storedCollectionUnreadable: true)
            .Should().Be(new GuardDecision.HeldBack(HeldBackKind.Empty, ShrinkGuard.Fingerprint([]), 0));
        ShrinkGuard.Evaluate(0, [], previous, storedCollectionUnreadable: true)
            .Should().BeOfType<GuardDecision.HeldBack>().Which.Kind.Should().Be(HeldBackKind.Empty);
    }

    [Fact]
    public void A_first_non_empty_answer_is_held_back_as_unverified()
    {
        ShrinkGuard.Evaluate(0, [1, 2, 3], null, storedCollectionUnreadable: true)
            .Should().Be(new GuardDecision.HeldBack(HeldBackKind.Unverified, ShrinkGuard.Fingerprint([1, 2, 3]), 3));
    }

    [Fact]
    public void The_same_set_twice_is_accepted_in_any_order()
    {
        var previous = new HeldBackRecord(HeldBackKind.Unverified, ShrinkGuard.Fingerprint([1, 2, 3]), 3, Detected);

        ShrinkGuard.Evaluate(0, [3, 1, 2], previous, storedCollectionUnreadable: true).Should().BeOfType<GuardDecision.Accept>();
    }

    [Fact]
    public void A_different_set_or_a_held_back_answer_of_another_kind_does_not_confirm()
    {
        var other = new HeldBackRecord(HeldBackKind.Unverified, ShrinkGuard.Fingerprint([1, 2]), 2, Detected);
        var shrunk = new HeldBackRecord(HeldBackKind.Shrunk, ShrinkGuard.Fingerprint([1, 2, 3]), 3, Detected);

        ShrinkGuard.Evaluate(0, [1, 2, 3], other, storedCollectionUnreadable: true).Should().BeOfType<GuardDecision.HeldBack>();
        ShrinkGuard.Evaluate(0, [1, 2, 3], shrunk, storedCollectionUnreadable: true).Should().BeOfType<GuardDecision.HeldBack>();
    }

    [Fact]
    public void Without_the_flag_the_ordinary_rules_apply()
    {
        ShrinkGuard.Evaluate(0, [1, 2, 3], null).Should().BeOfType<GuardDecision.Accept>();
    }
}

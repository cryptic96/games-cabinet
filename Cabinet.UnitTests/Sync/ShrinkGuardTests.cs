using Cabinet.Domain.Collection;
using FluentAssertions;

namespace Cabinet.UnitTests.Sync;

/// <summary>Proves the guard never accepts an empty collection over shown games, and accepts a big shrink only when it repeats.</summary>
[Trait("Category", "Sync")]
public class ShrinkGuardTests
{
    private static readonly DateTimeOffset Detected = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_first_sync_with_no_games_is_accepted()
    {
        ShrinkGuard.Evaluate(0, [], null).Should().BeOfType<GuardDecision.Accept>();
    }

    [Fact]
    public void A_first_sync_with_games_is_accepted()
    {
        ShrinkGuard.Evaluate(0, Ids(1, 12), null).Should().BeOfType<GuardDecision.Accept>();
    }

    [Fact]
    public void An_empty_collection_over_shown_games_is_held_back_as_empty()
    {
        var decision = ShrinkGuard.Evaluate(65, [], null);

        decision.Should().Be(new GuardDecision.HeldBack(HeldBackKind.Empty, ShrinkGuard.Fingerprint([]), 0));
    }

    [Fact]
    public void An_empty_collection_is_held_back_again_even_when_the_previous_record_is_the_same_empty_collection()
    {
        var previous = new HeldBackRecord(HeldBackKind.Empty, ShrinkGuard.Fingerprint([]), 0, Detected);

        ShrinkGuard.Evaluate(65, [], previous).Should().BeOfType<GuardDecision.HeldBack>().Which.Kind.Should().Be(HeldBackKind.Empty);
    }

    [Fact]
    public void An_empty_collection_is_held_back_when_the_previous_record_was_a_shrink()
    {
        var previous = new HeldBackRecord(HeldBackKind.Shrunk, ShrinkGuard.Fingerprint(Ids(1, 10)), 10, Detected);

        ShrinkGuard.Evaluate(65, [], previous).Should().BeOfType<GuardDecision.HeldBack>().Which.Kind.Should().Be(HeldBackKind.Empty);
    }

    [Fact]
    public void A_collection_of_exactly_half_the_shown_entries_is_accepted()
    {
        ShrinkGuard.Evaluate(64, Ids(1, 32), null).Should().BeOfType<GuardDecision.Accept>();
    }

    [Fact]
    public void A_collection_that_lost_more_than_half_is_held_back_as_shrunk_with_its_fingerprint_and_count()
    {
        var candidate = Ids(1, 32);

        var decision = ShrinkGuard.Evaluate(65, candidate, null);

        decision.Should().Be(new GuardDecision.HeldBack(HeldBackKind.Shrunk, ShrinkGuard.Fingerprint(candidate), 32));
    }

    [Fact]
    public void The_same_entries_in_another_order_confirm_a_held_back_shrink()
    {
        var first = Ids(1, 32);
        var previous = new HeldBackRecord(HeldBackKind.Shrunk, ShrinkGuard.Fingerprint(first), 32, Detected);

        ShrinkGuard.Evaluate(65, first.AsEnumerable().Reverse().ToList(), previous).Should().BeOfType<GuardDecision.Accept>();
    }

    [Fact]
    public void A_different_set_of_the_same_size_replaces_the_held_back_record()
    {
        var previous = new HeldBackRecord(HeldBackKind.Shrunk, ShrinkGuard.Fingerprint(Ids(1, 32)), 32, Detected);
        var other = Ids(100, 32);

        var decision = ShrinkGuard.Evaluate(65, other, previous);

        decision.Should().Be(new GuardDecision.HeldBack(HeldBackKind.Shrunk, ShrinkGuard.Fingerprint(other), 32));
    }

    [Fact]
    public void A_record_of_an_empty_collection_does_not_confirm_a_shrink()
    {
        var previous = new HeldBackRecord(HeldBackKind.Empty, ShrinkGuard.Fingerprint([]), 0, Detected);

        ShrinkGuard.Evaluate(65, Ids(1, 32), previous).Should().BeOfType<GuardDecision.HeldBack>();
    }

    [Fact]
    public void A_small_removal_is_accepted_at_once()
    {
        ShrinkGuard.Evaluate(65, Ids(1, 60), null).Should().BeOfType<GuardDecision.Accept>();
    }

    [Fact]
    public void A_larger_collection_is_accepted_at_once()
    {
        ShrinkGuard.Evaluate(65, Ids(1, 80), null).Should().BeOfType<GuardDecision.Accept>();
    }

    [Fact]
    public void A_collection_of_one_entry_over_one_shown_entry_is_accepted()
    {
        ShrinkGuard.Evaluate(1, Ids(7, 1), null).Should().BeOfType<GuardDecision.Accept>();
    }

    [Fact]
    public void The_fingerprint_is_lowercase_hex_of_the_sorted_identifiers_joined_by_commas()
    {
        var expected = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData("1,2,3"u8.ToArray()));

        ShrinkGuard.Fingerprint([3, 1, 2]).Should().Be(expected).And.MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Different_sets_have_different_fingerprints()
    {
        ShrinkGuard.Fingerprint(Ids(1, 32)).Should().NotBe(ShrinkGuard.Fingerprint(Ids(2, 32)));
    }

    private static List<long> Ids(long first, int count) => [.. Enumerable.Range(0, count).Select(offset => first + offset)];
}

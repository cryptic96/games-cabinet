using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using FluentAssertions;

namespace Cabinet.UnitTests.Collection;

/// <summary>Proves which base game an owned expansion is paired with, and that nothing else is ever paired.</summary>
[Trait("Category", "Snapshot")]
public sealed class ExpansionPairingTests
{
    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void One_owned_base_game_is_the_pairing()
    {
        var snapshot = Snapshot(
            [Base(1, 100), Expansion(2, 200)],
            (200, [Ref(100)]));

        ExpansionPairing.Pair(snapshot)[2].Should().Equal(Ref(100));
    }

    [Fact]
    public void Of_several_owned_base_games_the_one_with_the_lowest_collection_entry_wins_even_with_the_higher_game_id()
    {
        var snapshot = Snapshot(
            [Base(5, 900), Base(7, 100), Expansion(9, 200)],
            (200, [Ref(100), Ref(900)]));

        ExpansionPairing.Pair(snapshot)[9].Should().Equal(Ref(900));
    }

    [Fact]
    public void Adding_a_base_game_with_a_lower_game_id_and_a_higher_collection_entry_leaves_the_pairing_where_it_was()
    {
        SnapshotItem[] before = [Base(5, 900), Expansion(9, 200)];
        SnapshotItem[] after = [Base(5, 900), Base(12, 100), Expansion(9, 200)];
        var details = (200, (IReadOnlyList<BaseGameRef>)[Ref(100), Ref(900)]);

        ExpansionPairing.Pair(Snapshot(before, details))[9].Should().Equal(Ref(900));
        ExpansionPairing.Pair(Snapshot(after, details))[9].Should().Equal(Ref(900));
    }

    [Fact]
    public void Two_copies_of_one_base_game_give_one_reference()
    {
        var snapshot = Snapshot(
            [Base(3, 100), Base(8, 100), Expansion(9, 200)],
            (200, [Ref(100)]));

        ExpansionPairing.Pair(snapshot)[9].Should().Equal(Ref(100));
    }

    [Fact]
    public void A_base_game_owned_only_as_an_expansion_entry_is_not_a_base_game_so_the_expansion_is_an_orphan()
    {
        var snapshot = Snapshot(
            [Expansion(1, 100), Expansion(2, 200)],
            (200, [Ref(100), Ref(300)]));

        ExpansionPairing.Pair(snapshot)[2].Should().Equal(Ref(100), Ref(300));
    }

    [Fact]
    public void With_no_owned_base_game_every_linked_game_is_kept_in_the_order_the_details_gave_them()
    {
        var snapshot = Snapshot(
            [Base(1, 50), Expansion(2, 200)],
            (200, [Ref(900), Ref(100), Ref(500)]));

        ExpansionPairing.Pair(snapshot)[2].Should().Equal(Ref(900), Ref(100), Ref(500));
    }

    [Fact]
    public void An_expansion_without_details_or_without_links_has_no_entry()
    {
        var snapshot = Snapshot(
            [Base(1, 100), Expansion(2, 200), Expansion(3, 300)],
            (300, []));

        var paired = ExpansionPairing.Pair(snapshot);

        paired.Should().BeEmpty();
        ExpansionPairing.Pair(snapshot with { Games = null }).Should().BeEmpty();
    }

    [Fact]
    public void A_base_game_item_is_never_paired_even_when_its_details_name_it_as_an_expansion()
    {
        var snapshot = Snapshot(
            [Base(1, 100), Base(2, 200), Expansion(3, 300)],
            (200, [Ref(100)]),
            (300, [Ref(100)]));

        var paired = ExpansionPairing.Pair(snapshot);

        paired.Keys.Should().Equal(3);
    }

    [Fact]
    public void A_big_box_and_an_owned_expansion_it_contains_are_both_left_as_they_are_because_only_expansion_links_pair()
    {
        var snapshot = Snapshot(
            [Base(1, 100), Base(2, 200), Expansion(3, 300)],
            (300, [Ref(100)]));

        var paired = ExpansionPairing.Pair(snapshot);

        paired.Should().ContainKey(3).WhoseValue.Should().Equal(Ref(100));
        paired.Should().NotContainKey(2);
    }

    [Fact]
    public void An_entry_listed_as_both_a_base_game_and_an_expansion_counts_as_the_expansion_only()
    {
        var snapshot = Snapshot(
            [Base(1, 100), Expansion(1, 100), Expansion(2, 200)],
            (200, [Ref(100)]));

        ExpansionPairing.Pair(snapshot)[2].Should().Equal(Ref(100));
    }

    [Fact]
    public void The_same_collection_always_gives_the_same_pairing()
    {
        var snapshot = Snapshot(
            [Base(5, 900), Base(7, 100), Expansion(9, 200), Expansion(10, 300)],
            (200, [Ref(100), Ref(900)]),
            (300, [Ref(900)]));

        var first = ExpansionPairing.Pair(snapshot);
        var second = ExpansionPairing.Pair(snapshot);

        first.Keys.Should().Equal(second.Keys);
        first.Select(pair => pair.Value.Single()).Should().Equal(second.Select(pair => pair.Value.Single()));
    }

    private static CollectionSnapshot Snapshot(SnapshotItem[] items, params (int GameId, IReadOnlyList<BaseGameRef> Expands)[] details) =>
        new(
            CollectionSnapshot.CurrentSchemaVersion,
            Moment,
            items,
            Games: details.ToDictionary(
                entry => entry.GameId,
                entry => new GameDetails(Moment, null, null, null, null, null, null, null, null, null, [], [], entry.Expands, null)));

    private static SnapshotItem Base(long collectionId, int gameId) =>
        new(collectionId, gameId, $"Example Game {gameId}", ItemKind.Base, null, null, null);

    private static SnapshotItem Expansion(long collectionId, int gameId) =>
        new(collectionId, gameId, $"Example Expansion {gameId}", ItemKind.Expansion, null, null, null);

    private static BaseGameRef Ref(int gameId) => new(gameId, $"Example Game {gameId}");
}

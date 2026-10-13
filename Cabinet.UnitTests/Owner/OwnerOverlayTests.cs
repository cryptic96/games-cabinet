using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>Proves where the overlay puts base games, expansions, orphans and games while the owner data cannot be read.</summary>
[Trait("Category", "Owner")]
public sealed class OwnerOverlayTests
{
    private const string CabinetA = "loc1";
    private const string ShelfB = "loc2";

    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_base_game_gets_the_name_of_the_location_the_owner_gave_it()
    {
        var snapshot = Snapshot([Base(1, 100)]);

        var overlay = OwnerOverlay.From(snapshot, Data((100, CabinetA)));

        overlay.EffectiveLocationName(100).Should().Be("Cabinet A");
        overlay.OwnLocationId(100).Should().Be(CabinetA);
        overlay.FollowsBaseGameId(100).Should().BeNull();
    }

    [Fact]
    public void An_expansion_without_a_location_of_its_own_follows_its_owned_base_game()
    {
        var snapshot = Snapshot([Base(1, 100), Expansion(2, 200)], (200, [Ref(100)]));

        var overlay = OwnerOverlay.From(snapshot, Data((100, CabinetA)));

        overlay.EffectiveLocationName(200).Should().Be("Cabinet A");
        overlay.EffectiveLocationId(200).Should().Be(CabinetA);
        overlay.OwnLocationId(200).Should().BeNull();
        overlay.FollowsBaseGameId(200).Should().Be(100);
    }

    [Fact]
    public void An_expansion_with_a_location_of_its_own_keeps_it()
    {
        var snapshot = Snapshot([Base(1, 100), Expansion(2, 200)], (200, [Ref(100)]));

        var overlay = OwnerOverlay.From(snapshot, Data((100, CabinetA), (200, ShelfB)));

        overlay.EffectiveLocationName(200).Should().Be("Shelf B");
        overlay.FollowsBaseGameId(200).Should().BeNull();
    }

    [Fact]
    public void An_expansion_reports_its_owned_base_game_whether_or_not_it_has_a_location_of_its_own()
    {
        var snapshot = Snapshot([Base(1, 100), Expansion(2, 200), Expansion(3, 300)], (200, [Ref(100)]));

        var overlay = OwnerOverlay.From(snapshot, Data((200, ShelfB)));

        overlay.OwnedBaseGameId(200).Should().Be(100);
        overlay.FollowsBaseGameId(200).Should().BeNull();
        overlay.OwnedBaseGameId(300).Should().BeNull();
        overlay.OwnedBaseGameId(100).Should().BeNull();
    }

    [Fact]
    public void An_orphan_expansion_with_no_location_is_not_placed()
    {
        var snapshot = Snapshot([Expansion(2, 200)], (200, [Ref(100)]));

        var overlay = OwnerOverlay.From(snapshot, Data((100, CabinetA)));

        overlay.EffectiveLocationName(200).Should().BeNull();
        overlay.EffectiveLocationId(200).Should().BeNull();
        overlay.FollowsBaseGameId(200).Should().BeNull();
    }

    [Fact]
    public void An_orphan_expansion_with_a_location_of_its_own_is_placed_there()
    {
        var snapshot = Snapshot([Expansion(2, 200)], (200, [Ref(100)]));

        var overlay = OwnerOverlay.From(snapshot, Data((200, ShelfB)));

        overlay.EffectiveLocationName(200).Should().Be("Shelf B");
    }

    [Fact]
    public void An_expansion_of_two_owned_base_games_follows_the_one_the_layout_stands_it_beside()
    {
        var snapshot = Snapshot(
            [Base(5, 900), Base(7, 100), Expansion(9, 200)],
            (200, [Ref(100), Ref(900)]));

        var overlay = OwnerOverlay.From(snapshot, Data((900, CabinetA), (100, ShelfB)));

        overlay.FollowsBaseGameId(200).Should().Be(900);
        overlay.EffectiveLocationName(200).Should().Be("Cabinet A");
    }

    [Fact]
    public void An_expansion_follows_an_owned_base_game_that_has_no_location_by_being_not_placed()
    {
        var snapshot = Snapshot([Base(1, 100), Expansion(2, 200)], (200, [Ref(100)]));

        var overlay = OwnerOverlay.From(snapshot, Data());

        overlay.FollowsBaseGameId(200).Should().Be(100);
        overlay.EffectiveLocationName(200).Should().BeNull();
    }

    [Fact]
    public void A_base_game_owned_twice_has_the_same_location_on_both_entries()
    {
        var snapshot = Snapshot([Base(1, 100), Base(3, 100)]);

        var effective = OwnerOverlay.From(snapshot, Data((100, CabinetA))).Effective(snapshot);

        effective.Items.Should().OnlyContain(item => item.Location == "Cabinet A");
        effective.Items.Should().HaveCount(2);
    }

    [Fact]
    public void A_location_the_owner_data_no_longer_lists_leaves_the_game_not_placed()
    {
        var snapshot = Snapshot([Base(1, 100)]);

        var overlay = OwnerOverlay.From(snapshot, Data((100, "gone")));

        overlay.EffectiveLocationName(100).Should().BeNull();
        overlay.OwnLocationId(100).Should().BeNull();
    }

    [Fact]
    public void Unreadable_owner_data_gives_every_item_no_location()
    {
        var snapshot = Snapshot(
            [Base(1, 100) with { Location = "Crate 7" }, Expansion(2, 200)],
            (200, [Ref(100)]));

        var effective = OwnerOverlay.From(snapshot, null).Effective(snapshot);

        effective.Items.Should().OnlyContain(item => item.Location == null);
        OwnerOverlay.None.Effective(snapshot).Items.Should().OnlyContain(item => item.Location == null);
    }

    [Fact]
    public void The_effective_collection_differs_from_the_synced_one_only_in_location()
    {
        var snapshot = Snapshot(
            [Base(1, 100) with { Location = "Crate 7", Year = 2001 }, Expansion(2, 200)],
            (200, [Ref(100)]));

        var effective = OwnerOverlay.From(snapshot, Data((100, CabinetA))).Effective(snapshot);

        effective.Items.Select(item => item with { Location = null })
            .Should().Equal(snapshot.Items.Select(item => item with { Location = null }));
        effective.Items.Select(item => item.Location).Should().Equal("Cabinet A", "Cabinet A");
        (effective with { Items = snapshot.Items }).Should().Be(snapshot);
        snapshot.Items[0].Location.Should().Be("Crate 7");
    }

    [Fact]
    public void The_same_input_always_gives_the_same_places()
    {
        var snapshot = Snapshot(
            [Base(5, 900), Base(7, 100), Expansion(9, 200), Expansion(10, 300)],
            (200, [Ref(100), Ref(900)]),
            (300, [Ref(900)]));
        var data = Data((900, CabinetA));

        var first = OwnerOverlay.From(snapshot, data).Effective(snapshot);
        var second = OwnerOverlay.From(snapshot, data).Effective(snapshot);

        first.Items.Should().Equal(second.Items);
    }

    [Fact]
    public void Adding_a_location_gives_it_a_fresh_identifier_and_a_log_line()
    {
        var ids = new Queue<string>(["same", "same", "fresh"]);
        var first = LocationEdits.Add(OwnerData.Empty, "  Cabinet A ", ids.Dequeue);

        var second = LocationEdits.Add(first.Data, "Shelf B", ids.Dequeue);

        first.LogText.Should().Be("Added location \"Cabinet A\"");
        second.Data.Locations.Should().Equal(new OwnerLocation("same", "Cabinet A"), new OwnerLocation("fresh", "Shelf B"));
    }

    [Fact]
    public void Adding_a_location_with_a_taken_empty_or_overlong_name_changes_nothing()
    {
        var data = LocationEdits.Add(OwnerData.Empty, "Cabinet A", () => "a1").Data;

        LocationEdits.Add(data, "cabinet a", () => "b2").Changed.Should().BeFalse();
        LocationEdits.Add(data, "   ", () => "b2").StatusKey.Should().Be("name-empty");
        LocationEdits.Add(data, new string('x', OwnerData.MaxNameLength + 1), () => "b2").StatusKey.Should().Be("name-too-long");
    }

    [Fact]
    public void Assigning_the_location_a_game_already_has_changes_nothing_and_assigning_another_logs_the_move()
    {
        var data = LocationEdits.Add(LocationEdits.Add(OwnerData.Empty, "Cabinet A", () => CabinetA).Data, "Shelf B", () => ShelfB).Data;
        var placed = LocationEdits.Assign(data, 100, CabinetA, "Example Game 100");

        var again = LocationEdits.Assign(placed.Data, 100, CabinetA, "Example Game 100");
        var moved = LocationEdits.Assign(placed.Data, 100, ShelfB, "Example Game 100");
        var cleared = LocationEdits.Assign(moved.Data, 100, null, "Example Game 100");

        again.IsNone.Should().BeTrue();
        moved.LogText.Should().Be("Moved Example Game 100 to Shelf B");
        cleared.LogText.Should().Be("Example Game 100 is now not placed");
        cleared.Data.Games.Should().BeEmpty();
        LocationEdits.Assign(moved.Data, 100, null, "Example Expansion 100", followsBase: true).LogText
            .Should().Be("Example Expansion 100 now follows its base game");
        LocationEdits.Assign(data, 100, "missing", "Example Game 100").Changed.Should().BeFalse();
    }

    private static OwnerData Data(params (int GameId, string LocationId)[] assignments) =>
        OwnerData.Empty with
        {
            Locations = [new OwnerLocation(CabinetA, "Cabinet A"), new OwnerLocation(ShelfB, "Shelf B")],
            Games = [.. assignments.OrderBy(pair => pair.GameId).Select(pair => new OwnerGame(pair.GameId, pair.LocationId, null, null))],
        };

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

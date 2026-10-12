using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>Proves renaming, reordering and deleting locations: what each edit keeps, what it refuses, how it words the log, and what a delete does to the games placed there.</summary>
[Trait("Category", "Owner")]
public sealed class LocationEditsTests
{
    private const string CabinetA = "loc1";
    private const string ShelfB = "loc2";
    private const string CrateC = "loc3";

    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_rename_keeps_the_identifier_and_the_position_and_logs_both_names()
    {
        var renamed = LocationEdits.Rename(ThreeLocations(), ShelfB, "  Shelf D  ");

        renamed.Changed.Should().BeTrue();
        renamed.StatusKey.Should().Be("location-renamed");
        renamed.LogText.Should().Be("Renamed \"Shelf B\" to \"Shelf D\"");
        renamed.Data.Locations.Should().Equal(
            new OwnerLocation(CabinetA, "Cabinet A"),
            new OwnerLocation(ShelfB, "Shelf D"),
            new OwnerLocation(CrateC, "Crate C"));
    }

    [Fact]
    public void Games_follow_a_rename_because_they_refer_to_the_identifier()
    {
        var data = ThreeLocations() with { Games = [new OwnerGame(100, ShelfB, null, null)] };

        var renamed = LocationEdits.Rename(data, ShelfB, "Shelf D");

        renamed.Data.Games.Should().Equal(data.Games);
    }

    [Fact]
    public void A_rename_follows_the_same_name_rules_as_an_add()
    {
        var data = ThreeLocations();

        LocationEdits.Rename(data, ShelfB, "   ").StatusKey.Should().Be("name-empty");
        LocationEdits.Rename(data, ShelfB, new string('x', OwnerData.MaxNameLength + 1)).StatusKey.Should().Be("name-too-long");
        LocationEdits.Rename(data, ShelfB, "cabinet a").StatusKey.Should().Be("name-taken");
        LocationEdits.Rename(data, ShelfB, "Shelf‮B").StatusKey.Should().Be("name-characters");
        LocationEdits.Rename(data, ShelfB, "cabinet a").Changed.Should().BeFalse();
    }

    [Fact]
    public void A_location_may_keep_its_own_name_or_change_only_its_case()
    {
        var data = ThreeLocations();

        var same = LocationEdits.Rename(data, ShelfB, " Shelf B ");
        var upper = LocationEdits.Rename(data, ShelfB, "SHELF B");

        same.IsNone.Should().BeTrue();
        upper.Changed.Should().BeTrue();
        upper.Data.Locations[1].Name.Should().Be("SHELF B");
    }

    [Fact]
    public void Renaming_a_location_that_is_not_in_the_list_is_refused()
    {
        var refused = LocationEdits.Rename(ThreeLocations(), "missing", "Anything");

        refused.Changed.Should().BeFalse();
        refused.IsNone.Should().BeFalse();
        refused.StatusKey.Should().Be("unknown-location");
    }

    [Fact]
    public void Moving_swaps_a_location_with_its_neighbour_and_logs_the_direction()
    {
        var down = LocationEdits.Move(ThreeLocations(), CabinetA, 1);
        var up = LocationEdits.Move(ThreeLocations(), CrateC, -1);

        down.LogText.Should().Be("Moved \"Cabinet A\" down");
        down.StatusKey.Should().Be("location-moved");
        down.Data.Locations.Select(location => location.Id).Should().Equal(ShelfB, CabinetA, CrateC);
        up.LogText.Should().Be("Moved \"Crate C\" up");
        up.Data.Locations.Select(location => location.Id).Should().Equal(CabinetA, CrateC, ShelfB);
    }

    [Fact]
    public void Moving_past_either_end_changes_nothing()
    {
        var data = ThreeLocations();

        LocationEdits.Move(data, CabinetA, -1).IsNone.Should().BeTrue();
        LocationEdits.Move(data, CrateC, 1).IsNone.Should().BeTrue();
    }

    [Fact]
    public void Moving_a_location_that_is_not_in_the_list_or_by_a_wrong_step_is_refused()
    {
        LocationEdits.Move(ThreeLocations(), "missing", 1).StatusKey.Should().Be("unknown-location");
        LocationEdits.Move(ThreeLocations(), CabinetA, 2).Changed.Should().BeFalse();
    }

    [Fact]
    public void Deleting_an_empty_location_removes_it_and_logs_it_without_a_count()
    {
        var deleted = LocationEdits.Delete(ThreeLocations(), null, ShelfB);

        deleted.Changed.Should().BeTrue();
        deleted.StatusKey.Should().Be("location-deleted");
        deleted.LogText.Should().Be("Deleted location \"Shelf B\"");
        deleted.GamesNotPlaced.Should().Be(0);
        deleted.Data.Locations.Select(location => location.Id).Should().Equal(CabinetA, CrateC);
    }

    [Fact]
    public void Deleting_a_location_with_games_makes_them_not_placed_and_reports_how_many()
    {
        var snapshot = Snapshot([Base(1, 100), Base(2, 101), Base(3, 102)]);
        var data = ThreeLocations() with
        {
            Games = [new OwnerGame(100, ShelfB, null, null), new OwnerGame(101, ShelfB, null, null), new OwnerGame(102, CabinetA, null, null)],
        };

        var deleted = LocationEdits.Delete(data, snapshot, ShelfB);

        deleted.GamesNotPlaced.Should().Be(2);
        deleted.LogText.Should().Be("Deleted location \"Shelf B\" (2 games now not placed)");
        deleted.Data.Games.Should().Equal(new OwnerGame(102, CabinetA, null, null));
        OwnerOverlay.From(snapshot, deleted.Data).EffectiveLocationId(100).Should().BeNull();
        OwnerOverlay.From(snapshot, deleted.Data).EffectiveLocationId(102).Should().Be(CabinetA);
    }

    [Fact]
    public void The_log_line_for_one_game_is_singular()
    {
        var snapshot = Snapshot([Base(1, 100)]);
        var data = ThreeLocations() with { Games = [new OwnerGame(100, ShelfB, null, null)] };

        LocationEdits.Delete(data, snapshot, ShelfB).LogText.Should().Be("Deleted location \"Shelf B\" (1 game now not placed)");
    }

    [Fact]
    public void An_expansion_that_follows_its_base_game_becomes_not_placed_with_it()
    {
        var snapshot = Snapshot([Base(1, 100), Expansion(2, 200)], (200, [Ref(100)]));
        var data = ThreeLocations() with { Games = [new OwnerGame(100, ShelfB, null, null)] };

        var deleted = LocationEdits.Delete(data, snapshot, ShelfB);

        deleted.GamesNotPlaced.Should().Be(2);
        OwnerOverlay.From(snapshot, deleted.Data).EffectiveLocationId(200).Should().BeNull();
    }

    [Fact]
    public void An_expansion_with_its_own_location_goes_back_to_following_its_base_game_and_only_counts_when_that_is_not_placed()
    {
        var snapshot = Snapshot([Base(1, 100), Expansion(2, 200)], (200, [Ref(100)]));
        var data = ThreeLocations() with
        {
            Games = [new OwnerGame(100, CabinetA, null, null), new OwnerGame(200, ShelfB, null, null)],
        };

        var deleted = LocationEdits.Delete(data, snapshot, ShelfB);

        deleted.GamesNotPlaced.Should().Be(0);
        deleted.Data.Games.Should().Equal(new OwnerGame(100, CabinetA, null, null));
        OwnerOverlay.From(snapshot, deleted.Data).EffectiveLocationId(200).Should().Be(CabinetA);
        OwnerOverlay.From(snapshot, deleted.Data).FollowsBaseGameId(200).Should().Be(100);
    }

    [Fact]
    public void A_deleted_location_keeps_the_picture_decision_of_the_game_that_was_placed_there()
    {
        var picture = new PictureSetting(PictureChoice.MainPicture, true, null, null, null);
        var data = ThreeLocations() with { Games = [new OwnerGame(100, ShelfB, null, picture)] };

        var deleted = LocationEdits.Delete(data, Snapshot([Base(1, 100)]), ShelfB);

        deleted.Data.Games.Should().Equal(new OwnerGame(100, null, null, picture));
    }

    [Fact]
    public void Deleting_a_location_that_is_not_in_the_list_is_refused()
    {
        var refused = LocationEdits.Delete(ThreeLocations(), null, "missing");

        refused.Changed.Should().BeFalse();
        refused.StatusKey.Should().Be("unknown-location");
    }

    [Fact]
    public void Counting_the_games_a_delete_would_leave_not_placed_matches_what_the_delete_reports()
    {
        var snapshot = Snapshot([Base(1, 100), Expansion(2, 200), Base(3, 300)], (200, [Ref(100)]));
        var data = ThreeLocations() with { Games = [new OwnerGame(100, ShelfB, null, null), new OwnerGame(300, CabinetA, null, null)] };

        LocationEdits.CountLeftNotPlaced(data, snapshot, ShelfB).Should().Be(LocationEdits.Delete(data, snapshot, ShelfB).GamesNotPlaced);
        LocationEdits.CountLeftNotPlaced(data, snapshot, CrateC).Should().Be(0);
        LocationEdits.CountLeftNotPlaced(data, null, ShelfB).Should().Be(0);
    }

    [Fact]
    public void Moving_many_games_to_a_location_is_one_change_with_one_log_line()
    {
        var data = ThreeLocations() with { Games = [new OwnerGame(100, CabinetA, null, null)] };

        var moved = LocationEdits.MoveMany(data, [100, 101], MoveTarget.Location(ShelfB), TitleOfKnown, NeverFollows);

        moved.Changed.Should().BeTrue();
        moved.StatusKey.Should().Be("game-moved");
        moved.LogText.Should().Be("Moved 2 games to Shelf B");
        moved.Data.Games.Should().Equal(new OwnerGame(100, ShelfB, null, null), new OwnerGame(101, ShelfB, null, null));
    }

    [Fact]
    public void Moving_one_game_says_one_game_and_listing_a_game_twice_counts_it_once()
    {
        var moved = LocationEdits.MoveMany(ThreeLocations(), [101, 101], MoveTarget.Location(ShelfB), TitleOfKnown, NeverFollows);

        moved.LogText.Should().Be("Moved 1 game to Shelf B");
        moved.Data.Games.Should().Equal(new OwnerGame(101, ShelfB, null, null));
    }

    [Fact]
    public void Moving_games_to_not_placed_clears_their_location_and_keeps_their_picture_decision()
    {
        var picture = new PictureSetting(PictureChoice.MainPicture, true, null, null, null);
        var data = ThreeLocations() with
        {
            Games = [new OwnerGame(100, ShelfB, null, picture), new OwnerGame(101, ShelfB, null, null)],
        };

        var moved = LocationEdits.MoveMany(data, [100, 101], MoveTarget.NotPlaced, TitleOfKnown, NeverFollows);

        moved.LogText.Should().Be("2 games are now not placed");
        moved.StatusKey.Should().Be("game-unplaced");
        moved.Data.Games.Should().Equal(new OwnerGame(100, null, null, picture));
    }

    [Fact]
    public void Same_as_base_game_clears_only_the_expansions_that_have_an_owned_base_and_leaves_the_others()
    {
        var data = ThreeLocations() with
        {
            Games = [new OwnerGame(100, CabinetA, null, null), new OwnerGame(200, ShelfB, null, null), new OwnerGame(202, ShelfB, null, null)],
        };

        var moved = LocationEdits.MoveMany(data, [100, 200, 202], MoveTarget.SameAsBase, TitleOfKnown, id => id == 200);

        moved.LogText.Should().Be("1 expansion now follows its base game");
        moved.Data.Games.Should().Equal(new OwnerGame(100, CabinetA, null, null), new OwnerGame(202, ShelfB, null, null));
    }

    [Fact]
    public void A_move_that_changes_nothing_is_the_none_answer()
    {
        var data = ThreeLocations() with { Games = [new OwnerGame(100, ShelfB, null, null)] };

        LocationEdits.MoveMany(data, [100], MoveTarget.Location(ShelfB), TitleOfKnown, NeverFollows).IsNone.Should().BeTrue();
        LocationEdits.MoveMany(data, [101], MoveTarget.NotPlaced, TitleOfKnown, NeverFollows).IsNone.Should().BeTrue();
        LocationEdits.MoveMany(data, [100], MoveTarget.SameAsBase, TitleOfKnown, NeverFollows).IsNone.Should().BeTrue();
    }

    [Fact]
    public void Clearing_expansions_and_other_games_together_says_both_things()
    {
        var data = ThreeLocations() with
        {
            Games = [new OwnerGame(100, ShelfB, null, null), new OwnerGame(200, ShelfB, null, null), new OwnerGame(201, ShelfB, null, null)],
        };

        var moved = LocationEdits.MoveMany(data, [100, 200, 201], MoveTarget.NotPlaced, TitleOfKnown, id => id >= 200);

        moved.LogText.Should().Be("1 game is now not placed and 2 expansions now follow their base game");
        moved.Data.Games.Should().BeEmpty();
    }

    [Fact]
    public void Unknown_games_an_unknown_location_and_an_empty_selection_are_refused()
    {
        var data = ThreeLocations();

        var unknownGame = LocationEdits.MoveMany(data, [100, 999], MoveTarget.Location(ShelfB), TitleOfKnown, NeverFollows);
        var unknownLocation = LocationEdits.MoveMany(data, [100], MoveTarget.Location("missing"), TitleOfKnown, NeverFollows);
        var nothing = LocationEdits.MoveMany(data, [], MoveTarget.NotPlaced, TitleOfKnown, NeverFollows);

        unknownGame.Changed.Should().BeFalse();
        unknownGame.IsNone.Should().BeFalse();
        unknownGame.StatusKey.Should().Be("unknown-game");
        unknownLocation.StatusKey.Should().Be("unknown-location");
        nothing.StatusKey.Should().Be("nothing-selected");
    }

    private static string? TitleOfKnown(int gameId) => gameId is 100 or 101 or 200 or 201 or 202 ? $"Example Game {gameId}" : null;

    private static bool NeverFollows(int gameId) => false;

    private static OwnerData ThreeLocations() =>
        OwnerData.Empty with
        {
            Locations = [new OwnerLocation(CabinetA, "Cabinet A"), new OwnerLocation(ShelfB, "Shelf B"), new OwnerLocation(CrateC, "Crate C")],
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

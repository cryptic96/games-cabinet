using Cabinet.Domain.Owner;
using Cabinet.Service.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>
/// Proves a game's owner data is forgotten only after the game was missing from accepted collections for thirty days, that a
/// returning game keeps what the owner decided, and that these housekeeping writes are not owner changes.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerExpiryTests
{
    private const string CabinetA = "Cabinet A";
    private const int Kept = 100;
    private const int Leaving = 101;
    private const int Game = 100;
    private const string ShelfB = "Shelf B";

    [Fact]
    public async Task A_game_missing_from_an_accepted_collection_is_stamped_kept_for_29_days_and_removed_on_the_30th()
    {
        var harness = await HarnessWithLeavingGamePlacedAsync();
        var firstWithout = harness.Clock.GetUtcNow();

        harness.Publisher.Publish(OwnerTestKit.Listing(Kept));
        var stamped = harness.Data.Games.Single(game => game.GameId == Leaving);
        harness.Clock.Advance(TimeSpan.FromDays(29));
        var savesBefore = harness.Store.Saved.Count;
        harness.Publisher.Publish(OwnerTestKit.Listing(Kept));
        var stillThere = harness.Data.Games.Single(game => game.GameId == Leaving);
        harness.Clock.Advance(TimeSpan.FromDays(1));
        harness.Publisher.Publish(OwnerTestKit.Listing(Kept));

        stamped.AbsentSinceUtc.Should().Be(firstWithout);
        stillThere.AbsentSinceUtc.Should().Be(firstWithout);
        harness.Store.Saved.Should().HaveCount(savesBefore + 1);
        harness.Data.Games.Should().NotContain(game => game.GameId == Leaving);
        harness.Data.Games.Should().Contain(game => game.GameId == Kept);
    }

    [Fact]
    public async Task A_collection_read_back_from_disk_neither_stamps_nor_removes_a_game()
    {
        var harness = await HarnessWithLeavingGamePlacedAsync();
        harness.Publisher.Publish(OwnerTestKit.Listing(Kept));
        harness.Clock.Advance(TimeSpan.FromDays(90));
        var savesBefore = harness.Store.Saved.Count;

        harness.Publisher.Publish(OwnerTestKit.Listing(Kept), reconcile: false);

        harness.Store.Saved.Should().HaveCount(savesBefore);
        harness.Data.Games.Should().Contain(game => game.GameId == Leaving);

        harness.Publisher.Publish(OwnerTestKit.Listing(Kept));

        harness.Data.Games.Should().NotContain(game => game.GameId == Leaving);
    }

    [Fact]
    public void After_a_restart_a_game_stamped_absent_31_days_ago_survives_until_a_fetched_collection_is_judged()
    {
        var picture = new PictureSetting(PictureChoice.MainPicture, true, null, null, null);
        var store = new InMemoryOwnerDataStore
        {
            FileExists = true,
            State = new OwnerDataState.Ready(OwnerData.Empty with
            {
                Revision = 3,
                Games = [new OwnerGame(Leaving, null, OwnerTestKit.Start.AddDays(-31), picture)],
            }),
        };
        var harness = OwnerTestKit.Harness(store);

        harness.Publisher.Publish(OwnerTestKit.Listing(Kept), reconcile: false);

        harness.Data.Games.Should().ContainSingle(game => game.GameId == Leaving);
        store.Saved.Should().BeEmpty();

        harness.Publisher.Publish(OwnerTestKit.Listing(Kept));

        harness.Data.Games.Should().NotContain(game => game.GameId == Leaving);
    }

    [Fact]
    public async Task A_game_that_returns_before_30_days_has_its_stamp_cleared_and_keeps_its_location()
    {
        var harness = await HarnessWithLeavingGamePlacedAsync();
        var locationId = harness.Data.Games.Single(game => game.GameId == Leaving).LocationId;
        harness.Publisher.Publish(OwnerTestKit.Listing(Kept));
        harness.Clock.Advance(TimeSpan.FromDays(20));

        harness.Publisher.Publish(OwnerTestKit.Listing(Kept, Leaving));
        harness.Clock.Advance(TimeSpan.FromDays(60));
        harness.Publisher.Publish(OwnerTestKit.Listing(Kept, Leaving));

        var game = harness.Data.Games.Single(entry => entry.GameId == Leaving);
        game.AbsentSinceUtc.Should().BeNull();
        game.LocationId.Should().Be(locationId).And.NotBeNull();
    }

    [Fact]
    public void A_game_that_returns_keeps_its_picture_choice_and_the_clock_starts_over_when_it_leaves_again()
    {
        var picture = new PictureSetting(PictureChoice.MainPicture, true, null, null, null);
        var start = OwnerTestKit.Start;
        var store = new InMemoryOwnerDataStore
        {
            FileExists = true,
            State = new OwnerDataState.Ready(OwnerData.Empty with
            {
                Revision = 3,
                Games = [new OwnerGame(Leaving, null, start.AddDays(-10), picture)],
            }),
        };
        var harness = OwnerTestKit.Harness(store, OwnerTestKit.Listing(Kept, Leaving));
        harness.Data.Games.Single().Should().Be(new OwnerGame(Leaving, null, null, picture));

        harness.Clock.Advance(TimeSpan.FromDays(25));
        harness.Publisher.Publish(OwnerTestKit.Listing(Kept));
        harness.Clock.Advance(TimeSpan.FromDays(29));
        harness.Publisher.Publish(OwnerTestKit.Listing(Kept));

        harness.Data.Games.Single().Should().Be(new OwnerGame(Leaving, null, start.AddDays(25), picture));
    }

    [Fact]
    public void Nothing_is_stamped_or_removed_while_the_owner_data_cannot_be_read()
    {
        var store = new InMemoryOwnerDataStore { FileExists = true, State = new OwnerDataState.Unreadable("malformed") };
        var harness = OwnerTestKit.Harness(store, OwnerTestKit.Listing(Kept, Leaving));

        harness.Publisher.Publish(OwnerTestKit.Listing(Kept));
        harness.Clock.Advance(TimeSpan.FromDays(90));
        harness.Publisher.Publish(OwnerTestKit.Listing(Kept));

        store.Saved.Should().BeEmpty();
        store.KeptAt.Should().BeEmpty();
        harness.Service.State.Should().BeOfType<OwnerDataState.Unreadable>();
    }

    [Fact]
    public async Task The_housekeeping_write_adds_no_log_entry_no_count_and_no_revision_and_ends_the_chance_to_undo()
    {
        var harness = await HarnessWithLeavingGamePlacedAsync();
        var revision = harness.Service.Revision;
        var changes = harness.Data.Changes.Count;
        var counted = harness.Data.ChangesSinceDownload;
        harness.Service.UndoableRevision.Should().Be(revision);

        harness.Publisher.Publish(OwnerTestKit.Listing(Kept));

        harness.Service.Revision.Should().Be(revision);
        harness.Data.Changes.Should().HaveCount(changes);
        harness.Data.ChangesSinceDownload.Should().Be(counted);
        harness.Service.UndoableRevision.Should().BeNull();
        harness.Data.Games.Single(game => game.GameId == Leaving).AbsentSinceUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task A_collection_that_still_lists_every_game_writes_nothing()
    {
        var harness = await HarnessWithLeavingGamePlacedAsync();
        var saves = harness.Store.Saved.Count;
        var undoable = harness.Service.UndoableRevision;

        harness.Publisher.Publish(OwnerTestKit.Listing(Kept, Leaving));

        harness.Store.Saved.Should().HaveCount(saves);
        harness.Service.UndoableRevision.Should().Be(undoable);
    }

    [Fact]
    public async Task The_housekeeping_does_not_count_against_a_writer_working_from_the_current_revision()
    {
        var harness = await HarnessWithLeavingGamePlacedAsync();
        var revision = harness.Service.Revision;
        harness.Publisher.Publish(OwnerTestKit.Listing(Kept));

        var added = await harness.Service.MutateAsync(
            revision,
            data => LocationEdits.Add(data, "Shelf B", OwnerDataService.NewId),
            CancellationToken.None);

        added.Outcome.Should().Be(OwnerOutcome.Saved);
        added.Data!.Games.Single(game => game.GameId == Leaving).AbsentSinceUtc.Should().NotBeNull();
    }

    [Fact]
    public void A_per_copy_choice_is_stamped_when_its_entry_is_not_listed_cleared_when_listed_again_and_dropped_after_30_days()
    {
        var data = DataWith(new OwnerGame(Game, ShelfB, null, null, 7002));
        var now = OwnerTestKit.Start;

        var stamped = OwnerExpiry.Reconcile(data, Games(Game, Leaving), Copies((7001, Leaving)), now)!;
        var cleared = OwnerExpiry.Reconcile(stamped, Games(Game), Copies((7001, Leaving), (7002, Game)), now.AddDays(5))!;
        var restamped = OwnerExpiry.Reconcile(cleared, Games(Game, Leaving), Copies((7001, Leaving)), now.AddDays(6))!;
        var forgotten = OwnerExpiry.Reconcile(restamped, Games(Game, Leaving), Copies((7001, Leaving)), now.AddDays(36))!;

        stamped.Games.Should().ContainSingle().Which.AbsentSinceUtc.Should().Be(now);
        cleared.Games.Should().ContainSingle().Which.AbsentSinceUtc.Should().BeNull();
        restamped.Games.Should().ContainSingle().Which.AbsentSinceUtc.Should().Be(now.AddDays(6));
        forgotten.Games.Should().BeEmpty();
    }

    [Fact]
    public void A_game_wide_choice_is_listed_while_its_game_is_whichever_entries_the_game_has()
    {
        var data = DataWith(new OwnerGame(Game, ShelfB, null, null));

        var withNewEntry = OwnerExpiry.Reconcile(data, Games(Game), Copies((7009, Game)), OwnerTestKit.Start);
        var withoutGame = OwnerExpiry.Reconcile(data, Games(), Copies(), OwnerTestKit.Start);

        withNewEntry.Should().BeNull();
        withoutGame!.Games.Should().ContainSingle().Which.AbsentSinceUtc.Should().Be(OwnerTestKit.Start);
    }

    [Fact]
    public void A_re_added_copy_takes_over_the_absent_copys_choices()
    {
        var picture = new PictureSetting(PictureChoice.MainPicture, true, null, null, null);
        var data = DataWith(new OwnerGame(Game, ShelfB, null, picture, 7002));

        var result = OwnerExpiry.Reconcile(data, Games(Game), Copies((7009, Game)), OwnerTestKit.Start)!;

        result.Games.Should().ContainSingle().Which.Should().Be(new OwnerGame(Game, ShelfB, null, picture, 7009));
    }

    [Fact]
    public void A_stamped_copys_choices_are_handed_over_while_they_are_still_kept_and_not_after_they_expire()
    {
        var stamped = OwnerTestKit.Start.AddDays(-10);
        var expired = OwnerTestKit.Start.AddDays(-30);

        var kept = OwnerExpiry.Reconcile(DataWith(new OwnerGame(Game, ShelfB, stamped, null, 7002)), Games(Game), Copies((7009, Game)), OwnerTestKit.Start)!;
        var lost = OwnerExpiry.Reconcile(DataWith(new OwnerGame(Game, ShelfB, expired, null, 7002)), Games(Game), Copies((7009, Game)), OwnerTestKit.Start)!;

        kept.Games.Should().ContainSingle().Which.Should().Be(new OwnerGame(Game, ShelfB, null, null, 7009));
        lost.Games.Should().BeEmpty();
    }

    [Fact]
    public void With_two_new_copies_the_lowest_new_collection_id_takes_the_choices()
    {
        var data = DataWith(new OwnerGame(Game, ShelfB, null, null, 7002));

        var result = OwnerExpiry.Reconcile(data, Games(Game), Copies((7011, Game), (7009, Game)), OwnerTestKit.Start)!;

        result.Games.Should().ContainSingle().Which.CollectionId.Should().Be(7009);
    }

    [Fact]
    public void A_new_copy_that_already_has_its_own_choice_is_left_alone_and_the_absent_copy_waits_its_30_days()
    {
        var own = new OwnerGame(Game, "Cabinet A", null, null, 7009);
        var data = DataWith(new OwnerGame(Game, ShelfB, null, null, 7002), own);

        var result = OwnerExpiry.Reconcile(data, Games(Game), Copies((7009, Game)), OwnerTestKit.Start)!;

        result.Games.Should().Equal(
            new OwnerGame(Game, ShelfB, OwnerTestKit.Start, null, 7002),
            own);
    }

    [Fact]
    public void A_new_copy_covered_by_a_game_wide_choice_takes_nothing_over()
    {
        var wide = new OwnerGame(Game, "Cabinet A", null, null);
        var data = DataWith(wide, new OwnerGame(Game, ShelfB, null, null, 7002));

        var result = OwnerExpiry.Reconcile(data, Games(Game), Copies((7009, Game)), OwnerTestKit.Start)!;

        result.Games.Should().Equal(wide, new OwnerGame(Game, ShelfB, OwnerTestKit.Start, null, 7002));
    }

    [Fact]
    public void A_choice_is_never_handed_to_a_copy_of_another_game()
    {
        var data = DataWith(new OwnerGame(Game, ShelfB, null, null, 7002));

        var result = OwnerExpiry.Reconcile(data, Games(Game, Leaving), Copies((7009, Leaving), (7001, Game)), OwnerTestKit.Start)!;

        result.Games.Should().Equal(
            new OwnerGame(Game, ShelfB, null, null, 7001));
    }

    [Fact]
    public async Task A_copy_removed_and_added_again_on_BGG_comes_back_to_its_shelf_on_the_next_sync()
    {
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Snapshot([OwnerTestKit.Base(7002, Game, "Game 100")]));
        await harness.AddAsync("Shelf B");
        var shelf = harness.Data.Locations.Single().Id;
        await harness.Service.MutateAsync(
            harness.Service.Revision,
            data => LocationEdits.Assign(data, Game, 7002, [7002], shelf, "Game 100", false),
            CancellationToken.None);

        harness.Publisher.Publish(OwnerTestKit.Snapshot([OwnerTestKit.Base(7001, Leaving, "Game 101")]));
        harness.Clock.Advance(TimeSpan.FromDays(2));
        harness.Publisher.Publish(OwnerTestKit.Snapshot([OwnerTestKit.Base(7001, Leaving, "Game 101"), OwnerTestKit.Base(7009, Game, "Game 100")]));

        harness.Data.Games.Should().ContainSingle(entry => entry.GameId == Game)
            .Which.Should().Match<OwnerGame>(entry => entry.CollectionId == 7009 && entry.LocationId == shelf && entry.AbsentSinceUtc == null);
    }

    private static async Task<OwnerHarness> HarnessWithLeavingGamePlacedAsync()
    {
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Listing(Kept, Leaving));
        await harness.AddAsync(CabinetA);
        await harness.PlaceAsync(Leaving, CabinetA);
        await harness.PlaceAsync(Kept, CabinetA);

        return harness;
    }

    private static OwnerData DataWith(params OwnerGame[] games) => OwnerData.Empty with { Games = OwnerGames.Ordered(games) };

    private static HashSet<int> Games(params int[] gameIds) => [.. gameIds];

    private static Dictionary<long, int> Copies(params (long CollectionId, int GameId)[] entries) =>
        entries.ToDictionary(entry => entry.CollectionId, entry => entry.GameId);
}

using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>
/// Proves that no edit of a picture loses where a copy is kept: for a game owned twice with each copy in a different location,
/// every public edit leaves both copies in the places they were.
/// </summary>
[Trait("Category", "Owner")]
public sealed class PictureEditsKeepCopiesTests
{
    private const string CabinetA = "loc1";
    private const string ShelfB = "loc2";
    private const string OwnedUrl = "https://cf.example.org/owned.jpg";
    private const string MainUrl = "https://cf.example.org/main.jpg";
    private const string LinkUrl = "https://cf.example.org/link.jpg";
    private const int GameId = 100;
    private const long FirstCopy = 7001;
    private const long SecondCopy = 7002;
    private static readonly DateTimeOffset Moment = new(2030, 1, 10, 0, 0, 0, TimeSpan.Zero);
    private static readonly long[] Copies = [FirstCopy, SecondCopy];

    [Fact]
    public void Choosing_a_picture_for_every_copy_keeps_both_copies_where_they_are()
    {
        var change = PictureEdits.Choose(CopiesInTwoPlaces(), GameId, PictureChoice.MainPicture, "Quiet Quarry", OwnedUrl, MainUrl);

        change.Changed.Should().BeTrue();
        AssertPlacesKept(change.Data);
    }

    [Fact]
    public void Choosing_a_picture_for_one_copy_keeps_both_copies_where_they_are()
    {
        var change = PictureEdits.Choose(CopiesInTwoPlaces(), GameId, SecondCopy, Copies, PictureChoice.MainPicture, "Quiet Quarry", OwnedUrl, MainUrl);

        change.Changed.Should().BeTrue();
        AssertPlacesKept(change.Data);
    }

    [Fact]
    public void Choosing_a_picture_for_one_copy_when_the_game_has_a_game_wide_entry_keeps_both_copies_where_they_are()
    {
        var change = PictureEdits.Choose(GameWideInOnePlace(), GameId, SecondCopy, Copies, PictureChoice.MainPicture, "Quiet Quarry", OwnedUrl, MainUrl);

        change.Changed.Should().BeTrue();
        change.Data.Games.Select(game => (game.CollectionId, game.LocationId)).Should().Equal((FirstCopy, CabinetA), (SecondCopy, CabinetA));
    }

    [Fact]
    public void Linking_a_picture_keeps_both_copies_where_they_are()
    {
        var change = PictureEdits.UseLink(CopiesInTwoPlaces(), GameId, LinkUrl, WorkingRecord(), "Quiet Quarry", OwnedUrl, MainUrl);

        change.Changed.Should().BeTrue();
        AssertPlacesKept(change.Data);
        change.Data.Games.Where(game => game.CollectionId is not null).Select(game => game.Picture!.Choice).Should().OnlyContain(choice => choice == PictureChoice.Link);
    }

    [Fact]
    public void Linking_a_picture_for_one_copy_keeps_both_copies_where_they_are_and_links_only_that_copy()
    {
        var change = PictureEdits.UseLink(CopiesInTwoPlaces(), GameId, SecondCopy, Copies, LinkUrl, WorkingRecord(), "Quiet Quarry", OwnedUrl, MainUrl);

        change.Changed.Should().BeTrue();
        AssertPlacesKept(change.Data);
        change.Data.Games.Single(game => game.CollectionId == SecondCopy).Picture!.Link!.Url.Should().Be(LinkUrl);
        change.Data.Games.Single(game => game.CollectionId == FirstCopy).Picture.Should().BeNull();
    }

    [Fact]
    public void Linking_a_picture_for_one_copy_when_the_game_has_a_game_wide_entry_keeps_both_copies_where_they_are()
    {
        var change = PictureEdits.UseLink(GameWideInOnePlace(), GameId, SecondCopy, Copies, LinkUrl, WorkingRecord(), "Quiet Quarry", OwnedUrl, MainUrl);

        change.Changed.Should().BeTrue();
        change.Data.Games.Select(game => (game.CollectionId, game.LocationId)).Should().Equal((FirstCopy, CabinetA), (SecondCopy, CabinetA));
        change.Data.Games.Single(game => game.CollectionId == SecondCopy).Picture!.Choice.Should().Be(PictureChoice.Link);
    }

    [Fact]
    public void Linking_a_picture_for_one_copy_without_a_usable_record_is_refused()
    {
        var unusable = WorkingRecord() with { Status = ImageStatus.Failed };

        var change = PictureEdits.UseLink(CopiesInTwoPlaces(), GameId, SecondCopy, Copies, LinkUrl, unusable, "Quiet Quarry", OwnedUrl, MainUrl);

        change.Changed.Should().BeFalse();
        change.StatusKey.Should().Be("no-link");
    }

    [Fact]
    public void Removing_the_link_of_one_copy_keeps_both_copies_where_they_are()
    {
        var linked = PictureEdits.UseLink(CopiesInTwoPlaces(), GameId, SecondCopy, Copies, LinkUrl, WorkingRecord(), "Quiet Quarry", OwnedUrl, MainUrl).Data;

        var change = PictureEdits.RemoveLink(linked, GameId, SecondCopy, Copies, "Quiet Quarry");

        change.Changed.Should().BeTrue();
        AssertPlacesKept(change.Data);
        change.Data.Games.Should().OnlyContain(game => game.Picture == null);
    }

    [Fact]
    public void Removing_the_link_keeps_both_copies_where_they_are()
    {
        var linked = PictureEdits.UseLink(CopiesInTwoPlaces(), GameId, LinkUrl, WorkingRecord(), "Quiet Quarry", OwnedUrl, MainUrl).Data;

        var change = PictureEdits.RemoveLink(linked, GameId, "Quiet Quarry");

        change.Changed.Should().BeTrue();
        AssertPlacesKept(change.Data);
        change.Data.Games.Should().OnlyContain(game => game.Picture == null);
    }

    [Fact]
    public void Marking_a_game_as_checked_keeps_both_copies_where_they_are()
    {
        var change = PictureEdits.LooksRight(CopiesInTwoPlaces(), GameId, "Quiet Quarry", OwnedUrl, MainUrl, Moment);

        change.Changed.Should().BeTrue();
        AssertPlacesKept(change.Data);
    }

    [Fact]
    public void Marking_one_copy_as_checked_keeps_both_copies_where_they_are()
    {
        var change = PictureEdits.LooksRight(CopiesInTwoPlaces(), GameId, SecondCopy, Copies, "Quiet Quarry", OwnedUrl, MainUrl, Moment);

        change.Changed.Should().BeTrue();
        AssertPlacesKept(change.Data);
    }

    [Fact]
    public void An_edit_of_one_game_leaves_the_entries_of_other_games_as_they_are()
    {
        var other = new OwnerGame(200, ShelfB, null, null);
        var data = CopiesInTwoPlaces() with { Games = OwnerGames.Ordered([.. CopiesInTwoPlaces().Games, other]) };

        var change = PictureEdits.Choose(data, GameId, PictureChoice.MainPicture, "Quiet Quarry", OwnedUrl, MainUrl);

        change.Data.Games.Should().Contain(other);
    }

    private static void AssertPlacesKept(OwnerData after)
    {
        after.Games.Where(game => game.CollectionId == FirstCopy).Select(game => game.LocationId).Should().Equal(CabinetA);
        after.Games.Where(game => game.CollectionId == SecondCopy).Select(game => game.LocationId).Should().Equal(ShelfB);
        after.Games.Where(game => game.CollectionId is null).Select(game => game.LocationId).Should().OnlyContain(location => location == null);

        var overlay = OwnerOverlay.From(Snapshot(), after);

        overlay.EntryLocationId(FirstCopy).Should().Be(CabinetA);
        overlay.EntryLocationId(SecondCopy).Should().Be(ShelfB);
    }

    private static OwnerData CopiesInTwoPlaces() =>
        OwnerData.Empty with
        {
            Locations = [new OwnerLocation(CabinetA, "Cabinet A"), new OwnerLocation(ShelfB, "Shelf B")],
            Games =
            [
                new OwnerGame(GameId, CabinetA, null, null, FirstCopy),
                new OwnerGame(GameId, ShelfB, null, null, SecondCopy),
            ],
        };

    private static OwnerData GameWideInOnePlace() =>
        OwnerData.Empty with
        {
            Locations = [new OwnerLocation(CabinetA, "Cabinet A"), new OwnerLocation(ShelfB, "Shelf B")],
            Games = [new OwnerGame(GameId, CabinetA, null, null)],
        };

    private static CollectionSnapshot Snapshot() =>
        new(
            CollectionSnapshot.CurrentSchemaVersion,
            Moment,
            [.. Copies.Select(copy => new SnapshotItem(copy, GameId, "Quiet Quarry", ItemKind.Base, 2015, null, null))],
            null,
            null);

    private static ImageRecord WorkingRecord() =>
        new(LinkUrl, ImageStatus.Ok, Moment, [new ArtFile(240, 320, "0123456789abcdef-240.webp")], new ArtFeatures(0.0, 1.0, 0.0, 0.0, 4));
}

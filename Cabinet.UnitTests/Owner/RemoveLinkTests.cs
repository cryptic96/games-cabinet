using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>Proves the edit that removes a game's picture link: the link and its record go, a choice of the link falls back to Automatic, the change is logged in plain words, and an edit with nothing to remove changes nothing.</summary>
[Trait("Category", "Owner")]
public sealed class RemoveLinkTests
{
    private const string LinkUrl = "https://cf.example.org/pictures/link-shot.jpg";
    private const string OwnedUrl = "https://cf.example.org/owned.jpg";
    private const string MainUrl = "https://cf.example.org/main.jpg";
    private const int GameId = 7;
    private const long FirstCopy = 7001;
    private const long SecondCopy = 7002;
    private static readonly long[] Copies = [FirstCopy, SecondCopy];
    private static readonly DateTimeOffset Moment = new(2030, 1, 10, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Removing_the_link_that_is_the_choice_drops_the_link_and_its_record_and_falls_back_to_Automatic()
    {
        var data = With(PictureChoice.Link, isChecked: true, WorkingLink());

        var change = PictureEdits.RemoveLink(data, GameId, "Invented Game");

        change.Changed.Should().BeTrue();
        change.StatusKey.Should().Be("link-removed");
        change.LogText.Should().Be("Removed the picture link of Invented Game");
        change.Data.Games.Should().BeEmpty("nothing the owner decided is left for the game");
    }

    [Fact]
    public void Removing_a_link_that_is_not_the_choice_keeps_the_choice_and_the_remembered_addresses()
    {
        var data = With(PictureChoice.MainPicture, isChecked: true, WorkingLink());

        var change = PictureEdits.RemoveLink(data, GameId, "Invented Game");

        var setting = change.Data.Games.Single().Picture!;
        setting.Link.Should().BeNull();
        setting.Choice.Should().Be(PictureChoice.MainPicture);
        setting.Checked.Should().BeTrue();
        setting.SeenOwnedUrl.Should().Be(OwnedUrl);
        setting.SeenMainUrl.Should().Be(MainUrl);
    }

    [Fact]
    public void Removing_the_link_keeps_the_games_location()
    {
        var data = OwnerData.Empty with
        {
            Games = [new OwnerGame(GameId, "loc", null, new PictureSetting(PictureChoice.Link, true, OwnedUrl, MainUrl, WorkingLink()))],
        };

        var change = PictureEdits.RemoveLink(data, GameId, "Invented Game");

        change.Data.Games.Single().LocationId.Should().Be("loc");
        change.Data.Games.Single().Picture.Should().BeNull();
    }

    [Fact]
    public void A_link_that_was_never_fetched_can_be_removed_too()
    {
        var data = With(PictureChoice.Link, isChecked: true, new PictureLink(LinkUrl, null, 0, null));

        PictureEdits.RemoveLink(data, GameId, "Invented Game").Data.Games.Should().BeEmpty();
    }

    [Fact]
    public void A_game_without_a_link_has_nothing_to_remove()
    {
        PictureEdits.RemoveLink(OwnerData.Empty, GameId, "Invented Game").IsNone.Should().BeTrue();
        PictureEdits.RemoveLink(With(PictureChoice.MainPicture, true, null), GameId, "Invented Game").IsNone.Should().BeTrue();
    }

    [Fact]
    public void The_data_that_was_given_is_left_as_it_was()
    {
        var data = With(PictureChoice.Link, isChecked: true, WorkingLink());

        _ = PictureEdits.RemoveLink(data, GameId, "Invented Game");

        data.Games.Single().Picture!.Link.Should().NotBeNull();
    }

    [Fact]
    public void Removing_the_link_of_one_copy_leaves_the_link_of_the_other_copy_and_both_places()
    {
        var data = OwnerData.Empty with
        {
            Games =
            [
                new OwnerGame(GameId, "loc1", null, new PictureSetting(PictureChoice.Link, true, OwnedUrl, MainUrl, WorkingLink()), FirstCopy),
                new OwnerGame(GameId, "loc2", null, new PictureSetting(PictureChoice.Link, true, OwnedUrl, MainUrl, WorkingLink()), SecondCopy),
            ],
        };

        var change = PictureEdits.RemoveLink(data, GameId, SecondCopy, Copies, "Invented Game");

        change.Changed.Should().BeTrue();
        change.LogText.Should().Be("Removed the picture link of Invented Game (copy 2 of 2)");
        change.Data.Games.Single(game => game.CollectionId == FirstCopy).Picture!.Link.Should().NotBeNull();
        change.Data.Games.Single(game => game.CollectionId == SecondCopy).Picture.Should().BeNull();
        change.Data.Games.Select(game => game.LocationId).Should().Equal("loc1", "loc2");
    }

    [Fact]
    public void Removing_the_link_of_one_copy_when_the_link_holds_for_every_copy_splits_it_and_keeps_the_other_copys_link()
    {
        var data = With(PictureChoice.Link, isChecked: true, WorkingLink());

        var change = PictureEdits.RemoveLink(data, GameId, SecondCopy, Copies, "Invented Game");

        change.Changed.Should().BeTrue();
        change.Data.Games.Where(game => game.Picture?.Link is not null).Select(game => game.CollectionId).Should().Equal(FirstCopy);
    }

    [Fact]
    public void A_copy_without_a_link_has_nothing_to_remove_even_when_another_copy_has_one()
    {
        var data = OwnerData.Empty with
        {
            Games = [new OwnerGame(GameId, null, null, new PictureSetting(PictureChoice.Link, true, OwnedUrl, MainUrl, WorkingLink()), FirstCopy)],
        };

        PictureEdits.RemoveLink(data, GameId, SecondCopy, Copies, "Invented Game").IsNone.Should().BeTrue();
    }

    private static OwnerData With(PictureChoice choice, bool isChecked, PictureLink? link) =>
        OwnerData.Empty with { Games = [new OwnerGame(GameId, null, null, new PictureSetting(choice, isChecked, OwnedUrl, MainUrl, link))] };

    private static PictureLink WorkingLink() =>
        new(
            LinkUrl,
            new ImageRecord(LinkUrl, ImageStatus.Ok, Moment, [new ArtFile(240, 320, "0123456789abcdef-240.webp")], new ArtFeatures(0.0, 1.0, 0.0, 0.0, 4)),
            0,
            null);
}

using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;
using Cabinet.Service.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>
/// Proves what the art clean-up is told about the pictures the owner keeps: every file of every stored link, whichever picture
/// the game is drawn from now, nothing at all while the stored file cannot be read, and that a system write which changes what
/// visitors see announces itself without counting as an owner change.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerArtReferencesTests
{
    private const string Address = "https://cf.example.org/owner/linked.png";

    [Fact]
    public void Every_file_of_every_stored_link_is_referenced_whichever_picture_the_game_is_drawn_from()
    {
        var harness = Harness(
            Game(1, PictureChoice.Link, Linked("aaaaaaaaaaaaaaaa-480.webp", "aaaaaaaaaaaaaaaa-240.webp")),
            Game(2, PictureChoice.MainPicture, Linked("bbbbbbbbbbbbbbbb-240.webp")),
            Game(3, PictureChoice.Link, new PictureLink(Address, null, 0, null)),
            new OwnerGame(4, null, null, null));

        var referenced = harness.Service.ReferencedArtFiles();

        referenced.Should().BeEquivalentTo(["aaaaaaaaaaaaaaaa-240.webp", "aaaaaaaaaaaaaaaa-480.webp", "bbbbbbbbbbbbbbbb-240.webp"]);
    }

    [Fact]
    public void Nothing_is_referenced_when_the_owner_kept_no_link()
    {
        var harness = OwnerTestKit.Harness();

        harness.Service.ReferencedArtFiles().Should().BeEmpty();
    }

    [Fact]
    public void While_the_stored_file_cannot_be_read_the_answer_is_unknown_not_empty()
    {
        var store = new InMemoryOwnerDataStore { State = new OwnerDataState.Unreadable("malformed") };
        var harness = OwnerTestKit.Harness(store);

        harness.Service.ReferencedArtFiles().Should().BeNull();
    }

    [Fact]
    public async Task A_system_write_that_asks_to_announce_raises_changed_once_without_counting_as_an_owner_change()
    {
        var harness = Harness(Game(1, PictureChoice.Link, new PictureLink(Address, null, 0, null)));
        var raised = 0;
        harness.Service.Changed += () => raised++;
        var before = harness.Data;

        await harness.Service.MutateSystemAsync(
            data => data with { Games = [.. data.Games.Select(game => game with { Picture = game.Picture! with { Link = Linked("cccccccccccccccc-240.webp") } })] },
            CancellationToken.None,
            announce: true);

        raised.Should().Be(1);
        harness.Data.Revision.Should().Be(before.Revision);
        harness.Data.Changes.Should().Equal(before.Changes);
        harness.Data.ChangesSinceDownload.Should().Be(before.ChangesSinceDownload);
        harness.Service.ReferencedArtFiles().Should().BeEquivalentTo(["cccccccccccccccc-240.webp"]);
    }

    [Fact]
    public async Task A_system_write_stays_quiet_unless_it_asks_to_announce()
    {
        var harness = Harness(Game(1, PictureChoice.Automatic, null));
        var raised = 0;
        harness.Service.Changed += () => raised++;

        await harness.Service.MutateSystemAsync(data => data with { Finish = "walnut" }, CancellationToken.None);

        raised.Should().Be(0);
    }

    [Fact]
    public void Asking_for_link_work_raises_the_request_event()
    {
        var harness = OwnerTestKit.Harness();
        var raised = 0;
        harness.Service.LinkWorkRequested += () => raised++;

        harness.Service.RequestLinkWork();

        raised.Should().Be(1);
    }

    private static OwnerHarness Harness(params OwnerGame[] games)
    {
        var data = OwnerData.Empty with { Revision = 3, Games = games };

        return OwnerTestKit.Harness(new InMemoryOwnerDataStore { State = new OwnerDataState.Ready(data) });
    }

    private static OwnerGame Game(int gameId, PictureChoice choice, PictureLink? link) =>
        new(gameId, null, null, new PictureSetting(choice, true, null, null, link));

    private static PictureLink Linked(params string[] files) =>
        new(
            Address,
            new ImageRecord(
                Address,
                ImageStatus.Ok,
                OwnerTestKit.Start,
                [.. files.Select((name, index) => new ArtFile(480 - (index * 240), 640, name))]),
            0,
            null);
}

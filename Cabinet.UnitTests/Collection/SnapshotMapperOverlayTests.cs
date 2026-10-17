using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Collection;

/// <summary>
/// Proves that a picture the owner chose is drawn in place of the automatic pick when it can be, that the box keeps its shape
/// rules under the chosen picture, and that the choice counts in the collection version.
/// </summary>
[Trait("Category", "Snapshot")]
public sealed class SnapshotMapperOverlayTests
{
    private const string VersionUrl = "https://cf.example.org/version.jpg";
    private const string MainUrl = "https://cf.example.org/main.jpg";
    private const string LinkUrl = "https://cf.example.org/link.jpg";
    private const int GameId = 2;
    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly ArtFeatures FlatFeatures = new(0.0, 1.0, 0.0, 0.0, 4);
    private static readonly ArtFeatures UnsureFeatures = new(0.2, 0.95, 0.30, 0.20, 2);
    private static readonly ArtFeatures ThreeDFeatures = new(0.3, 0.70, 0.60, 0.50, 1);

    [Fact]
    public void Choosing_the_owned_edition_draws_it_even_when_the_automatic_pick_is_the_main_picture()
    {
        var snapshot = Snapshot(
            Item(),
            Record(VersionUrl, "1111111111111111", 600, 800, ThreeDFeatures),
            Record(MainUrl, "2222222222222222", 600, 800, FlatFeatures));

        var plain = SnapshotMapper.Explain(snapshot, ArtRules.Default).Single();
        var chosen = SnapshotMapper.Explain(snapshot, ArtRules.Default, OverlayOf(PictureChoice.OwnedEdition)).Single();

        plain.Pick.Should().Be(ArtPick.MainImage);
        plain.PickedByOwner.Should().BeFalse();
        chosen.Pick.Should().Be(ArtPick.VersionImage);
        chosen.PickedByOwner.Should().BeTrue();
        chosen.Mapped.Art!.Variants[0].Url.Should().Be("/art/1111111111111111-600.webp");
    }

    [Fact]
    public void Choosing_the_main_picture_draws_it_and_takes_the_spine_colour_from_it()
    {
        var snapshot = Snapshot(
            Item(),
            Record(VersionUrl, "1111111111111111", 600, 800, FlatFeatures, "#102030"),
            Record(MainUrl, "2222222222222222", 600, 800, FlatFeatures, "#301020"));

        var chosen = SnapshotMapper.Explain(snapshot, ArtRules.Default, OverlayOf(PictureChoice.MainPicture)).Single();

        chosen.Pick.Should().Be(ArtPick.MainImage);
        chosen.Mapped.Art!.Variants[0].Url.Should().Be("/art/2222222222222222-600.webp");
        chosen.Mapped.Colour!.Background.Should().Be("#301020");
    }

    [Fact]
    public void A_stored_link_that_is_usable_is_drawn_as_the_owner_link()
    {
        var snapshot = Snapshot(Item(), Record(VersionUrl, "1111111111111111", 600, 800, FlatFeatures));
        var link = Record(LinkUrl, "3333333333333333", 600, 800, UnsureFeatures, "#103020");

        var chosen = SnapshotMapper.Explain(snapshot, ArtRules.Default, OverlayOf(PictureChoice.Link, link)).Single();

        chosen.Pick.Should().Be(ArtPick.OwnerLink);
        chosen.PickedByOwner.Should().BeTrue();
        chosen.Mapped.Art!.Variants[0].Url.Should().Be("/art/3333333333333333-600.webp");
        chosen.Mapped.Colour!.Background.Should().Be("#103020");
    }

    [Fact]
    public void A_chosen_kind_with_no_usable_picture_gives_the_automatic_pick()
    {
        var onlyMain = Snapshot(Item(), Record(MainUrl, "2222222222222222", 600, 800, FlatFeatures));
        var failedVersion = Snapshot(
            Item(),
            new ImageRecord(VersionUrl, ImageStatus.Failed, Moment),
            Record(MainUrl, "2222222222222222", 600, 800, FlatFeatures));
        var failedLink = new ImageRecord(LinkUrl, ImageStatus.Failed, Moment);

        AssertUnchanged(onlyMain, OverlayOf(PictureChoice.OwnedEdition));
        AssertUnchanged(failedVersion, OverlayOf(PictureChoice.OwnedEdition));
        AssertUnchanged(onlyMain, OverlayOf(PictureChoice.Link, failedLink));
        AssertUnchanged(onlyMain, OverlayOf(PictureChoice.Link));
        AssertUnchanged(Snapshot(Item()), OverlayOf(PictureChoice.MainPicture));
    }

    [Fact]
    public void An_overlay_with_only_Automatic_settings_gives_exactly_the_plain_items_and_the_plain_version()
    {
        var snapshot = Snapshot(
            Item(),
            Record(VersionUrl, "1111111111111111", 600, 800, FlatFeatures),
            Record(MainUrl, "2222222222222222", 600, 800, FlatFeatures));
        var automatic = OwnerOverlay.From(
            snapshot,
            OwnerData.Empty with { Games = [new OwnerGame(GameId, null, null, new PictureSetting(PictureChoice.Automatic, true, VersionUrl, MainUrl, null))] });

        var items = SnapshotMapper.ToCabinetItems(snapshot, ArtRules.Default, automatic);

        items.Should().BeEquivalentTo(SnapshotMapper.ToCabinetItems(snapshot, ArtRules.Default));
        SnapshotMapper.Version(items, ArtRules.Default, snapshot, automatic)
            .Should().Be(SnapshotMapper.Version(items, ArtRules.Default, snapshot));
    }

    [Fact]
    public void A_chosen_flat_cover_may_shape_a_box_with_no_size_from_BGG()
    {
        var snapshot = Snapshot(
            Item(),
            Record(VersionUrl, "1111111111111111", 600, 800, FlatFeatures),
            Record(MainUrl, "2222222222222222", 800, 460, FlatFeatures));

        var chosen = SnapshotMapper.Explain(snapshot, ArtRules.Default, OverlayOf(PictureChoice.MainPicture)).Single();

        chosen.Shape.Source.Should().Be(BoxSource.CoverShape);
        chosen.Mapped.Box.WidthMm.Should().BeGreaterThan(chosen.Mapped.Box.HeightMm);
    }

    [Fact]
    public void A_chosen_3D_shot_never_shapes_the_box_with_or_without_a_size_from_BGG()
    {
        var withoutSize = Snapshot(Item(), Record(MainUrl, "2222222222222222", 800, 460, ThreeDFeatures));
        var withSize = Snapshot(Item() with { Dimensions = new VersionDimensions(6.3, 8.27, 2.09) }, Record(MainUrl, "2222222222222222", 800, 460, ThreeDFeatures));
        var overlay = OverlayOf(PictureChoice.MainPicture);

        var bare = SnapshotMapper.Explain(withoutSize, ArtRules.Default, overlay).Single();
        var sized = SnapshotMapper.Explain(withSize, ArtRules.Default, overlay).Single();
        var plainSized = SnapshotMapper.Explain(withSize, ArtRules.Default).Single();

        bare.Shape.Source.Should().Be(BoxSource.Default);
        bare.Mapped.Box.Should().Be(SnapshotMapper.DefaultBox(ItemKind.Base));
        sized.Shape.Source.Should().Be(BoxSource.RealSize);
        sized.Mapped.Box.Should().Be(plainSized.Mapped.Box);
    }

    [Fact]
    public void A_chosen_unsure_picture_may_only_turn_the_box()
    {
        var item = Item() with { Dimensions = new VersionDimensions(6.3, 8.27, 2.09) };
        var unsure = Snapshot(item, Record(MainUrl, "2222222222222222", 480, 300, UnsureFeatures));
        var overlay = OverlayOf(PictureChoice.MainPicture);

        var turned = SnapshotMapper.Explain(unsure, ArtRules.Default, overlay).Single();

        turned.Shape.Source.Should().Be(BoxSource.RealSize);
        turned.Mapped.Box.Should().Be(new BoxDimensions(210, 160, 53));
    }

    [Fact]
    public void A_choice_counts_in_the_version_even_when_the_picture_drawn_is_the_same()
    {
        var snapshot = Snapshot(Item(), Record(MainUrl, "2222222222222222", 600, 800, FlatFeatures));
        var plainItems = SnapshotMapper.ToCabinetItems(snapshot, ArtRules.Default);
        var chosenItems = SnapshotMapper.ToCabinetItems(snapshot, ArtRules.Default, OverlayOf(PictureChoice.MainPicture));

        var plain = SnapshotMapper.Version(plainItems, ArtRules.Default, snapshot);
        var chosen = SnapshotMapper.Version(chosenItems, ArtRules.Default, snapshot, OverlayOf(PictureChoice.MainPicture));

        chosenItems.Should().BeEquivalentTo(plainItems);
        chosen.Should().NotBe(plain);
    }

    [Fact]
    public void A_change_of_kind_or_of_link_address_is_a_new_version()
    {
        var snapshot = Snapshot(
            Item(),
            Record(VersionUrl, "1111111111111111", 600, 800, FlatFeatures),
            Record(MainUrl, "2222222222222222", 600, 800, FlatFeatures));
        var link = Record(LinkUrl, "3333333333333333", 600, 800, FlatFeatures);
        var otherLink = Record("https://cf.example.org/other.jpg", "3333333333333333", 600, 800, FlatFeatures);

        var versions = new[]
        {
            VersionWith(snapshot, OverlayOf(PictureChoice.OwnedEdition)),
            VersionWith(snapshot, OverlayOf(PictureChoice.MainPicture)),
            VersionWith(snapshot, OverlayOf(PictureChoice.Link, link)),
            VersionWith(snapshot, OverlayOf(PictureChoice.Link, otherLink)),
        };

        versions.Distinct().Should().HaveCount(versions.Length);
    }

    [Fact]
    public void Each_copy_is_drawn_from_its_own_choice_and_a_game_wide_choice_applies_to_copies_with_none()
    {
        var snapshot = TwoCopies();
        var gameWide = new OwnerGame(GameId, null, null, new PictureSetting(PictureChoice.MainPicture, true, null, null, null));
        var ownChoice = new OwnerGame(GameId, null, null, new PictureSetting(PictureChoice.OwnedEdition, true, null, null, null), 2);

        var plain = ArtOf(snapshot, OwnerOverlay.From(snapshot, OwnerData.Empty));
        var everyCopy = ArtOf(snapshot, OwnerOverlay.From(snapshot, OwnerData.Empty with { Games = [gameWide] }));
        var mixed = ArtOf(snapshot, OwnerOverlay.From(snapshot, OwnerData.Empty with { Games = [gameWide, ownChoice] }));
        var onlyOne = ArtOf(snapshot, OwnerOverlay.From(snapshot, OwnerData.Empty with { Games = [ownChoice] }));

        plain.Should().Equal(new Dictionary<long, string> { [1] = "/art/2222222222222222-600.webp", [2] = "/art/2222222222222222-600.webp" });
        everyCopy.Should().Equal(new Dictionary<long, string> { [1] = "/art/2222222222222222-600.webp", [2] = "/art/2222222222222222-600.webp" });
        mixed.Should().Equal(new Dictionary<long, string> { [1] = "/art/2222222222222222-600.webp", [2] = "/art/1111111111111111-600.webp" });
        onlyOne.Should().Equal(new Dictionary<long, string> { [1] = "/art/2222222222222222-600.webp", [2] = "/art/1111111111111111-600.webp" });
    }

    [Fact]
    public void Changing_only_one_copys_choice_changes_the_version_and_that_copys_art_alone()
    {
        var snapshot = TwoCopies();
        var before = OwnerOverlay.From(snapshot, OwnerData.Empty);
        var after = OwnerOverlay.From(
            snapshot,
            OwnerData.Empty with { Games = [new OwnerGame(GameId, null, null, new PictureSetting(PictureChoice.OwnedEdition, true, null, null, null), 2)] });

        var itemsBefore = SnapshotMapper.ToCabinetItems(snapshot, ArtRules.Default, before);
        var itemsAfter = SnapshotMapper.ToCabinetItems(snapshot, ArtRules.Default, after);

        SnapshotMapper.Version(itemsAfter, ArtRules.Default, snapshot, after)
            .Should().NotBe(SnapshotMapper.Version(itemsBefore, ArtRules.Default, snapshot, before));
        itemsAfter.Single(item => item.CollectionId == 1).Should().BeEquivalentTo(itemsBefore.Single(item => item.CollectionId == 1));
        itemsAfter.Single(item => item.CollectionId == 2).Art.Should().NotBeEquivalentTo(itemsBefore.Single(item => item.CollectionId == 2).Art);
    }

    [Fact]
    public void A_choice_for_one_copy_gives_a_different_version_than_the_same_choice_for_the_other_copy()
    {
        var snapshot = TwoCopies();
        var setting = new PictureSetting(PictureChoice.OwnedEdition, true, null, null, null);
        var first = OwnerOverlay.From(snapshot, OwnerData.Empty with { Games = [new OwnerGame(GameId, null, null, setting, 1)] });
        var second = OwnerOverlay.From(snapshot, OwnerData.Empty with { Games = [new OwnerGame(GameId, null, null, setting, 2)] });

        VersionWith(snapshot, first).Should().NotBe(VersionWith(snapshot, second));
    }

    private static Dictionary<long, string> ArtOf(CollectionSnapshot snapshot, OwnerOverlay overlay) =>
        SnapshotMapper.Explain(snapshot, ArtRules.Default, overlay)
            .ToDictionary(trace => trace.Item.CollectionId, trace => trace.Mapped.Art!.Variants[0].Url);

    private static CollectionSnapshot TwoCopies() =>
        new(
            CollectionSnapshot.CurrentSchemaVersion,
            Moment,
            [Item(), Item() with { CollectionId = 2 }],
            new[]
            {
                Record(VersionUrl, "1111111111111111", 600, 800, ThreeDFeatures),
                Record(MainUrl, "2222222222222222", 600, 800, FlatFeatures),
            }.ToDictionary(record => record.SourceUrl, StringComparer.Ordinal));

    private static void AssertUnchanged(CollectionSnapshot snapshot, OwnerOverlay overlay)
    {
        var plain = SnapshotMapper.Explain(snapshot, ArtRules.Default).Single();
        var chosen = SnapshotMapper.Explain(snapshot, ArtRules.Default, overlay).Single();

        chosen.Mapped.Should().BeEquivalentTo(plain.Mapped);
        chosen.Pick.Should().Be(plain.Pick);
        chosen.PickedByOwner.Should().BeFalse();
    }

    private static string VersionWith(CollectionSnapshot snapshot, OwnerOverlay overlay) =>
        SnapshotMapper.Version(SnapshotMapper.ToCabinetItems(snapshot, ArtRules.Default, overlay), ArtRules.Default, snapshot, overlay);

    private static OwnerOverlay OverlayOf(PictureChoice choice, ImageRecord? link = null)
    {
        var setting = new PictureSetting(choice, true, null, null, link is null ? null : new PictureLink(link.SourceUrl, link, 0, null));

        return OwnerOverlay.From(
            Snapshot(Item()),
            OwnerData.Empty with { Games = [new OwnerGame(GameId, null, null, setting)] });
    }

    private static SnapshotItem Item() =>
        new(1, GameId, "Invented Game", ItemKind.Base, null, null, null, VersionUrl, MainUrl);

    private static ImageRecord Record(string address, string hash, int width, int height, ArtFeatures features, string? background = null) =>
        new(
            address,
            ImageStatus.Ok,
            Moment,
            [new ArtFile(width, height, $"{hash}-{width}.webp")],
            features)
        {
            Colour = background is null ? null : new PaletteTone(background, "#ffffff"),
        };

    private static CollectionSnapshot Snapshot(SnapshotItem item, params ImageRecord[] records) =>
        new(CollectionSnapshot.CurrentSchemaVersion, Moment, [item], records.ToDictionary(record => record.SourceUrl, StringComparer.Ordinal));
}

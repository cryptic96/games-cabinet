using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>
/// Proves that the pictures page rows hold four tiles per game in a fixed order, that each tile shows the processed picture the
/// cabinet would use with its verdict, that the tile Automatic duplicates is flagged, and that the empty cases are told apart.
/// </summary>
[Trait("Category", "Owner")]
public sealed class PictureRowsTests
{
    private const string VersionUrl = "https://cf.example.org/version.jpg";
    private const string MainUrl = "https://cf.example.org/main.jpg";
    private const string LinkUrl = "https://cf.example.org/pictures/link-shot.jpg";
    private const int GameId = 7;
    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly ArtFeatures FlatFeatures = new(0.0, 1.0, 0.0, 0.0, 4);
    private static readonly ArtFeatures UnsureFeatures = new(0.2, 0.95, 0.30, 0.20, 2);
    private static readonly ArtFeatures ThreeDFeatures = new(0.3, 0.70, 0.60, 0.50, 1);
    private static readonly ArtFeatures CutOutFeatures = new(0.3, 0.70, 0.60, 0.50, 1, true);
    private static readonly ArtFeatures TightCropFeatures = new(0.3, 0.70, 0.60, 0.50, 1, false, true);

    [Fact]
    public void Every_game_has_four_tiles_in_the_fixed_order()
    {
        var rows = Build(Snapshot(Item(), Record(VersionUrl, "1111111111111111", 240, 320, FlatFeatures)), OwnerData.Empty);

        var row = rows.Single();

        row.Tiles.Select(tile => tile.Kind).Should().Equal(
            PictureChoice.Automatic,
            PictureChoice.OwnedEdition,
            PictureChoice.MainPicture,
            PictureChoice.Link);
        row.GameId.Should().Be(GameId);
        row.Title.Should().Be("Invented Game");
        row.Year.Should().Be(2019);
        row.Choice.Should().Be(PictureChoice.Automatic);
    }

    [Fact]
    public void A_row_with_only_one_candidate_still_has_four_tiles_and_the_missing_one_is_not_available()
    {
        var snapshot = Snapshot(Item(mainUrl: null), Record(VersionUrl, "1111111111111111", 240, 320, FlatFeatures));

        var row = Build(snapshot, OwnerData.Empty).Single();

        row.Tiles.Should().HaveCount(4);
        row.Tile(PictureChoice.OwnedEdition).Available.Should().BeTrue();
        row.Tile(PictureChoice.MainPicture).Available.Should().BeFalse();
        row.Tile(PictureChoice.MainPicture).Url.Should().BeNull();
        row.Tile(PictureChoice.Automatic).Available.Should().BeTrue();
    }

    [Fact]
    public void A_tile_shows_the_240_pixel_file_with_its_size_and_verdict_kind()
    {
        var snapshot = Snapshot(
            Item(),
            new ImageRecord(
                VersionUrl,
                ImageStatus.Ok,
                Moment,
                [new ArtFile(600, 800, "1111111111111111-600.webp"), new ArtFile(240, 320, "1111111111111111-240.webp")],
                FlatFeatures),
            Record(MainUrl, "2222222222222222", 480, 480, ThreeDFeatures));

        var row = Build(snapshot, OwnerData.Empty).Single();
        var owned = row.Tile(PictureChoice.OwnedEdition);
        var main = row.Tile(PictureChoice.MainPicture);

        owned.Url.Should().Be("/art/1111111111111111-240.webp");
        owned.Width.Should().Be(240);
        owned.Height.Should().Be(320);
        owned.Verdict.Should().Be(ArtVerdictKind.Flat);
        main.Url.Should().Be("/art/2222222222222222-480.webp");
        main.Verdict.Should().Be(ArtVerdictKind.ThreeD);
    }

    [Theory]
    [InlineData(0, ArtVerdictKind.Flat)]
    [InlineData(1, ArtVerdictKind.Unsure)]
    [InlineData(2, ArtVerdictKind.ThreeD)]
    [InlineData(3, ArtVerdictKind.CutOut)]
    [InlineData(4, ArtVerdictKind.TightCrop)]
    public void The_verdict_kind_of_a_tile_comes_from_the_shared_kinds(int which, ArtVerdictKind expected)
    {
        var features = new[] { FlatFeatures, UnsureFeatures, ThreeDFeatures, CutOutFeatures, TightCropFeatures }[which];
        var snapshot = Snapshot(Item(), Record(VersionUrl, "1111111111111111", 240, 320, features));

        var row = Build(snapshot, OwnerData.Empty).Single();

        row.Tile(PictureChoice.OwnedEdition).Verdict.Should().Be(expected);
    }

    [Fact]
    public void Automatic_that_uses_the_owned_edition_says_so_and_flags_that_tile()
    {
        var snapshot = Snapshot(
            Item(),
            Record(VersionUrl, "1111111111111111", 240, 320, FlatFeatures),
            Record(MainUrl, "2222222222222222", 240, 320, FlatFeatures));

        var row = Build(snapshot, OwnerData.Empty).Single();

        row.AutomaticSource.Should().Be(AutomaticSource.OwnedEdition);
        row.Tile(PictureChoice.Automatic).Url.Should().Be(row.Tile(PictureChoice.OwnedEdition).Url);
        row.Tile(PictureChoice.Automatic).Verdict.Should().Be(ArtVerdictKind.Flat);
        row.Tile(PictureChoice.OwnedEdition).IsAutomaticPick.Should().BeTrue();
        row.Tile(PictureChoice.MainPicture).IsAutomaticPick.Should().BeFalse();
    }

    [Fact]
    public void Automatic_that_uses_the_main_picture_says_so_and_flags_that_tile()
    {
        var snapshot = Snapshot(
            Item(),
            Record(VersionUrl, "1111111111111111", 240, 320, ThreeDFeatures),
            Record(MainUrl, "2222222222222222", 240, 320, FlatFeatures));

        var row = Build(snapshot, OwnerData.Empty).Single();

        row.AutomaticSource.Should().Be(AutomaticSource.MainPicture);
        row.Tile(PictureChoice.Automatic).Url.Should().Be("/art/2222222222222222-240.webp");
        row.Tile(PictureChoice.MainPicture).IsAutomaticPick.Should().BeTrue();
        row.Tile(PictureChoice.OwnedEdition).IsAutomaticPick.Should().BeFalse();
    }

    [Fact]
    public void Automatic_with_no_usable_picture_uses_a_generated_cover_in_the_game_colours_and_flags_no_tile()
    {
        var snapshot = Snapshot(Item(), new ImageRecord(VersionUrl, ImageStatus.Failed, Moment));

        var row = Build(snapshot, OwnerData.Empty).Single();
        var automatic = row.Tile(PictureChoice.Automatic);
        var tone = SpinePalette.Tones[SpinePalette.ToneFor(GameId)];

        row.AutomaticSource.Should().Be(AutomaticSource.GeneratedCover);
        automatic.Url.Should().BeNull();
        automatic.Verdict.Should().BeNull();
        row.Tiles.Should().OnlyContain(tile => !tile.IsAutomaticPick);
        row.CoverBackground.Should().Be(tone.Background);
        row.CoverText.Should().Be(tone.Text);
        row.CoverPattern.Should().Be(SpinePalette.PatternFor(GameId));
    }

    [Fact]
    public void A_candidate_with_an_address_but_no_usable_picture_is_still_available()
    {
        var snapshot = Snapshot(Item(), new ImageRecord(VersionUrl, ImageStatus.Failed, Moment));

        var tile = Build(snapshot, OwnerData.Empty).Single().Tile(PictureChoice.OwnedEdition);

        tile.Available.Should().BeTrue();
        tile.Url.Should().BeNull();
    }

    [Fact]
    public void Your_link_with_no_stored_link_is_not_available()
    {
        var row = Build(Snapshot(Item()), OwnerData.Empty).Single();

        var tile = row.Tile(PictureChoice.Link);

        tile.Available.Should().BeFalse();
        tile.Url.Should().BeNull();
        tile.LinkAddress.Should().BeNull();
        tile.Fetching.Should().BeFalse();
    }

    [Fact]
    public void A_stored_link_is_available_whether_or_not_it_is_chosen_with_its_copy_and_short_address()
    {
        var link = new ImageRecord(
            LinkUrl,
            ImageStatus.Ok,
            Moment,
            [new ArtFile(240, 240, "3333333333333333-240.webp")],
            UnsureFeatures);

        var row = Build(Snapshot(Item()), OwnerWith(PictureChoice.Automatic, new PictureLink(LinkUrl, link, 0, null))).Single();
        var tile = row.Tile(PictureChoice.Link);

        row.Choice.Should().Be(PictureChoice.Automatic);
        tile.Available.Should().BeTrue();
        tile.Url.Should().Be("/art/3333333333333333-240.webp");
        tile.Verdict.Should().Be(ArtVerdictKind.Unsure);
        tile.LinkAddress.Should().Be("cf.example.org/.../link-shot.jpg");
        tile.LinkFullAddress.Should().Be(LinkUrl);
        tile.Fetching.Should().BeFalse();
    }

    [Fact]
    public void A_stored_link_not_fetched_yet_is_fetching()
    {
        var row = Build(Snapshot(Item()), OwnerWith(PictureChoice.Link, new PictureLink(LinkUrl, null, 0, null))).Single();
        var tile = row.Tile(PictureChoice.Link);

        row.Choice.Should().Be(PictureChoice.Link);
        tile.Available.Should().BeTrue();
        tile.Url.Should().BeNull();
        tile.Fetching.Should().BeTrue();
    }

    [Fact]
    public void Games_are_listed_once_by_title_ignoring_case_with_a_name_for_a_blank_title_and_the_expansion_flag()
    {
        var snapshot = new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            Moment,
            [
                new SnapshotItem(1, 3, "zebra Crossing", ItemKind.Base, null, null, null),
                new SnapshotItem(2, 4, "Apple Orchard", ItemKind.Base, null, null, null),
                new SnapshotItem(3, 5, "  ", ItemKind.Expansion, null, null, null),
                new SnapshotItem(4, 4, "Apple Orchard", ItemKind.Base, null, null, null),
            ],
            new Dictionary<string, ImageRecord>());

        var rows = Build(snapshot, OwnerData.Empty);

        rows.Select(row => row.Title).Should().Equal("Apple Orchard", "Game 5", "zebra Crossing");
        rows.Single(row => row.GameId == 5).IsExpansion.Should().BeTrue();
    }

    [Fact]
    public void An_empty_collection_has_no_rows()
    {
        var snapshot = new CollectionSnapshot(CollectionSnapshot.CurrentSchemaVersion, Moment, [], new Dictionary<string, ImageRecord>());

        Build(snapshot, OwnerData.Empty).Should().BeEmpty();
    }

    private static IReadOnlyList<PictureRow> Build(CollectionSnapshot snapshot, OwnerData data) =>
        PictureRows.Build(snapshot, ArtRules.Default, data, Moment);

    private static OwnerData OwnerWith(PictureChoice choice, PictureLink? link) =>
        OwnerData.Empty with { Games = [new OwnerGame(GameId, null, null, new PictureSetting(choice, false, null, null, link))] };

    private static SnapshotItem Item(string? mainUrl = MainUrl) =>
        new(1, GameId, "Invented Game", ItemKind.Base, 2019, null, null, VersionUrl, mainUrl);

    private static ImageRecord Record(string address, string hash, int width, int height, ArtFeatures features) =>
        new(address, ImageStatus.Ok, Moment, [new ArtFile(width, height, $"{hash}-{width}.webp")], features);

    private static CollectionSnapshot Snapshot(SnapshotItem item, params ImageRecord[] records) =>
        new(CollectionSnapshot.CurrentSchemaVersion, Moment, [item], records.ToDictionary(record => record.SourceUrl, StringComparer.Ordinal));
}

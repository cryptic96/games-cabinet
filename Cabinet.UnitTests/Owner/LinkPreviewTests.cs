using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>
/// Proves that the preview of a linked picture is made by the same code that draws the cabinet: the box, where its shape
/// came from, the spine colour and the shortened title are the ones the cabinet draws once the link is used, and a
/// picture that is not a flat cover never gives the box its shape.
/// </summary>
[Trait("Category", "Owner")]
public sealed class LinkPreviewTests
{
    private const string LinkUrl = "https://cf.example.org/pictures/link-shot.jpg";
    private const int GameId = 7;
    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly ArtFeatures FlatFeatures = new(0.0, 1.0, 0.0, 0.0, 4);
    private static readonly ArtFeatures ThreeDFeatures = new(0.3, 0.70, 0.60, 0.50, 1);

    [Fact]
    public void A_flat_cover_on_a_game_without_a_real_size_gives_the_box_its_shape_and_says_so()
    {
        var snapshot = Snapshot(Item(dimensions: null));

        var preview = LinkPreview.Build(snapshot, GameId, ArtRules.Default, Record(240, 320, FlatFeatures))!;

        preview.ShapeSource.Should().Be(PreviewShapeSource.Picture);
        preview.Verdict.Should().Be(ArtVerdictKind.Flat);
        ((double)preview.BoxWidthMm / preview.BoxHeightMm).Should().BeApproximately(0.75, 0.02);
    }

    [Fact]
    public void A_flat_cover_on_a_game_with_a_real_size_of_the_same_shape_keeps_the_real_size()
    {
        var snapshot = Snapshot(Item(new VersionDimensions(9.0, 12.0, 3.0)));

        var preview = LinkPreview.Build(snapshot, GameId, ArtRules.Default, Record(240, 320, FlatFeatures))!;

        preview.ShapeSource.Should().Be(PreviewShapeSource.Bgg);
        preview.BoxWidthMm.Should().Be(229);
        preview.BoxHeightMm.Should().Be(305);
        preview.BoxDepthMm.Should().Be(76);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_3D_shot_never_gives_the_box_its_shape(bool withRealSize)
    {
        var snapshot = Snapshot(Item(withRealSize ? new VersionDimensions(9.0, 12.0, 3.0) : null));

        var preview = LinkPreview.Build(snapshot, GameId, ArtRules.Default, Record(240, 160, ThreeDFeatures))!;

        preview.Verdict.Should().Be(ArtVerdictKind.ThreeD);
        preview.ShapeSource.Should().Be(withRealSize ? PreviewShapeSource.Bgg : PreviewShapeSource.Estimate);
    }

    [Fact]
    public void The_preview_has_the_box_the_colours_and_the_edges_the_cabinet_draws_once_the_link_is_used()
    {
        var snapshot = Snapshot(Item(new VersionDimensions(9.0, 12.0, 3.0)));
        var record = Record(240, 320, FlatFeatures);
        var used = OwnerOverlay.From(snapshot, OwnerData.Empty with
        {
            Games = [new OwnerGame(GameId, null, null, new PictureSetting(PictureChoice.Link, true, null, null, new PictureLink(LinkUrl, record, 0, null)))],
        });

        var preview = LinkPreview.Build(snapshot, GameId, ArtRules.Default, record)!;
        var drawn = SnapshotMapper.ToCabinetItems(snapshot, ArtRules.Default, used).Single();

        new BoxDimensions(preview.BoxWidthMm, preview.BoxHeightMm, preview.BoxDepthMm).Should().Be(drawn.Box);
        preview.Spine.Should().Be(drawn.Colour);
        preview.Edges.Should().Be(drawn.Art!.Edges);
    }

    [Fact]
    public void The_spine_is_as_wide_as_the_cabinet_draws_it_and_carries_the_engines_shortened_title()
    {
        var snapshot = Snapshot(Item(new VersionDimensions(9.0, 12.0, 0.5), title: "A Very Long Invented Title For A Game About Gardens And Bridges"));

        var preview = LinkPreview.Build(snapshot, GameId, ArtRules.Default, Record(240, 320, FlatFeatures))!;

        preview.SpineHeightMm.Should().Be(preview.BoxHeightMm);
        preview.SpineWidthMm.Should().Be(SectionDesigns.Desktop.MinBoxThicknessMm);
        preview.SpineLabel.Should().Be(SpineLabel.Shorten(preview.Title, preview.BoxHeightMm / SectionDesigns.Desktop.LabelCharPitchMm));
        preview.SpineLabel.Should().EndWith("…");
    }

    [Fact]
    public void The_picture_is_the_240_pixel_file_and_a_picture_of_the_boxs_shape_fits_it_exactly()
    {
        var snapshot = Snapshot(Item(new VersionDimensions(9.0, 12.0, 3.0)));
        var record = Record(240, 320, FlatFeatures) with
        {
            Files = [new ArtFile(480, 640, "1111111111111111-480.webp"), new ArtFile(240, 320, "1111111111111111-240.webp")],
        };

        var preview = LinkPreview.Build(snapshot, GameId, ArtRules.Default, record)!;

        preview.ArtWidth.Should().Be(240);
        preview.ArtHeight.Should().Be(320);
        preview.Fit.Should().Be(ArtFit.Exact);
    }

    [Fact]
    public void A_picture_that_is_wider_than_the_box_fills_its_width()
    {
        var snapshot = Snapshot(Item(new VersionDimensions(9.0, 12.0, 3.0)));

        var preview = LinkPreview.Build(snapshot, GameId, ArtRules.Default, Record(240, 200, ThreeDFeatures))!;

        preview.Fit.Should().Be(ArtFit.Width);
    }

    [Fact]
    public void A_game_that_is_not_in_the_collection_or_a_record_that_cannot_be_drawn_has_no_preview()
    {
        var snapshot = Snapshot(Item(null));
        var unusable = Record(240, 320, FlatFeatures) with { Status = ImageStatus.Failed };

        LinkPreview.Build(snapshot, GameId + 1, ArtRules.Default, Record(240, 320, FlatFeatures)).Should().BeNull();
        LinkPreview.Build(snapshot, GameId, ArtRules.Default, unusable).Should().BeNull();
    }

    private static SnapshotItem Item(VersionDimensions? dimensions, string title = "Invented Game") =>
        new(1, GameId, title, ItemKind.Base, 2019, dimensions, null);

    private static ImageRecord Record(int width, int height, ArtFeatures features) =>
        new(
            LinkUrl,
            ImageStatus.Ok,
            Moment,
            [new ArtFile(width, height, $"1111111111111111-{width}.webp")],
            features,
            "#336699",
            new PaletteTone("#336699", "#ffffff"),
            new ArtEdges("#336699", "#336699", "#336699", "#336699"),
            3);

    private static CollectionSnapshot Snapshot(SnapshotItem item) =>
        new(CollectionSnapshot.CurrentSchemaVersion, Moment, [item], new Dictionary<string, ImageRecord>(StringComparer.Ordinal));
}

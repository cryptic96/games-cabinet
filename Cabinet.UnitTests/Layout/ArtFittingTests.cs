using Cabinet.Domain.Layout;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>Proves how a picture sits in a box front, which stored size a cover asks for, and that only face-out covers carry a picture.</summary>
[Trait("Category", "Layout")]
public sealed class ArtFittingTests
{
    private static readonly ArtImage TwoSizes = new(
        [
            new ArtVariant(480, 640, "/art/aaaaaaaaaaaaaaaa-480.webp"),
            new ArtVariant(240, 320, "/art/bbbbbbbbbbbbbbbb-240.webp"),
        ]);

    [Theory]
    [InlineData(100, 100, 1005, 1000)]
    [InlineData(100, 100, 995, 1000)]
    [InlineData(200, 300, 400, 600)]
    [InlineData(200, 300, 401, 600)]
    public void Shapes_within_one_percent_of_each_other_fit_exactly(int boxWidth, int boxHeight, int artWidth, int artHeight)
    {
        ArtFitting.Fit(boxWidth, boxHeight, artWidth, artHeight).Should().Be(ArtFit.Exact);
    }

    [Fact]
    public void A_picture_that_is_relatively_wider_fills_the_width_and_one_that_is_narrower_fills_the_height()
    {
        ArtFitting.Fit(100, 100, 1020, 1000).Should().Be(ArtFit.Width);
        ArtFitting.Fit(100, 100, 980, 1000).Should().Be(ArtFit.Height);
        ArtFitting.Fit(200, 300, 600, 600).Should().Be(ArtFit.Width);
        ArtFitting.Fit(300, 200, 600, 600).Should().Be(ArtFit.Height);
    }

    [Fact]
    public void A_shape_that_cannot_be_compared_fits_exactly()
    {
        ArtFitting.Fit(0, 100, 10, 10).Should().Be(ArtFit.Exact);
        ArtFitting.Fit(100, 100, 0, 10).Should().Be(ArtFit.Exact);
    }

    [Fact]
    public void A_cover_of_225_millimetres_asks_for_the_240_size_on_desktop_and_the_480_size_on_phone()
    {
        ArtFitting.Pick(TwoSizes, 225, SectionDesigns.Desktop)!.Width.Should().Be(240);
        ArtFitting.Pick(TwoSizes, 225, SectionDesigns.Phone)!.Width.Should().Be(480);
    }

    [Fact]
    public void A_cover_wider_than_the_small_size_allows_asks_for_the_largest_size_there_is()
    {
        ArtFitting.Pick(TwoSizes, 1_000, SectionDesigns.Phone)!.Width.Should().Be(480);
        ArtFitting.Pick(new ArtImage([new ArtVariant(240, 240, "/art/cccccccccccccccc-240.webp")]), 600, SectionDesigns.Phone)!.Width.Should().Be(240);
    }

    [Fact]
    public void A_picture_with_no_sizes_gives_nothing()
    {
        ArtFitting.Pick(new ArtImage([]), 225, SectionDesigns.Desktop).Should().BeNull();
    }

    [Fact]
    public void Only_covers_carry_art_and_the_art_names_the_picked_size_and_its_fit()
    {
        var items = SyntheticCollectionsWithArt(400);

        var placements = AllPlacements(CabinetLayoutEngine.Build(items, SectionDesigns.Desktop, LayoutOptions.Default));

        placements.Where(placement => placement.Art is not null).Should().OnlyContain(placement => placement.Kind == PlacementKind.Cover);
        placements.Where(placement => placement.Kind == PlacementKind.Cover).Should().NotBeEmpty().And.OnlyContain(placement => placement.Art != null);
        placements.Where(placement => placement.Kind != PlacementKind.Cover).Should().OnlyContain(placement => placement.Art == null);
        placements.Where(placement => placement.Art is not null).Should().OnlyContain(placement =>
            placement.Art!.Url.StartsWith("/art/", StringComparison.Ordinal) && placement.Art.Width > 0 && placement.Art.Height > 0);
    }

    [Fact]
    public void An_item_without_art_gives_a_cover_without_art()
    {
        var game = new CabinetItem(1, 1, "Invented Plain Game", ItemKind.Base, new BoxDimensions(200, 300, 60), []);
        var everyBoxFacesOut = new LayoutOptions(100, CoverStrategy.SizeWeighted, 6, 12);

        var placements = AllPlacements(CabinetLayoutEngine.Build([game], SectionDesigns.Desktop, everyBoxFacesOut));

        placements.Should().ContainSingle().Which.Art.Should().BeNull();
    }

    private static List<CabinetItem> SyntheticCollectionsWithArt(int count) =>
        [.. Cabinet.Domain.Samples.SyntheticCollections.Random(7, count, expansionPercent: 20)
            .Select(item => item with { Art = TwoSizes })];

    private static List<Placement> AllPlacements(CabinetLayout layout) =>
        [.. layout.Sections.SelectMany(section => section.Cubbies).SelectMany(cubby => cubby.Placements)];
}

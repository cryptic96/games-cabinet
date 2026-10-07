using Cabinet.Domain.Layout;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>Verifies the size classes, the cover chance and the few-games switch of the per-game orientation decision.</summary>
public class OrientationTests
{
    private static readonly SectionDesign Design = SectionDesigns.Desktop;

    [Theory]
    [InlineData(150, SizeClass.Small)]
    [InlineData(199, SizeClass.Small)]
    [InlineData(200, SizeClass.Standard)]
    [InlineData(319, SizeClass.Standard)]
    [InlineData(320, SizeClass.Large)]
    [InlineData(400, SizeClass.Large)]
    [Trait("Category", "Layout")]
    public void Size_class_follows_the_standing_height(int heightMm, SizeClass expected)
    {
        Orientation.SizeClassOf(new BoxDimensions(200, heightMm, 60)).Should().Be(expected);
    }

    [Theory]
    [InlineData(25, SizeClass.Small, 750)]
    [InlineData(25, SizeClass.Standard, 2500)]
    [InlineData(25, SizeClass.Large, 5500)]
    [InlineData(0, SizeClass.Large, 0)]
    [InlineData(100, SizeClass.Large, 9500)]
    [InlineData(100, SizeClass.Standard, 9500)]
    [InlineData(90, SizeClass.Standard, 9000)]
    [Trait("Category", "Layout")]
    public void Cover_chance_scales_with_size_and_is_capped(int share, SizeClass sizeClass, int expected)
    {
        Orientation.CoverChanceBasisPoints(share, sizeClass).Should().Be(expected);
    }

    [Theory]
    [InlineData(CoverStrategy.SizeWeighted)]
    [InlineData(CoverStrategy.Random)]
    [InlineData(CoverStrategy.OversizeOnly)]
    [Trait("Category", "Layout")]
    public void Few_games_make_every_box_face_out_whatever_the_strategy_and_share(CoverStrategy strategy)
    {
        var options = new LayoutOptions(0, strategy, 6, 12);

        for (var id = 1; id <= 200; id++)
        {
            Orientation.Decide(ItemOf(id, heightMm: 150 + (id % 200)), options, Design, fewGames: true)
                .Should().Be(BoxPose.Cover, "game {0}", id);
        }
    }

    [Theory]
    [InlineData(330, BoxPose.Spine)]
    [InlineData(331, BoxPose.Cover)]
    [InlineData(250, BoxPose.Spine)]
    [InlineData(390, BoxPose.Cover)]
    [Trait("Category", "Layout")]
    public void Oversize_only_faces_out_exactly_the_boxes_taller_than_the_design_maximum(int heightMm, BoxPose expected)
    {
        foreach (var share in new[] { 0, 100 })
        {
            var options = new LayoutOptions(share, CoverStrategy.OversizeOnly, 6, 0);

            Orientation.Decide(ItemOf(77, heightMm, depthMm: 60), options, Design, fewGames: false)
                .Should().Be(expected, "share {0}", share);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_desktop_design_allows_spines_up_to_330_millimetres()
    {
        Design.MaxSpineHeightMm.Should().Be(330);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_share_of_zero_never_faces_out_and_a_share_of_one_hundred_always_does_for_random()
    {
        for (var id = 1; id <= 300; id++)
        {
            var item = ItemOf(id, heightMm: 250, depthMm: 60);

            Orientation.Decide(item, new LayoutOptions(0, CoverStrategy.Random, 6, 0), Design, false)
                .Should().Be(BoxPose.Spine);
            Orientation.Decide(item, new LayoutOptions(100, CoverStrategy.Random, 6, 0), Design, false)
                .Should().Be(BoxPose.Cover);
        }
    }

    [Theory]
    [InlineData(150, 400, BoxPose.Cover)]
    [InlineData(400, 150, BoxPose.Spine)]
    [Trait("Category", "Layout")]
    public void The_oversize_rule_reads_the_pose_height_and_not_the_drawn_height(int drawnHeightMm, int poseHeightMm, BoxPose expected)
    {
        var options = new LayoutOptions(0, CoverStrategy.OversizeOnly, 6, 0);
        var item = ItemOf(77, drawnHeightMm) with { PoseHeightMm = poseHeightMm };

        Orientation.Decide(item, options, Design, fewGames: false).Should().Be(expected);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void An_item_without_a_pose_height_behaves_as_if_its_pose_height_were_its_drawn_height()
    {
        var options = new LayoutOptions(25, CoverStrategy.SizeWeighted, 6, 0);

        for (var id = 1; id <= 200; id++)
        {
            var plain = ItemOf(id, heightMm: 150 + (id % 200));

            Orientation.Decide(plain, options, Design, false)
                .Should().Be(Orientation.Decide(plain with { PoseHeightMm = plain.Box.HeightMm }, options, Design, false), "game {0}", id);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_size_weighted_chance_and_the_lie_flat_eligibility_read_the_pose_height()
    {
        var neverCover = new LayoutOptions(0, CoverStrategy.Random, 6, 0);
        var ids = Enumerable.Range(1, 300).ToList();
        var standing = ids.Select(id => Orientation.Decide(ItemOf(id, 300, depthMm: 60), neverCover, Design, false)).ToList();
        var shortPose = ids.Select(id => Orientation.Decide(ItemOf(id, 300, depthMm: 60) with { PoseHeightMm = 150 }, neverCover, Design, false)).ToList();

        standing.Should().OnlyContain(pose => pose == BoxPose.Spine, "a tall deep box cannot lie flat");
        shortPose.Should().Contain(BoxPose.Flat, "a box whose pose height is small may lie flat");
    }

    private static CabinetItem ItemOf(int bggId, int heightMm, int depthMm = 60) =>
        new(bggId, bggId, $"Invented Title {bggId}", ItemKind.Base, new BoxDimensions(heightMm * 3 / 4, heightMm, depthMm), []);
}

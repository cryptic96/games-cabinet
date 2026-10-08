using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using FluentAssertions;

namespace Cabinet.UnitTests.Collection;

/// <summary>Proves the order in which a box takes its size: real dimensions, a flat cover's shape, an estimate, a default.</summary>
[Trait("Category", "Snapshot")]
public sealed class BoxShapeTests
{
    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly ArtFile PortraitCover = new(750, 1000, "a-480.webp");
    private static readonly ArtFile NarrowCover = new(620, 1000, "b-480.webp");
    private static readonly ArtFile LandscapeCover = new(900, 800, "c-480.webp");

    [Fact]
    public void Dimensions_that_match_a_flat_cover_within_the_margin_are_used_as_they_are()
    {
        var shaped = BoxShape.Resolve(Item(new VersionDimensions(6.3, 8.27, 2.09)), null, PortraitCover, ArtRules.Default);

        shaped.Box.Should().Be(BoxFromVersion.TryMap(new VersionDimensions(6.3, 8.27, 2.09)));
        shaped.Source.Should().Be(BoxSource.RealSize);
        shaped.PoseHeightMm.Should().Be(shaped.Box.HeightMm);
    }

    [Fact]
    public void Dimensions_that_contradict_a_flat_cover_are_rebuilt_with_the_same_area_and_depth()
    {
        var real = BoxFromVersion.TryMap(new VersionDimensions(6.3, 8.27, 2.09))!;

        var shaped = BoxShape.Resolve(Item(new VersionDimensions(6.3, 8.27, 2.09)), null, NarrowCover, ArtRules.Default);

        shaped.Source.Should().Be(BoxSource.CoverShape);
        shaped.Box.DepthMm.Should().Be(real.DepthMm);
        ((double)shaped.Box.WidthMm * shaped.Box.HeightMm).Should().BeApproximately((double)real.WidthMm * real.HeightMm, real.WidthMm * real.HeightMm * 0.01);
        ((double)shaped.Box.WidthMm / shaped.Box.HeightMm).Should().BeApproximately(0.62, 0.01);
        shaped.PoseHeightMm.Should().Be(real.HeightMm, "how a box stands never follows its picture");
    }

    [Fact]
    public void A_picture_that_is_not_a_flat_cover_never_changes_the_box()
    {
        var item = Item(new VersionDimensions(6.3, 8.27, 2.09));

        var without = BoxShape.Resolve(item, null, null, ArtRules.Default);

        without.Source.Should().Be(BoxSource.RealSize);
        without.Box.Should().Be(BoxFromVersion.TryMap(item.Dimensions));
        BoxShape.Resolve(Item(null), Details(), null, ArtRules.Default).Source.Should().Be(BoxSource.Estimate);
    }

    [Fact]
    public void Without_dimensions_a_flat_landscape_cover_gives_a_landscape_front_whose_longer_side_is_the_class_height()
    {
        var details = Details();
        var estimate = SizeEstimate.Dimensions(SizeEstimate.Assign(details, null, null)!.Value, ItemKind.Base);

        var shaped = BoxShape.Resolve(Item(null), details, LandscapeCover, ArtRules.Default);

        shaped.Source.Should().Be(BoxSource.CoverShape);
        shaped.Box.WidthMm.Should().Be(estimate.HeightMm);
        shaped.Box.HeightMm.Should().Be((int)Math.Round(estimate.HeightMm * 800.0 / 900.0, MidpointRounding.AwayFromZero));
        shaped.Box.DepthMm.Should().Be(estimate.DepthMm);
        shaped.PoseHeightMm.Should().Be(estimate.HeightMm);
    }

    [Fact]
    public void Without_dimensions_a_flat_portrait_cover_keeps_the_class_height_and_takes_its_ratio()
    {
        var details = Details();
        var estimate = SizeEstimate.Dimensions(SizeEstimate.Assign(details, null, null)!.Value, ItemKind.Base);

        var shaped = BoxShape.Resolve(Item(null), details, PortraitCover, ArtRules.Default);

        shaped.Box.HeightMm.Should().Be(estimate.HeightMm);
        shaped.Box.WidthMm.Should().Be((int)Math.Round(estimate.HeightMm * 0.75, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void Without_dimensions_and_without_a_cover_the_class_box_applies_and_with_no_details_the_default_does()
    {
        var details = Details();
        var expected = SizeEstimate.Dimensions(SizeEstimate.Assign(details, null, null)!.Value, ItemKind.Base);

        var estimated = BoxShape.Resolve(Item(null), details, null, ArtRules.Default);
        var nothing = BoxShape.Resolve(Item(null), null, null, ArtRules.Default);

        estimated.Box.Should().Be(expected);
        estimated.Source.Should().Be(BoxSource.Estimate);
        nothing.Box.Should().Be(new BoxDimensions(225, 300, 60));
        nothing.Source.Should().Be(BoxSource.Default);
        BoxShape.Resolve(Item(null, ItemKind.Expansion), null, null, ArtRules.Default).Box.Should().Be(new BoxDimensions(200, 260, 40));
    }

    [Fact]
    public void A_stored_class_of_the_current_model_is_used_and_one_of_another_model_is_worked_out_again()
    {
        var details = Details() with { EstimatedSize = BoxSizeClass.Compact, EstimateModelVersion = SizeEstimate.ModelVersion };
        var stale = details with { EstimateModelVersion = SizeEstimate.ModelVersion + 1 };

        BoxShape.Resolve(Item(null), details, null, ArtRules.Default).Box.Should().Be(new BoxDimensions(100, 140, 30));
        BoxShape.Resolve(Item(null), stale, null, ArtRules.Default).Box.Should().Be(
            SizeEstimate.Dimensions(SizeEstimate.Assign(Details(), null, null)!.Value, ItemKind.Base));
    }

    [Fact]
    public void A_ratio_difference_exactly_at_the_margin_keeps_the_dimensions_and_just_above_it_rebuilds()
    {
        var item = Item(new VersionDimensions(5, 10, 2));
        var atMargin = new ArtFile(250, 440, "m.webp");
        var justAbove = new ArtFile(2500, 4399, "n.webp");

        BoxShape.Resolve(item, null, atMargin, ArtRules.Default).Source.Should().Be(BoxSource.RealSize);
        BoxShape.Resolve(item, null, justAbove, ArtRules.Default).Source.Should().Be(BoxSource.CoverShape);
    }

    [Fact]
    public void A_landscape_flat_cover_turns_real_sizes_of_the_same_proportions_keeping_area_depth_and_source()
    {
        var item = Item(new VersionDimensions(6.3, 8.27, 2.09));
        var cover = new ArtFile(1000, 760, "e-480.webp");

        var shaped = BoxShape.Resolve(item, null, cover, ArtRules.Default);

        shaped.Box.Should().Be(new BoxDimensions(210, 160, 53));
        shaped.Source.Should().Be(BoxSource.RealSize);
        shaped.PoseHeightMm.Should().Be(210);
    }

    [Fact]
    public void With_the_orientation_rule_off_real_sizes_stay_portrait_under_a_landscape_cover()
    {
        var item = Item(new VersionDimensions(6.3, 8.27, 2.09));
        var off = ArtRules.Default with { OrientFromCover = false };

        var shaped = BoxShape.Resolve(item, null, new ArtFile(1000, 760, "e-480.webp"), off);

        shaped.Box.Should().Be(new BoxDimensions(160, 210, 53));
        shaped.Source.Should().Be(BoxSource.RealSize);
    }

    [Fact]
    public void A_landscape_cover_turns_real_sizes_whose_rebuilt_front_would_not_be_believable()
    {
        var item = Item(new VersionDimensions(7, 8, 2));

        var shaped = BoxShape.Resolve(item, null, new ArtFile(1000, 50, "w.webp"), ArtRules.Default);

        shaped.Source.Should().Be(BoxSource.RealSize);
        shaped.Box.Should().Be(new BoxDimensions(203, 178, 51));
        shaped.PoseHeightMm.Should().Be(203);
    }

    [Fact]
    public void An_unsure_picture_wider_than_the_margin_turns_real_sizes_without_reshaping_them()
    {
        var item = Item(new VersionDimensions(6.3, 8.27, 2.09));

        var shaped = BoxShape.Resolve(item, null, null, ArtRules.Default, new ArtFile(1300, 1000, "u.webp"));

        shaped.Box.Should().Be(new BoxDimensions(210, 160, 53));
        shaped.Source.Should().Be(BoxSource.RealSize);
        shaped.PoseHeightMm.Should().Be(210);
    }

    [Fact]
    public void An_unsure_picture_at_the_margin_or_in_portrait_leaves_the_box_portrait()
    {
        var item = Item(new VersionDimensions(6.3, 8.27, 2.09));
        var portrait = new BoxDimensions(160, 210, 53);

        BoxShape.Resolve(item, null, null, ArtRules.Default, new ArtFile(1200, 1000, "m.webp")).Box.Should().Be(portrait);
        BoxShape.Resolve(item, null, null, ArtRules.Default, new ArtFile(1201, 1000, "n.webp")).Box.Should().Be(new BoxDimensions(210, 160, 53));
        BoxShape.Resolve(item, null, null, ArtRules.Default, new ArtFile(800, 1000, "p.webp")).Box.Should().Be(portrait);
        BoxShape.Resolve(item, null, null, ArtRules.Default with { UnsureLandscapeMarginPercent = 40 }, new ArtFile(1300, 1000, "q.webp"))
            .Box.Should().Be(portrait);
    }

    [Fact]
    public void An_unsure_landscape_picture_turns_an_estimated_or_default_box_and_keeps_its_source()
    {
        var details = Details();
        var estimate = SizeEstimate.Dimensions(SizeEstimate.Assign(details, null, null)!.Value, ItemKind.Base);
        var picture = new ArtFile(1300, 1000, "u.webp");

        var estimated = BoxShape.Resolve(Item(null), details, null, ArtRules.Default, picture);
        var nothing = BoxShape.Resolve(Item(null), null, null, ArtRules.Default, picture);

        estimated.Box.Should().Be(new BoxDimensions(estimate.HeightMm, estimate.WidthMm, estimate.DepthMm));
        estimated.Source.Should().Be(BoxSource.Estimate);
        estimated.PoseHeightMm.Should().Be(estimate.HeightMm);
        nothing.Box.Should().Be(new BoxDimensions(300, 225, 60));
        nothing.Source.Should().Be(BoxSource.Default);
        nothing.PoseHeightMm.Should().Be(300);
    }

    [Fact]
    public void An_unsure_picture_never_turns_a_box_when_pictures_may_not_orient_boxes()
    {
        var off = ArtRules.Default with { OrientFromCover = false };
        var picture = new ArtFile(1300, 1000, "u.webp");

        BoxShape.Resolve(Item(new VersionDimensions(6.3, 8.27, 2.09)), null, null, off, picture).Box.Should().Be(new BoxDimensions(160, 210, 53));
        BoxShape.Resolve(Item(null), null, null, off, picture).Box.Should().Be(new BoxDimensions(225, 300, 60));
    }

    [Fact]
    public void A_wider_margin_keeps_what_the_default_margin_rebuilds()
    {
        var item = Item(new VersionDimensions(6.3, 8.27, 2.09));
        var wider = ArtRules.Default with { ShapeMarginPercent = 20 };
        var between = new ArtFile(660, 1000, "d-480.webp");

        BoxShape.Resolve(item, null, between, ArtRules.Default).Source.Should().Be(BoxSource.CoverShape);
        BoxShape.Resolve(item, null, between, wider).Source.Should().Be(BoxSource.RealSize);
    }

    [Fact]
    public void A_landscape_cover_orients_a_rebuilt_front_only_when_the_rule_is_on()
    {
        var item = Item(new VersionDimensions(6.3, 8.27, 2.09));
        var off = ArtRules.Default with { OrientFromCover = false };

        var on = BoxShape.Resolve(item, null, LandscapeCover, ArtRules.Default).Box;
        var portrait = BoxShape.Resolve(item, null, LandscapeCover, off).Box;

        on.WidthMm.Should().BeGreaterThan(on.HeightMm);
        portrait.WidthMm.Should().BeLessThan(portrait.HeightMm);
        var withoutDimensions = BoxShape.Resolve(Item(null), Details(), LandscapeCover, off).Box;
        withoutDimensions.WidthMm.Should().BeLessThan(withoutDimensions.HeightMm);
        BoxShape.Resolve(item, null, PortraitCover, ArtRules.Default).Box.WidthMm.Should().BeLessThan(BoxShape.Resolve(item, null, PortraitCover, ArtRules.Default).Box.HeightMm);
    }

    [Fact]
    public void A_rebuilt_front_outside_the_believable_range_leaves_the_dimensions_as_they_are()
    {
        var item = Item(new VersionDimensions(7, 7, 2));

        var shaped = BoxShape.Resolve(item, null, new ArtFile(50, 1000, "s.webp"), ArtRules.Default);

        shaped.Source.Should().Be(BoxSource.RealSize);
        shaped.Box.Should().Be(BoxFromVersion.TryMap(item.Dimensions));
    }

    [Fact]
    public void Plausibility_bounds_stay_inclusive()
    {
        BoxFromVersion.TryMap(new VersionDimensions(50 / 25.4, 700 / 25.4, 5 / 25.4)).Should().Be(new BoxDimensions(50, 700, 5));
        BoxFromVersion.TryMap(new VersionDimensions(49 / 25.4, 700 / 25.4, 5 / 25.4)).Should().BeNull();
        BoxFromVersion.TryMap(new VersionDimensions(50 / 25.4, 701 / 25.4, 5 / 25.4)).Should().BeNull();
        BoxFromVersion.TryMap(new VersionDimensions(50 / 25.4, 700 / 25.4, 300 / 25.4)).Should().Be(new BoxDimensions(50, 700, 300));
        BoxFromVersion.TryMap(new VersionDimensions(50 / 25.4, 700 / 25.4, 301 / 25.4)).Should().BeNull();
    }

    private static SnapshotItem Item(VersionDimensions? dimensions, ItemKind kind = ItemKind.Base) =>
        new(1, 2, "Invented Game", kind, null, dimensions, null);

    private static GameDetails Details() =>
        new(Moment, 2, 4, 60, null, null, null, 2.0, null, null, [], [], [], null);
}

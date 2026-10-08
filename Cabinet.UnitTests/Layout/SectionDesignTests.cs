using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Pins the self-checking of section designs and the scaling of oversize boxes: every shipped design validates, a
/// mistake in a hand-edited design is named instead of looping the engine, and the worst-case families and the biggest
/// plausible boxes still land inside one cubby of an empty section.
/// </summary>
public class SectionDesignTests
{
    private const int ExpansionCount = 30;
    private const int ThickDepthMm = 55;
    private const int ThinDepthMm = 30;
    private const int FirstBaseId = 1;

    private static readonly LayoutOptions AllCovers = new(0, CoverStrategy.SizeWeighted, 6, 100);
    private static readonly LayoutOptions AllSpines = new(0, CoverStrategy.SizeWeighted, 6, 0, CoverFromExpansions: 0);

    public static TheoryData<string> DesignNames => new(SectionDesigns.All.Select(design => design.Name));

    [Theory]
    [MemberData(nameof(DesignNames))]
    [Trait("Category", "Layout")]
    public void Every_shipped_design_passes_validation(string name)
    {
        SectionDesigns.TryGet(name, out var design).Should().BeTrue();

        var act = design!.Validate;

        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_shipped_designs_derive_their_interior_height_from_their_rows()
    {
        SectionDesigns.Desktop.InteriorHeightMm.Should().Be(1850);
        SectionDesigns.Phone.InteriorHeightMm.Should().Be(2560);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_row_whose_widths_do_not_add_up_is_named_by_its_position()
    {
        var design = new SectionDesign("test", 640, 20, [new ShelfRow(300, [300, 320]), new ShelfRow(300, [300, 300])])
        {
            StackColumnWidthMm = 190,
        };

        var act = design.Validate;

        act.Should().Throw<InvalidOperationException>().WithMessage("*row 2*620*640*");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_design_with_no_rows_is_rejected()
    {
        var design = new SectionDesign("test", 640, 20, []);

        var act = design.Validate;

        act.Should().Throw<InvalidOperationException>().WithMessage("*no rows*");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_design_whose_anchor_cubby_is_narrower_than_its_tallest_height_is_rejected()
    {
        var design = new SectionDesign("test", 300, 20, [new ShelfRow(400, [300])]) { StackColumnWidthMm = 60 };

        var act = design.Validate;

        act.Should().Throw<InvalidOperationException>().WithMessage("*anchor*narrower*");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_design_whose_box_floor_exceeds_the_tallest_layer_is_rejected()
    {
        var design = SectionDesigns.Desktop with { MinBoxThicknessMm = SectionDesigns.Desktop.MaxLayerHeightMm + 1 };

        var act = design.Validate;

        act.Should().Throw<InvalidOperationException>().WithMessage("*minimum box thickness*maximum layer height*");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_design_whose_two_line_upright_width_is_below_its_minimum_upright_width_is_rejected()
    {
        var design = SectionDesigns.Desktop with { TwoLineUprightWidthMm = SectionDesigns.Desktop.MinUprightExpansionWidthMm - 1 };

        var act = design.Validate;

        act.Should().Throw<InvalidOperationException>().WithMessage("*two-line upright width*minimum upright width*");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_design_whose_two_line_orphan_height_is_below_its_minimum_orphan_height_is_rejected()
    {
        var design = SectionDesigns.Phone with { TwoLineOrphanHeightMm = SectionDesigns.Phone.MinOrphanHeightMm - 1 };

        var act = design.Validate;

        act.Should().Throw<InvalidOperationException>().WithMessage("*two-line orphan height*minimum orphan height*");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_design_that_cannot_hold_the_deepest_spine_with_the_stack_column_is_rejected()
    {
        var design = new SectionDesign("test", 300, 20, [new ShelfRow(300, [300])]);

        var act = design.Validate;

        act.Should().Throw<InvalidOperationException>().WithMessage("*stack column*");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_design_reports_every_problem_in_one_message()
    {
        var design = new SectionDesign("test", 640, 20, [new ShelfRow(300, [300, 300]), new ShelfRow(300, [300, 300])]);

        var act = design.Validate;

        act.Should().Throw<InvalidOperationException>().WithMessage("*row 1*row 2*");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Building_with_an_invalid_design_throws_before_any_placement()
    {
        var design = new SectionDesign("test", 640, 20, [new ShelfRow(300, [300, 300])]);
        var items = SyntheticCollections.Random(1, 5);

        var act = () => CabinetLayoutEngine.Build(items, design);

        act.Should().Throw<InvalidOperationException>().WithMessage("*'test' section design is not valid*");
    }

    [Theory]
    [MemberData(nameof(DesignNames))]
    [Trait("Category", "Layout")]
    public void A_base_game_at_the_family_limits_with_thin_expansions_stands_in_its_cubby_and_the_next_with_a_marker(string name)
    {
        var design = DesignNamed(name);
        var limits = design.Limits;
        var baseGame = BaseOf(limits.MaxFamilyBaseWidthMm, limits.MaxHeightMm, depth: 50);
        var items = FamilyOf(baseGame, ExpansionCount, ThinDepthMm);

        var layout = CabinetLayoutEngine.Build(items, design, AllCovers);

        LayoutAssertions.AssertValid(layout, items);
        var family = LayoutAssertions.PlacementsWithPosition(layout).ToList();
        family.Select(entry => (entry.Section, entry.Cubby)).Distinct().Should().HaveCountLessThanOrEqualTo(2, "the family stands in one cubby or in two neighbouring ones");
        family.Should().Contain(entry => entry.Placement.Kind == PlacementKind.Cover && entry.Placement.WidthMm == limits.MaxFamilyBaseWidthMm);
        family.Should().Contain(entry => entry.Placement.Kind == PlacementKind.MoreMarker, "thirty expansions never all fit");
        family.Should().NotContain(entry => entry.Placement.Kind == PlacementKind.ExpansionSpine);
    }

    [Theory]
    [MemberData(nameof(DesignNames))]
    [Trait("Category", "Layout")]
    public void A_base_game_at_the_family_limits_with_thick_expansions_stacks_them_all_and_shows_a_marker(string name)
    {
        var design = DesignNamed(name);
        var limits = design.Limits;
        var baseGame = BaseOf(limits.MaxFamilyBaseWidthMm, limits.MaxHeightMm, depth: 50);
        var items = FamilyOf(baseGame, ExpansionCount, ThickDepthMm);

        var layout = CabinetLayoutEngine.Build(items, design, AllCovers);

        LayoutAssertions.AssertValid(layout, items);
        var placed = LayoutAssertions.PlacementsWithPosition(layout).ToList();
        placed.Select(entry => (entry.Section, entry.Cubby)).Distinct().Should().HaveCountLessThanOrEqualTo(2, "the family stands in one cubby or in two neighbouring ones");
        placed.Should().NotContain(entry => entry.Placement.Kind == PlacementKind.ExpansionSpine, "no room is left beside a base game at the width limit");
        placed.Should().Contain(entry => entry.Placement.Kind == PlacementKind.ExpansionLayer);
        placed.Should().Contain(entry => entry.Placement.Kind == PlacementKind.MoreMarker);
    }

    [Theory]
    [MemberData(nameof(DesignNames))]
    [Trait("Category", "Layout")]
    public void A_narrow_spine_base_with_thick_expansions_stands_two_upright_and_stacks_the_rest_with_a_marker(string name)
    {
        var design = DesignNamed(name);
        var baseGame = BaseOf(220, 300, depth: 40);
        var items = FamilyOf(baseGame, ExpansionCount, ThickDepthMm);

        var layout = CabinetLayoutEngine.Build(items, design, AllSpines);

        LayoutAssertions.AssertValid(layout, items);
        var placed = LayoutAssertions.PlacementsWithPosition(layout).ToList();
        placed.Select(entry => (entry.Section, entry.Cubby)).Distinct().Should().HaveCountLessThanOrEqualTo(2, "the family stands in one cubby or in two neighbouring ones");
        placed.Count(entry => entry.Placement.Kind == PlacementKind.ExpansionSpine).Should().Be(Orientation.MaxUprightExpansions);
        placed.Should().Contain(entry => entry.Placement.Kind == PlacementKind.ExpansionLayer);
        placed.Should().Contain(entry => entry.Placement.Kind == PlacementKind.MoreMarker);
    }

    [Theory]
    [MemberData(nameof(DesignNames))]
    [Trait("Category", "Layout")]
    public void A_box_far_larger_than_the_design_is_drawn_within_the_limits_with_its_proportions_and_a_capped_depth(string name)
    {
        var design = DesignNamed(name);
        var limits = design.Limits;
        var huge = new CabinetItem(FirstBaseId, 1, "Invented Huge Box", ItemKind.Base, new BoxDimensions(600, 600, 200), []);

        var cover = SinglePlacement(CabinetLayoutEngine.Build([huge], design, AllCovers));
        var spine = SinglePlacement(CabinetLayoutEngine.Build([huge], design, AllSpines));

        cover.Kind.Should().Be(PlacementKind.Cover);
        cover.WidthMm.Should().BeLessThanOrEqualTo(limits.MaxWidthMm);
        cover.HeightMm.Should().BeLessThanOrEqualTo(limits.MaxHeightMm);
        Math.Abs(cover.WidthMm - cover.HeightMm).Should().BeLessThanOrEqualTo(1, "a square front stays square");
        spine.Kind.Should().Be(PlacementKind.Spine);
        spine.WidthMm.Should().Be(limits.MaxDepthMm);
        spine.HeightMm.Should().BeLessThanOrEqualTo(limits.MaxHeightMm);
    }

    [Theory]
    [MemberData(nameof(DesignNames))]
    [Trait("Category", "Layout")]
    public void A_wide_base_game_standing_as_a_spine_keeps_its_full_height_with_or_without_expansions(string name)
    {
        var design = DesignNamed(name);
        var baseGame = BaseOf(400, 360, depth: 70);
        var family = FamilyOf(baseGame, 3, ThinDepthMm);

        var alone = SinglePlacement(CabinetLayoutEngine.Build([baseGame], design, AllSpines));
        var withExpansions = CabinetLayoutEngine.Build(family, design, AllSpines);

        LayoutAssertions.AssertValid(withExpansions, family);
        var familyBase = withExpansions.Sections.SelectMany(section => section.Cubbies).SelectMany(cubby => cubby.Placements)
            .Single(placement => placement.GameId == baseGame.BggId && placement.Kind == PlacementKind.Spine);
        design.Limits.MaxFamilyBaseWidthMm.Should().BeLessThan(400, "the box is wider than a family base may be when it faces out");
        alone.Kind.Should().Be(PlacementKind.Spine);
        alone.HeightMm.Should().Be(360);
        familyBase.HeightMm.Should().Be(360, "how wide the front is does not matter for a box that stands as a spine");
        familyBase.WidthMm.Should().Be(alone.WidthMm);
    }

    [Theory]
    [MemberData(nameof(DesignNames))]
    [Trait("Category", "Layout")]
    public void A_wide_base_game_facing_out_with_expansions_is_scaled_to_the_family_width_limit(string name)
    {
        var design = DesignNamed(name);
        var limits = design.Limits;
        var baseGame = BaseOf(500, 360, depth: 70);
        var family = FamilyOf(baseGame, 3, ThinDepthMm);

        var layout = CabinetLayoutEngine.Build(family, design, AllCovers);

        LayoutAssertions.AssertValid(layout, family);
        var familyBase = layout.Sections.SelectMany(section => section.Cubbies).SelectMany(cubby => cubby.Placements)
            .Single(placement => placement.GameId == baseGame.BggId);
        familyBase.Kind.Should().Be(PlacementKind.Cover);
        familyBase.WidthMm.Should().Be(limits.MaxFamilyBaseWidthMm);
        familyBase.HeightMm.Should().Be(360 * limits.MaxFamilyBaseWidthMm / 500);
    }

    [Theory]
    [InlineData("65", 1)]
    [InlineData("400", 5)]
    [Trait("Category", "Layout")]
    public void The_samples_hold_a_few_oversize_boxes_and_build_valid_layouts_on_both_profiles(string name, int oversizeCount)
    {
        SyntheticCollections.TryGetSample(name, out var items);
        var limits = SectionDesigns.Desktop.Limits;

        items.Count(item => item.Box.WidthMm > limits.MaxWidthMm || item.Box.HeightMm > limits.MaxHeightMm)
            .Should().Be(oversizeCount);

        foreach (var design in SectionDesigns.All)
        {
            LayoutAssertions.AssertValid(CabinetLayoutEngine.Build(items, design), items);
        }
    }

    private static SectionDesign DesignNamed(string name)
    {
        SectionDesigns.TryGet(name, out var design).Should().BeTrue();

        return design!;
    }

    private static Placement SinglePlacement(CabinetLayout layout) =>
        layout.Sections.Should().ContainSingle().Subject.Cubbies.SelectMany(cubby => cubby.Placements).Should().ContainSingle().Subject;

    private static CabinetItem BaseOf(int width, int height, int depth) =>
        new(FirstBaseId, 1, "Invented Family Base", ItemKind.Base, new BoxDimensions(width, height, depth), []);

    private static List<CabinetItem> FamilyOf(CabinetItem baseGame, int expansions, int depth) =>
    [
        baseGame,
        .. Enumerable.Range(1, expansions).Select(number => new CabinetItem(
            baseGame.BggId + number,
            baseGame.CollectionId + number,
            $"Invented Expansion {number}",
            ItemKind.Expansion,
            new BoxDimensions(120, 200, depth),
            [new BaseGameRef(baseGame.BggId, baseGame.Title)])),
    ];
}

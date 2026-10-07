using Cabinet.Domain.Layout;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Pins that a thin box is drawn at its real thickness down to the one-line floor of the profile, and that the layout says
/// from millimetres alone whether the box has room for the second line naming its base game.
/// </summary>
public class ThinBoxTests
{
    private static readonly LayoutOptions SpinesOnly = new(0, CoverStrategy.SizeWeighted, 6, 0);

    public static TheoryData<string> Profiles => new(SectionDesigns.DesktopName, SectionDesigns.PhoneName);

    [Fact]
    [Trait("Category", "Layout")]
    public void On_desktop_a_thin_upright_expansion_is_drawn_at_its_real_width_and_shows_its_title_alone()
    {
        var baseGame = BaseOf(1);
        var thin = ExpansionOf(2, baseGame, depth: 55);

        var upright = Uprights(SectionDesigns.Desktop, baseGame, thin).Single();

        upright.WidthMm.Should().Be(55);
        upright.ShowBaseLine.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void On_desktop_an_upright_expansion_wide_enough_for_two_lines_shows_both()
    {
        var baseGame = BaseOf(1);
        var wide = ExpansionOf(2, baseGame, depth: 70);

        var upright = Uprights(SectionDesigns.Desktop, baseGame, wide).Single();

        upright.WidthMm.Should().Be(70);
        upright.ShowBaseLine.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void On_a_phone_a_thin_upright_expansion_is_drawn_at_the_tap_floor_and_shows_its_title_alone()
    {
        var baseGame = BaseOf(1);
        var thin = ExpansionOf(2, baseGame, depth: 55);

        var upright = Uprights(SectionDesigns.Phone, baseGame, thin).Single();

        upright.WidthMm.Should().Be(SectionDesigns.Phone.MinBoxThicknessMm).And.Be(59);
        upright.ShowBaseLine.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void On_a_phone_an_upright_expansion_wide_enough_for_two_lines_shows_both()
    {
        var baseGame = BaseOf(1);
        var wide = ExpansionOf(2, baseGame, depth: 80);

        var upright = Uprights(SectionDesigns.Phone, baseGame, wide).Single();

        upright.WidthMm.Should().Be(80);
        upright.ShowBaseLine.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void On_desktop_a_thin_orphan_box_is_drawn_at_the_floor_and_a_deep_one_at_its_real_height()
    {
        var thin = OrphanOf(1, depth: 30);
        var deep = OrphanOf(2, depth: 90);

        var placements = Place(SectionDesigns.Desktop, thin, deep);

        var thinBox = placements.Single(placement => placement.GameId == 1);
        var deepBox = placements.Single(placement => placement.GameId == 2);
        thinBox.Kind.Should().Be(PlacementKind.OrphanExpansion);
        thinBox.HeightMm.Should().Be(34);
        thinBox.ShowBaseLine.Should().BeFalse();
        deepBox.HeightMm.Should().Be(90);
        deepBox.ShowBaseLine.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void On_a_phone_a_thin_orphan_box_is_drawn_at_the_tap_floor()
    {
        var thin = OrphanOf(1, depth: 30);
        var deep = OrphanOf(2, depth: 95);

        var placements = Place(SectionDesigns.Phone, thin, deep);

        var thinBox = placements.Single(placement => placement.GameId == 1);
        var deepBox = placements.Single(placement => placement.GameId == 2);
        thinBox.HeightMm.Should().Be(59);
        thinBox.ShowBaseLine.Should().BeFalse();
        deepBox.HeightMm.Should().Be(95);
        deepBox.ShowBaseLine.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    [Trait("Category", "Layout")]
    public void Expansion_layers_are_never_thinner_than_the_one_line_floor(string profile)
    {
        SectionDesigns.TryGet(profile, out var design).Should().BeTrue();
        var baseGame = BaseOf(1);
        var thinLayer = ExpansionOf(2, baseGame, depth: 10);

        var layer = Place(design!, baseGame, thinLayer).Single(placement => placement.Kind == PlacementKind.ExpansionLayer);

        layer.HeightMm.Should().BeGreaterThanOrEqualTo(Math.Max(design!.MinLayerHeightMm, design.MinBoxThicknessMm));
        layer.HeightMm.Should().BeGreaterThanOrEqualTo(34);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Desktop_layers_may_be_drawn_down_to_thirty_four_millimetres_and_no_thicker_than_seventy()
    {
        SectionDesigns.Desktop.MinLayerHeightMm.Should().Be(34);
        SectionDesigns.Desktop.MaxLayerHeightMm.Should().Be(70);
    }

    [Theory]
    [MemberData(nameof(SampleCases))]
    [Trait("Category", "Layout")]
    public void Only_upright_expansions_and_orphan_boxes_carry_a_base_line_decision_and_it_follows_the_drawn_size(string profile, string sample)
    {
        SectionDesigns.TryGet(profile, out var design).Should().BeTrue();
        Cabinet.Domain.Samples.SyntheticCollections.TryGetSample(sample, out var items).Should().BeTrue();

        var layout = CabinetLayoutEngine.Build(items, design!);
        var placements = layout.Sections.SelectMany(section => section.Cubbies).SelectMany(cubby => cubby.Placements).ToList();

        placements.Where(placement => placement.Kind != PlacementKind.ExpansionSpine && placement.Kind != PlacementKind.OrphanExpansion)
            .Should().OnlyContain(placement => placement.ShowBaseLine == null);
        placements.Where(placement => placement.Kind == PlacementKind.ExpansionSpine)
            .Should().OnlyContain(placement => placement.ShowBaseLine == (placement.WidthMm >= design!.TwoLineUprightWidthMm)
                && placement.WidthMm >= Math.Max(design.MinUprightExpansionWidthMm, design.MinBoxThicknessMm));
        placements.Where(placement => placement.Kind == PlacementKind.OrphanExpansion)
            .Should().OnlyContain(placement => placement.ShowBaseLine == (placement.HeightMm >= design!.TwoLineOrphanHeightMm)
                && placement.HeightMm >= Math.Max(design.MinOrphanHeightMm, design.MinBoxThicknessMm));
    }

    public static TheoryData<string, string> SampleCases => new()
    {
        { SectionDesigns.DesktopName, "65" },
        { SectionDesigns.DesktopName, "400" },
        { SectionDesigns.PhoneName, "65" },
        { SectionDesigns.PhoneName, "400" },
    };

    private static List<Placement> Place(SectionDesign design, params CabinetItem[] items) =>
        [.. CabinetLayoutEngine.Build(items, design, SpinesOnly).Sections
            .SelectMany(section => section.Cubbies)
            .SelectMany(cubby => cubby.Placements)];

    private static List<Placement> Uprights(SectionDesign design, params CabinetItem[] items) =>
        [.. Place(design, items).Where(placement => placement.Kind == PlacementKind.ExpansionSpine)];

    private static CabinetItem BaseOf(int bggId) =>
        new(bggId, bggId, $"Invented Base {bggId}", ItemKind.Base, new BoxDimensions(150, 250, 40), []);

    private static CabinetItem ExpansionOf(int bggId, CabinetItem baseGame, int depth) =>
        new(
            bggId,
            bggId,
            $"Invented Expansion {bggId}",
            ItemKind.Expansion,
            new BoxDimensions(100, 200, depth),
            [new BaseGameRef(baseGame.BggId, baseGame.Title)]);

    private static CabinetItem OrphanOf(int bggId, int depth) =>
        new(
            bggId,
            bggId,
            $"Invented Orphan {bggId}",
            ItemKind.Expansion,
            new BoxDimensions(120, 200, depth),
            [new BaseGameRef(900 + bggId, $"Invented Absent Base {bggId}")]);
}

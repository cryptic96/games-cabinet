using Cabinet.Domain.Layout;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Pins what every drawn box says about itself: which collection entry it is, so two copies of one game stay two boxes,
/// and whether it is an expansion, even when no base game is known.
/// </summary>
public class PlacementIdentityTests
{
    private const int ThinDepthMm = 30;
    private static readonly SectionDesign Design = SectionDesigns.Desktop;
    private static readonly LayoutOptions AllSpines = new(0, CoverStrategy.SizeWeighted, 6, 0);

    [Fact]
    [Trait("Category", "Layout")]
    public void An_expansion_without_a_known_base_game_is_flagged_as_an_expansion_and_names_no_base()
    {
        var orphan = new CabinetItem(10, 7, "Invented Orphan Pack", ItemKind.Expansion, new BoxDimensions(120, 200, 40), []);

        var placement = AllPlacements(CabinetLayoutEngine.Build([orphan], Design, AllSpines)).Should().ContainSingle().Subject;

        placement.IsExpansion.Should().BeTrue();
        placement.BaseTitle.Should().BeNull();
        placement.EntryId.Should().Be(7);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_base_game_is_not_flagged_as_an_expansion()
    {
        var game = new CabinetItem(1, 3, "Invented Base Game", ItemKind.Base, new BoxDimensions(200, 300, 60), []);

        var placement = AllPlacements(CabinetLayoutEngine.Build([game], Design, AllSpines)).Should().ContainSingle().Subject;

        placement.IsExpansion.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Two_copies_of_one_game_are_two_placements_with_their_own_entry_ids()
    {
        var first = new CabinetItem(1, 11, "Invented Twin Game", ItemKind.Base, new BoxDimensions(200, 300, 60), []);
        var second = first with { CollectionId = 12 };

        var placements = AllPlacements(CabinetLayoutEngine.Build([first, second], Design, AllSpines));

        placements.Should().HaveCount(2);
        placements.Select(placement => placement.GameId).Should().OnlyContain(id => id == 1);
        placements.Select(placement => placement.EntryId).Should().BeEquivalentTo([11L, 12L]);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_stack_layer_carries_the_entry_of_its_expansion_and_the_marker_the_entry_of_its_base_game()
    {
        var baseGame = new CabinetItem(1, 100, "Invented Family Base", ItemKind.Base, new BoxDimensions(220, 300, 50), []);
        var items = new List<CabinetItem> { baseGame };
        items.AddRange(Enumerable.Range(1, 40).Select(number => new CabinetItem(
            1 + number,
            200 + number,
            $"Invented Expansion {number}",
            ItemKind.Expansion,
            new BoxDimensions(120, 200, ThinDepthMm),
            [new BaseGameRef(baseGame.BggId, baseGame.Title)])));

        var placements = AllPlacements(CabinetLayoutEngine.Build(items, Design, AllSpines));
        var layers = placements.Where(placement => placement.Kind == PlacementKind.ExpansionLayer).ToList();
        var marker = placements.Should().ContainSingle(placement => placement.Kind == PlacementKind.MoreMarker).Subject;

        layers.Should().NotBeEmpty();
        layers.Should().OnlyContain(layer => layer.IsExpansion == true && layer.EntryId == 200 + (layer.GameId - 1));
        marker.EntryId.Should().Be(100);
        marker.IsExpansion.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_serialised_layout_has_an_entry_id_on_every_placement_and_never_states_that_a_box_is_not_an_expansion()
    {
        var game = new CabinetItem(1, 3, "Invented Base Game", ItemKind.Base, new BoxDimensions(200, 300, 60), []);
        var orphan = new CabinetItem(10, 7, "Invented Orphan Pack", ItemKind.Expansion, new BoxDimensions(120, 200, 40), []);
        var layout = CabinetLayoutEngine.Build([game, orphan], Design, AllSpines);

        var json = LayoutJson.Serialize(layout);

        var placementCount = AllPlacements(layout).Count;
        System.Text.RegularExpressions.Regex.Matches(json, "\"entryId\":").Count.Should().Be(placementCount);
        json.Should().Contain("\"isExpansion\":true");
        json.Should().NotContain("\"isExpansion\":false");
        json.Should().NotContain("\"isExpansion\":null");
    }

    private static List<Placement> AllPlacements(CabinetLayout layout) =>
        layout.Sections.SelectMany(section => section.Cubbies).SelectMany(cubby => cubby.Placements).ToList();
}

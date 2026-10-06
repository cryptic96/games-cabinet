using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Pins how expansions stand beside their base games: one thin layer each in a fixed column right of the base, stacked up
/// from the floor, collapsing into a marker that counts the rest, and always in the cubby of the base game.
/// </summary>
public class FamilyLayoutTests
{
    private static readonly SectionDesign Design = SectionDesigns.Desktop;
    private static readonly LayoutOptions SpinesOnly = new(0, CoverStrategy.SizeWeighted, 6, 0);

    public static TheoryData<string> LargeSamples => new("65", "400");

    [Theory]
    [MemberData(nameof(LargeSamples))]
    [Trait("Category", "Layout")]
    public void Every_expansion_with_an_owned_base_is_a_layer_right_of_its_base_or_counted_in_its_marker(string name)
    {
        SyntheticCollections.TryGetSample(name, out var items);
        var layout = CabinetLayoutEngine.Build(items, Design);
        var placed = LayoutAssertions.PlacementsWithPosition(layout);
        var familyCount = 0;

        foreach (var family in FamiliesOf(items))
        {
            familyCount++;
            var baseEntry = placed.Single(entry => entry.Placement.GameId == family.Key && IsStanding(entry.Placement));
            var layers = LayersOf(placed, family.Key);
            var marker = placed.SingleOrDefault(entry => entry.Placement.Kind == PlacementKind.MoreMarker && entry.Placement.FamilyId == family.Key);

            layers.Should().OnlyContain(layer => layer.Placement.XMm == baseEntry.Placement.XMm + baseEntry.Placement.WidthMm, "family {0}", family.Key);
            layers.Should().OnlyContain(layer => layer.Placement.WidthMm == Design.StackColumnWidthMm);
            (layers.Count + (marker.Placement?.MoreCount ?? 0)).Should().Be(family.Count(), "family {0}", family.Key);
        }

        familyCount.Should().BeGreaterThan(0);
    }

    [Theory]
    [MemberData(nameof(LargeSamples))]
    [Trait("Category", "Layout")]
    public void Layers_touch_each_other_from_the_floor_up_in_collection_order_and_stay_under_the_shelf_above(string name)
    {
        SyntheticCollections.TryGetSample(name, out var items);
        var layout = CabinetLayoutEngine.Build(items, Design);
        var placed = LayoutAssertions.PlacementsWithPosition(layout);

        foreach (var family in FamiliesOf(items))
        {
            var baseEntry = placed.Single(entry => entry.Placement.GameId == family.Key && IsStanding(entry.Placement));
            var cubbyHeight = layout.Sections[baseEntry.Section].Cubbies[baseEntry.Cubby].HeightMm;
            var layers = LayersOf(placed, family.Key);
            var expected = family.OrderBy(item => item.CollectionId).ThenBy(item => item.BggId).Select(item => item.BggId).Take(layers.Count);
            var top = 0;

            layers.Select(layer => layer.Placement.GameId).Should().Equal(expected, "family {0} lists its layers in collection order", family.Key);

            foreach (var layer in layers)
            {
                layer.Placement.YMm.Should().Be(top, "layers of family {0} touch from the floor up", family.Key);
                top += layer.Placement.HeightMm;
            }

            var marker = placed.SingleOrDefault(entry => entry.Placement.Kind == PlacementKind.MoreMarker && entry.Placement.FamilyId == family.Key);

            if (marker.Placement is not null)
            {
                marker.Placement.YMm.Should().Be(top, "the marker sits on the top layer of family {0}", family.Key);
                top += marker.Placement.HeightMm;
            }

            top.Should().BeLessThanOrEqualTo(cubbyHeight, "family {0} stays under the shelf above", family.Key);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_sample_of_sixty_five_has_a_family_that_needs_a_marker_and_names_how_many_are_hidden()
    {
        SyntheticCollections.TryGetSample("65", out var items);
        var layout = CabinetLayoutEngine.Build(items, Design);

        var markers = LayoutAssertions.PlacementsWithPosition(layout)
            .Where(entry => entry.Placement.Kind == PlacementKind.MoreMarker)
            .Select(entry => entry.Placement)
            .ToList();

        markers.Should().NotBeEmpty();
        markers.Should().OnlyContain(marker => marker.MoreCount >= 1 && marker.Label.Length == 0);
        markers.Should().OnlyContain(marker => marker.HeightMm == Design.MarkerHeightMm && marker.WidthMm == Design.StackColumnWidthMm);
        markers.Max(marker => marker.MoreCount).Should().BeGreaterThanOrEqualTo(3, "the family of nine shows at most six layers at the default maximum");
    }

    [Theory]
    [MemberData(nameof(LargeSamples))]
    [Trait("Category", "Layout")]
    public void No_base_game_with_expansions_lies_flat(string name)
    {
        SyntheticCollections.TryGetSample(name, out var items);
        var layout = CabinetLayoutEngine.Build(items, Design);
        var placed = LayoutAssertions.PlacementsWithPosition(layout);

        foreach (var family in FamiliesOf(items))
        {
            placed.Single(entry => entry.Placement.GameId == family.Key && IsStanding(entry.Placement))
                .Placement.Kind.Should().NotBe(PlacementKind.FlatBox, "family {0}", family.Key);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Base_games_with_expansions_face_out_and_stand_as_spines_in_the_large_sample()
    {
        SyntheticCollections.TryGetSample("400", out var items);
        var layout = CabinetLayoutEngine.Build(items, Design);
        var placed = LayoutAssertions.PlacementsWithPosition(layout);

        var kinds = FamiliesOf(items)
            .Select(family => placed.Single(entry => entry.Placement.GameId == family.Key && IsStanding(entry.Placement)).Placement.Kind)
            .Distinct()
            .ToList();

        kinds.Should().Contain([PlacementKind.Cover, PlacementKind.Spine]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(20)]
    [Trait("Category", "Layout")]
    public void No_stack_shows_more_layers_than_the_setting_allows(int stackMax)
    {
        SyntheticCollections.TryGetSample("400", out var items);
        var options = new LayoutOptions(25, CoverStrategy.SizeWeighted, stackMax, 12);

        var layout = CabinetLayoutEngine.Build(items, Design, options);

        var placed = LayoutAssertions.PlacementsWithPosition(layout);
        var biggest = FamiliesOf(items).Max(family => LayersOf(placed, family.Key).Count);
        biggest.Should().BeLessThanOrEqualTo(stackMax);
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_collection_without_expansions_has_no_layer_and_no_marker()
    {
        var items = SyntheticCollections.Random(4, 80);

        var layout = CabinetLayoutEngine.Build(items, Design);

        LayoutAssertions.PlacementsWithPosition(layout)
            .Where(entry => entry.Placement.Kind is PlacementKind.ExpansionLayer or PlacementKind.MoreMarker)
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_base_game_with_one_expansion_shows_exactly_one_layer_and_no_marker()
    {
        var items = SyntheticCollections.Random(6, 40);
        var baseGame = items[3];
        var expansion = SyntheticCollections.NextExpansion(items, baseGame.BggId, 6);

        var layout = CabinetLayoutEngine.Build([.. items, expansion], Design);

        var placed = LayoutAssertions.PlacementsWithPosition(layout);
        LayersOf(placed, baseGame.BggId).Select(layer => layer.Placement.GameId).Should().Equal(expansion.BggId);
        placed.Should().NotContain(entry => entry.Placement.Kind == PlacementKind.MoreMarker);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void An_expansion_that_comes_before_its_base_game_still_stands_in_the_base_games_stack()
    {
        SyntheticCollections.TryGetSample("65", out var items);
        var earlier = items
            .Where(item => item.Kind == ItemKind.Expansion)
            .Select(item => (Expansion: item, ParentId: LayoutAssertions.OwnedParentOf(item, items)))
            .Where(pair => pair.ParentId is not null)
            .Single(pair => pair.Expansion.CollectionId < items.Single(item => item.BggId == pair.ParentId).CollectionId);

        var layout = CabinetLayoutEngine.Build(items, Design);

        var placed = LayoutAssertions.PlacementsWithPosition(layout);
        var baseEntry = placed.Single(entry => entry.Placement.GameId == earlier.ParentId && IsStanding(entry.Placement));
        var layer = placed.Single(entry => entry.Placement.GameId == earlier.Expansion.BggId);
        layer.Placement.Kind.Should().Be(PlacementKind.ExpansionLayer);
        (layer.Section, layer.Cubby).Should().Be((baseEntry.Section, baseEntry.Cubby));
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void An_expansion_for_two_owned_games_joins_the_one_with_the_lower_identifier()
    {
        SyntheticCollections.TryGetSample("65", out var items);
        var twoParents = items.Single(item => item.Kind == ItemKind.Expansion && item.ExpansionOf.Count(reference => items.Any(owned => owned.BggId == reference.BggId)) == 2);
        var lower = twoParents.ExpansionOf.Min(reference => reference.BggId);
        var higher = twoParents.ExpansionOf.Max(reference => reference.BggId);

        var layout = CabinetLayoutEngine.Build(items, Design);

        var placed = LayoutAssertions.PlacementsWithPosition(layout);
        var layer = placed.Single(entry => entry.Placement.GameId == twoParents.BggId);
        layer.Placement.FamilyId.Should().Be(lower);
        LayersOf(placed, higher).Should().NotContain(entry => entry.Placement.GameId == twoParents.BggId);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Layer_and_marker_names_carry_the_expansion_and_base_titles()
    {
        SyntheticCollections.TryGetSample("65", out var items);
        var layout = CabinetLayoutEngine.Build(items, Design);
        var byId = items.ToDictionary(item => item.BggId);

        var layers = LayoutAssertions.PlacementsWithPosition(layout)
            .Select(entry => entry.Placement)
            .Where(placement => placement.Kind is PlacementKind.ExpansionLayer or PlacementKind.MoreMarker)
            .ToList();

        layers.Should().NotBeEmpty();

        foreach (var placement in layers)
        {
            placement.BaseTitle.Should().Be(byId[placement.FamilyId!.Value].Title);
            placement.Title.Should().Be(byId[placement.GameId].Title, "a layer keeps its full title and a marker names its base game");
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Layer_heights_follow_the_expansion_depth_within_the_design_limits()
    {
        var baseGame = BaseOf(1, depth: 60);
        var thin = ExpansionOf(2, baseGame, depth: 10);
        var thick = ExpansionOf(3, baseGame, depth: 200);
        var design = new SectionDesign("test", 620, 20, [new ShelfRow(400, [600])]);

        var layout = CabinetLayoutEngine.Build([baseGame, thin, thick], design, SpinesOnly);

        var layers = layout.Sections[0].Cubbies[0].Placements.Where(placement => placement.Kind == PlacementKind.ExpansionLayer).OrderBy(placement => placement.YMm).ToList();
        layers.Select(layer => layer.HeightMm).Should().Equal(design.MinLayerHeightMm, design.MaxLayerHeightMm);
        layers.Select(layer => layer.YMm).Should().Equal(0, design.MinLayerHeightMm);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_base_game_without_expansions_takes_no_room_for_a_stack_column()
    {
        var design = new SectionDesign("test", 620, 20, [new ShelfRow(300, [300, 300])]);
        var first = BaseOf(1, depth: 100);
        var second = BaseOf(2, depth: 100);

        var plain = CabinetLayoutEngine.Build([first, second], design, SpinesOnly);
        var withStack = CabinetLayoutEngine.Build([first, second, ExpansionOf(3, first, depth: 30)], design, SpinesOnly);

        plain.Sections[0].Cubbies[0].Placements.Select(placement => placement.GameId).Should().BeEquivalentTo([1, 2]);
        withStack.Sections[0].Cubbies[0].Placements.Select(placement => placement.GameId).Should().BeEquivalentTo([1, 3]);
        withStack.Sections[0].Cubbies[1].Placements.Select(placement => placement.GameId).Should().Equal(2);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Expansion_labels_keep_whole_text_elements_and_the_full_title_stays_on_the_placement()
    {
        var baseGame = BaseOf(1, depth: 60);
        var title = string.Concat(Enumerable.Repeat("Brin\U0001F3B2Orvä", 8));
        var expansion = ExpansionOf(2, baseGame, depth: 30) with { Title = title };
        var design = new SectionDesign("test", 620, 20, [new ShelfRow(400, [600])]);

        var layout = CabinetLayoutEngine.Build([baseGame, expansion], design, SpinesOnly);

        var layer = layout.Sections[0].Cubbies[0].Placements.Single(placement => placement.Kind == PlacementKind.ExpansionLayer);
        layer.Title.Should().Be(title);
        layer.Label.Should().Be(SpineLabel.Shorten(title, design.StackColumnWidthMm / design.LabelCharPitchMm));
        layer.Label.Should().NotBe(title);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Collections_with_expansions_build_valid_cabinets_across_seeds()
    {
        for (var seed = 1; seed <= 30; seed++)
        {
            var items = SyntheticCollections.Random(seed, 150, 20);

            var layout = CabinetLayoutEngine.Build(items, Design);

            LayoutAssertions.AssertValid(layout, items);
        }
    }

    private static IEnumerable<IGrouping<int, CabinetItem>> FamiliesOf(IReadOnlyList<CabinetItem> items) =>
        items
            .Where(item => item.Kind == ItemKind.Expansion && LayoutAssertions.OwnedParentOf(item, items) is not null)
            .GroupBy(item => LayoutAssertions.OwnedParentOf(item, items)!.Value);

    private static List<(int Section, int Cubby, Placement Placement)> LayersOf(
        IReadOnlyList<(int Section, int Cubby, Placement Placement)> placed,
        int baseId) =>
        placed
            .Where(entry => entry.Placement.Kind == PlacementKind.ExpansionLayer && entry.Placement.FamilyId == baseId)
            .OrderBy(entry => entry.Placement.YMm)
            .ToList();

    private static bool IsStanding(Placement placement) =>
        placement.Kind is PlacementKind.Cover or PlacementKind.Spine or PlacementKind.FlatBox;

    private static CabinetItem BaseOf(int bggId, int depth) =>
        new(bggId, bggId, $"Invented Base {(char)('a' + (bggId % 26))}", ItemKind.Base, new BoxDimensions(150, 250, depth), []);

    private static CabinetItem ExpansionOf(int bggId, CabinetItem baseGame, int depth) =>
        new(
            bggId,
            bggId,
            $"Invented Expansion {(char)('a' + (bggId % 26))}",
            ItemKind.Expansion,
            new BoxDimensions(100, 200, depth),
            [new BaseGameRef(baseGame.BggId, baseGame.Title)]);
}

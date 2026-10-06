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
    private const int MaxThinDepthMm = 45;
    private static readonly LayoutOptions SpinesOnly = new(0, CoverStrategy.SizeWeighted, 6, 0);
    private static readonly LayoutOptions EveryBoxFacesOut = new(100, CoverStrategy.Random, 6, 0);

    public static TheoryData<string> LargeSamples => new("65", "400");

    [Theory]
    [MemberData(nameof(LargeSamples))]
    [Trait("Category", "Layout")]
    public void Every_expansion_with_an_owned_base_is_an_upright_a_layer_right_of_the_uprights_or_counted_in_its_marker(string name)
    {
        SyntheticCollections.TryGetSample(name, out var items);
        var layout = CabinetLayoutEngine.Build(items, Design);
        var placed = LayoutAssertions.PlacementsWithPosition(layout);
        var familyCount = 0;

        foreach (var family in FamiliesOf(items))
        {
            familyCount++;
            var baseEntry = placed.Single(entry => entry.Placement.GameId == family.Key && IsStanding(entry.Placement));
            var uprights = UprightsOf(placed, family.Key);
            var layers = LayersOf(placed, family.Key);
            var marker = placed.SingleOrDefault(entry => entry.Placement.Kind == PlacementKind.MoreMarker && entry.Placement.FamilyId == family.Key);
            var columnX = baseEntry.Placement.XMm + baseEntry.Placement.WidthMm + uprights.Sum(upright => upright.Placement.WidthMm);

            layers.Should().OnlyContain(layer => layer.Placement.XMm == columnX, "family {0}", family.Key);
            layers.Should().OnlyContain(layer => layer.Placement.WidthMm == Design.StackColumnWidthMm);
            (uprights.Count + layers.Count + (marker.Placement?.MoreCount ?? 0)).Should().Be(family.Count(), "family {0}", family.Key);
        }

        familyCount.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("65", 1)]
    [InlineData("400", 5)]
    [Trait("Category", "Layout")]
    public void Thick_expansions_stand_upright_in_collection_order_until_the_room_or_the_maximum_is_used(string name, int atLeast)
    {
        SyntheticCollections.TryGetSample(name, out var items);
        var layout = CabinetLayoutEngine.Build(items, Design);
        var placed = LayoutAssertions.PlacementsWithPosition(layout);
        var totalUprights = 0;

        foreach (var family in FamiliesOf(items))
        {
            var baseEntry = placed.Single(entry => entry.Placement.GameId == family.Key && IsStanding(entry.Placement));
            var uprights = UprightsOf(placed, family.Key);
            var thick = family
                .OrderBy(item => item.CollectionId).ThenBy(item => item.BggId)
                .Where(Orientation.StandsUpright)
                .ToList();
            totalUprights += uprights.Count;

            uprights.Select(entry => entry.Placement.GameId).Should().Equal(
                thick.Take(uprights.Count).Select(item => item.BggId), "family {0}: uprights are the earliest thick expansions", family.Key);

            if (uprights.Count < Math.Min(Orientation.MaxUprightExpansions, thick.Count))
            {
                var next = thick[uprights.Count];
                var room = Design.Limits.MaxWidthMm - baseEntry.Placement.WidthMm - Design.StackColumnWidthMm
                    - uprights.Sum(entry => entry.Placement.WidthMm);

                Math.Max(Math.Min(next.Box.DepthMm, SectionDesign.MaxBoxDepthMm), Design.MinUprightExpansionWidthMm)
                    .Should().BeGreaterThan(room, "family {0}: the next thick expansion did not fit", family.Key);
            }

            uprights.Select(entry => entry.Placement.GameId)
                .Should().OnlyContain(id => Orientation.StandsUpright(family.Single(item => item.BggId == id)), "family {0}: thin expansions never stand upright", family.Key);
        }

        totalUprights.Should().BeGreaterThanOrEqualTo(atLeast);
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
            var uprightIds = UprightsOf(placed, family.Key).Select(entry => entry.Placement.GameId).ToHashSet();
            var expected = family
                .Where(item => !uprightIds.Contains(item.BggId))
                .OrderBy(item => item.CollectionId).ThenBy(item => item.BggId)
                .Select(item => item.BggId)
                .Take(layers.Count);
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
        markers.Max(marker => marker.MoreCount).Should().BeGreaterThanOrEqualTo(1, "the family of nine has at most two uprights and shows at most six layers at the default maximum");
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
        var expansion = Thin(SyntheticCollections.NextExpansion(items, baseGame.BggId, 6));

        var layout = CabinetLayoutEngine.Build([.. items, expansion], Design);

        var placed = LayoutAssertions.PlacementsWithPosition(layout);
        LayersOf(placed, baseGame.BggId).Select(layer => layer.Placement.GameId).Should().Equal(expansion.BggId);
        placed.Should().NotContain(entry => entry.Placement.Kind == PlacementKind.MoreMarker);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void An_expansion_that_comes_before_its_base_game_still_stands_beside_it_upright_or_in_its_stack()
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
        layer.Placement.Kind.Should().BeOneOf(PlacementKind.ExpansionLayer, PlacementKind.ExpansionSpine);
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
        var uprightOne = ExpansionOf(3, baseGame, depth: 200);
        var uprightTwo = ExpansionOf(4, baseGame, depth: 200);
        var thick = ExpansionOf(5, baseGame, depth: 200);
        var design = new SectionDesign("test", 620, 20, [new ShelfRow(400, [600])]);

        var layout = CabinetLayoutEngine.Build([baseGame, thin, uprightOne, uprightTwo, thick], design, SpinesOnly);

        var placements = layout.Sections[0].Cubbies[0].Placements;
        placements.Where(placement => placement.Kind == PlacementKind.ExpansionSpine).Select(placement => placement.GameId)
            .Should().Equal(3, 4);
        var layers = placements.Where(placement => placement.Kind == PlacementKind.ExpansionLayer).OrderBy(placement => placement.YMm).ToList();
        layers.Select(layer => layer.GameId).Should().Equal(2, 5);
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

    [Fact]
    [Trait("Category", "Layout")]
    public void Orphan_expansions_in_the_sample_are_boxes_named_after_the_lowest_identifier_base_they_name()
    {
        SyntheticCollections.TryGetSample("65", out var items);
        var orphans = items
            .Where(item => item.Kind == ItemKind.Expansion && LayoutAssertions.OwnedParentOf(item, items) is null)
            .ToList();
        orphans.Should().HaveCount(3);

        var layout = CabinetLayoutEngine.Build(items, Design);

        var byId = LayoutAssertions.PlacementsWithPosition(layout)
            .Where(entry => entry.Placement.Kind != PlacementKind.MoreMarker)
            .ToDictionary(entry => entry.Placement.GameId, entry => entry.Placement);

        foreach (var orphan in orphans)
        {
            var placement = byId[orphan.BggId];
            placement.Kind.Should().Be(PlacementKind.OrphanExpansion);
            placement.BaseTitle.Should().Be(orphan.ExpansionOf.OrderBy(reference => reference.BggId).First().Title);
            placement.HeightMm.Should().BeGreaterThanOrEqualTo(Design.MinOrphanHeightMm);
            placement.Title.Should().Be(orphan.Title);
        }

        orphans.Max(orphan => orphan.ExpansionOf[0].Title.Length).Should().BeGreaterThanOrEqualTo(58, "one orphan names a base game with a very long title");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void An_expansion_that_names_no_base_game_is_an_orphan_box_without_a_base_title()
    {
        var items = SyntheticCollections.Random(3, 30);
        var nameless = new CabinetItem(
            items.Max(item => item.BggId) + 1,
            items.Max(item => item.CollectionId) + 1,
            "Invented Nameless Expansion",
            ItemKind.Expansion,
            new BoxDimensions(120, 200, 30),
            []);

        var layout = CabinetLayoutEngine.Build([.. items, nameless], Design);

        var placement = LayoutAssertions.PlacementsWithPosition(layout).Single(entry => entry.Placement.GameId == nameless.BggId).Placement;
        placement.Kind.Should().Be(PlacementKind.OrphanExpansion);
        placement.BaseTitle.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void An_expansion_that_names_another_expansion_or_itself_stands_alone_as_an_orphan()
    {
        var items = SyntheticCollections.Random(8, 30);
        var first = SyntheticCollections.NextOrphanExpansion(items, 8);
        var second = SyntheticCollections.NextOrphanExpansion([.. items, first], 9) with
        {
            ExpansionOf = [new BaseGameRef(first.BggId, first.Title)],
        };
        var selfish = SyntheticCollections.NextOrphanExpansion([.. items, first, second], 10);
        selfish = selfish with { ExpansionOf = [new BaseGameRef(selfish.BggId, selfish.Title)] };
        var all = new List<CabinetItem>([.. items, first, second, selfish]);

        var layout = CabinetLayoutEngine.Build(all, Design);

        var byId = LayoutAssertions.PlacementsWithPosition(layout).ToDictionary(entry => entry.Placement.GameId, entry => entry.Placement);
        byId[second.BggId].Kind.Should().Be(PlacementKind.OrphanExpansion);
        byId[second.BggId].BaseTitle.Should().Be(first.Title);
        byId[selfish.BggId].Kind.Should().Be(PlacementKind.OrphanExpansion);
        LayoutAssertions.AssertValid(layout, all);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Expansions_stacked_beside_their_base_do_not_count_towards_the_few_games_threshold()
    {
        var items = new List<CabinetItem>(SyntheticCollections.Random(5, 11));

        for (var step = 0; step < 5; step++)
        {
            items.Add(SyntheticCollections.NextExpansion(items, items[step].BggId, 100 + step));
        }

        var layout = CabinetLayoutEngine.Build(items, Design);

        var standing = LayoutAssertions.PlacementsWithPosition(layout)
            .Select(entry => entry.Placement)
            .Where(placement => placement.Kind is not (PlacementKind.ExpansionLayer or PlacementKind.MoreMarker or PlacementKind.ExpansionSpine))
            .ToList();
        standing.Should().HaveCount(11).And.OnlyContain(placement => placement.Kind == PlacementKind.Cover);
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Eleven_games_with_one_orphan_expansion_leave_the_few_games_look_and_follow_the_normal_mix()
    {
        var bases = SyntheticCollections.Random(5, 11);
        var orphan = SyntheticCollections.NextOrphanExpansion(bases, 5);

        var layout = CabinetLayoutEngine.Build([.. bases, orphan], Design);

        var byId = LayoutAssertions.PlacementsWithPosition(layout).ToDictionary(entry => entry.Placement.GameId, entry => entry.Placement);
        byId[orphan.BggId].Kind.Should().Be(PlacementKind.OrphanExpansion);

        foreach (var item in bases)
        {
            byId[item.BggId].Kind.Should().Be(
                ToKind(Orientation.Decide(item, LayoutOptions.Default, Design, fewGames: false)),
                "game {0} follows the normal mix at twelve top-level games", item.BggId);
        }

        byId.Values.Should().Contain(placement => placement.Kind != PlacementKind.Cover);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void In_the_few_games_look_an_orphan_expansion_faces_out_and_keeps_the_base_title()
    {
        var bases = SyntheticCollections.Random(5, 4);
        var orphan = SyntheticCollections.NextOrphanExpansion(bases, 5);

        var layout = CabinetLayoutEngine.Build([.. bases, orphan], Design);

        var placement = LayoutAssertions.PlacementsWithPosition(layout).Single(entry => entry.Placement.GameId == orphan.BggId).Placement;
        placement.Kind.Should().Be(PlacementKind.Cover);
        placement.BaseTitle.Should().Be(orphan.ExpansionOf[0].Title);
        placement.Title.Should().Be(orphan.Title);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Orphan_boxes_join_the_flat_stacks_and_never_drop_below_the_orphan_minimum()
    {
        var design = new SectionDesign("test", 620, 20, [new ShelfRow(400, [600])]);
        var thin = new CabinetItem(1, 1, "Invented Thin Expansion", ItemKind.Expansion, new BoxDimensions(100, 200, 15), [new BaseGameRef(900, "Invented Absent Base")]);
        var thick = new CabinetItem(2, 2, "Invented Thick Expansion", ItemKind.Expansion, new BoxDimensions(100, 200, 120), [new BaseGameRef(901, "Invented Other Base")]);

        var layout = CabinetLayoutEngine.Build([thin, thick], design, SpinesOnly);

        var boxes = layout.Sections[0].Cubbies[0].Placements.OrderBy(placement => placement.YMm).ToList();
        boxes.Should().OnlyContain(placement => placement.Kind == PlacementKind.OrphanExpansion);
        boxes.Select(box => box.XMm).Distinct().Should().ContainSingle("both lie in one stack");
        boxes.Select(box => box.HeightMm).OrderBy(height => height).Should().Equal(design.MinOrphanHeightMm, 120);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_narrow_spine_base_shows_two_uprights_and_stacks_the_third_thick_and_the_thin_expansion()
    {
        var design = new SectionDesign("test", 620, 20, [new ShelfRow(400, [600])]);
        var baseGame = BaseOf(1, depth: 40);
        var items = new[]
        {
            baseGame,
            ExpansionOf(2, baseGame, depth: 60),
            ExpansionOf(3, baseGame, depth: 60),
            ExpansionOf(4, baseGame, depth: 60),
            ExpansionOf(5, baseGame, depth: 30),
        };

        var layout = CabinetLayoutEngine.Build(items, design, SpinesOnly);

        var placements = layout.Sections[0].Cubbies[0].Placements;
        var uprights = placements.Where(placement => placement.Kind == PlacementKind.ExpansionSpine).OrderBy(placement => placement.XMm).ToList();
        uprights.Select(placement => placement.GameId).Should().Equal(2, 3);
        uprights.Select(placement => placement.XMm).Should().Equal(40, 40 + design.MinUprightExpansionWidthMm);
        placements.Where(placement => placement.Kind == PlacementKind.ExpansionLayer).Select(placement => placement.GameId)
            .Should().BeEquivalentTo([4, 5]);
        placements.Where(placement => placement.Kind == PlacementKind.ExpansionLayer)
            .Should().OnlyContain(placement => placement.XMm == 40 + (2 * design.MinUprightExpansionWidthMm));
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_cover_base_at_the_family_width_limit_stacks_a_thin_and_a_thick_expansion()
    {
        var design = new SectionDesign("test", 620, 20, [new ShelfRow(400, [600])]);
        var baseGame = new CabinetItem(1, 1, "Invented Wide Base", ItemKind.Base, new BoxDimensions(design.Limits.MaxFamilyBaseWidthMm, 380, 60), []);
        var items = new[] { baseGame, ExpansionOf(2, baseGame, depth: 30), ExpansionOf(3, baseGame, depth: 70) };

        var layout = CabinetLayoutEngine.Build(items, design, EveryBoxFacesOut);

        var placements = layout.Sections[0].Cubbies[0].Placements;
        placements.Single(placement => placement.GameId == 1).Kind.Should().Be(PlacementKind.Cover);
        placements.Should().NotContain(placement => placement.Kind == PlacementKind.ExpansionSpine);
        placements.Where(placement => placement.Kind == PlacementKind.ExpansionLayer).Select(placement => placement.GameId)
            .Should().BeEquivalentTo([2, 3]);
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_two_hundred_millimetre_cover_base_with_two_thick_expansions_shows_one_upright_because_the_column_room_is_counted()
    {
        var design = new SectionDesign("test", 460, 20, [new ShelfRow(400, [460])]);
        var baseGame = new CabinetItem(1, 1, "Invented Cover Base", ItemKind.Base, new BoxDimensions(200, 380, 60), []);
        var items = new[] { baseGame, ExpansionOf(2, baseGame, depth: 60), ExpansionOf(3, baseGame, depth: 60) };

        var layout = CabinetLayoutEngine.Build(items, design, EveryBoxFacesOut);

        var placements = layout.Sections[0].Cubbies[0].Placements;
        placements.Where(placement => placement.Kind == PlacementKind.ExpansionSpine).Select(placement => placement.GameId)
            .Should().Equal(2);
        placements.Where(placement => placement.Kind == PlacementKind.ExpansionLayer).Select(placement => placement.GameId)
            .Should().Equal(3);
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_family_with_only_uprights_has_no_layers_and_no_marker_and_takes_base_plus_upright_width()
    {
        var design = new SectionDesign("test", 620, 20, [new ShelfRow(400, [600])]);
        var baseGame = BaseOf(1, depth: 40);
        var member = new LayoutMember(baseGame, BoxPose.Spine)
        {
            Uprights = [ExpansionOf(2, baseGame, depth: 60), ExpansionOf(3, baseGame, depth: 60)],
        };
        var width = 40 + (2 * design.MinUprightExpansionWidthMm);
        var exactFit = new CubbyDesign(0, 0, 0, width, 400);

        var placements = CubbyArrangement.TryArrange(design, exactFit, [member], SpinesOnly, 0);

        placements.Should().NotBeNull("no stack column is reserved when nothing lies in the stack");
        placements!.Select(placement => placement.Kind).Should().Equal(
            PlacementKind.Spine, PlacementKind.ExpansionSpine, PlacementKind.ExpansionSpine);
        placements.Max(placement => placement.XMm + placement.WidthMm).Should().Be(width);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void An_upright_carries_its_family_its_base_title_a_label_cut_to_its_height_and_the_least_width()
    {
        var design = new SectionDesign("test", 620, 20, [new ShelfRow(400, [600])]);
        var baseGame = BaseOf(1, depth: 40);
        var title = string.Concat(Enumerable.Repeat("Orvä Brin", 12));
        var expansion = ExpansionOf(2, baseGame, depth: 52) with { Title = title };

        var layout = CabinetLayoutEngine.Build([baseGame, expansion], design, SpinesOnly);

        var upright = layout.Sections[0].Cubbies[0].Placements.Single(placement => placement.Kind == PlacementKind.ExpansionSpine);
        upright.FamilyId.Should().Be(1);
        upright.BaseTitle.Should().Be(baseGame.Title);
        upright.Title.Should().Be(title);
        upright.WidthMm.Should().Be(design.MinUprightExpansionWidthMm, "a 52 mm deep box is drawn at the least upright width");
        upright.HeightMm.Should().Be(expansion.Box.HeightMm);
        upright.YMm.Should().Be(0);
        upright.Label.Should().Be(SpineLabel.Shorten(title, expansion.Box.HeightMm / design.LabelCharPitchMm));
        upright.Label.Should().NotBe(title);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void In_the_few_games_look_an_upright_stands_beside_a_cover_base()
    {
        var baseGame = new CabinetItem(1, 1, "Invented Cover Base", ItemKind.Base, new BoxDimensions(150, 250, 40), []);
        var others = Enumerable.Range(2, 3).Select(id => new CabinetItem(id, id, $"Invented Other {id}", ItemKind.Base, new BoxDimensions(150, 250, 40), []));
        var items = new List<CabinetItem>([baseGame, .. others, ExpansionOf(9, baseGame, depth: 60)]);

        var layout = CabinetLayoutEngine.Build(items, Design);

        var placed = LayoutAssertions.PlacementsWithPosition(layout);
        var cover = placed.Single(entry => entry.Placement.GameId == 1).Placement;
        var upright = placed.Single(entry => entry.Placement.GameId == 9).Placement;
        cover.Kind.Should().Be(PlacementKind.Cover);
        upright.Kind.Should().Be(PlacementKind.ExpansionSpine);
        upright.XMm.Should().Be(cover.XMm + cover.WidthMm);
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void An_upright_expansion_taller_than_any_cubby_is_scaled_to_fit()
    {
        var baseGame = BaseOf(1, depth: 40);
        var tall = ExpansionOf(2, baseGame, depth: 60) with { Box = new BoxDimensions(900, 1200, 60) };
        var items = new[] { baseGame, tall };

        var layout = CabinetLayoutEngine.Build(items, Design, SpinesOnly);

        var upright = LayoutAssertions.PlacementsWithPosition(layout).Single(entry => entry.Placement.GameId == 2);
        upright.Placement.Kind.Should().Be(PlacementKind.ExpansionSpine);
        upright.Placement.HeightMm.Should().BeLessThanOrEqualTo(Design.Limits.MaxHeightMm);
        LayoutAssertions.AssertValid(layout, items);
    }

    private static PlacementKind ToKind(BoxPose pose) =>
        pose switch
        {
            BoxPose.Cover => PlacementKind.Cover,
            BoxPose.Flat => PlacementKind.FlatBox,
            _ => PlacementKind.Spine,
        };

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

    private static List<(int Section, int Cubby, Placement Placement)> UprightsOf(
        IReadOnlyList<(int Section, int Cubby, Placement Placement)> placed,
        int baseId) =>
        placed
            .Where(entry => entry.Placement.Kind == PlacementKind.ExpansionSpine && entry.Placement.FamilyId == baseId)
            .OrderBy(entry => entry.Placement.XMm)
            .ToList();

    private static CabinetItem Thin(CabinetItem expansion) =>
        expansion with { Box = expansion.Box with { DepthMm = Math.Min(expansion.Box.DepthMm, MaxThinDepthMm) } };

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

using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Pins the mix of face-out covers, upright spines and flat stacks: the share of covers, every cover strategy, the
/// few-games boundary, per-game independence and the shape of the flat stacks.
/// </summary>
public class ShelfMixTests
{
    private const int MaxFlatBoxesPerStack = 4;
    private const int FlatDepthLimitMm = 40;

    private static readonly SectionDesign Design = SectionDesigns.Desktop;

    [Theory]
    [InlineData("65")]
    [InlineData("400")]
    [Trait("Category", "Layout")]
    public void With_default_settings_between_fifteen_and_thirty_five_percent_of_boxes_face_out(string name)
    {
        SyntheticCollections.TryGetSample(name, out var items);

        var layout = CabinetLayoutEngine.Build(items, Design, LayoutOptions.Default);

        var placements = PlacementsOf(layout).ToList();
        var covers = placements.Count(placement => placement.Kind == PlacementKind.Cover);
        var topLevel = placements.Count(placement => placement.Kind is PlacementKind.Cover or PlacementKind.Spine
            or PlacementKind.FlatBox or PlacementKind.OrphanExpansion);
        var percent = covers * 100.0 / topLevel;
        percent.Should().BeInRange(15, 35);
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Eleven_games_all_face_out_and_twelve_and_thirteen_follow_the_strategy()
    {
        var options = LayoutOptions.Default with { LieFlatBeforeNewSection = false };

        var eleven = SyntheticCollections.Random(5, 11);
        var elevenLayout = CabinetLayoutEngine.Build(eleven, Design, options);
        PlacementsOf(elevenLayout).Should().OnlyContain(placement => placement.Kind == PlacementKind.Cover);
        LayoutAssertions.AssertValid(elevenLayout, eleven);

        foreach (var count in new[] { 12, 13 })
        {
            var items = SyntheticCollections.Random(5, count);
            var layout = CabinetLayoutEngine.Build(items, Design, options);
            var byId = PlacementsOf(layout).ToDictionary(placement => placement.GameId);

            foreach (var item in items)
            {
                byId[item.BggId].Kind.Should().Be(
                    ToKind(Orientation.Decide(item, options, Design, fewGames: false)),
                    "game {0} of {1}", item.BggId, count);
            }

            LayoutAssertions.AssertValid(layout, items);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Eleven_games_in_any_input_order_give_the_same_cabinet()
    {
        var items = SyntheticCollections.Random(8, 11);

        var expected = LayoutJson.Serialize(CabinetLayoutEngine.Build(items, Design));
        var reversed = LayoutJson.Serialize(CabinetLayoutEngine.Build(items.Reverse().ToList(), Design));

        reversed.Should().Be(expected);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Oversize_only_faces_out_exactly_the_games_taller_than_the_maximum_whatever_the_share()
    {
        SyntheticCollections.TryGetSample("65", out var items);
        var expectedCovers = items.Where(item => item.Box.HeightMm > Design.MaxSpineHeightMm).Select(item => item.BggId).Order().ToList();
        expectedCovers.Should().NotBeEmpty("the sample has tall boxes");
        var json = new List<string>();

        foreach (var share in new[] { 0, 100 })
        {
            var options = new LayoutOptions(share, CoverStrategy.OversizeOnly, 6, 12, false);
            var layout = CabinetLayoutEngine.Build(items, Design, options);

            PlacementsOf(layout)
                .Where(placement => placement.Kind == PlacementKind.Cover)
                .Select(placement => placement.GameId)
                .Order()
                .Should().Equal(expectedCovers, "share {0}", share);
            json.Add(LayoutJson.Serialize(layout with { OptionsFingerprint = string.Empty }));
        }

        json[0].Should().Be(json[1]);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Random_strategy_decides_from_the_game_identifier_regardless_of_size()
    {
        var options = new LayoutOptions(40, CoverStrategy.Random, 6, 12);
        var differentSizes = new[] { Box(150, 30), Box(250, 60), Box(380, 100) };

        for (var bggId = 1; bggId <= 300; bggId++)
        {
            var decisions = differentSizes
                .Select(box => Orientation.Decide(ItemOf(bggId, box), options, Design, fewGames: false) == BoxPose.Cover)
                .Distinct()
                .ToList();

            decisions.Should().ContainSingle("game {0} must decide the same at every size", bggId);
        }
    }

    [Theory]
    [InlineData(CoverStrategy.SizeWeighted)]
    [InlineData(CoverStrategy.Random)]
    [Trait("Category", "Layout")]
    public void Raising_the_share_never_turns_a_cover_back_into_a_spine(CoverStrategy strategy)
    {
        SyntheticCollections.TryGetSample("400", out var items);
        var lower = new LayoutOptions(25, strategy, 6, 12);
        var higher = new LayoutOptions(40, strategy, 6, 12);
        var coversAtLower = 0;
        var coversAtHigher = 0;

        foreach (var item in items)
        {
            var atLower = Orientation.Decide(item, lower, Design, false) == BoxPose.Cover;
            var atHigher = Orientation.Decide(item, higher, Design, false) == BoxPose.Cover;
            coversAtLower += atLower ? 1 : 0;
            coversAtHigher += atHigher ? 1 : 0;

            if (atLower)
            {
                atHigher.Should().BeTrue("game {0} was a cover at the lower share", item.BggId);
            }
        }

        coversAtHigher.Should().BeGreaterThan(coversAtLower);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Appending_a_game_above_the_threshold_leaves_every_existing_game_standing_as_before()
    {
        for (var seed = 1; seed <= 50; seed++)
        {
            var items = SyntheticCollections.Random(seed, 60);
            var next = SyntheticCollections.NextBaseGame(items, seed);
            var before = KindsByGame(CabinetLayoutEngine.Build(items, Design));
            var after = KindsByGame(CabinetLayoutEngine.Build([.. items, next], Design));

            foreach (var (gameId, kind) in before)
            {
                after[gameId].Should().Be(kind, "seed {0}, game {1}", seed, gameId);
            }
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_share_of_zero_with_size_weighting_gives_no_covers_above_the_threshold()
    {
        SyntheticCollections.TryGetSample("400", out var items);
        var options = new LayoutOptions(0, CoverStrategy.SizeWeighted, 6, 12);

        var layout = CabinetLayoutEngine.Build(items, Design, options);

        PlacementsOf(layout).Should().NotContain(placement => placement.Kind == PlacementKind.Cover);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void With_lying_flat_switched_off_flat_boxes_only_come_from_small_or_thin_games()
    {
        SyntheticCollections.TryGetSample("400", out var items);
        var byId = items.ToDictionary(item => item.BggId);

        var layout = CabinetLayoutEngine.Build(items, Design, LayoutOptions.Default with { LieFlatBeforeNewSection = false });

        var flat = PlacementsOf(layout).Where(placement => placement.Kind == PlacementKind.FlatBox).ToList();
        flat.Should().NotBeEmpty("a collection of four hundred has small and thin boxes");
        flat.Should().OnlyContain(
            placement => IsSmallOrThin(byId[placement.GameId].Box),
            "with the setting off only small or thin games lie flat");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void With_lying_flat_switched_on_a_big_box_lies_flat_instead_of_opening_a_section()
    {
        SyntheticCollections.TryGetSample("400", out var items);
        var byId = items.ToDictionary(item => item.BggId);

        var layout = CabinetLayoutEngine.Build(items, Design);

        PlacementsOf(layout)
            .Where(placement => placement.Kind == PlacementKind.FlatBox)
            .Should().Contain(
                placement => !IsSmallOrThin(byId[placement.GameId].Box),
                "a big box that fits no cubby standing lies flat");
        LayoutAssertions.AssertValid(layout, items);
    }

    [Theory]
    [InlineData("65")]
    [InlineData("400")]
    [Trait("Category", "Layout")]
    public void Lying_flat_before_a_new_section_needs_fewer_sections_than_switching_it_off(string name)
    {
        SyntheticCollections.TryGetSample(name, out var items);

        var on = CabinetLayoutEngine.Build(items, Design, LayoutOptions.Default);
        var off = CabinetLayoutEngine.Build(items, Design, LayoutOptions.Default with { LieFlatBeforeNewSection = false });

        on.Sections.Count.Should().BeLessThan(off.Sections.Count);
        LayoutAssertions.AssertValid(on, items);
        LayoutAssertions.AssertValid(off, items);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_pile_of_flat_boxes_goes_longest_first_and_thicker_first_among_equal_lengths()
    {
        var design = new SectionDesign("test", 1000, 10, [new ShelfRow(300, [1000])]);
        var options = new LayoutOptions(0, CoverStrategy.SizeWeighted, 6, 0);
        var sizes = new (int Length, int Thickness)[] { (150, 30), (190, 25), (170, 40), (190, 35) };
        var ids = Enumerable.Range(1, 200)
            .Where(id => Orientation.Decide(ItemOf(id, Box(150, 20)), options, design, false) == BoxPose.Flat)
            .Take(sizes.Length)
            .ToList();
        var items = ids.Select((id, index) => ItemOf(id, new BoxDimensions(100, sizes[index].Length, sizes[index].Thickness))).ToList();

        var layout = CabinetLayoutEngine.Build(items, design, options);

        var pile = layout.Sections[0].Cubbies[0].Placements.OrderBy(placement => placement.YMm).ToList();
        pile.Select(placement => (placement.WidthMm, placement.HeightMm))
            .Should().Equal((190, 35), (190, 25), (170, 40), (150, 30));
        pile.Select(placement => placement.XMm).Distinct().Should().ContainSingle("the four boxes form one pile");
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_big_box_lies_flat_in_a_short_cubby_when_no_cubby_has_room_for_it_standing()
    {
        var design = TallThenShortDesign();
        var items = new[] { ItemOf(1, new BoxDimensions(290, 380, 40)), ItemOf(2, new BoxDimensions(280, 390, 80)) };

        var layout = CabinetLayoutEngine.Build(items, design, EveryBoxFacesOut);

        layout.Sections.Should().HaveCount(1);
        layout.Sections[0].Cubbies[0].Placements.Should().ContainSingle().Which.Kind.Should().Be(PlacementKind.Cover);
        var lying = layout.Sections[0].Cubbies[1].Placements.Should().ContainSingle().Subject;
        lying.Kind.Should().Be(PlacementKind.FlatBox);
        lying.GameId.Should().Be(2);
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void With_lying_flat_switched_off_the_same_big_box_faces_out_in_a_second_section()
    {
        var design = TallThenShortDesign();
        var items = new[] { ItemOf(1, new BoxDimensions(290, 380, 40)), ItemOf(2, new BoxDimensions(280, 390, 80)) };

        var layout = CabinetLayoutEngine.Build(items, design, EveryBoxFacesOut with { LieFlatBeforeNewSection = false });

        layout.Sections.Should().HaveCount(2);
        layout.Sections[1].Cubbies.SelectMany(cubby => cubby.Placements)
            .Should().ContainSingle().Which.Kind.Should().Be(PlacementKind.Cover);
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_cover_stands_in_a_later_tall_cubby_rather_than_lying_flat_in_an_earlier_short_one()
    {
        var design = new SectionDesign("test", 450, 10, [new ShelfRow(150, [450]), new ShelfRow(400, [450])]);
        var items = new[] { ItemOf(1, new BoxDimensions(290, 380, 40)) };

        var layout = CabinetLayoutEngine.Build(items, design, EveryBoxFacesOut);

        layout.Sections.Should().HaveCount(1);
        layout.Sections[0].Cubbies[0].Placements.Should().BeEmpty();
        layout.Sections[0].Cubbies[1].Placements.Should().ContainSingle().Which.Kind.Should().Be(PlacementKind.Cover);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Appending_a_game_that_lies_flat_changes_only_the_short_cubby_it_lands_in()
    {
        var design = TallThenShortDesign();
        var first = ItemOf(1, new BoxDimensions(290, 380, 40));
        var second = ItemOf(2, new BoxDimensions(280, 390, 80));

        var before = CabinetLayoutEngine.Build([first], design, EveryBoxFacesOut);
        var after = CabinetLayoutEngine.Build([first, second], design, EveryBoxFacesOut);

        LayoutAssertions.ChangedCubbies(before, after).Should().Equal([(0, 1)]);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_base_game_with_an_expansion_opens_a_new_section_instead_of_lying_flat()
    {
        var design = TallThenShortDesign();
        var baseGame = ItemOf(2, new BoxDimensions(200, 380, 60));
        var expansion = new CabinetItem(
            3, 3, "Invented Expansion 3", ItemKind.Expansion, new BoxDimensions(100, 150, 30), [new BaseGameRef(2, baseGame.Title)]);
        var items = new[] { ItemOf(1, new BoxDimensions(290, 380, 40)), baseGame, expansion };

        var layout = CabinetLayoutEngine.Build(items, design, EveryBoxFacesOut);

        layout.Sections.Should().HaveCount(2);
        var placed = LayoutAssertions.PlacementsWithPosition(layout);
        placed.Single(entry => entry.Placement.GameId == 2).Should().Match<(int Section, int Cubby, Placement Placement)>(
            entry => entry.Section == 1 && entry.Placement.Kind == PlacementKind.Cover);
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Eleven_big_covers_stay_covers_in_the_few_games_look_although_they_need_several_sections()
    {
        var items = Enumerable.Range(1, 11).Select(id => ItemOf(id, new BoxDimensions(300, 380, 60))).ToList();

        var layout = CabinetLayoutEngine.Build(items, Design, LayoutOptions.Default);

        layout.Sections.Count.Should().BeGreaterThan(1);
        PlacementsOf(layout).Should().OnlyContain(placement => placement.Kind == PlacementKind.Cover);
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Flat_stacks_are_short_fit_the_cubby_and_rest_on_the_floor_or_the_box_below()
    {
        SyntheticCollections.TryGetSample("400", out var items);
        var layout = CabinetLayoutEngine.Build(items, Design);
        var stackCount = 0;

        foreach (var cubby in layout.Sections.SelectMany(section => section.Cubbies))
        {
            var columns = cubby.Placements
                .Where(placement => placement.Kind is PlacementKind.FlatBox or PlacementKind.OrphanExpansion)
                .GroupBy(placement => placement.XMm);

            foreach (var column in columns)
            {
                var stack = column.OrderBy(placement => placement.YMm).ToList();
                stackCount++;
                stack.Should().HaveCountLessThanOrEqualTo(MaxFlatBoxesPerStack);
                stack[0].YMm.Should().Be(0);

                for (var index = 1; index < stack.Count; index++)
                {
                    stack[index].YMm.Should().Be(stack[index - 1].YMm + stack[index - 1].HeightMm);
                }

                (stack[^1].YMm + stack[^1].HeightMm).Should().BeLessThanOrEqualTo(cubby.HeightMm);
            }
        }

        stackCount.Should().BeGreaterThan(0);
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_stack_of_flat_boxes_starts_a_new_column_after_four_boxes()
    {
        var design = new SectionDesign("test", 1000, 10, [new ShelfRow(300, [1000])]);
        var options = new LayoutOptions(0, CoverStrategy.SizeWeighted, 6, 0);
        var items = Enumerable.Range(1, 80)
            .Select(id => ItemOf(id, Box(150, 20)))
            .Where(item => Orientation.Decide(item, options, design, false) == BoxPose.Flat)
            .Take(9)
            .ToList();
        items.Should().HaveCount(9, "about half of the small boxes lie flat");

        var layout = CabinetLayoutEngine.Build(items, design, options);

        var columns = layout.Sections[0].Cubbies[0].Placements.GroupBy(placement => placement.XMm).ToList();
        columns.Select(column => column.Count()).Order().Should().Equal(1, 4, 4);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Appending_a_game_still_changes_at_most_one_cubby_while_flat_stacks_are_present()
    {
        var sawFlat = false;

        for (var seed = 1; seed <= 50; seed++)
        {
            var items = SyntheticCollections.Random(seed, 120);
            var next = SyntheticCollections.NextBaseGame(items, seed);
            var before = CabinetLayoutEngine.Build(items, Design);
            var after = CabinetLayoutEngine.Build([.. items, next], Design);

            sawFlat |= PlacementsOf(before).Any(placement => placement.Kind == PlacementKind.FlatBox);
            LayoutAssertions.ChangedCubbies(before, after).Should().HaveCountLessThanOrEqualTo(1, "seed {0}", seed);
            LayoutAssertions.AssertValid(after, [.. items, next]);
        }

        sawFlat.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Every_sample_stays_valid_under_each_strategy()
    {
        foreach (var name in SyntheticCollections.SampleNames)
        {
            SyntheticCollections.TryGetSample(name, out var items);

            foreach (var strategy in Enum.GetValues<CoverStrategy>())
            {
                var layout = CabinetLayoutEngine.Build(items, Design, new LayoutOptions(60, strategy, 6, 12));

                LayoutAssertions.AssertValid(layout, items);
            }
        }
    }

    private static LayoutOptions EveryBoxFacesOut { get; } = new(100, CoverStrategy.Random, 6, 0);

    private static SectionDesign TallThenShortDesign() =>
        new("test", 450, 10, [new ShelfRow(400, [450]), new ShelfRow(150, [450])]);

    private static bool IsSmallOrThin(BoxDimensions box) =>
        Orientation.SizeClassOf(box) == SizeClass.Small || box.DepthMm <= FlatDepthLimitMm;

    private static IEnumerable<Placement> PlacementsOf(CabinetLayout layout) =>
        layout.Sections.SelectMany(section => section.Cubbies).SelectMany(cubby => cubby.Placements);

    private static Dictionary<int, PlacementKind> KindsByGame(CabinetLayout layout) =>
        PlacementsOf(layout).ToDictionary(placement => placement.GameId, placement => placement.Kind);

    private static PlacementKind ToKind(BoxPose pose) =>
        pose switch
        {
            BoxPose.Cover => PlacementKind.Cover,
            BoxPose.Flat => PlacementKind.FlatBox,
            _ => PlacementKind.Spine,
        };

    private static BoxDimensions Box(int heightMm, int depthMm) => new(heightMm * 3 / 4, heightMm, depthMm);

    private static CabinetItem ItemOf(int bggId, BoxDimensions box) =>
        new(bggId, bggId, $"Invented Title {bggId}", ItemKind.Base, box, []);
}

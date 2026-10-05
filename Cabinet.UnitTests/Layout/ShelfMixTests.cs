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

        var covers = PlacementsOf(layout).Count(placement => placement.Kind == PlacementKind.Cover);
        var percent = covers * 100.0 / items.Count;
        percent.Should().BeInRange(15, 35);
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Eleven_games_all_face_out_and_twelve_and_thirteen_follow_the_strategy()
    {
        var options = LayoutOptions.Default;

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
            var options = new LayoutOptions(share, CoverStrategy.OversizeOnly, 6, 12);
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
    public void Flat_boxes_only_come_from_small_or_thin_games()
    {
        SyntheticCollections.TryGetSample("400", out var items);
        var byId = items.ToDictionary(item => item.BggId);

        var layout = CabinetLayoutEngine.Build(items, Design);

        var flat = PlacementsOf(layout).Where(placement => placement.Kind == PlacementKind.FlatBox).ToList();
        flat.Should().NotBeEmpty("a collection of four hundred has small and thin boxes");

        foreach (var placement in flat)
        {
            var box = byId[placement.GameId].Box;
            (Orientation.SizeClassOf(box) == SizeClass.Small || box.DepthMm <= FlatDepthLimitMm)
                .Should().BeTrue("game {0} is neither small nor thin", placement.GameId);
        }
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
                .Where(placement => placement.Kind == PlacementKind.FlatBox)
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

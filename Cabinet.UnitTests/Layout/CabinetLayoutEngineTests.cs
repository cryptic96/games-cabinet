using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Pins the determinism contract of the layout engine: valid output for every sample, a total ordering of items, byte
/// identical output for the same set, and appends that touch at most one cubby.
/// </summary>
public class CabinetLayoutEngineTests
{
    private const int StabilitySeeds = 200;
    private const int StabilityCollectionSize = 120;

    private static readonly LayoutOptions SpinesOnly = new(0, CoverStrategy.SizeWeighted, 6, 0);

    public static TheoryData<string> SampleNames => new(SyntheticCollections.SampleNames);

    [Theory]
    [MemberData(nameof(SampleNames))]
    [Trait("Category", "Layout")]
    public void Every_sample_builds_a_valid_layout(string name)
    {
        SyntheticCollections.TryGetSample(name, out var items);

        var layout = CabinetLayoutEngine.Build(items, SectionDesigns.Desktop);

        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void An_empty_collection_gives_one_section_of_the_minimum_rows_and_no_placements()
    {
        var layout = CabinetLayoutEngine.Build([], SectionDesigns.Desktop);
        var firstRows = SectionDesigns.Desktop.Rows.Take(CabinetLayoutEngine.MinTrimmedRows).Sum(row => row.CubbyWidthsMm.Count);

        layout.Sections.Should().ContainSingle();
        layout.Sections[0].Cubbies.Should().HaveCount(firstRows);
        layout.Sections[0].Cubbies.Should().OnlyContain(cubby => cubby.Placements.Count == 0);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_one_game_collection_gives_one_section_with_one_placement()
    {
        SyntheticCollections.TryGetSample("1", out var items);

        var layout = CabinetLayoutEngine.Build(items, SectionDesigns.Desktop);

        layout.Sections.Should().ContainSingle();
        layout.Sections[0].Cubbies.SelectMany(cubby => cubby.Placements).Should().ContainSingle();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Section_counts_never_decrease_as_samples_grow_and_the_largest_needs_several()
    {
        var counts = new[] { "0", "1", "5", "12", "65", "400" }
            .Select(name =>
            {
                SyntheticCollections.TryGetSample(name, out var items);

                return CabinetLayoutEngine.Build(items, SectionDesigns.Desktop).Sections.Count;
            })
            .ToList();

        counts.Should().BeInAscendingOrder();
        counts[^1].Should().BeGreaterThan(1);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Shuffling_the_input_order_gives_byte_identical_json()
    {
        var items = SyntheticCollections.Random(3, 80);
        var expected = LayoutJson.Serialize(CabinetLayoutEngine.Build(items, SectionDesigns.Desktop));

        for (var seed = 1; seed <= 10; seed++)
        {
            var shuffled = Shuffle(items, (ulong)seed);

            LayoutJson.Serialize(CabinetLayoutEngine.Build(shuffled, SectionDesigns.Desktop))
                .Should().Be(expected, "shuffle seed {0}", seed);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Two_items_in_the_same_collection_entry_are_taken_in_game_identifier_order()
    {
        var design = new SectionDesign("test", 590, 10, [new ShelfRow(280, [290, 290])]) { StackColumnWidthMm = 100 };
        var low = ItemOf(bggId: 100, collectionId: 7, depth: 150);
        var high = ItemOf(bggId: 200, collectionId: 7, depth: 150);

        var layout = CabinetLayoutEngine.Build([high, low], design, SpinesOnly);

        layout.Sections[0].Cubbies[0].Placements.Select(placement => placement.GameId).Should().Equal(100);
        layout.Sections[0].Cubbies[1].Placements.Select(placement => placement.GameId).Should().Equal(200);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_repeated_collection_and_game_pair_is_rejected()
    {
        var item = ItemOf(bggId: 100, collectionId: 7, depth: 60);

        var act = () => CabinetLayoutEngine.Build([item, item], SectionDesigns.Desktop, SpinesOnly);

        act.Should().Throw<ArgumentException>().WithMessage("*100*");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Appending_a_game_changes_at_most_one_cubby_across_seeded_collections()
    {
        for (var seed = 1; seed <= StabilitySeeds; seed++)
        {
            var items = SyntheticCollections.Random(seed, StabilityCollectionSize);
            var next = SyntheticCollections.NextBaseGame(items, seed);
            var before = CabinetLayoutEngine.Build(items, SectionDesigns.Desktop);
            var after = CabinetLayoutEngine.Build([.. items, next], SectionDesigns.Desktop);

            var changed = LayoutAssertions.ChangedCubbies(before, after);

            changed.Should().HaveCountLessThanOrEqualTo(1, "seed {0}", seed);
            LayoutAssertions.AssertValid(after, [.. items, next]);

            if (after.Sections.Count > before.Sections.Count)
            {
                changed.Should().OnlyContain(position => position.Section >= before.Sections.Count, "seed {0} opened a new section", seed);
            }
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Appending_the_first_game_to_an_empty_collection_changes_only_its_cubby()
    {
        var before = CabinetLayoutEngine.Build([], SectionDesigns.Desktop);
        var first = SyntheticCollections.NextBaseGame([], 9);
        var after = CabinetLayoutEngine.Build([first], SectionDesigns.Desktop);

        LayoutAssertions.ChangedCubbies(before, after).Should().ContainSingle();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_game_exactly_as_wide_as_the_remaining_space_is_placed_in_that_cubby()
    {
        var design = new SectionDesign("test", 300, 10, [new ShelfRow(300, [300])]) { StackColumnWidthMm = 100 };
        var wide = ItemOf(bggId: 1, collectionId: 1, depth: 150);
        var exact = ItemOf(bggId: 2, collectionId: 2, depth: 150);

        var layout = CabinetLayoutEngine.Build([wide, exact], design, SpinesOnly);

        layout.Sections.Should().ContainSingle();
        var placements = layout.Sections[0].Cubbies[0].Placements;
        placements.Select(placement => placement.GameId).Should().BeEquivalentTo([1, 2]);
        placements.Should().OnlyContain(placement => placement.Kind == PlacementKind.Spine);
        placements.Sum(placement => placement.WidthMm).Should().Be(300);
        placements.Max(placement => placement.XMm + placement.WidthMm).Should().Be(300);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_box_taller_than_every_cubby_is_scaled_down_to_fit_with_its_proportions_kept()
    {
        var design = new SectionDesign("test", 300, 10, [new ShelfRow(300, [300])]) { StackColumnWidthMm = 100 };
        var tooTall = ItemOf(bggId: 1, collectionId: 1, depth: 50, height: 350);

        var layout = CabinetLayoutEngine.Build([tooTall], design, SpinesOnly);

        var placement = layout.Sections.Should().ContainSingle().Subject.Cubbies[0].Placements.Should().ContainSingle().Subject;
        placement.HeightMm.Should().Be(300);
        placement.WidthMm.Should().Be(50, "a spine is as wide as the box is deep, and the depth is not scaled");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_cover_wider_than_the_cubby_is_scaled_down_by_its_width()
    {
        var design = new SectionDesign("test", 300, 10, [new ShelfRow(300, [300])]) { StackColumnWidthMm = 100 };
        var wide = new CabinetItem(1, 1, "Invented Wide Title", ItemKind.Base, new BoxDimensions(600, 300, 50), []);
        var options = new LayoutOptions(0, CoverStrategy.SizeWeighted, 6, 100);

        var layout = CabinetLayoutEngine.Build([wide], design, options);

        var placement = layout.Sections.Should().ContainSingle().Subject.Cubbies[0].Placements.Should().ContainSingle().Subject;
        placement.Kind.Should().Be(PlacementKind.Cover);
        placement.WidthMm.Should().Be(300);
        placement.HeightMm.Should().Be(150, "the front keeps its proportions");
    }

    private static CabinetItem ItemOf(int bggId, long collectionId, int depth, int height = 250) =>
        new(bggId, collectionId, $"Invented Title {(char)('a' + (bggId % 26))}", ItemKind.Base, new BoxDimensions(100, height, depth), []);

    private static List<CabinetItem> Shuffle(IReadOnlyList<CabinetItem> items, ulong seed)
    {
        var shuffled = items.ToList();
        var generator = new SplitMix64(seed);

        for (var index = shuffled.Count - 1; index > 0; index--)
        {
            var other = generator.NextInt(0, index + 1);
            (shuffled[index], shuffled[other]) = (shuffled[other], shuffled[index]);
        }

        return shuffled;
    }
}

using Cabinet.Domain.Layout;
using Cabinet.FakeBgg;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Proves which games form a series, the order series are placed in, and that a series stands together in the cabinet.
/// </summary>
public class SeriesLayoutTests
{
    private const int SeriesFamily = 9000;
    private const int OtherSeriesFamily = 9001;

    private static readonly SectionDesign TwoCubbies = new("test", 590, 10, [new ShelfRow(280, [290, 290])]) { StackColumnWidthMm = 100 };
    private static readonly LayoutOptions SpinesOnly = new(0, CoverStrategy.SizeWeighted, 6, 0);

    [Theory]
    [InlineData("Game: Example Line", true)]
    [InlineData("Series: Example Saga", true)]
    [InlineData("  Series: Example Saga  ", true)]
    [InlineData("Theme: Invented Theme 1", false)]
    [InlineData("Components: Invented Pieces", false)]
    [InlineData("Players: Invented Solo Rules", false)]
    [InlineData("Gameplay: Not A Series", false)]
    [InlineData("game: wrong case", false)]
    [InlineData("Series:", false)]
    [InlineData("Series:No Space", false)]
    [InlineData("", false)]
    [InlineData("Example Saga", false)]
    [Trait("Category", "Layout")]
    public void Only_game_and_series_families_name_a_series(string name, bool expected)
    {
        SeriesGrouping.IsSeriesFamily(name).Should().Be(expected);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Two_games_that_carry_the_same_series_family_form_one_series_placed_where_the_first_would_go()
    {
        var items = new[] { Game(1, 1, SeriesFamily), Game(2, 2), Game(3, 3, SeriesFamily) };

        var groups = SeriesGrouping.Group(items);

        Shape(groups).Should().Be("0,2|1");
        groups.Select(group => group.AnchorGameId).Should().Equal(1, 2);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_series_family_only_one_game_carries_links_nothing()
    {
        var items = new[] { Game(1, 1, SeriesFamily), Game(2, 2), Game(3, 3, OtherSeriesFamily) };

        var groups = SeriesGrouping.Group(items);

        groups.Should().OnlyContain(group => group.Indices.Count == 1);
        groups.Select(group => group.Indices[0]).Should().Equal(0, 1, 2);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_series_family_carried_by_two_copies_of_one_game_links_nothing()
    {
        var items = new[] { Game(1, 1, SeriesFamily), Game(1, 2, SeriesFamily) with { Title = "Invented Other Name" } };

        SeriesGrouping.Group(items).Should().OnlyContain(group => group.Indices.Count == 1);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Series_are_joined_through_the_games_they_share_a_family_with()
    {
        var items = new[] { Game(1, 1, SeriesFamily), Game(2, 2, SeriesFamily, OtherSeriesFamily), Game(3, 3), Game(4, 4, OtherSeriesFamily) };

        var groups = SeriesGrouping.Group(items);

        Shape(groups).Should().Be("0,1,3|2");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Series_are_ordered_by_their_earliest_entry_and_their_games_by_entry()
    {
        var items = new[]
        {
            Game(10, 1),
            Game(11, 2, SeriesFamily),
            Game(12, 3),
            Game(13, 4, OtherSeriesFamily),
            Game(14, 5, SeriesFamily),
            Game(15, 6, OtherSeriesFamily),
            Game(16, 7, SeriesFamily),
        };

        var groups = SeriesGrouping.Group(items);

        Shape(groups).Should().Be("0|1,4,6|2|3,5");
        groups.Select(group => group.AnchorGameId).Should().Equal(10, 11, 12, 13);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Items_without_series_families_each_stand_alone_in_entry_order()
    {
        var items = new[] { Game(1, 1), Game(2, 2) with { SeriesFamilies = null }, Game(3, 3) };

        SeriesGrouping.Group(items).Select(group => group.Indices.Single()).Should().Equal(0, 1, 2);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_series_that_fits_one_cubby_stands_in_the_first_cubby_that_can_take_it_whole()
    {
        var items = new[]
        {
            Game(1, 1),
            Game(2, 2),
            Game(3, 3),
            Game(4, 4, SeriesFamily),
            Game(5, 5, SeriesFamily),
        };

        var layout = CabinetLayoutEngine.Build(items, TwoCubbies, SpinesOnly);

        LayoutAssertions.AssertValid(layout, items);
        Cubby(layout, 0).Select(placement => placement.GameId).Should().BeEquivalentTo([1, 2, 3]);
        Cubby(layout, 1).Select(placement => placement.GameId).Should().BeEquivalentTo([4, 5]);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_series_stands_side_by_side_in_entry_order_whatever_else_shares_the_cubby()
    {
        var design = new SectionDesign("test", 800, 10, [new ShelfRow(280, [800])]) { StackColumnWidthMm = 100 };

        for (var firstId = 1; firstId <= 40; firstId++)
        {
            var plain = Enumerable.Range(0, 5).Select(index => Game(firstId + (index * 7), 10 + index)).ToList();
            var series = new[] { Game(500 + firstId, 20, SeriesFamily), Game(900 - firstId, 21, SeriesFamily), Game(100 + firstId, 22, SeriesFamily) };
            var items = plain.Concat(series).ToList();

            var layout = CabinetLayoutEngine.Build(items, design, SpinesOnly);

            var ordered = Cubby(layout, 0).OrderBy(placement => placement.XMm).Select(placement => placement.GameId).ToList();
            var first = ordered.IndexOf(series[0].BggId);
            ordered.Skip(first).Take(3).Should().Equal(series.Select(game => game.BggId), "first game identifier {0}", firstId);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_cubby_holding_no_series_orders_its_boxes_as_it_always_did()
    {
        var plain = Enumerable.Range(1, 6).Select(id => Game(id * 11, id)).ToList();
        var withEmptyFamilies = plain.Select(item => item with { SeriesFamilies = [] }).ToList();

        var layout = CabinetLayoutEngine.Build(plain, TwoCubbies, SpinesOnly);

        LayoutJson.Serialize(CabinetLayoutEngine.Build(withEmptyFamilies, TwoCubbies, SpinesOnly)).Should().Be(LayoutJson.Serialize(layout));
        var cubby = Cubby(layout, 0);
        cubby.OrderBy(placement => placement.XMm).Select(placement => placement.GameId)
            .Should().Equal(cubby.OrderBy(placement => StableHash.Hash(placement.GameId, StableHash.CubbyOrderSaltBase)).Select(placement => placement.GameId));
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Shuffling_the_input_order_with_series_gives_byte_identical_json()
    {
        var items = Enumerable.Range(1, 30)
            .Select(index => index % 4 == 0 ? Game(index, index, SeriesFamily) : index % 5 == 0 ? Game(index, index, OtherSeriesFamily) : Game(index, index))
            .ToList();
        var expected = LayoutJson.Serialize(CabinetLayoutEngine.Build(items, TwoCubbies, SpinesOnly));

        for (var seed = 1; seed <= 10; seed++)
        {
            var shuffled = Shuffle(items, (ulong)seed);

            LayoutJson.Serialize(CabinetLayoutEngine.Build(shuffled, TwoCubbies, SpinesOnly)).Should().Be(expected, "shuffle seed {0}", seed);
        }
    }

    [Theory]
    [InlineData("Kelmont: The Vossmere Accord", "KELMONT")]
    [InlineData("Kelmont", "KELMONT")]
    [InlineData("Tarnwyn - Brindle Reborn", "TARNWYN")]
    [InlineData("Tarnwyn \u2013 Dawn", "TARNWYN")]
    [InlineData("NOX: Other", "NOX")]
    [InlineData("  Nox  :  Yar and Wend", "NOX")]
    [InlineData("Brin   dle: Two", "BRIN DLE")]
    [InlineData("Spider-Man", "SPIDER-MAN")]
    [InlineData("Alpha: Beta - Gamma", "ALPHA")]
    [InlineData("Alpha - Beta: Gamma", "ALPHA")]
    [InlineData("Alpha \u2013 Beta - Gamma", "ALPHA")]
    [Trait("Category", "Layout")]
    public void The_title_key_is_the_title_before_its_first_colon_or_spaced_dash_ignoring_case_and_spacing(string title, string expected)
    {
        SeriesGrouping.TitleKey(title).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(": Subtitle only")]
    [InlineData(" - Subtitle only")]
    [Trait("Category", "Layout")]
    public void A_blank_title_has_no_title_key(string? title)
    {
        SeriesGrouping.TitleKey(title).Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Games_whose_titles_share_a_title_key_form_one_series_and_blank_titles_link_nothing()
    {
        var items = new[]
        {
            Titled(1, 1, "Kelmont: The Vossmere Accord"),
            Titled(2, 2, "Tarnwyn - Brindle Reborn"),
            Titled(3, 3, "Kelmont"),
            Titled(4, 4, string.Empty),
            Titled(5, 5, "Tarnwyn: Dawn"),
            Titled(6, 6, "   "),
            Titled(7, 7, "Nox: Yar and Wend"),
            Titled(8, 8, "NOX: Other"),
            Titled(9, 9, "Unrelated"),
        };

        Shape(SeriesGrouping.Group(items)).Should().Be("0,2|1,4|3|5|6,7|8");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Title_links_and_family_links_join_transitively_and_two_copies_of_one_game_form_one_series()
    {
        var items = new[]
        {
            Titled(1, 1, "Kelmont: One"),
            Titled(2, 2, "Brindle", SeriesFamily),
            Titled(3, 3, "Kelmont: Two", OtherSeriesFamily),
            Titled(4, 4, "Voss", OtherSeriesFamily),
            Titled(5, 5, "Brindle: Again"),
            Titled(6, 6, "Zim"),
            Titled(6, 7, "Zim"),
        };

        Shape(SeriesGrouping.Group(items)).Should().Be("0,2,3|1,4|5,6");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Turning_grouping_off_makes_every_game_a_series_of_its_own()
    {
        var items = new[] { Titled(1, 1, "Kelmont: One", SeriesFamily), Titled(2, 2, "Kelmont: Two", SeriesFamily), Titled(3, 3, "Other") };

        Shape(SeriesGrouping.Group(items, groupSeries: false)).Should().Be("0|1|2");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_series_too_long_for_one_cubby_continues_in_the_next_with_the_continuing_game_first()
    {
        var plain = Game(900, 1);
        var series = Enumerable.Range(1, 6).Select(index => Game(index * 13, 10 + index, SeriesFamily)).ToList();
        var ids = series.Select(game => game.BggId).ToList();

        for (var lateId = 1000; lateId < 1040; lateId++)
        {
            var late = WithDepth(lateId, 100, 60);
            var items = new[] { plain }.Concat(series).Append(late).ToList();

            var layout = CabinetLayoutEngine.Build(items, TwoCubbies, SpinesOnly);

            LayoutAssertions.AssertValid(layout, items);
            InStandingOrder(layout, 1).Take(3).Should().Equal(ids.Skip(3), "the game that continues from the earlier cubby stands first, late game {0}", lateId);
            InStandingOrder(layout, 1).Should().Contain(lateId);
            InStandingOrder(layout, 0).Where(ids.Contains).Should().Equal(ids.Take(3));
            InStandingOrder(layout, 0).Should().Contain(plain.BggId);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_later_series_game_never_goes_back_to_a_cubby_before_the_game_that_precedes_it()
    {
        var design = new SectionDesign("test", 810, 10, [new ShelfRow(280, [400, 400])]) { StackColumnWidthMm = 100 };
        var plain = Enumerable.Range(1, 6).Select(index => WithDepth(index, index, 50)).Append(WithDepth(7, 7, 160)).ToList();
        var series = new[]
        {
            WithDepth(101, 10, 50, SeriesFamily),
            WithDepth(102, 11, 70, SeriesFamily),
            WithDepth(103, 12, 70, SeriesFamily),
            WithDepth(104, 13, 70, SeriesFamily),
            WithDepth(105, 14, 45, SeriesFamily),
        };
        var items = plain.Concat(series).ToList();

        var layout = CabinetLayoutEngine.Build(items, design, SpinesOnly);

        LayoutAssertions.AssertValid(layout, items);
        InStandingOrder(layout, 0).Where(id => id >= 100).Should().Equal(101);
        InStandingOrder(layout, 1).Where(id => id >= 100).Should().Equal(102, 103, 104);
        InStandingOrder(layout, 1).First().Should().Be(102, "the game that continues from the earlier cubby stands first");
        InStandingOrder(layout, 0, sectionIndex: 1).Should().ContainSingle().Which.Should().Be(105, "the last game does not go back to the room left beside the first one");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_series_that_no_cubby_takes_whole_starts_where_it_can_run_on_without_skipping_a_cubby()
    {
        var design = new SectionDesign("test", 670, 10, [new ShelfRow(280, [250, 100, 300])]) { StackColumnWidthMm = 100 };
        var plain = new[] { WithDepth(1, 1, 150), WithDepth(2, 2, 120) };
        var series = new[] { WithDepth(101, 10, 70, SeriesFamily), WithDepth(102, 11, 150, SeriesFamily) };
        var items = plain.Concat(series).ToList();

        var layout = CabinetLayoutEngine.Build(items, design, SpinesOnly);

        LayoutAssertions.AssertValid(layout, items);
        InStandingOrder(layout, 0).Should().Equal([1], "the series would leave the narrow middle cubby out if it started beside the first game");
        InStandingOrder(layout, 1).Should().Equal([101], "the series starts in the first cubby from which it runs on without a gap");
        InStandingOrder(layout, 2).Should().Equal([102, 2], "the game that continues the series stands first in the next cubby");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_family_of_a_series_keeps_its_marker_when_the_next_game_needs_the_room_its_second_column_would_take()
    {
        var design = new SectionDesign("test", 540, 20, [new ShelfRow(300, [220, 300])]) { StackColumnWidthMm = 140, MaxSpineHeightMm = 250 };
        var family = Titled(1, 1, "Kelmont: One") with { Box = new BoxDimensions(150, 240, 40) };
        var later = Titled(2, 2, "Kelmont: Two") with { Box = new BoxDimensions(200, 280, 60) };
        var expansions = Enumerable.Range(10, 8).Select(id => ExpansionOf(id, id, family)).ToList();
        var items = new List<CabinetItem>([family, later, .. expansions]);
        var options = new LayoutOptions(0, CoverStrategy.OversizeOnly, 6, 0, CoverFromExpansions: 0);

        var layout = CabinetLayoutEngine.Build(items, design, options);

        LayoutAssertions.AssertValid(layout, items);
        LayoutAssertions.SeriesGaps(layout, items, design).Should().BeEmpty();
        layout.Sections.Should().ContainSingle("the series fits the existing section once the family keeps its stack in one column");
        Cubby(layout, 0).Should().OnlyContain(placement => placement.FamilyId == 1, "the family stands whole in the first cubby");
        Cubby(layout, 0).Single(placement => placement.Kind == PlacementKind.MoreMarker).MoreCount.Should().Be(3, "the family keeps the expansions that do not fit under its shelf behind its marker");
        Cubby(layout, 1).Should().ContainSingle().Which.GameId.Should().Be(2, "the next game of the series has the next cubby to itself instead of a second column");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_series_whose_games_cannot_stand_side_by_side_even_in_an_empty_section_still_runs_forward()
    {
        var design = new SectionDesign("test", 300, 20, [new ShelfRow(300, [300]), new ShelfRow(150, [300])]) { StackColumnWidthMm = 100, MaxSpineHeightMm = 250 };
        var first = Titled(1, 1, "Kelmont: One") with { Box = new BoxDimensions(200, 280, 60) };
        var second = Titled(2, 2, "Kelmont: Two") with { Box = new BoxDimensions(200, 280, 60) };
        var items = new List<CabinetItem>([first, second]);
        var options = new LayoutOptions(0, CoverStrategy.OversizeOnly, 6, 0, LieFlatBeforeNewSection: false);

        var layout = CabinetLayoutEngine.Build(items, design, options);

        LayoutAssertions.AssertValid(layout, items);
        InStandingOrder(layout, 0).Should().Equal([1], "only the tall cubby takes either box, and not both");
        InStandingOrder(layout, 0, sectionIndex: 1).Should().Equal([2], "the second game goes on to the next tall cubby, past the short one");
        LayoutAssertions.SeriesGaps(layout, items, design).Should().ContainSingle("the one gap no arrangement of this design can avoid");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_series_of_the_invented_bgg_collection_never_skip_a_cubby_at_the_committed_cover_share()
    {
        var items = FakeCollectionItems.Map(SyntheticBggCollection.Create(65));
        var options = LayoutOptions.Default with { CoverSharePercent = 33 };

        var layout = CabinetLayoutEngine.Build(items, SectionDesigns.Desktop, options);

        LayoutAssertions.AssertValid(layout, items);
        LayoutAssertions.SeriesGaps(layout, items, SectionDesigns.Desktop).Should().BeEmpty("every game of a series stands in the cubby of the game before it or the very next one");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_long_series_never_stands_before_the_game_that_precedes_it_in_reading_order()
    {
        var series = Enumerable.Range(1, 30).Select(index => Game(index * 3, index, SeriesFamily)).ToList();

        var layout = CabinetLayoutEngine.Build(series, TwoCubbies, SpinesOnly);

        LayoutAssertions.AssertValid(layout, series);
        var positions = LayoutAssertions.PlacementsWithPosition(layout)
            .ToDictionary(entry => entry.Placement.GameId, entry => (entry.Section, entry.Cubby));
        var ordered = series.Select(game => positions[game.BggId]).ToList();

        ordered.Should().BeInAscendingOrder();
        layout.Sections.Count.Should().BeGreaterThan(1, "thirty spines need more than one section of two cubbies");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void With_grouping_off_the_arrangement_equals_the_one_made_without_any_series()
    {
        var linked = Enumerable.Range(1, 40)
            .Select(index => Titled(index, index, index % 3 == 0 ? $"Kelmont: Part {index}" : $"Invented Title {index}", index % 4 == 0 ? new[] { SeriesFamily } : []))
            .ToList();
        var unlinked = linked.Select(item => item with { Title = $"Invented Title {item.BggId}", SeriesFamilies = null }).ToList();
        var off = SpinesOnly with { GroupSeries = false };

        var grouped = LayoutAssertions.Geometry(CabinetLayoutEngine.Build(linked, TwoCubbies, SpinesOnly));
        var ungrouped = LayoutAssertions.Geometry(CabinetLayoutEngine.Build(linked, TwoCubbies, off));

        ungrouped.Should().Be(LayoutAssertions.Geometry(CabinetLayoutEngine.Build(unlinked, TwoCubbies, off)));
        ungrouped.Should().Be(LayoutAssertions.Geometry(CabinetLayoutEngine.Build(unlinked, TwoCubbies, SpinesOnly)));
        grouped.Should().NotBe(ungrouped, "grouping on really changes this collection");
        off.Fingerprint.Should().NotBe(SpinesOnly.Fingerprint);
    }

    internal static string Shape(IReadOnlyList<SeriesGroup> groups) =>
        string.Join('|', groups.Select(group => string.Join(',', group.Indices)));

    internal static CabinetItem Game(int id, long entry, params int[] families) => WithDepth(id, entry, 60, families);

    internal static CabinetItem WithDepth(int id, long entry, int depth, params int[] families) =>
        new(id, entry, $"Invented Title {id}", ItemKind.Base, new BoxDimensions(100, 250, depth), [], SeriesFamilies: families);

    private static CabinetItem Titled(int id, long entry, string title, params int[] families) =>
        Game(id, entry, families) with { Title = title };

    private static CabinetItem ExpansionOf(int id, long entry, CabinetItem baseGame) =>
        new(id, entry, $"Invented Expansion {id}", ItemKind.Expansion, new BoxDimensions(120, 200, 45), [new BaseGameRef(baseGame.BggId, baseGame.Title)]);

    private static List<int> InStandingOrder(CabinetLayout layout, int cubbyIndex, int sectionIndex = 0) =>
        [.. Cubby(layout, cubbyIndex, sectionIndex).OrderBy(placement => placement.XMm).Select(placement => placement.GameId)];

    internal static IReadOnlyList<Placement> Cubby(CabinetLayout layout, int cubbyIndex, int sectionIndex = 0) =>
        layout.Sections.Single(section => section.Index == sectionIndex).Cubbies.Single(cubby => cubby.Index == cubbyIndex).Placements;

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

using Cabinet.Domain.Layout;
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

    internal static string Shape(IReadOnlyList<SeriesGroup> groups) =>
        string.Join('|', groups.Select(group => string.Join(',', group.Indices)));

    internal static CabinetItem Game(int id, long entry, params int[] families) =>
        new(id, entry, $"Invented Title {id}", ItemKind.Base, new BoxDimensions(100, 250, 60), [], SeriesFamilies: families);

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

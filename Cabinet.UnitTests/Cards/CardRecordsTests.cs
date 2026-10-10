using Cabinet.Domain.Cards;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using FluentAssertions;

namespace Cabinet.UnitTests.Cards;

/// <summary>Proves the card records carry every shown detail from stored data, list an expansion on every owned base, round once, and send nothing else.</summary>
[Trait("Category", "Cards")]
public sealed class CardRecordsTests
{
    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_game_with_details_gets_every_shown_field_and_lists_in_source_order()
    {
        var details = Details(
            minPlayers: 2,
            maxPlayers: 4,
            playingTime: 60,
            minPlayTime: 60,
            maxPlayTime: 90,
            minAge: 10,
            weight: 2.449,
            average: 7.85,
            designers: ["Second Designer", "First Designer"],
            mechanics: ["Drafting", "Auction", "Trading", "Dice rolling", "Set collection"]);

        var card = BuildOne(Snapshot([Base(1, 100)], (1, details)), Item(1, 100));

        card.MinPlayers.Should().Be(2);
        card.MaxPlayers.Should().Be(4);
        card.PlayTime.Should().Be(60);
        card.MinPlayTime.Should().Be(60);
        card.MaxPlayTime.Should().Be(90);
        card.MinAge.Should().Be(10);
        card.Weight.Should().Be(2.4);
        card.Rating.Should().Be(7.9);
        card.Designers.Should().Equal("Second Designer", "First Designer");
        card.Mechanics.Should().Equal("Drafting", "Auction", "Trading", "Dice rolling", "Set collection");
    }

    [Fact]
    public void The_ranked_rating_and_the_other_stored_extras_never_reach_the_json()
    {
        var details = Details(average: 7.1, bayes: 6.123456, families: [new FamilyLink(77, "Series: Invented Lines")]) with
        {
            MainImageUrl = "https://example.com/original.jpg",
            DetailsVersion = 1,
        };
        var cards = CardRecords.Build([Item(1, 100)], Snapshot([Base(1, 100)], (1, details)), SectionDesigns.Desktop);

        var json = CardRecords.Serialize(new CardsDocument("\"layout\"", cards));

        json.Should().NotContain("6.123456");
        json.ToLowerInvariant().Should().NotContain("bayes");
        json.ToLowerInvariant().Should().NotContain("famil");
        json.Should().NotContain("example.com");
        json.Should().NotContain("enriched");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-40)]
    public void A_zero_or_negative_count_time_or_age_is_absent_not_zero(int value)
    {
        var details = Details(minPlayers: value, maxPlayers: value, playingTime: value, minPlayTime: value, maxPlayTime: value, minAge: value);

        var card = BuildOne(Snapshot([Base(1, 100)], (1, details)), Item(1, 100));

        card.MinPlayers.Should().BeNull();
        card.MaxPlayers.Should().BeNull();
        card.PlayTime.Should().BeNull();
        card.MinPlayTime.Should().BeNull();
        card.MaxPlayTime.Should().BeNull();
        card.MinAge.Should().BeNull();
    }

    [Fact]
    public void A_game_with_no_details_gets_empty_values_and_empty_lists()
    {
        var card = BuildOne(Snapshot([Base(1, 100)]), Item(1, 100));

        card.MinPlayers.Should().BeNull();
        card.Weight.Should().BeNull();
        card.Rating.Should().BeNull();
        card.Designers.Should().BeEmpty();
        card.Mechanics.Should().BeEmpty();
        card.Expansions.Should().BeEmpty();
        card.Bases.Should().BeEmpty();
    }

    [Fact]
    public void An_expansion_of_two_owned_games_is_listed_on_both_cards_and_names_both_bases_by_game_id()
    {
        var snapshot = Snapshot(
            [Base(20, 100), Base(10, 110), Expansion(30, 120)],
            (30, Details(expands: [Ref(20, "Twenty"), Ref(10, "Ten")])));
        var items = new[] { Item(20, 100, "Twenty"), Item(10, 110, "Ten"), Item(30, 120, "Extra", ItemKind.Expansion, [Ref(20, "Twenty")]) };

        var cards = CardRecords.Build(items, snapshot, SectionDesigns.Desktop);

        cards[0].Expansions.Select(link => link.GameId).Should().Equal(30);
        cards[1].Expansions.Select(link => link.GameId).Should().Equal(30);
        cards[2].Bases.Select(link => (link.GameId, link.EntryId)).Should().Equal((10, 110L), (20, 100L));
        cards[2].Bases.Should().OnlyContain(link => link.Chip != null && link.Chip.Length == 7 && link.Chip[0] == '#');
        cards[2].Expansions.Should().BeEmpty();
    }

    [Fact]
    public void An_expansion_with_one_owned_and_one_missing_base_lists_only_the_owned_one()
    {
        var snapshot = Snapshot(
            [Base(20, 100), Expansion(30, 120)],
            (30, Details(expands: [Ref(20, "Twenty"), Ref(99, "Missing")])));
        var items = new[] { Item(20, 100, "Twenty"), Item(30, 120, "Extra", ItemKind.Expansion, [Ref(20, "Twenty")]) };

        var cards = CardRecords.Build(items, snapshot, SectionDesigns.Desktop);

        cards[1].Bases.Select(link => link.GameId).Should().Equal(20);
    }

    [Fact]
    public void An_expansion_whose_bases_are_not_owned_names_them_without_entry_or_chip_and_is_listed_nowhere()
    {
        var snapshot = Snapshot(
            [Base(20, 100), Expansion(30, 120)],
            (30, Details(expands: [Ref(99, "Missing Game")])));
        var items = new[] { Item(20, 100, "Twenty"), Item(30, 120, "Extra", ItemKind.Expansion, [Ref(99, "Missing Game")]) };

        var cards = CardRecords.Build(items, snapshot, SectionDesigns.Desktop);

        cards[1].Bases.Should().Equal(new CardLink(null, 99, "Missing Game", null));
        cards[0].Expansions.Should().BeEmpty();
    }

    [Fact]
    public void A_base_owned_twice_is_linked_by_its_earliest_entry()
    {
        var snapshot = Snapshot(
            [Base(20, 300), Base(20, 100), Expansion(30, 120)],
            (30, Details(expands: [Ref(20, "Twenty")])));
        var items = new[] { Item(20, 300, "Twenty"), Item(20, 100, "Twenty"), Item(30, 120, "Extra", ItemKind.Expansion, [Ref(20, "Twenty")]) };

        var cards = CardRecords.Build(items, snapshot, SectionDesigns.Desktop);

        cards[2].Bases.Select(link => link.EntryId).Should().Equal(100L);
    }

    [Fact]
    public void A_base_entry_that_is_also_an_expansion_entry_is_not_an_owned_base()
    {
        var snapshot = Snapshot(
            [Base(20, 100), Expansion(20, 100), Expansion(30, 120)],
            (30, Details(expands: [Ref(20, "Twenty")])));
        var items = new[] { Item(20, 100, "Twenty", ItemKind.Expansion, []), Item(30, 120, "Extra", ItemKind.Expansion, [Ref(20, "Twenty")]) };

        var cards = CardRecords.Build(items, snapshot, SectionDesigns.Desktop);

        cards[1].Bases.Should().Equal(new CardLink(null, 20, "Twenty", null));
    }

    [Fact]
    public void Year_and_location_come_from_the_stored_entry_and_are_absent_without_a_snapshot()
    {
        var stored = Base(1, 100) with { Year = 2011, Location = "Crate 7" };

        var withSnapshot = BuildOne(Snapshot([stored]), Item(1, 100));
        var without = CardRecords.Build([Item(1, 100)], null, SectionDesigns.Desktop)[0];

        withSnapshot.Year.Should().Be(2011);
        withSnapshot.Location.Should().Be("Crate 7");
        without.Year.Should().BeNull();
        without.Location.Should().BeNull();
    }

    [Theory]
    [InlineData(2.45, 2.5)]
    [InlineData(7.849, 7.8)]
    [InlineData(1.5, 1.5)]
    [InlineData(2.5, 2.5)]
    [InlineData(3.5, 3.5)]
    [InlineData(4.5, 4.5)]
    [InlineData(2.05, 2.1)]
    public void Weight_and_rating_are_rounded_once_to_one_decimal_with_the_midpoint_away_from_zero(double stored, double shown)
    {
        var card = BuildOne(Snapshot([Base(1, 100)], (1, Details(weight: stored, average: stored))), Item(1, 100));

        card.Weight.Should().Be(shown);
        card.Rating.Should().Be(shown);
    }

    [Fact]
    public void Equal_player_counts_are_both_sent_and_a_stated_time_alone_sends_no_range()
    {
        var details = Details(minPlayers: 2, maxPlayers: 2, playingTime: 45);

        var card = BuildOne(Snapshot([Base(1, 100)], (1, details)), Item(1, 100));

        card.MinPlayers.Should().Be(2);
        card.MaxPlayers.Should().Be(2);
        card.PlayTime.Should().Be(45);
        card.MinPlayTime.Should().BeNull();
        card.MaxPlayTime.Should().BeNull();
    }

    [Fact]
    public void Titles_designers_and_mechanics_are_sent_exactly_as_stored()
    {
        var details = Details(designers: ["  odd CASE name \U0001F3B2 "], mechanics: ["MIXED case Mechanic"]);

        var card = BuildOne(Snapshot([Base(1, 100)], (1, details)), Item(1, 100, "  tItLe: with \U0001F3B2 "));

        card.Title.Should().Be("  tItLe: with \U0001F3B2 ");
        card.Designers.Should().Equal("  odd CASE name \U0001F3B2 ");
        card.Mechanics.Should().Equal("MIXED case Mechanic");
    }

    [Fact]
    public void The_cover_is_the_narrowest_stored_size_wide_enough_for_the_profile_and_else_the_widest()
    {
        var art = new ArtImage(
            [
                new ArtVariant(480, 640, "/art/aaaaaaaaaaaaaaaa-480.webp"),
                new ArtVariant(240, 320, "/art/bbbbbbbbbbbbbbbb-240.webp"),
            ],
            new ArtEdges("#111111", "#222222", "#333333", "#444444"));
        var item = Item(1, 100) with { Art = art };

        var desktop = CardRecords.Build([item], null, SectionDesigns.Desktop)[0].Cover!;
        var phone = CardRecords.Build([item], null, SectionDesigns.Phone)[0].Cover!;

        desktop.Url.Should().Be("/art/aaaaaaaaaaaaaaaa-480.webp");
        phone.Url.Should().Be("/art/bbbbbbbbbbbbbbbb-240.webp");
        phone.Edges.Should().Be(art.Edges);
        CardRecords.Build([Item(2, 101)], null, SectionDesigns.Desktop)[0].Cover.Should().BeNull();
    }

    [Fact]
    public void The_ratio_is_the_box_front_width_over_its_height_and_the_placeholder_indices_come_from_the_game()
    {
        var card = CardRecords.Build([Item(1, 100) with { Box = new BoxDimensions(150, 200, 50) }], null, SectionDesigns.Desktop)[0];

        card.Ratio.Should().Be(0.75);
        card.ToneIndex.Should().Be(SpinePalette.ToneFor(1));
        card.PatternIndex.Should().Be(SpinePalette.PatternFor(1));
    }

    private static CardRecord BuildOne(CollectionSnapshot snapshot, CabinetItem item) =>
        CardRecords.Build([item], snapshot, SectionDesigns.Desktop)[0];

    private static CabinetItem Item(
        int gameId,
        long entryId,
        string title = "Invented Title",
        ItemKind kind = ItemKind.Base,
        IReadOnlyList<BaseGameRef>? expansionOf = null) =>
        new(gameId, entryId, title, kind, new BoxDimensions(200, 280, 60), expansionOf ?? []);

    private static BaseGameRef Ref(int gameId, string title) => new(gameId, title);

    private static SnapshotItem Base(int gameId, long entryId) =>
        new(entryId, gameId, "Invented Title", ItemKind.Base, null, null, null);

    private static SnapshotItem Expansion(int gameId, long entryId) =>
        new(entryId, gameId, "Invented Expansion", ItemKind.Expansion, null, null, null);

    private static CollectionSnapshot Snapshot(SnapshotItem[] items, params (int GameId, GameDetails Details)[] details) =>
        new(
            CollectionSnapshot.CurrentSchemaVersion,
            Moment,
            items,
            null,
            details.ToDictionary(entry => entry.GameId, entry => entry.Details));

    private static GameDetails Details(
        int? minPlayers = null,
        int? maxPlayers = null,
        int? playingTime = null,
        int? minPlayTime = null,
        int? maxPlayTime = null,
        int? minAge = null,
        double? weight = null,
        double? average = null,
        double? bayes = null,
        IReadOnlyList<string>? designers = null,
        IReadOnlyList<string>? mechanics = null,
        IReadOnlyList<BaseGameRef>? expands = null,
        IReadOnlyList<FamilyLink>? families = null) =>
        new(
            Moment,
            minPlayers,
            maxPlayers,
            playingTime,
            minPlayTime,
            maxPlayTime,
            minAge,
            weight,
            average,
            bayes,
            designers ?? [],
            mechanics ?? [],
            expands ?? [],
            null,
            Families: families);
}

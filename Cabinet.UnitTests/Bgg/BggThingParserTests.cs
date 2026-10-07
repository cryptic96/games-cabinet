using System.Globalization;
using System.Text;
using System.Xml;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Repository.Bgg;
using FluentAssertions;

namespace Cabinet.UnitTests.Bgg;

/// <summary>
/// Proves the details parser reads what it should, cleans and caps the text it keeps, stores a missing or impossible number
/// as unknown, ignores every link it is not meant to read and rejects hostile or wrong documents.
/// </summary>
[Trait("Category", "Enrichment")]
public sealed class BggThingParserTests
{
    private static readonly DateTimeOffset Moment = new(2030, 4, 5, 6, 7, 8, TimeSpan.Zero);

    [Fact]
    public void A_full_entry_is_read_into_every_field()
    {
        var parsed = Parse(Answer(
            Item(
                10,
                "<image>  https://cf.example.org/main-10.jpg  </image>"
                + Value("minplayers", "2") + Value("maxplayers", "5") + Value("playingtime", "90")
                + Value("minplaytime", "60") + Value("maxplaytime", "90") + Value("minage", "12")
                + Link("boardgamedesigner", 1, "Invented Designer 1") + Link("boardgamedesigner", 2, "Invented Designer 2")
                + Link("boardgamemechanic", 3, "Example Mechanic A")
                + Link("boardgameexpansion", 4, "Example Base", inbound: true)
                + Ratings("7.25", "6.5", "2.75"))));

        parsed.SkippedItems.Should().Be(0);
        var details = parsed.Games.Should().ContainSingle().Which.Value;
        parsed.Games.Keys.Should().Equal(10);
        details.EnrichedAtUtc.Should().Be(Moment);
        Counts(details).Should().Be(Counts(2, 5, 90, 60, 90, 12));
        Scores(details).Should().Be(Scores(7.25, 6.5, 2.75));
        details.Designers.Should().Equal("Invented Designer 1", "Invented Designer 2");
        details.Mechanics.Should().Equal("Example Mechanic A");
        details.ExpandsGames.Should().Equal(new BaseGameRef(4, "Example Base"));
        details.MainImageUrl.Should().Be("https://cf.example.org/main-10.jpg");
    }

    [Fact]
    public void Designers_and_mechanics_are_cleaned_deduplicated_in_answer_order_and_blank_ones_dropped()
    {
        var parsed = Parse(Answer(
            Item(
                1,
                Link("boardgamedesigner", 1, "Second‮ Name")
                + Link("boardgamedesigner", 2, "  First\u0085  ")
                + Link("boardgamedesigner", 3, "Second Name")
                + Link("boardgamedesigner", 4, "   ")
                + Link("boardgamedesigner", 5, "\u0080")
                + Link("boardgamemechanic", 6, "Example Mechanic A")
                + Link("boardgamemechanic", 7, "Example Mechanic A"))));

        var details = parsed.Games[1];

        details.Designers.Should().Equal("Second Name", "First");
        details.Mechanics.Should().Equal("Example Mechanic A");
    }

    [Fact]
    public void Designers_mechanics_and_expanded_games_are_capped_at_twenty_each_keeping_the_first_ones()
    {
        var links = string.Concat(Enumerable.Range(1, 30).Select(index => Link("boardgamedesigner", index, $"Invented Designer {index}")))
            + string.Concat(Enumerable.Range(1, 30).Select(index => Link("boardgamemechanic", index, $"Example Mechanic {index}")))
            + string.Concat(Enumerable.Range(1, 30).Select(index => Link("boardgameexpansion", 100 + index, $"Base {index}", inbound: true)));

        var details = Parse(Answer(Item(1, links))).Games[1];

        BggThingParser.MaxListEntries.Should().Be(20);
        details.Designers.Should().HaveCount(20).And.StartWith("Invented Designer 1", "Invented Designer 2");
        details.Mechanics.Should().HaveCount(20);
        details.ExpandsGames.Should().HaveCount(20);
        details.ExpandsGames[0].Should().Be(new BaseGameRef(101, "Base 1"));
        details.ExpandsGames[19].Should().Be(new BaseGameRef(120, "Base 20"));
    }

    [Fact]
    public void Only_inbound_expansion_links_are_read_and_every_other_link_type_is_ignored()
    {
        var details = Parse(Answer(
            Item(
                1,
                Link("boardgameexpansion", 20, "Outbound Expansion")
                + Link("boardgamecompilation", 21, "Big Box", inbound: true)
                + Link("boardgameimplementation", 22, "Reimplementation", inbound: true)
                + Link("boardgamefamily", 23, "Family", inbound: true)
                + Link("boardgameexpansion", 24, "Owned Base", inbound: true)
                + Link("boardgameexpansion", 24, "Owned Base Again", inbound: true)
                + Link("boardgameexpansion", 25, "Second Base", inbound: true)
                + "<link type=\"boardgameexpansion\" id=\"26\" value=\"Flag Is Not True\" inbound=\"false\" />"
                + "<link type=\"boardgameexpansion\" id=\"x\" value=\"No Id\" inbound=\"true\" />"))).Games[1];

        details.ExpandsGames.Should().Equal(new BaseGameRef(24, "Owned Base"), new BaseGameRef(25, "Second Base"));
        details.Designers.Should().BeEmpty();
        details.Mechanics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("two")]
    [InlineData("2.5")]
    [InlineData("")]
    public void A_whole_number_field_that_is_not_a_positive_whole_number_is_stored_as_unknown(string value)
    {
        var details = Parse(Answer(Item(1, Value("minplayers", value) + Value("maxplayers", value) + Value("playingtime", value)
            + Value("minplaytime", value) + Value("maxplaytime", value) + Value("minage", value)))).Games[1];

        Counts(details).Should().Be(Counts(null, null, null, null, null, null));
    }

    [Fact]
    public void A_missing_field_is_unknown_and_a_missing_statistics_block_leaves_every_rating_unknown()
    {
        var details = Parse(Answer(Item(1, Value("minplayers", "2")))).Games[1];

        Counts(details).Should().Be(Counts(2, null, null, null, null, null));
        Scores(details).Should().Be(Scores(null, null, null));
        details.MainImageUrl.Should().BeNull();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1.5")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("heavy")]
    [InlineData("")]
    public void A_rating_that_is_zero_negative_not_finite_or_not_a_number_is_stored_as_unknown(string value)
    {
        var details = Parse(Answer(Item(1, Ratings(value, value, value)))).Games[1];

        Scores(details).Should().Be(Scores(null, null, null));
    }

    [Fact]
    public void A_rating_is_the_decimal_BGG_sent_read_with_the_invariant_culture_and_never_rounded()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("nl-NL");

        try
        {
            var details = Parse(Answer(Item(1, Ratings("7.123456789", "6.05", "1.0001")))).Games[1];

            Scores(details).Should().Be(Scores(7.123456789, 6.05, 1.0001));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void An_entry_without_a_usable_identifier_is_skipped_and_counted_and_the_rest_is_kept()
    {
        var parsed = Parse(Answer(
            "<item type=\"boardgame\"><name type=\"primary\" value=\"No Id\" /></item>",
            "<item type=\"boardgame\" id=\"abc\" />",
            "<item type=\"boardgame\" id=\"-4\" />",
            Item(7, Value("minplayers", "3"))));

        parsed.SkippedItems.Should().Be(3);
        parsed.Games.Keys.Should().Equal(7);
    }

    [Fact]
    public void Markup_like_text_in_a_name_is_kept_as_text()
    {
        var details = Parse(Answer(Item(1, Link("boardgamedesigner", 1, "&lt;script&gt;alert(1)&lt;/script&gt; &amp; Co")))).Games[1];

        details.Designers.Should().Equal("<script>alert(1)</script> & Co");
    }

    [Fact]
    public void A_picture_address_that_is_not_usable_is_unknown()
    {
        var details = Parse(Answer(Item(1, "<image>javascript:alert(1)</image>"))).Games[1];

        details.MainImageUrl.Should().BeNull();
    }

    [Fact]
    public void A_document_with_a_type_definition_is_refused()
    {
        var parse = () => Parse("<!DOCTYPE items [<!ENTITY x \"y\">]><items />");

        parse.Should().Throw<XmlException>();
    }

    [Fact]
    public void A_document_over_the_character_limit_is_refused_and_one_just_under_is_read()
    {
        var over = () => ParsePadded(20_100_000);

        over.Should().Throw<XmlException>();
        ParsePadded(19_900_000).Games.Should().BeEmpty();
    }

    [Theory]
    [InlineData("<errors><error><message>Rate limit exceeded</message></error></errors>")]
    [InlineData("<message>Your request has been accepted</message>")]
    [InlineData("<html><body>Just a moment</body></html>")]
    public void A_root_that_is_not_the_items_element_is_rejected(string document)
    {
        var parse = () => Parse(document);

        parse.Should().Throw<BggAnswerException>();
    }

    [Fact]
    public void A_document_that_stops_in_the_middle_is_not_well_formed()
    {
        var parse = () => Parse("<items><item id=\"1\"><name value=\"Exam");

        parse.Should().Throw<XmlException>();
    }

    [Fact]
    public void The_same_answer_gives_the_same_details()
    {
        var answer = Answer(Item(1, Link("boardgamedesigner", 1, "Invented Designer 1") + Ratings("7", "6", "2")));

        var first = Parse(answer).Games[1];
        var second = Parse(answer).Games[1];

        first.SameAs(second).Should().BeTrue();
    }

    private static (int? MinPlayers, int? MaxPlayers, int? PlayingTime, int? MinPlayTime, int? MaxPlayTime, int? MinAge) Counts(GameDetails details) =>
        (details.MinPlayers, details.MaxPlayers, details.PlayingTime, details.MinPlayTime, details.MaxPlayTime, details.MinAge);

    private static (int? MinPlayers, int? MaxPlayers, int? PlayingTime, int? MinPlayTime, int? MaxPlayTime, int? MinAge) Counts(
        int? minPlayers, int? maxPlayers, int? playingTime, int? minPlayTime, int? maxPlayTime, int? minAge) =>
        (minPlayers, maxPlayers, playingTime, minPlayTime, maxPlayTime, minAge);

    private static (double? Average, double? BayesAverage, double? Weight) Scores(GameDetails details) =>
        (details.Average, details.BayesAverage, details.Weight);

    private static (double? Average, double? BayesAverage, double? Weight) Scores(double? average, double? bayesAverage, double? weight) =>
        (average, bayesAverage, weight);

    private static ParsedThings Parse(string answer) =>
        BggThingParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(answer)), Moment);

    private static ParsedThings ParsePadded(int paddingCharacters)
    {
        var builder = new StringBuilder("<items><!--", paddingCharacters + 100);
        builder.Append('x', paddingCharacters);
        builder.Append("--></items>");

        return BggThingParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(builder.ToString())), Moment);
    }

    private static string Answer(params string[] items) => $"<items termsofuse=\"https://example.com/terms\">{string.Concat(items)}</items>";

    private static string Item(int id, string content) =>
        $"<item type=\"boardgame\" id=\"{id}\"><name type=\"primary\" value=\"Example {id}\" />{content}</item>";

    private static string Value(string name, string value) => $"<{name} value=\"{value}\" />";

    private static string Link(string type, int id, string value, bool inbound = false) =>
        $"<link type=\"{type}\" id=\"{id}\" value=\"{value}\"{(inbound ? " inbound=\"true\"" : string.Empty)} />";

    private static string Ratings(string average, string bayes, string weight) =>
        "<statistics page=\"1\"><ratings>"
        + Value("average", average) + Value("bayesaverage", bayes) + Value("averageweight", weight)
        + "</ratings></statistics>";
}

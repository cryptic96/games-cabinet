using System.Text;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.FakeBgg;
using Cabinet.Repository.Bgg;
using FluentAssertions;

namespace Cabinet.UnitTests.Bgg;

/// <summary>Proves the collection reader keeps what BGG lists as owned, exactly as listed, and survives odd entries.</summary>
[Trait("Category", "Bgg")]
public class BggCollectionParserTests
{
    [Fact]
    public void Only_owned_entries_are_kept_and_names_are_read_as_element_text()
    {
        const string answer = """
            <items totalitems="2" termsofuse="https://example.com/terms">
              <item objecttype="thing" objectid="100" subtype="boardgame" collid="900">
                <name sortindex="1"> Example &amp; Co </name>
                <yearpublished>1999</yearpublished>
                <status own="1" />
              </item>
              <item objecttype="thing" objectid="101" subtype="boardgame" collid="901">
                <name sortindex="1">Not Owned</name>
                <status own="0" />
              </item>
            </items>
            """;

        var parsed = Parse(answer);

        parsed.TotalItems.Should().Be(2);
        parsed.SkippedItems.Should().Be(0);
        parsed.Items.Should().ContainSingle().Which.Should().Be(
            new SnapshotItem(900, 100, "Example & Co", ItemKind.Base, 1999, null, null));
    }

    [Fact]
    public void Two_copies_of_one_game_are_two_items_with_their_own_entry_identifiers()
    {
        var parsed = Parse(Answer(
            Entry(900, 100, "Twice Owned"),
            Entry(901, 100, "Twice Owned")));

        parsed.Items.Select(item => (item.CollectionId, item.GameId)).Should().Equal((900, 100), (901, 100));
    }

    [Fact]
    public void An_entry_without_a_usable_identifier_is_skipped_and_counted_and_the_rest_is_kept()
    {
        var parsed = Parse(Answer(
            Entry(900, 100, "Kept"),
            "<item objectid=\"101\" collid=\"not-a-number\"><name>Broken</name><status own=\"1\" /></item>",
            "<item collid=\"902\"><name>No Game</name><status own=\"1\" /></item>",
            Entry(903, 103, "Also Kept")));

        parsed.Items.Select(item => item.Title).Should().Equal("Kept", "Also Kept");
        parsed.SkippedItems.Should().Be(2);
    }

    [Fact]
    public void Control_characters_are_removed_and_the_title_is_trimmed_and_capped()
    {
        var longTitle = new string('x', 450);

        var parsed = Parse(Answer(
            Entry(900, 100, "\tLeft\nover \t"),
            Entry(901, 101, longTitle),
            Entry(902, 102, "  Plain Title  ")));

        parsed.Items.Select(item => item.Title).Should().Equal(
            "Leftover",
            new string('x', BggCollectionParser.MaxTitleLength),
            "Plain Title");
    }

    [Fact]
    public void Every_character_below_space_is_removed_from_a_title()
    {
        BggCollectionParser.CleanTitle("\u0001Left\u001fover\u0000").Should().Be("Leftover");
    }

    [Fact]
    public void A_cap_never_splits_a_surrogate_pair()
    {
        var title = new string('x', BggCollectionParser.MaxTitleLength - 1) + "\U0001F3B2";

        BggCollectionParser.CleanTitle(title).Should().Be(new string('x', BggCollectionParser.MaxTitleLength - 1));
    }

    [Fact]
    public void A_blank_title_stays_blank_and_everything_else_in_a_title_is_left_alone()
    {
        var parsed = Parse(Answer(
            Entry(900, 100, string.Empty),
            Entry(901, 101, "  "),
            Entry(902, 102, "Tom &amp; Jerry: ÄÖ 港の灯台")));

        parsed.Items.Select(item => item.Title).Should().Equal(string.Empty, string.Empty, "Tom &amp; Jerry: ÄÖ 港の灯台");
    }

    [Fact]
    public void Dimensions_are_read_from_the_value_attributes_of_the_owned_version()
    {
        var parsed = Parse(Answer(Entry(900, 100, "Sized", Version("6.3", "8.27", "2.09"))));

        parsed.Items.Single().Dimensions.Should().Be(new VersionDimensions(6.3, 8.27, 2.09));
    }

    [Theory]
    [InlineData("", "8.27", "2.09")]
    [InlineData("6.3", "wide", "2.09")]
    public void A_missing_or_unreadable_part_means_no_dimensions(string width, string length, string depth)
    {
        var parsed = Parse(Answer(Entry(900, 100, "Odd", Version(width, length, depth))));

        parsed.Items.Single().Dimensions.Should().BeNull();
    }

    [Fact]
    public void An_entry_without_a_version_has_no_dimensions_and_a_zero_triple_is_kept_as_reported()
    {
        var parsed = Parse(Answer(
            Entry(900, 100, "No Version"),
            Entry(901, 101, "Zero", Version("0", "0", "0"))));

        parsed.Items[0].Dimensions.Should().BeNull();
        parsed.Items[1].Dimensions.Should().Be(new VersionDimensions(0, 0, 0));
    }

    [Fact]
    public void The_location_is_read_only_when_private_info_is_on()
    {
        var answer = Answer(
            Entry(900, 100, "Placed", privateInfo: "<privateinfo inventorylocation=\"  Shelf A  \" />"),
            Entry(901, 101, "Blank", privateInfo: "<privateinfo inventorylocation=\"   \" />"),
            Entry(902, 102, "None"));

        var withPrivateInfo = Parse(answer, includePrivateInfo: true);
        var withoutPrivateInfo = Parse(answer, includePrivateInfo: false);

        withPrivateInfo.Items.Select(item => item.Location).Should().Equal("Shelf A", null, null);
        withoutPrivateInfo.Items.Should().OnlyContain(item => item.Location == null);
    }

    [Fact]
    public void A_location_is_capped_in_length()
    {
        var parsed = Parse(
            Answer(Entry(900, 100, "Placed", privateInfo: $"<privateinfo inventorylocation=\"{new string('y', 400)}\" />")),
            includePrivateInfo: true);

        parsed.Items.Single().Location.Should().HaveLength(BggCollectionParser.MaxLocationLength);
    }

    [Fact]
    public void The_synthetic_answer_is_read_with_copies_expansions_dimensions_and_locations()
    {
        var items = SyntheticBggCollection.Create(65);
        var all = BggXml.Collection(items, new CollectionQuery(OwnOnly: true, null, null, Version: true, ShowPrivate: true));

        var parsed = BggCollectionParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(all)), ItemKind.Base, includePrivateInfo: true);

        parsed.SkippedItems.Should().Be(0);
        parsed.Items.Should().HaveCount(parsed.TotalItems!.Value);
        parsed.Items.Where(item => item.Location is not null).Select(item => item.Location).Should().BeEquivalentTo("Shelf A", "Shelf B");
        parsed.Items.Count(item => item.GameId == SyntheticBggCollection.FirstObjectId).Should().Be(2);
        parsed.Items.Should().Contain(item => item.Dimensions != null).And.Contain(item => item.Dimensions == null);
    }

    [Fact]
    public void A_document_with_a_type_definition_and_an_answer_that_is_not_a_collection_are_rejected()
    {
        var withDtd = "<!DOCTYPE items [<!ENTITY x \"y\">]><items totalitems=\"0\" />";

        var dtd = () => BggCollectionParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(withDtd)), ItemKind.Base, false);
        var notACollection = () => BggCollectionParser.Parse(new MemoryStream("<message>wait</message>"u8.ToArray()), ItemKind.Base, false);

        dtd.Should().Throw<System.Xml.XmlException>();
        notACollection.Should().Throw<BggAnswerException>();
    }

    private static ParsedCollection Parse(string answer, bool includePrivateInfo = false) =>
        BggCollectionParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(answer)), ItemKind.Base, includePrivateInfo);

    private static string Answer(params string[] entries) =>
        $"<items totalitems=\"{entries.Length}\">{string.Concat(entries)}</items>";

    private static string Entry(long collectionId, int gameId, string name, string version = "", string privateInfo = "") =>
        $"<item objecttype=\"thing\" objectid=\"{gameId}\" subtype=\"boardgame\" collid=\"{collectionId}\">"
        + $"<name sortindex=\"1\">{System.Security.SecurityElement.Escape(name)}</name>{version}"
        + $"<status own=\"1\" />{privateInfo}</item>";

    private static string Version(string width, string length, string depth) =>
        $"<version><item type=\"boardgameversion\" id=\"1\"><width value=\"{width}\" /><length value=\"{length}\" /><depth value=\"{depth}\" /></item></version>";
}

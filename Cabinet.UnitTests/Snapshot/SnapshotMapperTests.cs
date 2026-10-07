using System.Text;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Repository.Bgg;
using FluentAssertions;

namespace Cabinet.UnitTests.Snapshot;

/// <summary>Proves the mapped collection and its version do not depend on the order the source listed the items in.</summary>
[Trait("Category", "Snapshot")]
public class SnapshotMapperTests
{
    private static readonly SnapshotItem[] Items =
    [
        new(30, 3, "Third", ItemKind.Base, 2001, null, null),
        new(10, 2, "Second Base", ItemKind.Base, null, null, null),
        new(10, 1, "Same Entry Lower Game", ItemKind.Expansion, null, null, null),
        new(20, 9, "Middle", ItemKind.Base, 1999, null, null),
    ];

    [Fact]
    public void Items_are_ordered_by_collection_id_then_game_id()
    {
        var mapped = SnapshotMapper.ToCabinetItems(Snapshot(Items));

        mapped.Select(item => (item.CollectionId, item.BggId)).Should().Equal((10, 1), (10, 2), (20, 9), (30, 3));
        mapped.Should().OnlyContain(item => item.ExpansionOf.Count == 0);
    }

    [Fact]
    public void Items_get_the_default_box_for_their_kind()
    {
        SnapshotMapper.DefaultBox(ItemKind.Base).Should().Be(new BoxDimensions(225, 300, 60));
        SnapshotMapper.DefaultBox(ItemKind.Expansion).Should().Be(new BoxDimensions(200, 260, 40));
    }

    [Fact]
    public void The_version_is_the_same_for_any_source_order_and_changes_with_the_content()
    {
        var forward = SnapshotMapper.Version(SnapshotMapper.ToCabinetItems(Snapshot(Items)));
        var reversed = SnapshotMapper.Version(SnapshotMapper.ToCabinetItems(Snapshot(Items.Reverse())));
        var renamed = SnapshotMapper.Version(SnapshotMapper.ToCabinetItems(Snapshot(Items.Select(
            item => item.GameId == 9 ? item with { Title = "Renamed" } : item))));

        reversed.Should().Be(forward);
        renamed.Should().NotBe(forward);
        forward.Should().MatchRegex("^[0-9a-f]{16}$");
    }

    [Fact]
    public void An_empty_collection_has_a_version_too()
    {
        SnapshotMapper.Version(SnapshotMapper.ToCabinetItems(Snapshot([]))).Should().MatchRegex("^[0-9a-f]{16}$");
    }

    [Fact]
    public void The_parser_keeps_owned_entries_only_and_reads_names_as_element_text()
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

        var parsed = BggCollectionParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(answer)), ItemKind.Base);

        parsed.TotalItems.Should().Be(2);
        parsed.Items.Should().ContainSingle().Which.Should().Be(
            new SnapshotItem(900, 100, "Example & Co", ItemKind.Base, 1999, null, null));
    }

    [Fact]
    public void The_parser_rejects_a_document_with_a_type_definition_and_an_answer_that_is_not_a_collection()
    {
        var withDtd = "<!DOCTYPE items [<!ENTITY x \"y\">]><items totalitems=\"0\" />";

        var dtd = () => BggCollectionParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(withDtd)), ItemKind.Base);
        var notACollection = () => BggCollectionParser.Parse(new MemoryStream("<message>wait</message>"u8.ToArray()), ItemKind.Base);

        dtd.Should().Throw<System.Xml.XmlException>();
        notACollection.Should().Throw<BggAnswerException>();
    }

    private static CollectionSnapshot Snapshot(IEnumerable<SnapshotItem> items) =>
        new(CollectionSnapshot.CurrentSchemaVersion, DateTimeOffset.UnixEpoch, [.. items]);
}

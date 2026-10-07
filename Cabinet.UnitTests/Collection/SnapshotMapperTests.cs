using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using FluentAssertions;

namespace Cabinet.UnitTests.Collection;

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
    public void Items_are_ordered_by_collection_id_then_game_id_and_one_entry_is_one_item()
    {
        var mapped = SnapshotMapper.ToCabinetItems(Snapshot(Items));

        mapped.Select(item => (item.CollectionId, item.BggId)).Should().Equal((10, 1), (20, 9), (30, 3));
        mapped.Should().OnlyContain(item => item.ExpansionOf.Count == 0);
    }

    [Fact]
    public void An_entry_listed_as_a_base_game_and_as_an_expansion_is_one_expansion()
    {
        var mapped = SnapshotMapper.ToCabinetItems(Snapshot(Items.Reverse()));

        mapped.Should().ContainSingle(item => item.CollectionId == 10)
            .Which.Kind.Should().Be(ItemKind.Expansion);
    }

    [Fact]
    public void Two_entries_for_the_same_game_stay_two_items()
    {
        SnapshotItem[] copies =
        [
            new(11, 5, "Twice Owned", ItemKind.Base, null, null, null),
            new(12, 5, "Twice Owned", ItemKind.Base, null, null, null),
        ];

        var mapped = SnapshotMapper.ToCabinetItems(Snapshot(copies));

        mapped.Select(item => item.CollectionId).Should().Equal(11, 12);
        mapped.Select(item => item.BggId).Should().Equal(5, 5);
    }

    [Fact]
    public void Boxes_come_from_the_owned_version_and_fall_back_to_the_default_for_the_kind()
    {
        SnapshotItem[] items =
        [
            new(1, 1, "Sized", ItemKind.Base, null, new VersionDimensions(9.5, 11.75, 3.1), null),
            new(2, 2, "Zero", ItemKind.Base, null, new VersionDimensions(0, 0, 0), null),
            new(3, 3, "Unknown Expansion", ItemKind.Expansion, null, null, null),
        ];

        var mapped = SnapshotMapper.ToCabinetItems(Snapshot(items));

        mapped[0].Box.Should().Be(BoxFromVersion.Map(new VersionDimensions(9.5, 11.75, 3.1), ItemKind.Base));
        mapped[0].Box.Should().NotBe(SnapshotMapper.DefaultBox(ItemKind.Base));
        mapped[1].Box.Should().Be(new BoxDimensions(225, 300, 60));
        mapped[2].Box.Should().Be(new BoxDimensions(200, 260, 40));
    }

    [Fact]
    public void The_default_box_is_the_one_realistic_size_per_kind()
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
        var resized = SnapshotMapper.Version(SnapshotMapper.ToCabinetItems(Snapshot(Items.Select(
            item => item.GameId == 9 ? item with { Dimensions = new VersionDimensions(9.5, 11.75, 3.1) } : item))));

        reversed.Should().Be(forward);
        renamed.Should().NotBe(forward);
        resized.Should().NotBe(forward);
        forward.Should().MatchRegex("^[0-9a-f]{16}$");
    }

    [Fact]
    public void An_empty_collection_has_a_version_too()
    {
        SnapshotMapper.Version(SnapshotMapper.ToCabinetItems(Snapshot([]))).Should().MatchRegex("^[0-9a-f]{16}$");
    }

    private static CollectionSnapshot Snapshot(IEnumerable<SnapshotItem> items) =>
        new(CollectionSnapshot.CurrentSchemaVersion, DateTimeOffset.UnixEpoch, [.. items]);
}

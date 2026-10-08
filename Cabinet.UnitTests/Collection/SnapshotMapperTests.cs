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
    public void An_expansion_carries_the_games_its_details_say_it_expands_and_a_base_game_never_does()
    {
        var snapshot = Snapshot(Items) with { Games = Games((1, [new BaseGameRef(2, "Second Base")]), (2, [new BaseGameRef(1, "Loop")])) };

        var mapped = SnapshotMapper.ToCabinetItems(snapshot);

        mapped.Single(item => item.BggId == 1).ExpansionOf.Should().Equal(new BaseGameRef(2, "Second Base"));
        mapped.Where(item => item.Kind == ItemKind.Base).Should().OnlyContain(item => item.ExpansionOf.Count == 0);
    }

    [Fact]
    public void The_version_changes_when_an_expansions_references_change_and_not_when_other_details_change()
    {
        var plain = Snapshot(Items) with { Games = Games((1, [new BaseGameRef(2, "Second Base")])) };
        var otherBase = Snapshot(Items) with { Games = Games((1, [new BaseGameRef(3, "Third")])) };
        var renamedBase = Snapshot(Items) with { Games = Games((1, [new BaseGameRef(2, "Renamed Base")])) };
        var richer = Snapshot(Items) with
        {
            Games = new Dictionary<int, GameDetails>
            {
                [1] = Details([new BaseGameRef(2, "Second Base")]) with { Designers = ["Invented Designer 1"], Average = 7.5, Weight = 3.1, MinPlayers = 2 },
                [9] = Details([]) with { Mechanics = ["Example Mechanic A"], BayesAverage = 6 },
            },
        };

        string Version(CollectionSnapshot snapshot) => SnapshotMapper.Version(SnapshotMapper.ToCabinetItems(snapshot));

        Version(otherBase).Should().NotBe(Version(plain));
        Version(renamedBase).Should().NotBe(Version(plain));
        Version(richer).Should().Be(Version(plain));
        Version(Snapshot(Items)).Should().NotBe(Version(plain));
    }

    [Fact]
    public void An_item_carries_the_ids_of_its_game_and_series_families_only_distinct_and_in_source_order()
    {
        var details = Details([]) with
        {
            Families =
            [
                new FamilyLink(7101, "Theme: Invented Theme 1"),
                new FamilyLink(7002, "Game: Example Line"),
                new FamilyLink(7201, "Components: Invented Pieces"),
                new FamilyLink(7001, "Series: Example Saga"),
                new FamilyLink(7002, "Game: Example Line"),
                new FamilyLink(7301, "Players: Invented Solo Rules"),
                new FamilyLink(7401, "Gameplay: Not A Series"),
                new FamilyLink(7501, "series: Wrong Case"),
            ],
        };
        var snapshot = Snapshot(Items) with { Games = new Dictionary<int, GameDetails> { [9] = details } };

        var mapped = SnapshotMapper.ToCabinetItems(snapshot);

        mapped.Single(item => item.BggId == 9).SeriesFamilies.Should().Equal(7002, 7001);
        mapped.Where(item => item.BggId != 9).Should().OnlyContain(item => item.SeriesFamilies == null || item.SeriesFamilies.Count == 0);
    }

    [Fact]
    public void The_version_changes_when_a_series_family_changes_and_not_when_a_broad_family_does()
    {
        GameDetails With(params FamilyLink[] families) => Details([]) with { Families = families };

        CollectionSnapshot SnapshotWith(GameDetails details) =>
            Snapshot(Items) with { Games = new Dictionary<int, GameDetails> { [9] = details } };

        string Version(CollectionSnapshot snapshot) => SnapshotMapper.Version(SnapshotMapper.ToCabinetItems(snapshot));

        var series = Version(SnapshotWith(With(new FamilyLink(7001, "Series: Example Saga"))));
        var otherSeries = Version(SnapshotWith(With(new FamilyLink(7002, "Series: Example Saga"))));
        var withBroad = Version(SnapshotWith(With(new FamilyLink(7101, "Theme: Invented Theme 1"), new FamilyLink(7001, "Series: Example Saga"))));
        var none = Version(SnapshotWith(With()));

        otherSeries.Should().NotBe(series);
        none.Should().NotBe(series);
        withBroad.Should().Be(series);
    }

    [Fact]
    public void An_empty_collection_has_a_version_too()
    {
        SnapshotMapper.Version(SnapshotMapper.ToCabinetItems(Snapshot([]))).Should().MatchRegex("^[0-9a-f]{16}$");
    }

    private static Dictionary<int, GameDetails> Games(params (int GameId, IReadOnlyList<BaseGameRef> Expands)[] games) =>
        games.ToDictionary(entry => entry.GameId, entry => Details(entry.Expands));

    private static GameDetails Details(IReadOnlyList<BaseGameRef> expands) =>
        new(DateTimeOffset.UnixEpoch, null, null, null, null, null, null, null, null, null, [], [], expands, null);

    private static CollectionSnapshot Snapshot(IEnumerable<SnapshotItem> items) =>
        new(CollectionSnapshot.CurrentSchemaVersion, DateTimeOffset.UnixEpoch, [.. items]);
}

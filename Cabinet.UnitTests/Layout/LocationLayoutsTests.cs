using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.Service.Collection;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Pins the cabinets built per storage location: the owner's order with the games that have no location last, nothing for a
/// location that holds no game, a location's cabinet that never changes when another location gains games, the few-games rule
/// applied to each location alone, and expansions paired again among the games of their own location.
/// </summary>
public sealed class LocationLayoutsTests
{
    private const string CabinetId = "a1b2c3d4";
    private const string ShelfId = "e5f6a7b8";
    private const string LentId = "c0ffee01";

    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly SectionDesign Design = SectionDesigns.Desktop;

    private static readonly LayoutOptions Options = LayoutOptions.Default;

    private static readonly IReadOnlyList<OwnerLocation> Locations =
    [
        new OwnerLocation(CabinetId, "Cabinet A"),
        new OwnerLocation(ShelfId, "Shelf B"),
        new OwnerLocation(LentId, "Lent out"),
    ];

    [Fact]
    [Trait("Category", "Layout")]
    public void The_groups_follow_the_owners_order_with_Unknown_last_and_an_empty_location_left_out()
    {
        var snapshot = SnapshotOf(Bases(1, 3), Bases(2, 4, 20), Bases(3, 2, 40));
        var book = Book((CabinetId, 1, 3), (ShelfId, 20, 4), (null, 40, 2));

        var built = Build(snapshot, book);

        built.Select(location => location.Id).Should().Equal(CabinetId, ShelfId, null);
        built.Select(location => location.Name).Should().Equal("Cabinet A", "Shelf B", null);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_owners_order_wins_over_the_order_the_games_were_added_in()
    {
        var snapshot = SnapshotOf(Bases(1, 3), Bases(2, 3, 20));
        var book = new LocationBook(
            [new OwnerLocation(ShelfId, "Shelf B"), new OwnerLocation(CabinetId, "Cabinet A")],
            Entries((CabinetId, 1, 3), (ShelfId, 20, 3)));

        Build(snapshot, book).Select(location => location.Id).Should().Equal(ShelfId, CabinetId);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void With_no_game_placed_only_Unknown_remains()
    {
        var snapshot = SnapshotOf(Bases(1, 5));
        var book = new LocationBook(Locations, new Dictionary<long, string>());

        var built = Build(snapshot, book);

        built.Should().ContainSingle().Which.Id.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void An_owner_without_locations_gets_no_cabinets()
    {
        var built = Build(SnapshotOf(Bases(1, 5)), LocationBook.Empty);

        built.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_collection_with_no_games_gets_no_cabinets()
    {
        var built = Build(SnapshotOf(), new LocationBook(Locations, new Dictionary<long, string>()));

        built.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_locations_cabinet_is_the_same_whether_another_location_holds_three_games_or_thirty()
    {
        var few = Build(SnapshotOf(Bases(1, 3), Bases(2, 5, 100)), Book((CabinetId, 1, 3), (ShelfId, 100, 5)));
        var many = Build(SnapshotOf(Bases(1, 30), Bases(2, 5, 100)), Book((CabinetId, 1, 30), (ShelfId, 100, 5)));

        Json(few.Single(location => location.Id == ShelfId)).Should().Be(Json(many.Single(location => location.Id == ShelfId)));
        Json(few.Single(location => location.Id == CabinetId)).Should().NotBe(Json(many.Single(location => location.Id == CabinetId)));
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_location_with_eleven_games_faces_every_box_out_and_one_with_twelve_does_not()
    {
        var eleven = Build(SnapshotOf(Bases(1, 11)), Book((CabinetId, 1, 11))).Single();
        var twelve = Build(SnapshotOf(Bases(1, 12)), Book((CabinetId, 1, 12))).Single();

        PlacementsOf(eleven).Should().HaveCount(11).And.OnlyContain(placement => placement.Kind == PlacementKind.Cover);
        PlacementsOf(twelve).Should().HaveCount(12).And.Contain(placement => placement.Kind != PlacementKind.Cover);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_few_games_rule_counts_each_locations_games_alone()
    {
        var snapshot = SnapshotOf(Bases(1, 30), Bases(2, 4, 100));
        var built = Build(snapshot, Book((CabinetId, 1, 30), (ShelfId, 100, 4)));

        PlacementsOf(built.Single(location => location.Id == ShelfId)).Should().OnlyContain(placement => placement.Kind == PlacementKind.Cover);
        PlacementsOf(built.Single(location => location.Id == CabinetId)).Should().Contain(placement => placement.Kind != PlacementKind.Cover);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void An_expansion_whose_only_owned_base_is_elsewhere_is_drawn_in_its_own_location_with_its_base_title()
    {
        var snapshot = SnapshotWithExpansions(
            [Base(1, 1, "Base One"), Expansion(2, 2, "Expansion Two"), Base(3, 3, "Base Three")],
            "Base One");
        var book = new LocationBook(Locations, Entries((CabinetId, 1, 1), (ShelfId, 2, 2)));

        var shelf = Build(snapshot, book).Single(location => location.Id == ShelfId);
        var expansion = PlacementsOf(shelf).Single(placement => placement.EntryId == 2);

        expansion.IsExpansion.Should().BeTrue();
        expansion.BaseTitle.Should().Be("Base One");
        expansion.FamilyId.Should().BeNull("it stands beside nothing in this cabinet");
        PlacementsOf(shelf).Select(placement => placement.EntryId).Should().BeEquivalentTo(new long[] { 2, 3 });
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void An_expansion_with_owned_bases_in_two_locations_stands_beside_the_base_in_its_own_location()
    {
        var snapshot = SnapshotWithExpansions(
            [Base(1, 1, "Base One"), Base(2, 2, "Base Two"), Expansion(3, 3, "Expansion Three")],
            "Base One",
            "Base Two");
        var book = new LocationBook(Locations, Entries((CabinetId, 1, 1), (ShelfId, 2, 2)));

        var shelf = Build(snapshot, book).Single(location => location.Id == ShelfId);
        var expansion = PlacementsOf(shelf).Single(placement => placement.EntryId == 3);

        expansion.Kind.Should().BeOneOf(PlacementKind.ExpansionLayer, PlacementKind.ExpansionSpine);
        expansion.FamilyId.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void An_expansion_that_follows_its_base_stands_beside_it_in_the_bases_cabinet()
    {
        var snapshot = SnapshotWithExpansions([Base(1, 1, "Base One"), Expansion(2, 2, "Expansion Two")], "Base One");
        var book = new LocationBook(Locations, Entries((CabinetId, 1, 1), (CabinetId, 2, 1)));

        var cabinet = Build(snapshot, book).Single();
        var expansion = PlacementsOf(cabinet).Single(placement => placement.EntryId == 2);

        cabinet.Id.Should().Be(CabinetId);
        expansion.Kind.Should().BeOneOf(PlacementKind.ExpansionLayer, PlacementKind.ExpansionSpine);
        expansion.FamilyId.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Every_game_is_drawn_in_exactly_one_cabinet_and_two_builds_give_the_same_json()
    {
        var snapshot = SnapshotOf(Bases(1, 14), Bases(2, 6, 100), Bases(3, 5, 200));
        var book = Book((CabinetId, 1, 14), (ShelfId, 100, 6));

        var first = Build(snapshot, book);
        var again = Build(snapshot, book);

        first.SelectMany(PlacementsOf).Select(placement => placement.EntryId).Should().OnlyHaveUniqueItems().And.HaveCount(25);
        LocationLayouts.Serialize("\"tag\"", first).Should().Be(LocationLayouts.Serialize("\"tag\"", again));
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_location_with_one_or_two_games_is_a_short_complete_piece()
    {
        var built = Build(SnapshotOf(Bases(1, 2), Bases(2, 1, 100)), Book((CabinetId, 1, 2), (ShelfId, 100, 1)));

        foreach (var location in built)
        {
            location.Layout.Sections.Should().ContainSingle();
            location.Layout.Sections[0].Cubbies.Count.Should().BeLessThan(Design.Cubbies.Count);
            location.Layout.Sections[0].HeightMm.Should().BeLessThan(Design.InteriorHeightMm);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_document_names_the_big_cabinets_tag_and_leaves_out_what_is_absent()
    {
        var built = Build(SnapshotOf(Bases(1, 2), Bases(2, 1, 100)), Book((CabinetId, 1, 2), (null, 100, 1)));

        var json = LocationLayouts.Serialize("\"tag\"", built);

        json.Should().StartWith("{\"layout\":\"\\u0022tag\\u0022\",\"layoutVersion\":").And.Contain("\"locations\":[{\"id\":\"a1b2c3d4\",\"name\":\"Cabinet A\",\"sections\":");
        json.Should().Contain("{\"sections\":[", "the Unknown cabinet has neither an id nor a name");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_state_builds_each_profile_once()
    {
        var snapshot = SnapshotOf(Bases(1, 3), Bases(2, 2, 100));
        var overlay = OwnerOverlay.From(snapshot, OwnerData.Empty with
        {
            Locations = Locations,
            Games =
            [
                new OwnerGame(1, CabinetId, null, null),
                new OwnerGame(2, CabinetId, null, null),
                new OwnerGame(3, CabinetId, null, null),
                new OwnerGame(100, ShelfId, null, null),
                new OwnerGame(101, ShelfId, null, null),
            ],
        });

        var state = CollectionState.FromSnapshot(snapshot, ArtRules.Default, overlay);
        var next = CollectionState.FromSnapshot(snapshot, ArtRules.Default, overlay);

        state.LocationLayoutsFor(Design, Options).Should().BeSameAs(state.LocationLayoutsFor(Design, Options));
        state.LocationLayoutsFor(Design, Options).Should().NotBeSameAs(state.LocationLayoutsFor(SectionDesigns.Phone, Options));
        next.LocationLayoutsFor(Design, Options).Should().NotBeSameAs(state.LocationLayoutsFor(Design, Options));
        next.LocationLayoutsFor(Design, Options).Json.Should().Be(state.LocationLayoutsFor(Design, Options).Json);
        state.LocationLayoutsFor(Design, Options).ETag.Should().Be(
            state.LayoutFor(Design, Options).ETag.TrimEnd('"') + "-by-location\"");
    }

    private static IReadOnlyList<LocationLayout> Build(CollectionSnapshot snapshot, LocationBook book) =>
        LocationLayouts.Build(SnapshotMapper.ToCabinetItems(snapshot), snapshot, book, Design, Options);

    private static string Json(LocationLayout location) => LayoutJson.Serialize(location.Layout);

    private static IEnumerable<Placement> PlacementsOf(LocationLayout location) =>
        location.Layout.Sections.SelectMany(section => section.Cubbies).SelectMany(cubby => cubby.Placements);

    private static LocationBook Book(params (string? Location, long First, int Count)[] groups) =>
        new(Locations, Entries(groups));

    private static Dictionary<long, string> Entries(params (string? Location, long First, int Count)[] groups)
    {
        var entries = new Dictionary<long, string>();

        foreach (var (location, first, count) in groups)
        {
            for (var offset = 0; offset < count; offset++)
            {
                if (location is not null)
                {
                    entries[first + offset] = location;
                }
            }
        }

        return entries;
    }

    private static SnapshotItem[] Bases(int group, int count, long first = 1) =>
        [.. Enumerable.Range(0, count).Select(offset => Base(first + offset, (int)(first + offset), $"Invented Game {group}-{offset}"))];

    private static SnapshotItem Base(long entry, int gameId, string title) =>
        new(entry, gameId, title, ItemKind.Base, 2015, null, null);

    private static SnapshotItem Expansion(long entry, int gameId, string title) =>
        new(entry, gameId, title, ItemKind.Expansion, 2015, null, null);

    private static CollectionSnapshot SnapshotOf(params SnapshotItem[][] groups) =>
        new(CollectionSnapshot.CurrentSchemaVersion, Moment, [.. groups.SelectMany(group => group)], null, null);

    private static CollectionSnapshot SnapshotWithExpansions(SnapshotItem[] items, params string[] baseTitles)
    {
        var details = new GameDetails(Moment, 2, 4, 60, null, null, 10, 2.4, 7.4, 7.0, ["Invented Designer"], ["Drafting"], [], null);
        var games = new Dictionary<int, GameDetails>();

        foreach (var item in items.Where(item => item.Kind == ItemKind.Expansion))
        {
            games[item.GameId] = details with
            {
                ExpandsGames = [.. baseTitles
                    .Select(title => items.First(candidate => candidate.Title == title))
                    .Select(candidate => new BaseGameRef(candidate.GameId, candidate.Title))],
            };
        }

        return new CollectionSnapshot(CollectionSnapshot.CurrentSchemaVersion, Moment, items, null, games);
    }
}

using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>Proves the location book lists the owner's locations in order, gives each entry its place and counts the copies in each location.</summary>
[Trait("Category", "Owner")]
public sealed class LocationBookTests
{
    private const string CabinetA = "a1b2c3d4";
    private const string ShelfB = "e5f6a7b8";
    private const string LentOut = "c0ffee01";

    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_book_lists_the_locations_in_the_owners_order()
    {
        var book = BookOf(Data());

        book.Order.Select(location => location.Name).Should().Equal("Cabinet A", "Shelf B", "Lent out");
    }

    [Fact]
    public void An_entry_has_the_place_of_its_own_copy_and_an_expansion_follows_its_base()
    {
        var book = BookOf(Data(new OwnerGame(100, ShelfB, null, null)));

        book.LocationOf(1).Should().Be(ShelfB);
        book.LocationOf(2).Should().Be(ShelfB);
        book.LocationOf(3).Should().Be(ShelfB);
        book.LocationOf(4).Should().BeNull();
    }

    [Fact]
    public void An_expansion_stored_apart_keeps_its_own_place()
    {
        var book = BookOf(Data(new OwnerGame(100, ShelfB, null, null), new OwnerGame(200, CabinetA, null, null)));

        book.LocationOf(1).Should().Be(ShelfB);
        book.LocationOf(3).Should().Be(CabinetA);
    }

    [Fact]
    public void Two_copies_of_one_game_can_be_in_different_places()
    {
        var book = BookOf(Data(new OwnerGame(100, CabinetA, null, null, 1), new OwnerGame(100, ShelfB, null, null, 2)));

        book.LocationOf(1).Should().Be(CabinetA);
        book.LocationOf(2).Should().Be(ShelfB);
    }

    [Fact]
    public void Copies_are_counted_in_each_location()
    {
        var book = BookOf(Data(new OwnerGame(100, ShelfB, null, null)));

        book.CopiesAt(ShelfB).Should().Be(3);
        book.CopiesAt(CabinetA).Should().Be(0);
        book.CopiesAt(LentOut).Should().Be(0);
    }

    [Fact]
    public void The_text_changes_when_the_order_a_name_an_id_or_the_place_of_an_entry_changes()
    {
        var data = Data(new OwnerGame(100, CabinetA, null, null, 1));
        var text = BookOf(data).Text;

        var reordered = data with { Locations = [data.Locations[1], data.Locations[0], data.Locations[2]] };
        var renamed = data with { Locations = [data.Locations[0] with { Name = "Cabinet C" }, data.Locations[1], data.Locations[2]] };
        var reidentified = data with
        {
            Locations = [data.Locations[0], data.Locations[1], data.Locations[2] with { Id = "c0ffee02" }],
        };
        var moved = data with { Games = [new OwnerGame(100, ShelfB, null, null, 1)] };

        BookOf(reordered).Text.Should().NotBe(text);
        BookOf(renamed).Text.Should().NotBe(text);
        BookOf(reidentified).Text.Should().NotBe(text);
        BookOf(moved).Text.Should().NotBe(text);
        BookOf(data).Text.Should().Be(text);
    }

    [Fact]
    public void An_empty_book_has_no_locations_and_no_text()
    {
        LocationBook.Empty.Order.Should().BeEmpty();
        LocationBook.Empty.EntryLocations.Should().BeEmpty();
        LocationBook.Empty.Text.Should().BeEmpty();
        LocationBook.Empty.LocationOf(1).Should().BeNull();
        BookOf(OwnerData.Empty).Text.Should().BeEmpty();
    }

    [Fact]
    public void An_entry_in_a_location_with_a_malformed_id_is_left_out()
    {
        var data = OwnerData.Empty with
        {
            Locations = [new OwnerLocation("not-hex!", "Cabinet A")],
            Games = [new OwnerGame(100, "not-hex!", null, null)],
        };

        BookOf(data).EntryLocations.Should().BeEmpty();
    }

    private static LocationBook BookOf(OwnerData data)
    {
        var snapshot = Snapshot();

        return LocationBook.From(OwnerOverlay.From(snapshot, data), snapshot.Items.Select(item => item.CollectionId));
    }

    private static OwnerData Data(params OwnerGame[] games) =>
        OwnerData.Empty with
        {
            Locations = [new OwnerLocation(CabinetA, "Cabinet A"), new OwnerLocation(ShelfB, "Shelf B"), new OwnerLocation(LentOut, "Lent out")],
            Games = games,
        };

    private static CollectionSnapshot Snapshot() =>
        new(
            CollectionSnapshot.CurrentSchemaVersion,
            Moment,
            [
                new SnapshotItem(1, 100, "Example Game 100", ItemKind.Base, null, null, null),
                new SnapshotItem(2, 100, "Example Game 100", ItemKind.Base, null, null, null),
                new SnapshotItem(3, 200, "Example Expansion 200", ItemKind.Expansion, null, null, null),
                new SnapshotItem(4, 300, "Example Game 300", ItemKind.Base, null, null, null),
            ],
            Games: new Dictionary<int, GameDetails>
            {
                [200] = new GameDetails(Moment, null, null, null, null, null, null, null, null, null, [], [], [new BaseGameRef(100, "Example Game 100")], null),
            });
}

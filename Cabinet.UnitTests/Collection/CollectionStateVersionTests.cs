using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.Service.Collection;
using FluentAssertions;

namespace Cabinet.UnitTests.Collection;

/// <summary>Proves the collection version follows the owner's locations: their order, their names and where each copy is kept.</summary>
[Trait("Category", "Snapshot")]
public sealed class CollectionStateVersionTests
{
    private const string CabinetA = "a1b2c3d4";
    private const string ShelfB = "e5f6a7b8";
    private const string LentOut = "c0ffee01";

    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Two_states_that_differ_only_in_the_order_of_the_locations_have_different_versions()
    {
        var data = Data(new OwnerGame(100, CabinetA, null, null));
        var reordered = data with { Locations = [data.Locations[1], data.Locations[0], data.Locations[2]] };

        VersionOf(data).Should().NotBe(VersionOf(reordered));
    }

    [Fact]
    public void A_state_without_owner_locations_keeps_the_version_it_had_before()
    {
        var snapshot = Snapshot();
        var plain = CollectionState.FromSnapshot(snapshot);

        VersionOf(OwnerData.Empty).Should().Be(plain.Version);
        CollectionState.FromSnapshot(snapshot, ArtRules.Default, OwnerOverlay.None).Version.Should().Be(plain.Version);
        plain.Locations.Should().BeSameAs(LocationBook.Empty);
    }

    [Fact]
    public void The_version_changes_after_a_location_moves_up_and_after_a_copy_moves()
    {
        var data = Data(new OwnerGame(100, CabinetA, null, null, 1), new OwnerGame(100, CabinetA, null, null, 2));
        var before = VersionOf(data);

        var movedUp = LocationEdits.Move(data, ShelfB, -1);
        var copyMoved = LocationEdits.Assign(data, 100, 2, [1, 2], ShelfB, "Example Game 100");

        movedUp.Changed.Should().BeTrue();
        copyMoved.Changed.Should().BeTrue();
        VersionOf(movedUp.Data).Should().NotBe(before);
        VersionOf(copyMoved.Data).Should().NotBe(before);
        VersionOf(movedUp.Data).Should().NotBe(VersionOf(copyMoved.Data));
    }

    [Fact]
    public void The_version_stays_the_same_after_a_change_that_touches_nothing_visible()
    {
        var data = Data(new OwnerGame(100, CabinetA, null, null));

        VersionOf(data with { Revision = data.Revision + 5 }).Should().Be(VersionOf(data));
        VersionOf(data).Should().Be(VersionOf(data));
    }

    [Fact]
    public void The_state_holds_the_book_of_the_owners_locations()
    {
        var state = StateOf(Data(new OwnerGame(100, ShelfB, null, null)));

        state.Locations.Order.Select(location => location.Id).Should().Equal(CabinetA, ShelfB, LentOut);
        state.Locations.LocationOf(1).Should().Be(ShelfB);
    }

    private static string? VersionOf(OwnerData data) => StateOf(data).Version;

    private static CollectionState StateOf(OwnerData data)
    {
        var snapshot = Snapshot();

        return CollectionState.FromSnapshot(snapshot, ArtRules.Default, OwnerOverlay.From(snapshot, data));
    }

    private static OwnerData Data(params OwnerGame[] games) =>
        OwnerData.Empty with
        {
            Revision = 1,
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
                new SnapshotItem(3, 300, "Example Game 300", ItemKind.Base, null, null, null),
            ]);
}

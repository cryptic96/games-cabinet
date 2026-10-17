using System.Text;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.Repository.Images;
using Cabinet.Repository.Storage;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cabinet.UnitTests.Owner;

/// <summary>
/// Proves that where every copy of a game is kept survives being saved, downloaded, restored and shown on its card, for a game
/// owned once and a game owned three times, including a choice that older files hold for every copy of a game.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerPerCopyRoundTripTests : IDisposable
{
    private const string CabinetA = "0a1b2c3d";
    private const string ShelfB = "4e5f6a7b";
    private const int OnceGame = 100;
    private const int ThriceGame = 200;

    private static readonly ArtSourcePolicy Policy = new(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "example.org" });
    private static readonly DateTimeOffset Moment = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly TemporaryDirectory _source = new();
    private readonly TemporaryDirectory _target = new();

    public void Dispose()
    {
        _source.Dispose();
        _target.Dispose();
    }

    [Fact]
    public void A_game_owned_once_and_a_game_owned_three_times_keep_every_copys_place_through_save_download_and_restore()
    {
        var snapshot = Collection();
        var data = Data(
            new OwnerGame(OnceGame, ShelfB, null, null),
            new OwnerGame(ThriceGame, CabinetA, null, null),
            new OwnerGame(ThriceGame, ShelfB, null, null, 7002));

        var restored = RoundTrip(data);

        Places(snapshot, restored).Should().Equal(Places(snapshot, data));
        Places(snapshot, restored).Should().Equal(
            new Dictionary<long, string?> { [1] = "Shelf B", [7001] = "Cabinet A", [7002] = "Shelf B", [7003] = "Cabinet A" });
    }

    [Fact]
    public void A_game_wide_entry_from_an_older_file_keeps_applying_to_every_copy_after_the_round_trip()
    {
        var snapshot = Collection();
        var data = Data(new OwnerGame(ThriceGame, ShelfB, null, null));

        var restored = RoundTrip(data);

        Places(snapshot, restored).Should().Equal(
            new Dictionary<long, string?> { [1] = null, [7001] = "Shelf B", [7002] = "Shelf B", [7003] = "Shelf B" });
        restored.Games.Should().ContainSingle().Which.CollectionId.Should().BeNull();
    }

    [Fact]
    public void An_edit_of_one_copy_of_a_game_with_an_older_game_wide_choice_round_trips_with_the_other_copies_unmoved()
    {
        var snapshot = Collection();
        var data = Data(new OwnerGame(ThriceGame, CabinetA, null, null));

        var edited = LocationEdits.Assign(data, ThriceGame, 7003, [7001, 7002, 7003], ShelfB, "Quiet Quarry").Data;
        var restored = RoundTrip(edited);

        Places(snapshot, restored).Should().Equal(
            new Dictionary<long, string?> { [1] = null, [7001] = "Cabinet A", [7002] = "Cabinet A", [7003] = "Shelf B" });
    }

    [Fact]
    public void The_card_of_each_copy_carries_the_location_that_copy_has_after_the_round_trip()
    {
        var snapshot = Collection();
        var data = Data(
            new OwnerGame(ThriceGame, CabinetA, null, null),
            new OwnerGame(ThriceGame, ShelfB, null, null, 7002));

        var restored = RoundTrip(data);
        var effective = OwnerOverlay.From(snapshot, restored).Effective(snapshot);

        effective.Items.ToDictionary(item => item.CollectionId, item => item.Location)
            .Should().Equal(new Dictionary<long, string?> { [1] = null, [7001] = "Cabinet A", [7002] = "Shelf B", [7003] = "Cabinet A" });
    }

    [Fact]
    public void A_stored_second_format_file_with_a_game_wide_choice_places_every_copy_and_survives_the_round_trip()
    {
        File.WriteAllBytes(
            Path.Combine(_source.FullPath, OwnerDataStore.FileName),
            Encoding.UTF8.GetBytes(
                $"{{\"kind\":\"{OwnerData.KindMarker}\",\"schemaVersion\":2,\"revision\":5,"
                + "\"look\":{\"design\":\"legs\",\"wood\":\"oak\",\"backs\":\"painted\",\"lights\":\"evenings\",\"depth\":\"deep\"},"
                + $"\"locations\":[{{\"id\":\"{CabinetA}\",\"name\":\"Cabinet A\"}}],"
                + $"\"games\":[{{\"gameId\":{ThriceGame},\"locationId\":\"{CabinetA}\"}}],\"changesSinceDownload\":0,\"changes\":[]}}"));
        var loaded = Loaded(new OwnerDataStore(_source.FullPath, NullLogger<OwnerDataStore>.Instance));

        var restored = RoundTrip(loaded);

        Places(Collection(), restored).Should().Equal(
            new Dictionary<long, string?> { [1] = null, [7001] = "Cabinet A", [7002] = "Cabinet A", [7003] = "Cabinet A" });
    }

    private static CollectionSnapshot Collection() =>
        new(
            CollectionSnapshot.CurrentSchemaVersion,
            Moment,
            [
                new SnapshotItem(1, OnceGame, "Lantern Harbour", ItemKind.Base, 2018, null, null),
                new SnapshotItem(7001, ThriceGame, "Quiet Quarry", ItemKind.Base, 2015, null, null),
                new SnapshotItem(7002, ThriceGame, "Quiet Quarry", ItemKind.Base, 2019, null, null),
                new SnapshotItem(7003, ThriceGame, "Quiet Quarry", ItemKind.Base, 2021, null, null),
            ]);

    private static OwnerData Data(params OwnerGame[] games) =>
        OwnerData.Empty with
        {
            Revision = 4,
            Locations = [new OwnerLocation(CabinetA, "Cabinet A"), new OwnerLocation(ShelfB, "Shelf B")],
            Games = OwnerGames.Ordered(games),
        };

    private static Dictionary<long, string?> Places(CollectionSnapshot snapshot, OwnerData data)
    {
        var overlay = OwnerOverlay.From(snapshot, data);

        return snapshot.Items.ToDictionary(item => item.CollectionId, item => overlay.EntryLocationName(item.CollectionId));
    }

    private static OwnerData Loaded(OwnerDataStore store) =>
        store.Load().Should().BeOfType<OwnerDataState.Ready>().Which.Data;

    private OwnerData RoundTrip(OwnerData data)
    {
        var stored = new OwnerDataStore(_source.FullPath, NullLogger<OwnerDataStore>.Instance);
        stored.Save(data);
        var saved = Loaded(stored);
        var download = OwnerDataExport.Write(saved, Moment);
        var check = OwnerDataExport.Check(download, Policy);

        check.IsOk.Should().BeTrue();
        check.FormatVersion.Should().Be(OwnerData.CurrentSchemaVersion);

        var fresh = new OwnerDataStore(_target.FullPath, NullLogger<OwnerDataStore>.Instance);
        fresh.Save(check.Data!);

        return Loaded(fresh);
    }
}

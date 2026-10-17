using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Repository.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cabinet.UnitTests.Snapshot;

/// <summary>
/// Proves the stored collection stays readable across releases: a file written before the record of undescribed games
/// existed loads, the record round-trips, and a member this build does not know is skipped instead of setting the file aside.
/// </summary>
[Trait("Category", "Snapshot")]
public sealed class StoredSnapshotCompatibilityTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"cabinet-compat-tests-{Guid.NewGuid():N}");

    public StoredSnapshotCompatibilityTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string SnapshotPath => Path.Combine(_directory, SnapshotStore.FileName);

    [Fact]
    public void A_stored_collection_without_the_attempt_record_loads_and_stays_in_the_same_schema()
    {
        File.WriteAllText(
            SnapshotPath,
            """
            {
              "schemaVersion": 2,
              "capturedAtUtc": "2030-01-01T00:00:00Z",
              "items": [ { "collectionId": 7, "gameId": 11, "title": "First Example", "kind": "base" } ]
            }
            """);

        var loaded = CreateStore().Load();

        loaded.Should().NotBeNull();
        loaded!.SchemaVersion.Should().Be(2);
        loaded.DetailAttempts.Should().BeNull();
        File.Exists(SnapshotPath + ".bad").Should().BeFalse();
    }

    [Fact]
    public void The_attempt_record_round_trips_and_keeps_the_schema_version()
    {
        var moment = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var snapshot = new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            moment,
            [new SnapshotItem(7, 11, "First Example", ItemKind.Base, null, null, null)],
            DetailAttempts: new Dictionary<int, DateTimeOffset> { [11] = moment.AddHours(-3) });
        var store = CreateStore();

        store.Save(snapshot);

        store.Load()!.DetailAttempts.Should().BeEquivalentTo(new Dictionary<int, DateTimeOffset> { [11] = moment.AddHours(-3) });
        File.ReadAllText(SnapshotPath).Should().Contain("\"schemaVersion\":2").And.Contain("\"detailAttempts\"");
        CollectionSnapshot.CurrentSchemaVersion.Should().Be(2);
    }

    [Fact]
    public void A_stored_collection_without_alternate_names_or_an_edition_name_loads_in_the_same_schema()
    {
        File.WriteAllText(
            SnapshotPath,
            """
            {
              "schemaVersion": 2,
              "capturedAtUtc": "2030-01-01T00:00:00Z",
              "items": [ { "collectionId": 7, "gameId": 11, "title": "First Example", "kind": "base" } ],
              "games": {
                "11": { "enrichedAtUtc": "2030-01-01T00:00:00Z", "designers": [], "mechanics": [], "expandsGames": [], "detailsVersion": 1 }
              }
            }
            """);

        var loaded = CreateStore().Load();

        loaded.Should().NotBeNull();
        loaded!.SchemaVersion.Should().Be(2);
        loaded.Items.Single().VersionName.Should().BeNull();
        loaded.Games![11].AlternateNames.Should().BeNull();
        File.Exists(SnapshotPath + ".bad").Should().BeFalse();
    }

    [Fact]
    public void Alternate_names_and_an_edition_name_round_trip_unchanged_and_keep_the_schema_version()
    {
        var moment = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var snapshot = new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            moment,
            [new SnapshotItem(7, 11, "First Example", ItemKind.Base, null, null, null, VersionName: "First Example Deluxe edition")],
            Games: new Dictionary<int, GameDetails>
            {
                [11] = new(moment, null, null, null, null, null, null, null, null, null, [], [], [], null, DetailsVersion: GameDetails.CurrentDetailsVersion, AlternateNames: ["Voorbeeldspel", "Ex\u00e4mple"]),
            });
        var store = CreateStore();

        store.Save(snapshot);

        store.Load().Should().BeEquivalentTo(snapshot, options => options.WithStrictOrdering());
        File.ReadAllText(SnapshotPath).Should().Contain("\"schemaVersion\":2").And.Contain("\"alternateNames\"").And.Contain("\"versionName\"");
    }

    [Fact]
    public void A_stored_collection_with_a_member_this_build_does_not_know_still_loads()
    {
        File.WriteAllText(
            SnapshotPath,
            """
            {
              "schemaVersion": 2,
              "capturedAtUtc": "2030-01-01T00:00:00Z",
              "items": [ { "collectionId": 7, "gameId": 11, "title": "First Example", "kind": "base", "somethingNew": { "a": 1 } } ],
              "detailAttempts": { "11": "2030-01-01T00:00:00Z" },
              "aMemberFromALaterRelease": [ 1, 2, 3 ]
            }
            """);

        var loaded = CreateStore().Load();

        loaded.Should().NotBeNull();
        loaded!.Items.Should().ContainSingle();
        loaded.DetailAttempts.Should().ContainKey(11);
        File.Exists(SnapshotPath + ".bad").Should().BeFalse();
    }

    [Fact]
    public void A_collection_stored_without_attempts_loads_back_without_attempts()
    {
        var store = CreateStore();

        store.Save(new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero),
            [new SnapshotItem(7, 11, "First Example", ItemKind.Base, null, null, null)]));

        store.Load()!.DetailAttempts.Should().BeNull();
    }

    private SnapshotStore CreateStore() => new(_directory, NullLogger<SnapshotStore>.Instance);
}

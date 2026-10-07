using System.Text.Json;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Repository.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Cabinet.UnitTests.Snapshot;

/// <summary>Proves the stored collection round-trips, survives damage by being set aside, and never leaves temporary files.</summary>
[Trait("Category", "Snapshot")]
public sealed class SnapshotStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"cabinet-snapshot-tests-{Guid.NewGuid():N}");
    private readonly CapturingLogger _logger = new();

    public SnapshotStoreTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string SnapshotPath => Path.Combine(_directory, "snapshot.json");

    private string BadPath => SnapshotPath + ".bad";

    [Fact]
    public void A_missing_file_loads_as_no_snapshot()
    {
        CreateStore().Load().Should().BeNull();
        File.Exists(BadPath).Should().BeFalse();
    }

    [Fact]
    public void A_saved_collection_loads_back_the_same_and_is_written_with_camel_case_names()
    {
        var snapshot = new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            [
                new SnapshotItem(7, 11, "First Example", ItemKind.Base, 1999, new VersionDimensions(6.3, 8.27, 2.09), "Shelf A"),
                new SnapshotItem(8, 12, "Second Example", ItemKind.Expansion, null, null, null),
            ]);
        var store = CreateStore();

        store.Save(snapshot);

        store.Load().Should().BeEquivalentTo(snapshot);
        var text = File.ReadAllText(SnapshotPath);
        text.Should().Contain("\"schemaVersion\":2").And.Contain("\"kind\":\"expansion\"").And.Contain("\"collectionId\":7");
    }

    [Fact]
    public void A_schema_2_file_round_trips_its_picture_addresses_and_image_records()
    {
        var moment = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var snapshot = new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            moment,
            [new SnapshotItem(7, 11, "First Example", ItemKind.Base, null, null, null, "https://cf.example.org/v.jpg", "https://cf.example.org/m.jpg")],
            new Dictionary<string, ImageRecord>
            {
                ["https://cf.example.org/v.jpg"] = new("https://cf.example.org/v.jpg", ImageStatus.Ok, moment, [new ArtFile(480, 640, "0123456789abcdef-480.webp"), new ArtFile(240, 320, "fedcba9876543210-240.webp")]),
                ["https://cf.example.org/m.jpg"] = new("https://cf.example.org/m.jpg", ImageStatus.Undecodable, moment),
            });
        var store = CreateStore();

        store.Save(snapshot);

        var loaded = store.Load();
        loaded.Should().BeEquivalentTo(snapshot, options => options.WithStrictOrdering());
        File.ReadAllText(SnapshotPath).Should().Contain("\"images\"").And.Contain("\"status\":\"undecodable\"").And.Contain("\"versionImageUrl\"");
    }

    [Fact]
    public void A_schema_2_file_round_trips_every_field_of_the_game_details()
    {
        var moment = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var snapshot = new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            moment,
            [new SnapshotItem(7, 11, "First Example", ItemKind.Base, null, null, null)],
            Games: new Dictionary<int, GameDetails>
            {
                [11] = new(
                    moment,
                    2,
                    4,
                    60,
                    30,
                    90,
                    10,
                    2.4375,
                    7.123456,
                    6.5,
                    ["Invented Designer 1", "Invented Designer 2"],
                    ["Example Mechanic A"],
                    [new BaseGameRef(5, "Example Base")],
                    "https://cf.example.org/main.jpg"),
                [12] = new(moment, null, null, null, null, null, null, null, null, null, [], [], [], null),
            });
        var store = CreateStore();

        store.Save(snapshot);

        store.Load().Should().BeEquivalentTo(snapshot, options => options.WithStrictOrdering());
        File.ReadAllText(SnapshotPath).Should().Contain("\"games\"").And.Contain("\"expandsGames\"").And.Contain("\"bayesAverage\":6.5");
    }

    [Fact]
    public void A_file_without_games_loads_with_none()
    {
        File.WriteAllText(
            SnapshotPath,
            "{ \"schemaVersion\": 2, \"capturedAtUtc\": \"2026-01-01T00:00:00Z\", \"items\": [ { \"collectionId\": 5, \"gameId\": 9, \"title\": \"Example\", \"kind\": \"base\" } ] }");

        var loaded = CreateStore().Load();

        loaded.Should().NotBeNull();
        loaded!.Games.Should().BeNull();
        File.Exists(BadPath).Should().BeFalse();
    }

    [Fact]
    public void Game_details_without_their_lists_are_malformed_and_the_file_is_set_aside()
    {
        File.WriteAllText(
            SnapshotPath,
            "{ \"schemaVersion\": 2, \"capturedAtUtc\": \"2026-01-01T00:00:00Z\", \"items\": [], "
            + "\"games\": { \"9\": { \"enrichedAtUtc\": \"2026-01-01T00:00:00Z\" } } }");

        CreateStore().Load().Should().BeNull();

        File.Exists(BadPath).Should().BeTrue();
    }

    [Fact]
    public void A_schema_1_file_loads_with_its_items_and_no_images()
    {
        File.WriteAllText(
            SnapshotPath,
            "{ \"schemaVersion\": 1, \"capturedAtUtc\": \"2026-01-01T00:00:00Z\", \"items\": [ { \"collectionId\": 5, \"gameId\": 9, \"title\": \"Example\", \"kind\": \"base\" } ] }");

        var loaded = CreateStore().Load();

        loaded.Should().NotBeNull();
        loaded!.SchemaVersion.Should().Be(1);
        loaded.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new SnapshotItem(5, 9, "Example", ItemKind.Base, null, null, null));
        loaded.Images.Should().BeNull();
        File.Exists(BadPath).Should().BeFalse();
    }

    [Fact]
    public void An_image_record_without_its_status_is_malformed_and_the_file_is_set_aside()
    {
        File.WriteAllText(
            SnapshotPath,
            "{ \"schemaVersion\": 2, \"capturedAtUtc\": \"2026-01-01T00:00:00Z\", \"items\": [], "
            + "\"images\": { \"https://cf.example.org/a.jpg\": { \"sourceUrl\": \"https://cf.example.org/a.jpg\", \"attemptedAtUtc\": \"2026-01-01T00:00:00Z\" } } }");

        CreateStore().Load().Should().BeNull();

        File.Exists(SnapshotPath).Should().BeFalse();
        File.Exists(BadPath).Should().BeTrue();
    }

    [Fact]
    public void A_save_leaves_no_temporary_file_behind()
    {
        var store = CreateStore();

        store.Save(Snapshot());
        store.Save(Snapshot());

        Directory.EnumerateFileSystemEntries(_directory).Select(Path.GetFileName)
            .Should().BeEquivalentTo(["snapshot.json"]);
    }

    [Fact]
    public void Malformed_json_loads_as_no_snapshot_and_the_file_is_set_aside_with_one_log_line()
    {
        File.WriteAllText(SnapshotPath, "{ \"schemaVersion\": 1, \"items\": [");

        CreateStore().Load().Should().BeNull();

        File.Exists(SnapshotPath).Should().BeFalse();
        File.ReadAllText(BadPath).Should().Contain("schemaVersion");
        _logger.Messages.Should().ContainSingle().Which.Should().Contain("malformed").And.NotContain("schemaVersion");
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{ \"schemaVersion\": 1 }")]
    [InlineData("{ \"schemaVersion\": 0, \"capturedAtUtc\": \"2026-01-01T00:00:00Z\", \"items\": [] }")]
    [InlineData("{ \"schemaVersion\": 1, \"capturedAtUtc\": \"2026-01-01T00:00:00Z\", \"items\": [null] }")]
    public void An_empty_or_unusable_file_loads_as_no_snapshot_and_is_set_aside(string content)
    {
        File.WriteAllText(SnapshotPath, content);

        CreateStore().Load().Should().BeNull();

        File.Exists(SnapshotPath).Should().BeFalse();
        File.Exists(BadPath).Should().BeTrue();
    }

    [Fact]
    public void A_newer_schema_loads_as_no_snapshot_and_is_set_aside()
    {
        File.WriteAllText(SnapshotPath, "{ \"schemaVersion\": 3, \"capturedAtUtc\": \"2026-01-01T00:00:00Z\", \"items\": [] }");

        CreateStore().Load().Should().BeNull();

        File.Exists(SnapshotPath).Should().BeFalse();
        File.Exists(BadPath).Should().BeTrue();
        _logger.Messages.Should().ContainSingle().Which.Should().Contain("newer schema");
    }

    [Fact]
    public void A_file_that_cannot_be_read_is_left_in_place_and_loads_once_it_can_be_read()
    {
        var store = CreateStore();
        store.Save(Snapshot());

        using (new FileStream(SnapshotPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            store.Load().Should().BeNull();
        }

        File.Exists(BadPath).Should().BeFalse();
        _logger.Messages.Should().ContainSingle().Which.Should().Contain("unreadable").And.Contain("left in place");
        store.Load().Should().BeEquivalentTo(Snapshot());
    }

    [Fact]
    public void A_stored_file_that_cannot_be_read_is_reported_until_it_is_read_or_replaced()
    {
        var store = CreateStore();
        store.Save(Snapshot());
        store.HasUnreadStoredCollection.Should().BeFalse();

        var locked = new FileStream(SnapshotPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        store.Load().Should().BeNull();
        store.HasUnreadStoredCollection.Should().BeTrue();
        store.Load().Should().BeNull();
        store.HasUnreadStoredCollection.Should().BeTrue();

        locked.Dispose();
        store.Load().Should().NotBeNull();
        store.HasUnreadStoredCollection.Should().BeFalse();

        locked = new FileStream(SnapshotPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        store.Load().Should().BeNull();
        store.Save(Snapshot());
        locked.Dispose();
        store.HasUnreadStoredCollection.Should().BeFalse("the file was replaced with a readable one");
    }

    [Fact]
    public void A_missing_or_set_aside_file_is_not_reported_as_unread()
    {
        var store = CreateStore();
        store.Load().Should().BeNull();
        store.HasUnreadStoredCollection.Should().BeFalse();

        File.WriteAllText(SnapshotPath, "damaged");
        store.Load().Should().BeNull();

        store.HasUnreadStoredCollection.Should().BeFalse("a damaged file is moved aside, so nothing readable is at risk");
        File.Exists(SnapshotPath).Should().BeFalse();
    }

    [Fact]
    public void Setting_a_file_aside_replaces_an_older_bad_file()
    {
        File.WriteAllText(BadPath, "older damaged content");
        File.WriteAllText(SnapshotPath, "newer damaged content");

        CreateStore().Load().Should().BeNull();

        File.ReadAllText(BadPath).Should().Be("newer damaged content");
    }

    [Fact]
    public void Unknown_fields_are_ignored_and_missing_optional_fields_take_their_defaults()
    {
        File.WriteAllText(
            SnapshotPath,
            """
            {
              "schemaVersion": 1,
              "capturedAtUtc": "2026-01-01T00:00:00Z",
              "futureField": { "anything": true },
              "items": [ { "collectionId": 5, "gameId": 9, "title": "Example", "kind": "base", "extra": 1 } ]
            }
            """);

        var loaded = CreateStore().Load();

        loaded.Should().NotBeNull();
        loaded!.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new SnapshotItem(5, 9, "Example", ItemKind.Base, null, null, null));
        File.Exists(BadPath).Should().BeFalse();
    }

    [Theory]
    [InlineData("\"kind\": 1")]
    [InlineData("\"kind\": 0")]
    [InlineData("\"kind\": \"1\"")]
    [InlineData("\"kind\": \"Base\"")]
    [InlineData("\"kind\": \"base, expansion\"")]
    [InlineData("\"kind\": \"boardgame\"")]
    [InlineData("\"kind\": null")]
    public void An_enum_stored_as_a_number_or_an_unknown_name_is_malformed_and_set_aside(string kind)
    {
        var content = Items($"{{ \"collectionId\": 5, \"gameId\": 9, \"title\": \"Example\", {kind} }}");
        File.WriteAllText(SnapshotPath, content);

        CreateStore().Load().Should().BeNull();

        File.Exists(SnapshotPath).Should().BeFalse();
        File.ReadAllText(BadPath).Should().Be(content);
        _logger.Messages.Should().ContainSingle().Which.Should().Contain("malformed");
    }

    [Theory]
    [InlineData("\"collectionId\": 5, \"gameId\": 9, \"title\": null, \"kind\": \"base\"")]
    [InlineData("\"collectionId\": 5, \"gameId\": 9, \"kind\": \"base\"")]
    [InlineData("\"gameId\": 9, \"title\": \"Example\", \"kind\": \"base\"")]
    [InlineData("\"collectionId\": 5, \"title\": \"Example\", \"kind\": \"base\"")]
    [InlineData("\"collectionId\": 5, \"gameId\": 9, \"title\": \"Example\"")]
    [InlineData("\"collectionId\": \"five\", \"gameId\": 9, \"title\": \"Example\", \"kind\": \"base\"")]
    [InlineData("\"collectionId\": 5, \"gameId\": 9, \"title\": 7, \"kind\": \"base\"")]
    public void An_item_with_a_null_title_or_a_missing_required_field_makes_the_snapshot_malformed(string fields)
    {
        var content = Items($"{{ {fields} }}");
        File.WriteAllText(SnapshotPath, content);

        CreateStore().Load().Should().BeNull();

        File.Exists(SnapshotPath).Should().BeFalse();
        File.ReadAllText(BadPath).Should().Be(content);
        _logger.Messages.Should().ContainSingle().Which.Should().Contain("malformed");
    }

    [Fact]
    public void A_snapshot_without_its_captured_time_is_malformed_and_set_aside()
    {
        File.WriteAllText(SnapshotPath, "{ \"schemaVersion\": 1, \"items\": [] }");

        CreateStore().Load().Should().BeNull();

        File.Exists(SnapshotPath).Should().BeFalse();
        File.Exists(BadPath).Should().BeTrue();
    }

    [Fact]
    public void A_blank_title_the_source_gave_is_stored_and_loads_back()
    {
        var snapshot = new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            DateTimeOffset.UnixEpoch,
            [new SnapshotItem(1, 2, string.Empty, ItemKind.Expansion, null, null, null)]);
        var store = CreateStore();
        store.Save(snapshot);

        store.Load().Should().BeEquivalentTo(snapshot);
        File.Exists(BadPath).Should().BeFalse();
    }

    [Fact]
    public void A_file_with_odd_content_that_cannot_be_read_is_left_in_place_and_judged_once_it_can()
    {
        var content = Items("{ \"collectionId\": 5, \"gameId\": 9, \"title\": \"Example\", \"kind\": 1 }");
        File.WriteAllText(SnapshotPath, content);
        var store = CreateStore();

        using (new FileStream(SnapshotPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            store.Load().Should().BeNull();
            store.HasUnreadStoredCollection.Should().BeTrue();
        }

        File.ReadAllText(SnapshotPath).Should().Be(content);
        File.Exists(BadPath).Should().BeFalse();
        _logger.Messages.Should().ContainSingle().Which.Should().Contain("unreadable");

        store.Load().Should().BeNull();

        File.Exists(SnapshotPath).Should().BeFalse();
        File.ReadAllText(BadPath).Should().Be(content);
        store.HasUnreadStoredCollection.Should().BeFalse("a damaged file is moved aside, so nothing readable is at risk");
    }

    [Fact]
    public void A_snapshot_with_every_field_set_written_by_the_current_code_loads_unchanged()
    {
        var snapshot = new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            new DateTimeOffset(2030, 5, 6, 7, 8, 9, TimeSpan.Zero),
            [
                new SnapshotItem(1, 2, "Full", ItemKind.Base, 2001, new VersionDimensions(1.5, 2.5, 3.5), "Shelf A"),
                new SnapshotItem(3, 4, "Plain", ItemKind.Expansion, null, null, null),
            ]);
        CreateStore().Save(snapshot);

        CreateStore().Load().Should().BeEquivalentTo(snapshot, options => options.WithStrictOrdering());
        File.Exists(BadPath).Should().BeFalse();
        _logger.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Stray_temporary_files_are_removed_and_other_files_stay()
    {
        File.WriteAllText(Path.Combine(_directory, ".snapshot.json.0123456789abcdef.tmp"), "partial");
        File.WriteAllText(Path.Combine(_directory, ".other.json.fedcba9876543210.tmp"), "partial");
        File.WriteAllText(SnapshotPath, JsonSerializer.Serialize(new { schemaVersion = 1 }));
        File.WriteAllText(Path.Combine(_directory, "notes.tmp"), "kept");

        AtomicJsonFile.RemoveStrayTemporaryFiles(_directory);

        Directory.EnumerateFileSystemEntries(_directory).Select(Path.GetFileName)
            .Should().BeEquivalentTo(["snapshot.json", "notes.tmp"]);
    }

    private static string Items(string item) =>
        $"{{ \"schemaVersion\": 1, \"capturedAtUtc\": \"2026-01-01T00:00:00Z\", \"items\": [ {item} ] }}";

    private static CollectionSnapshot Snapshot() =>
        new(CollectionSnapshot.CurrentSchemaVersion, DateTimeOffset.UnixEpoch, [new SnapshotItem(1, 2, "Example", ItemKind.Base, null, null, null)]);

    private SnapshotStore CreateStore() => new(_directory, _logger);

    private sealed class CapturingLogger : ILogger<SnapshotStore>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            exception.Should().BeNull("only a category is logged, never an exception with the file's content");
        }
    }
}

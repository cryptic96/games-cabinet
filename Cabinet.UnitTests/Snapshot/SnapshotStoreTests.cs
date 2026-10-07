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
        text.Should().Contain("\"schemaVersion\":1").And.Contain("\"kind\":\"expansion\"").And.Contain("\"collectionId\":7");
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
        File.WriteAllText(SnapshotPath, "{ \"schemaVersion\": 2, \"capturedAtUtc\": \"2026-01-01T00:00:00Z\", \"items\": [] }");

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

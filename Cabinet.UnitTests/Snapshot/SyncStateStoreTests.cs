using Cabinet.Domain.Collection;
using Cabinet.Repository.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Cabinet.UnitTests.Snapshot;

/// <summary>Proves the sync bookkeeping round-trips and that a damaged or too-new file is set aside instead of being trusted.</summary>
[Trait("Category", "Snapshot")]
public sealed class SyncStateStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"cabinet-sync-state-tests-{Guid.NewGuid():N}");
    private readonly CapturingLogger _logger = new();

    public SyncStateStoreTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string StatePath => Path.Combine(_directory, "sync-state.json");

    private string BadPath => StatePath + ".bad";

    [Fact]
    public void A_missing_file_loads_as_the_initial_state_and_sets_nothing_aside()
    {
        CreateStore().Load().Should().Be(SyncState.Initial);
        File.Exists(BadPath).Should().BeFalse();
        _logger.Messages.Should().BeEmpty();
    }

    [Fact]
    public void A_saved_state_loads_back_the_same_and_is_written_with_camel_case_names()
    {
        var detected = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var state = new SyncState(
            SyncState.CurrentSchemaVersion,
            detected,
            detected.AddMinutes(1),
            detected.AddMinutes(-60),
            SyncResult.HeldBack,
            SyncFailure.Throttled,
            2,
            detected.AddMinutes(10),
            new HeldBackRecord(HeldBackKind.Shrunk, "abc123", 4, detected));
        var store = CreateStore();

        store.Save(state);

        store.Load().Should().BeEquivalentTo(state);
        File.ReadAllText(StatePath).Should()
            .Contain("\"lastResult\":\"heldBack\"")
            .And.Contain("\"lastFailure\":\"throttled\"")
            .And.Contain("\"kind\":\"shrunk\"");
    }

    [Fact]
    public void A_save_leaves_no_temporary_file_behind()
    {
        var store = CreateStore();

        store.Save(SyncState.Initial);
        store.Save(SyncState.Initial with { ConsecutiveFailures = 1 });

        Directory.EnumerateFileSystemEntries(_directory).Select(Path.GetFileName)
            .Should().BeEquivalentTo(["sync-state.json"]);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{\"schemaVersion\":1,\"lastResult\":\"exploded\"}")]
    [InlineData("{\"schemaVersion\":0}")]
    [InlineData("{\"schemaVersion\":1,\"consecutiveFailures\":-4}")]
    public void A_malformed_file_loads_as_initial_and_is_set_aside_with_one_logged_category(string content)
    {
        File.WriteAllText(StatePath, content);

        CreateStore().Load().Should().Be(SyncState.Initial);

        File.Exists(StatePath).Should().BeFalse();
        File.ReadAllText(BadPath).Should().Be(content);
        _logger.Messages.Should().ContainSingle().Which.Should().Contain("malformed");
    }

    [Fact]
    public void A_newer_schema_loads_as_initial_and_is_set_aside()
    {
        File.WriteAllText(StatePath, $"{{\"schemaVersion\":{SyncState.CurrentSchemaVersion + 1},\"consecutiveFailures\":3}}");

        CreateStore().Load().Should().Be(SyncState.Initial);

        File.Exists(StatePath).Should().BeFalse();
        File.Exists(BadPath).Should().BeTrue();
        _logger.Messages.Should().ContainSingle().Which.Should().Contain("newer schema");
    }

    [Fact]
    public void A_file_that_cannot_be_read_is_left_in_place_and_loads_once_it_can_be_read()
    {
        var state = SyncState.Initial with { ConsecutiveFailures = 2 };
        var store = CreateStore();
        store.Save(state);

        using (new FileStream(StatePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            store.Load().Should().Be(SyncState.Initial);
        }

        File.Exists(BadPath).Should().BeFalse();
        _logger.Messages.Should().ContainSingle().Which.Should().Contain("unreadable").And.Contain("left in place");
        store.Load().Should().Be(state);
    }

    private SyncStateStore CreateStore() => new(_directory, _logger);

    private sealed class CapturingLogger : ILogger<SyncStateStore>
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

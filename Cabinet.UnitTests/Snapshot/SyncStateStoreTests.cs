using Cabinet.Domain.Collection;
using Cabinet.Repository.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Snapshot;

/// <summary>Proves the sync bookkeeping round-trips and that a damaged or too-new file is set aside instead of being trusted.</summary>
[Trait("Category", "Snapshot")]
public sealed class SyncStateStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"cabinet-sync-state-tests-{Guid.NewGuid():N}");
    private static readonly DateTimeOffset Now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly TimeSpan ManualCooldown = TimeSpan.FromMinutes(10);
    private static readonly string Fingerprint = ShrinkGuard.Fingerprint([1, 2, 3]);

    private readonly CapturingLogger _logger = new();
    private readonly FakeTimeProvider _clock = new(Now);

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
        var detected = Now;
        var state = new SyncState(
            SyncState.CurrentSchemaVersion,
            detected,
            detected.AddMinutes(1),
            detected.AddMinutes(-60),
            SyncResult.HeldBack,
            SyncFailure.Throttled,
            2,
            detected.AddMinutes(5),
            new HeldBackRecord(HeldBackKind.Shrunk, Fingerprint, 4, detected));
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

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"lastResult\":2}")]
    [InlineData("{\"schemaVersion\":1,\"lastResult\":\"2\"}")]
    [InlineData("{\"schemaVersion\":1,\"lastResult\":\"Changed\"}")]
    [InlineData("{\"schemaVersion\":1,\"lastResult\":\"changed, failed\"}")]
    [InlineData("{\"schemaVersion\":1,\"lastFailure\":1}")]
    [InlineData("{\"schemaVersion\":1,\"lastFailure\":\"somethingNew\"}")]
    [InlineData("{\"schemaVersion\":1,\"heldBack\":{\"kind\":0,\"fingerprint\":\"x\",\"count\":1,\"detectedUtc\":\"2030-01-02T03:04:05Z\"}}")]
    public void An_enum_stored_as_a_number_or_an_unknown_name_is_malformed_and_set_aside(string content)
    {
        File.WriteAllText(StatePath, content);

        CreateStore().Load().Should().Be(SyncState.Initial);

        File.Exists(StatePath).Should().BeFalse();
        File.ReadAllText(BadPath).Should().Be(content);
        _logger.Messages.Should().ContainSingle().Which.Should().Contain("malformed");
    }

    [Fact]
    public void A_cooldown_beyond_the_manual_cooldown_from_now_is_clamped_with_one_log_line_and_no_values()
    {
        var store = CreateStore();
        store.Save(SyncState.Initial with { CooldownEndsUtc = Now.AddYears(40), ConsecutiveFailures = 2 });

        var loaded = store.Load();

        loaded.CooldownEndsUtc.Should().Be(Now + ManualCooldown);
        loaded.ConsecutiveFailures.Should().Be(2);
        File.Exists(BadPath).Should().BeFalse();
        _logger.Messages.Should().ContainSingle().Which.Should().NotContain("2070").And.NotContain("40");
    }

    [Theory]
    [InlineData(-30)]
    [InlineData(0)]
    [InlineData(300)]
    [InlineData(600)]
    public void A_cooldown_within_the_manual_cooldown_from_now_is_kept_as_stored(int secondsFromNow)
    {
        var ends = Now.AddSeconds(secondsFromNow);
        var store = CreateStore();
        store.Save(SyncState.Initial with { CooldownEndsUtc = ends });

        store.Load().CooldownEndsUtc.Should().Be(ends);
        _logger.Messages.Should().BeEmpty();
    }

    [Fact]
    public void A_cooldown_is_checked_against_the_clock_at_the_time_of_loading()
    {
        var store = CreateStore();
        store.Save(SyncState.Initial with { CooldownEndsUtc = Now.AddMinutes(30) });

        _clock.Advance(TimeSpan.FromMinutes(25));

        store.Load().CooldownEndsUtc.Should().Be(Now.AddMinutes(30));
        _logger.Messages.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc123")]
    [InlineData("0123456789abcdef")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0")]
    public void A_held_back_record_with_a_malformed_fingerprint_is_dropped_with_one_log_line_and_the_rest_is_kept(string fingerprint)
    {
        var store = CreateStore();
        store.Save(SyncState.Initial with
        {
            ConsecutiveFailures = 3,
            HeldBack = new HeldBackRecord(HeldBackKind.Shrunk, fingerprint, 4, Now),
        });

        var loaded = store.Load();

        loaded.HeldBack.Should().BeNull();
        loaded.ConsecutiveFailures.Should().Be(3);
        File.Exists(BadPath).Should().BeFalse();
        _logger.Messages.Should().ContainSingle().Which.Should().NotMatchRegex("[0-9a-f]{16}");
    }

    [Fact]
    public void A_held_back_record_with_a_well_formed_fingerprint_is_kept()
    {
        var heldBack = new HeldBackRecord(HeldBackKind.Unverified, Fingerprint, 4, Now);
        var store = CreateStore();
        store.Save(SyncState.Initial with { HeldBack = heldBack });

        store.Load().HeldBack.Should().Be(heldBack);
        _logger.Messages.Should().BeEmpty();
    }

    [Fact]
    public void A_state_with_every_field_set_written_by_the_current_code_loads_unchanged()
    {
        var state = new SyncState(
            SyncState.CurrentSchemaVersion,
            Now.AddMinutes(-3),
            Now.AddMinutes(-2),
            Now.AddHours(-1),
            SyncResult.Failed,
            SyncFailure.Queued,
            5,
            Now.AddMinutes(7),
            new HeldBackRecord(HeldBackKind.Empty, ShrinkGuard.Fingerprint([]), 0, Now.AddMinutes(-3)));
        CreateStore().Save(state);

        CreateStore().Load().Should().Be(state);
        File.Exists(BadPath).Should().BeFalse();
        _logger.Messages.Should().BeEmpty();
    }

    [Fact]
    public void A_file_with_odd_content_that_cannot_be_read_is_left_in_place_and_judged_once_it_can()
    {
        var content = "{\"schemaVersion\":1,\"lastResult\":2}";
        File.WriteAllText(StatePath, content);
        var store = CreateStore();

        using (new FileStream(StatePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            store.Load().Should().Be(SyncState.Initial);
        }

        File.ReadAllText(StatePath).Should().Be(content);
        File.Exists(BadPath).Should().BeFalse();
        _logger.Messages.Should().ContainSingle().Which.Should().Contain("unreadable");

        store.Load().Should().Be(SyncState.Initial);

        File.Exists(StatePath).Should().BeFalse();
        File.ReadAllText(BadPath).Should().Be(content);
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

    private SyncStateStore CreateStore() => new(_directory, _clock, ManualCooldown, _logger);

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

using System.Text;
using Cabinet.Domain.Owner;
using Cabinet.Repository.Storage;
using Cabinet.Service.Owner;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Owner;

/// <summary>
/// Proves the single owner-data writer: a repeat is no change, a stale writer is told so, the latest change can be taken
/// back once, nothing is written while the file is unreadable, and the log is capped while the count keeps counting.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerDataServiceTests : IDisposable
{
    private const string CabinetA = "Cabinet A";
    private const string ShelfB = "Shelf B";

    private readonly TemporaryDirectory _directory = new();

    private string FilePath => Path.Combine(_directory.FullPath, OwnerDataStore.FileName);

    public void Dispose() => _directory.Dispose();

    [Fact]
    public async Task Assigning_the_location_a_game_already_has_changes_nothing_at_all()
    {
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Collection());
        await harness.AddAsync(CabinetA);
        await harness.PlaceAsync(100, CabinetA);
        var revision = harness.Service.Revision;
        var changes = harness.Data.Changes.Count;
        var counted = harness.Data.ChangesSinceDownload;
        var version = harness.Version;
        var saves = harness.Store.Saved.Count;

        var again = await harness.PlaceAsync(100, CabinetA);

        again.Outcome.Should().Be(OwnerOutcome.NoChange);
        again.Data!.Revision.Should().Be(revision);
        harness.Service.Revision.Should().Be(revision);
        harness.Data.Changes.Should().HaveCount(changes);
        harness.Data.ChangesSinceDownload.Should().Be(counted);
        harness.Version.Should().Be(version);
        harness.Store.Saved.Should().HaveCount(saves);
    }

    [Fact]
    public async Task A_change_carries_a_log_entry_a_count_and_a_new_collection_version()
    {
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Collection());
        await harness.AddAsync(CabinetA);
        var before = harness.Version;

        var placed = await harness.PlaceAsync(100, CabinetA);

        placed.Outcome.Should().Be(OwnerOutcome.Saved);
        placed.Data!.Changes[0].Text.Should().Be("Moved Game 100 to Cabinet A");
        placed.Data.ChangesSinceDownload.Should().Be(2);
        harness.Version.Should().NotBe(before);
    }

    [Fact]
    public async Task A_write_from_an_older_revision_is_answered_out_of_date_and_writes_nothing()
    {
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Collection());
        var first = await harness.AddAsync(CabinetA);
        await harness.AddAsync(ShelfB);
        var saves = harness.Store.Saved.Count;
        var version = harness.Version;

        var stale = await harness.Service.MutateAsync(
            first.Data!.Revision,
            data => LocationEdits.Add(data, "Crate 7", OwnerDataService.NewId),
            CancellationToken.None);

        stale.Outcome.Should().Be(OwnerOutcome.OutOfDate);
        stale.Data!.Locations.Select(location => location.Name).Should().Equal(CabinetA, ShelfB);
        harness.Store.Saved.Should().HaveCount(saves);
        harness.Version.Should().Be(version);
    }

    [Fact]
    public async Task Undo_right_after_a_change_restores_the_previous_data_under_a_higher_revision()
    {
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Collection());
        await harness.AddAsync(CabinetA);
        var before = harness.Data;
        var versionBefore = harness.Version;
        var placed = await harness.PlaceAsync(100, CabinetA);
        harness.Clock.Advance(TimeSpan.FromMinutes(3));

        var undone = await harness.Service.UndoAsync(placed.Data!.Revision, CancellationToken.None);

        undone.Outcome.Should().Be(OwnerOutcome.Saved);
        undone.StatusKey.Should().Be("undone");
        undone.CanUndo.Should().BeFalse();
        undone.Data!.Revision.Should().Be(placed.Data.Revision + 1);
        undone.Data.SavedAtUtc.Should().Be(harness.Clock.GetUtcNow());
        undone.Data.Locations.Should().BeEquivalentTo(before.Locations);
        undone.Data.Games.Should().BeEquivalentTo(before.Games);
        undone.Data.Changes.Should().BeEquivalentTo(before.Changes);
        undone.Data.ChangesSinceDownload.Should().Be(before.ChangesSinceDownload).And.Be(placed.Data.ChangesSinceDownload - 1);
        harness.Version.Should().Be(versionBefore);
        harness.Service.UndoableRevision.Should().BeNull();
    }

    [Fact]
    public async Task Undo_after_another_write_answers_out_of_date_and_changes_nothing()
    {
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Collection());
        await harness.AddAsync(CabinetA);
        var placed = await harness.PlaceAsync(100, CabinetA);
        await harness.AddAsync(ShelfB);
        var saves = harness.Store.Saved.Count;
        var revision = harness.Service.Revision;

        var stale = await harness.Service.UndoAsync(placed.Data!.Revision, CancellationToken.None);

        stale.Outcome.Should().Be(OwnerOutcome.OutOfDate);
        stale.StatusKey.Should().Be("cant-undo");
        harness.Store.Saved.Should().HaveCount(saves);
        harness.Service.Revision.Should().Be(revision);
        harness.Data.Locations.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_second_undo_answers_out_of_date_and_changes_nothing()
    {
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Collection());
        await harness.AddAsync(CabinetA);
        var placed = await harness.PlaceAsync(100, CabinetA);
        var undone = await harness.Service.UndoAsync(placed.Data!.Revision, CancellationToken.None);
        var saves = harness.Store.Saved.Count;

        var again = await harness.Service.UndoAsync(undone.Data!.Revision, CancellationToken.None);
        var repeated = await harness.Service.UndoAsync(placed.Data.Revision, CancellationToken.None);

        again.Outcome.Should().Be(OwnerOutcome.OutOfDate);
        repeated.Outcome.Should().Be(OwnerOutcome.OutOfDate);
        harness.Store.Saved.Should().HaveCount(saves);
        harness.Service.Revision.Should().Be(undone.Data.Revision);
    }

    [Fact]
    public async Task Undo_with_nothing_written_yet_answers_out_of_date()
    {
        var harness = OwnerTestKit.Harness();

        var result = await harness.Service.UndoAsync(0, CancellationToken.None);

        result.Outcome.Should().Be(OwnerOutcome.OutOfDate);
        harness.Store.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task A_change_that_made_no_difference_keeps_the_earlier_change_undoable()
    {
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Collection());
        await harness.AddAsync(CabinetA);
        var placed = await harness.PlaceAsync(100, CabinetA);
        await harness.PlaceAsync(100, CabinetA);

        var undone = await harness.Service.UndoAsync(placed.Data!.Revision, CancellationToken.None);

        undone.Outcome.Should().Be(OwnerOutcome.Saved);
    }

    [Fact]
    public async Task Every_write_is_refused_while_the_file_is_unreadable_and_nothing_is_saved()
    {
        var store = new InMemoryOwnerDataStore { State = new OwnerDataState.Unreadable("malformed") };
        var harness = OwnerTestKit.Harness(store, OwnerTestKit.Collection());

        var added = await harness.AddAsync(CabinetA);
        var undone = await harness.Service.UndoAsync(0, CancellationToken.None);

        added.Outcome.Should().Be(OwnerOutcome.Unreadable);
        added.Data.Should().BeNull();
        undone.Outcome.Should().Be(OwnerOutcome.Unreadable);
        store.Saved.Should().BeEmpty();
        harness.Service.UndoableRevision.Should().BeNull();
    }

    [Fact]
    public async Task The_log_keeps_two_hundred_entries_while_the_change_count_goes_on_counting()
    {
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Collection());
        await harness.AddAsync(CabinetA);
        await harness.AddAsync(ShelfB);

        for (var index = 0; index < 203; index++)
        {
            await harness.PlaceAsync(100, index % 2 == 0 ? CabinetA : ShelfB);
        }

        harness.Data.Changes.Should().HaveCount(OwnerData.MaxStoredChanges);
        harness.Data.Changes[0].Text.Should().Be("Moved Game 100 to Cabinet A");
        harness.Data.ChangesSinceDownload.Should().Be(205);
        harness.Data.Revision.Should().Be(205);
    }

    [Fact]
    public async Task A_refused_change_writes_nothing_and_keeps_the_latest_change_undoable()
    {
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Collection());
        var added = await harness.AddAsync(CabinetA);
        var saves = harness.Store.Saved.Count;

        var refused = await harness.AddAsync("cabinet a");

        refused.Outcome.Should().Be(OwnerOutcome.Refused);
        refused.StatusKey.Should().Be("name-taken");
        refused.CanUndo.Should().BeTrue();
        harness.Store.Saved.Should().HaveCount(saves);
        harness.Service.UndoableRevision.Should().Be(added.Data!.Revision);
    }

    [Fact]
    public async Task A_save_that_fails_is_refused_and_leaves_the_data_as_it_was()
    {
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Collection());
        var before = harness.Data;
        harness.Store.FailNextSave = true;

        var failed = await harness.AddAsync(CabinetA);

        failed.Outcome.Should().Be(OwnerOutcome.Refused);
        failed.StatusKey.Should().Be("save-failed");
        harness.Data.Should().BeSameAs(before);
    }

    [Fact]
    public async Task The_first_write_keeps_an_earlier_version_and_a_write_half_an_hour_later_does_not()
    {
        var store = new InMemoryOwnerDataStore { FileExists = true };
        var harness = OwnerTestKit.Harness(store);

        await harness.AddAsync(CabinetA);
        harness.Clock.Advance(TimeSpan.FromMinutes(30));
        await harness.AddAsync(ShelfB);

        store.KeptAt.Should().Equal(OwnerTestKit.Start);
    }

    [Fact]
    public async Task A_write_more_than_an_hour_after_the_last_copy_keeps_another_version()
    {
        var store = new InMemoryOwnerDataStore { FileExists = true };
        var harness = OwnerTestKit.Harness(store);
        await harness.AddAsync(CabinetA);
        harness.Clock.Advance(TimeSpan.FromMinutes(30));
        await harness.AddAsync(ShelfB);

        harness.Clock.Advance(TimeSpan.FromMinutes(31));
        await harness.AddAsync("Shelf C");

        store.KeptAt.Should().Equal(OwnerTestKit.Start, OwnerTestKit.Start.AddMinutes(61));
    }

    [Fact]
    public async Task A_write_that_changed_nothing_and_a_refused_write_keep_no_version()
    {
        var store = new InMemoryOwnerDataStore { FileExists = true };
        var harness = OwnerTestKit.Harness(store);
        await harness.AddAsync(CabinetA);
        harness.Clock.Advance(TimeSpan.FromHours(3));

        await harness.AddAsync(CabinetA);
        await harness.Service.MutateAsync(0, data => LocationEdits.Add(data, ShelfB, OwnerDataService.NewId), CancellationToken.None);

        store.KeptAt.Should().Equal(OwnerTestKit.Start);
    }

    [Fact]
    public async Task An_undo_also_keeps_an_earlier_version_when_one_is_due()
    {
        var store = new InMemoryOwnerDataStore { FileExists = true };
        var harness = OwnerTestKit.Harness(store);
        var added = await harness.AddAsync(CabinetA);
        harness.Clock.Advance(TimeSpan.FromHours(2));

        await harness.Service.UndoAsync(added.Data!.Revision, CancellationToken.None);

        store.KeptAt.Should().Equal(OwnerTestKit.Start, OwnerTestKit.Start.AddHours(2));
    }

    [Fact]
    public async Task The_first_save_after_an_upgrade_keeps_the_old_format_file_byte_for_byte_even_inside_the_hour()
    {
        var oldFile = FirstFormatFile();
        var store = CreateStore();
        store.KeepEarlierVersion(OwnerTestKit.Start.AddMinutes(-10));
        var keptBefore = store.ReadEarlierVersion(0);
        var service = CreateService(store, out _);

        var result = await service.MutateAsync(service.Revision, data => LocationEdits.Add(data, CabinetA, OwnerDataService.NewId), CancellationToken.None);

        result.Outcome.Should().Be(OwnerOutcome.Saved);
        store.EarlierVersions().Should().HaveCount(2);
        store.ReadEarlierVersion(0).Should().Equal(oldFile);
        store.ReadEarlierVersion(1).Should().Equal(keptBefore);
        store.StoredSchemaVersion().Should().Be(OwnerData.CurrentSchemaVersion);
    }

    [Fact]
    public async Task A_second_save_five_minutes_after_the_upgrade_keeps_no_further_copy()
    {
        FirstFormatFile();
        var store = CreateStore();
        store.KeepEarlierVersion(OwnerTestKit.Start.AddMinutes(-10));
        var service = CreateService(store, out var clock);
        await service.MutateAsync(service.Revision, data => LocationEdits.Add(data, CabinetA, OwnerDataService.NewId), CancellationToken.None);

        clock.Advance(TimeSpan.FromMinutes(5));
        await service.MutateAsync(service.Revision, data => LocationEdits.Add(data, ShelfB, OwnerDataService.NewId), CancellationToken.None);

        store.EarlierVersions().Should().HaveCount(2);
    }

    [Fact]
    public async Task The_first_save_after_an_upgrade_made_by_the_sync_keeps_the_old_format_file()
    {
        var oldFile = FirstFormatFile(gameId: 100);
        var store = CreateStore();
        store.KeepEarlierVersion(OwnerTestKit.Start.AddMinutes(-10));
        var service = CreateService(store, out _);

        await service.ReconcileAsync(new HashSet<int>(), CancellationToken.None);

        store.EarlierVersions().Should().HaveCount(2);
        store.ReadEarlierVersion(0).Should().Equal(oldFile);
        store.StoredSchemaVersion().Should().Be(OwnerData.CurrentSchemaVersion);
    }

    [Fact]
    public async Task An_undo_that_finds_an_old_format_file_on_disk_keeps_it_first()
    {
        var store = CreateStore();
        var service = CreateService(store, out _);
        var added = await service.MutateAsync(service.Revision, data => LocationEdits.Add(data, CabinetA, OwnerDataService.NewId), CancellationToken.None);
        store.KeepEarlierVersion(OwnerTestKit.Start.AddMinutes(-1));
        var oldFile = FirstFormatFile();

        await service.UndoAsync(added.Data!.Revision, CancellationToken.None);

        store.ReadEarlierVersion(0).Should().Equal(oldFile);
        store.EarlierVersions().Should().HaveCount(2);
    }

    [Fact]
    public async Task A_save_over_a_file_of_the_current_format_follows_the_hourly_rule()
    {
        var store = new InMemoryOwnerDataStore { FileExists = true };
        var harness = OwnerTestKit.Harness(store);
        await harness.AddAsync(CabinetA);
        store.StoredSchemaVersion().Should().Be(OwnerData.CurrentSchemaVersion);

        harness.Clock.Advance(TimeSpan.FromMinutes(10));
        await harness.AddAsync(ShelfB);

        store.KeptAt.Should().Equal(OwnerTestKit.Start);
    }

    [Fact]
    public async Task A_save_over_an_old_format_file_in_the_in_memory_store_keeps_a_copy_inside_the_hour()
    {
        var store = new InMemoryOwnerDataStore { FileExists = true };
        var harness = OwnerTestKit.Harness(store);
        await harness.AddAsync(CabinetA);
        harness.Clock.Advance(TimeSpan.FromMinutes(10));
        store.SeededSchemaVersion = 1;

        await harness.AddAsync(ShelfB);

        store.KeptAt.Should().Equal(OwnerTestKit.Start, OwnerTestKit.Start.AddMinutes(10));
    }

    private byte[] FirstFormatFile(int? gameId = null)
    {
        var games = gameId is { } id ? $"{{\"gameId\":{id},\"locationId\":null}}" : string.Empty;
        var bytes = Encoding.UTF8.GetBytes(
            "{\"kind\":\"games-cabinet-owner-data\",\"schemaVersion\":1,\"revision\":3,\"finish\":\"classic\","
            + $"\"locations\":[],\"games\":[{games}],\"changesSinceDownload\":0,\"changes\":[]}}");
        File.WriteAllBytes(FilePath, bytes);

        return bytes;
    }

    private OwnerDataStore CreateStore() => new(_directory.FullPath, NullLogger<OwnerDataStore>.Instance);

    private static OwnerDataService CreateService(OwnerDataStore store, out FakeTimeProvider clock)
    {
        clock = new FakeTimeProvider(OwnerTestKit.Start);

        return new OwnerDataService(store, clock, NullLogger<OwnerDataService>.Instance);
    }
}

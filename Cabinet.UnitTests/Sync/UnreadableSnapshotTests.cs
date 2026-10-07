using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Repository.Storage;
using Cabinet.Service.Collection;
using Cabinet.Service.Sync;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Sync;

/// <summary>
/// Proves a stored collection that exists but cannot be read is never replaced by the first answer that comes along: an empty
/// answer is never accepted, any other answer waits for a second identical one, and once the file can be read again the
/// ordinary rules apply.
/// </summary>
[Trait("Category", "Sync")]
public sealed class UnreadableSnapshotTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2030, 1, 15, 12, 0, 0, TimeSpan.Zero));

    public void Dispose() => _directory.Dispose();

    private string SnapshotPath => Path.Combine(_directory.FullPath, SnapshotStore.FileName);

    [Fact]
    public async Task An_empty_answer_never_replaces_a_stored_file_that_cannot_be_read()
    {
        var original = StoreCollection(Ids(1, 10));
        using var locked = Lock();
        var world = StartUp();

        var first = await world.Run(Ids(), null);
        var second = await world.Run(Ids(), first.HeldBack);

        first.Result.Should().Be(SyncResult.HeldBack);
        first.HeldBack!.Kind.Should().Be(HeldBackKind.Empty);
        second.Result.Should().Be(SyncResult.HeldBack);
        second.HeldBack!.Kind.Should().Be(HeldBackKind.Empty);
        locked.Dispose();
        File.ReadAllBytes(SnapshotPath).Should().Equal(original);
        world.Collection.Current.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task A_first_answer_waits_for_an_identical_second_one_before_it_replaces_the_stored_file()
    {
        var original = StoreCollection(Ids(1, 10));
        using var locked = Lock();
        var world = StartUp();

        var first = await world.Run(Ids(100, 3), null);

        first.Result.Should().Be(SyncResult.HeldBack);
        first.HeldBack!.Kind.Should().Be(HeldBackKind.Unverified);
        first.HeldBack.Count.Should().Be(3);
        world.Collection.Current.Items.Should().BeEmpty();
        locked.Dispose();
        File.ReadAllBytes(SnapshotPath).Should().Equal(original);
    }

    [Fact]
    public async Task An_identical_second_answer_replaces_the_stored_file_and_ordinary_rules_apply_from_then_on()
    {
        StoreCollection(Ids(1, 10));
        var locked = Lock();
        var world = StartUp();
        var first = await world.Run(Ids(100, 3), null);

        var second = await world.Run(Ids(100, 3), first.HeldBack);
        locked.Dispose();

        second.Result.Should().Be(SyncResult.Changed);
        world.Collection.Current.Items.Should().HaveCount(3);
        world.Snapshots.HasUnreadStoredCollection.Should().BeFalse();
        new SnapshotStore(_directory.FullPath, NullLogger<SnapshotStore>.Instance).Load()!.Items.Should().HaveCount(3);
        var shrunk = await world.Run(Ids(100, 1), null);
        shrunk.Result.Should().Be(SyncResult.HeldBack);
        shrunk.HeldBack!.Kind.Should().Be(HeldBackKind.Shrunk);
    }

    [Fact]
    public async Task A_different_answer_does_not_confirm_the_held_back_one()
    {
        var original = StoreCollection(Ids(1, 10));
        using var locked = Lock();
        var world = StartUp();
        var first = await world.Run(Ids(100, 3), null);

        var second = await world.Run(Ids(200, 3), first.HeldBack);

        second.Result.Should().Be(SyncResult.HeldBack);
        second.HeldBack!.Fingerprint.Should().NotBe(first.HeldBack!.Fingerprint);
        locked.Dispose();
        File.ReadAllBytes(SnapshotPath).Should().Equal(original);
    }

    [Fact]
    public async Task The_confirmation_survives_a_restart_while_the_file_is_still_unreadable()
    {
        StoreCollection(Ids(1, 10));
        using var locked = Lock();
        var beforeRestart = StartUp();
        var first = await beforeRestart.Run(Ids(100, 3), null);
        var afterRestart = StartUp();

        var second = await afterRestart.Run(Ids(100, 3), first.HeldBack);

        second.Result.Should().Be(SyncResult.Changed);
        afterRestart.Collection.Current.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task A_file_that_can_be_read_again_becomes_the_baseline_and_a_shrunken_answer_is_held_back_as_usual()
    {
        StoreCollection(Ids(1, 10));
        var locked = Lock();
        var world = StartUp();
        (await world.Run(Ids(1, 10), null)).Result.Should().Be(SyncResult.HeldBack, "nothing confirms the first answer yet");
        world.Collection.Current.Items.Should().BeEmpty();
        locked.Dispose();

        var shrunk = await world.Run(Ids(1, 2), null);

        shrunk.Result.Should().Be(SyncResult.HeldBack);
        shrunk.HeldBack!.Kind.Should().Be(HeldBackKind.Shrunk);
        world.Collection.Current.Items.Should().HaveCount(10, "the stored collection is shown again once it can be read");
    }

    [Fact]
    public async Task A_file_that_can_be_read_again_lets_the_same_collection_count_as_unchanged()
    {
        StoreCollection(Ids(1, 10));
        var locked = Lock();
        var world = StartUp();
        await world.Run(Ids(1, 10), null);
        locked.Dispose();

        var again = await world.Run(Ids(1, 10), null);

        again.Result.Should().Be(SyncResult.Unchanged);
    }

    [Fact]
    public async Task A_failed_fetch_leaves_an_unreadable_file_alone()
    {
        var original = StoreCollection(Ids(1, 10));
        using var locked = Lock();
        var world = StartUp();

        var result = await world.Run(new CollectionFetchResult.Failed(SyncFailure.Unavailable), null);
        locked.Dispose();

        result.Result.Should().Be(SyncResult.Failed);
        File.ReadAllBytes(SnapshotPath).Should().Equal(original);
    }

    [Fact]
    public async Task With_no_stored_file_the_first_answer_is_accepted_at_once()
    {
        var world = StartUp();

        var first = await world.Run(Ids(1, 10), null);

        first.Result.Should().Be(SyncResult.Changed);
        File.Exists(SnapshotPath).Should().BeTrue();
    }

    [Fact]
    public async Task A_stored_file_that_is_damaged_and_set_aside_does_not_hold_the_next_answer_back()
    {
        File.WriteAllText(SnapshotPath, "not json");
        var world = StartUp();

        var first = await world.Run(Ids(1, 10), null);

        first.Result.Should().Be(SyncResult.Changed);
        File.Exists(SnapshotPath + ".bad").Should().BeTrue();
    }

    private static CollectionFetchResult.Fetched Ids(int firstId, int count) =>
        new([.. Enumerable.Range(firstId, count).Select(id => new SnapshotItem(id, id + 1000, $"Example {id}", ItemKind.Base, null, null, null))]);

    private static CollectionFetchResult.Fetched Ids() => new([]);

    private byte[] StoreCollection(CollectionFetchResult.Fetched collection)
    {
        new SnapshotStore(_directory.FullPath, NullLogger<SnapshotStore>.Instance).Save(
            new CollectionSnapshot(CollectionSnapshot.CurrentSchemaVersion, _clock.GetUtcNow(), collection.Items));

        return File.ReadAllBytes(SnapshotPath);
    }

    private FileStream Lock() => new(SnapshotPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

    private World StartUp()
    {
        var snapshots = new SnapshotStore(_directory.FullPath, NullLogger<SnapshotStore>.Instance);
        var collection = new CollectionStore();
        var startup = snapshots.Load();

        if (startup is not null)
        {
            collection.Replace(CollectionState.FromSnapshot(startup));
        }

        return new World(snapshots, collection, _clock);
    }

    private sealed class World(SnapshotStore snapshots, CollectionStore collection, TimeProvider clock)
    {
        public SnapshotStore Snapshots { get; } = snapshots;

        public CollectionStore Collection { get; } = collection;

        public Task<SyncRunResult> Run(CollectionFetchResult answer, HeldBackRecord? heldBack) =>
            new SyncRunner(() => new FixedSource(answer), Snapshots, Collection, clock, NullLogger<SyncRunner>.Instance)
                .RunAsync(heldBack, TestContext.Current.CancellationToken);
    }

    private sealed class FixedSource(CollectionFetchResult answer) : ICollectionSource
    {
        public Task<CollectionFetchResult> FetchOwnedAsync(CancellationToken cancellationToken) => Task.FromResult(answer);
    }
}

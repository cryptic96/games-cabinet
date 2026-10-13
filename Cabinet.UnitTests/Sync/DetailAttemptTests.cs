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
/// Proves a game the details source never describes is asked for once and then left alone for a day on the injected clock,
/// that the record of it is dropped when its details arrive, and that visitors never see the record as a change.
/// </summary>
[Trait("Category", "Enrichment")]
public sealed class DetailAttemptTests : IDisposable
{
    private const int DescribedGame = 1001;
    private const int UndescribedGame = 1002;

    private static readonly DateTimeOffset Start = new(2030, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Start);
    private readonly TemporaryDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public async Task A_requested_game_the_answer_leaves_out_is_stamped_with_the_time_it_was_asked()
    {
        var source = new ScriptedDetails([UndescribedGame]);
        var latest = await RunStep(source, Snapshot());

        latest.Games!.Keys.Should().Equal(DescribedGame);
        latest.DetailAttempts.Should().BeEquivalentTo(new Dictionary<int, DateTimeOffset> { [UndescribedGame] = Start });
    }

    [Fact]
    public async Task A_game_whose_details_arrive_loses_its_attempt_record()
    {
        var source = new ScriptedDetails([]);
        var snapshot = Snapshot() with { DetailAttempts = new Dictionary<int, DateTimeOffset> { [UndescribedGame] = Start - TimeSpan.FromDays(2) } };

        var latest = await RunStep(source, snapshot);

        latest.Games!.Keys.Should().BeEquivalentTo([DescribedGame, UndescribedGame]);
        latest.DetailAttempts.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task An_answer_that_brings_no_details_still_stores_the_attempt_so_the_next_run_can_back_off()
    {
        var source = new ScriptedDetails([DescribedGame, UndescribedGame]);
        var commits = 0;

        var latest = await RunStep(source, Snapshot(), () => commits++);

        commits.Should().Be(1);
        latest.Games.Should().BeNullOrEmpty();
        latest.DetailAttempts!.Keys.Should().BeEquivalentTo([DescribedGame, UndescribedGame]);
    }

    [Fact]
    public async Task An_undescribed_game_is_asked_once_not_an_hour_later_and_again_a_day_after_it_was_first_asked()
    {
        var source = new ScriptedDetails([UndescribedGame]);
        var latest = await RunStep(source, Snapshot());
        source.Requested.Should().Equal([DescribedGame, UndescribedGame]);

        _clock.Advance(TimeSpan.FromHours(1));
        latest = await RunStep(source, latest);
        source.Requested.Should().Equal([DescribedGame, UndescribedGame], "an hour later nothing is asked");

        _clock.Advance(EnrichmentPlanner.MissingDetailsRetry - TimeSpan.FromHours(1));
        latest = await RunStep(source, latest);

        source.Requested.Should().Equal([DescribedGame, UndescribedGame, UndescribedGame]);
        latest.DetailAttempts![UndescribedGame].Should().Be(Start + EnrichmentPlanner.MissingDetailsRetry);
    }

    [Fact]
    public async Task A_failed_call_records_no_attempt()
    {
        var source = new ScriptedDetails([], fail: true);

        var latest = await RunStep(source, Snapshot());

        latest.DetailAttempts.Should().BeNullOrEmpty();
        source.Requested.Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_run_carries_the_attempts_of_still_owned_games_forward_and_drops_the_rest()
    {
        var runner = Runner(new ScriptedDetails([UndescribedGame]), out var store, out _);
        await runner.RunAsync(null, TestContext.Current.CancellationToken);
        store.Load()!.DetailAttempts.Should().ContainKey(UndescribedGame);

        var smaller = Runner(new ScriptedDetails([UndescribedGame]), out var smallerStore, out _, items: [Item(1, DescribedGame)], existing: store.Load());
        await smaller.RunAsync(null, TestContext.Current.CancellationToken);

        smallerStore.Load()!.DetailAttempts.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task A_run_whose_only_difference_is_a_newer_attempt_stamp_is_stored_but_not_reported_as_a_change()
    {
        var source = new ScriptedDetails([UndescribedGame]);
        var runner = Runner(source, out var store, out var collection);
        (await runner.RunAsync(null, TestContext.Current.CancellationToken)).Result.Should().Be(SyncResult.Changed);
        var versionAfterFirst = collection.Current.Version;
        var firstStamp = store.Load()!.DetailAttempts![UndescribedGame];

        _clock.Advance(EnrichmentPlanner.MissingDetailsRetry + TimeSpan.FromMinutes(5));
        var second = await runner.RunAsync(null, TestContext.Current.CancellationToken);

        second.Result.Should().Be(SyncResult.Unchanged);
        collection.Current.Version.Should().Be(versionAfterFirst);
        store.Load()!.DetailAttempts![UndescribedGame].Should().BeAfter(firstStamp);
    }

    private static SnapshotItem Item(long collectionId, int gameId) =>
        new(collectionId, gameId, $"Example {gameId}", ItemKind.Base, null, null, null);

    private static GameDetails Details(DateTimeOffset at) =>
        new(at, 2, 4, 60, null, null, null, 2.5, null, null, [], [], [], null, DetailsVersion: GameDetails.CurrentDetailsVersion);

    private CollectionSnapshot Snapshot() =>
        new(CollectionSnapshot.CurrentSchemaVersion, Start, [Item(1, DescribedGame), Item(2, UndescribedGame)]);

    private async Task<CollectionSnapshot> RunStep(ScriptedDetails source, CollectionSnapshot snapshot, Action? onCommit = null)
    {
        var sync = new EnrichmentSync(() => source, EnrichmentOptions.Default, _clock, NullLogger<EnrichmentSync>.Instance);
        var latest = snapshot;

        await sync.RunAsync(
            snapshot,
            _clock.GetUtcNow().AddHours(1),
            next =>
            {
                latest = next;
                onCommit?.Invoke();

                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        return latest;
    }

    private SyncRunner Runner(
        ScriptedDetails details,
        out SnapshotStore store,
        out CollectionStore collection,
        IReadOnlyList<SnapshotItem>? items = null,
        CollectionSnapshot? existing = null)
    {
        store = new SnapshotStore(_directory.FullPath, NullLogger<SnapshotStore>.Instance);
        collection = new CollectionStore();

        if (existing is not null)
        {
            store.Save(existing);
            collection.Replace(CollectionState.FromSnapshot(existing));
        }

        var owned = items ?? [Item(1, DescribedGame), Item(2, UndescribedGame)];
        var enrichment = new EnrichmentSync(() => details, EnrichmentOptions.Default, _clock, NullLogger<EnrichmentSync>.Instance);

        return new SyncRunner(
            () => new FixedSource(owned),
            store,
            collection,
            _clock,
            NullLogger<SyncRunner>.Instance,
            enrichment: enrichment);
    }

    private sealed class FixedSource(IReadOnlyList<SnapshotItem> items) : ICollectionSource
    {
        public Task<CollectionFetchResult> FetchOwnedAsync(CancellationToken cancellationToken) =>
            Task.FromResult<CollectionFetchResult>(new CollectionFetchResult.Fetched(items));
    }

    private sealed class ScriptedDetails(IReadOnlyCollection<int> leftOut, bool fail = false) : IEnrichmentSource
    {
        private readonly List<int> _requested = [];

        public IReadOnlyList<int> Requested => _requested;

        public Task<EnrichmentFetchResult> FetchDetailsAsync(IReadOnlyList<int> gameIds, CancellationToken cancellationToken)
        {
            _requested.AddRange(gameIds);

            if (fail)
            {
                return Task.FromResult<EnrichmentFetchResult>(new EnrichmentFetchResult.Failed(SyncFailure.Unavailable));
            }

            var answered = gameIds.Where(id => !leftOut.Contains(id)).ToDictionary(id => id, _ => Details(Start));

            return Task.FromResult<EnrichmentFetchResult>(new EnrichmentFetchResult.Fetched(answered));
        }
    }
}

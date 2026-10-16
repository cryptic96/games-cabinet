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
/// Proves the other titles of a game and the name of an owned edition are kept by the sync: a change of the other titles
/// alone is saved and published, details read before the other titles existed are read again once, and an edition name
/// that is newly read is saved.
/// </summary>
[Trait("Category", "Enrichment")]
public sealed class NameSyncTests : IDisposable
{
    private const int FirstGame = 1001;
    private const int SecondGame = 1002;

    private static readonly DateTimeOffset Start = new(2030, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Start);
    private readonly TemporaryDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Details_that_differ_only_in_alternate_names_are_not_the_same()
    {
        var plain = Details(["Example Title"]);

        plain.SameAs(Details(["Another Title"])).Should().BeFalse();
        plain.SameAs(Details(["Example Title"])).Should().BeTrue();
        plain.SameAs(Details(null)).Should().BeFalse();
        Details(null).SameAs(Details(null)).Should().BeTrue();
        Details([]).SameAs(Details(null)).Should().BeFalse();
    }

    [Fact]
    public async Task A_change_of_alternate_names_alone_is_saved_and_published()
    {
        var source = new NamedDetails(["First Other Title"]);
        var runner = Runner(source, out var store, out var collection);
        (await runner.RunAsync(null, TestContext.Current.CancellationToken)).Result.Should().Be(SyncResult.Changed);
        var versionAfterFirst = collection.Current.Version;
        store.Load()!.Games![FirstGame].AlternateNames.Should().Equal("First Other Title");

        source.Names = ["First Other Title", "Second Other Title"];
        _clock.Advance(EnrichmentOptions.Default.RefreshAfter + TimeSpan.FromHours(1));
        var second = await runner.RunAsync(null, TestContext.Current.CancellationToken);

        second.Result.Should().Be(SyncResult.Changed);
        collection.Current.Version.Should().NotBe(versionAfterFirst);
        store.Load()!.Games![FirstGame].AlternateNames.Should().Equal("First Other Title", "Second Other Title");
    }

    [Fact]
    public async Task Details_read_before_this_change_are_read_again_once()
    {
        var source = new NamedDetails(["Current Title"]);
        var older = Details(null) with { DetailsVersion = 1 };
        var existing = Snapshot() with
        {
            Games = new Dictionary<int, GameDetails> { [FirstGame] = older, [SecondGame] = older },
        };
        var runner = Runner(source, out var store, out _, existing: existing);

        await runner.RunAsync(null, TestContext.Current.CancellationToken);
        source.Requested.Should().BeEquivalentTo([FirstGame, SecondGame]);
        store.Load()!.Games!.Values.Should().OnlyContain(details => details.DetailsVersion == GameDetails.CurrentDetailsVersion);
        store.Load()!.Games![FirstGame].AlternateNames.Should().Equal("Current Title");

        _clock.Advance(TimeSpan.FromHours(1));
        await runner.RunAsync(null, TestContext.Current.CancellationToken);

        source.Requested.Should().HaveCount(2, "nothing is read a second time inside the refresh time");
    }

    [Fact]
    public async Task A_newly_read_edition_name_is_saved()
    {
        var source = new NamedDetails(["Other Title"]);
        var owned = new List<SnapshotItem>(Items(versionName: null));
        var runner = Runner(source, out var store, out _, items: owned);
        await runner.RunAsync(null, TestContext.Current.CancellationToken);
        store.Load()!.Items.Should().OnlyContain(item => item.VersionName == null);

        owned.Clear();
        owned.AddRange(Items(versionName: "Quiet Quarry Deluxe edition"));
        await runner.RunAsync(null, TestContext.Current.CancellationToken);

        store.Load()!.Items.Should().OnlyContain(item => item.VersionName == "Quiet Quarry Deluxe edition");
    }

    private static IReadOnlyList<SnapshotItem> Items(string? versionName) =>
        [
            new(1, FirstGame, "Example 1", ItemKind.Base, null, null, null, VersionName: versionName),
            new(2, SecondGame, "Example 2", ItemKind.Base, null, null, null, VersionName: versionName),
        ];

    private static GameDetails Details(IReadOnlyList<string>? names) =>
        new(Start, 2, 4, 60, null, null, null, 2.5, null, null, [], [], [], null, DetailsVersion: GameDetails.CurrentDetailsVersion, AlternateNames: names);

    private static CollectionSnapshot Snapshot() =>
        new(CollectionSnapshot.CurrentSchemaVersion, Start, Items(versionName: null));

    private SyncRunner Runner(
        NamedDetails details,
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

        var owned = items ?? Items(versionName: null);
        var enrichment = new EnrichmentSync(() => details, EnrichmentOptions.Default, _clock, NullLogger<EnrichmentSync>.Instance);

        return new SyncRunner(
            () => new LiveSource(owned),
            store,
            collection,
            _clock,
            NullLogger<SyncRunner>.Instance,
            enrichment: enrichment);
    }

    private sealed class LiveSource(IReadOnlyList<SnapshotItem> items) : ICollectionSource
    {
        public Task<CollectionFetchResult> FetchOwnedAsync(CancellationToken cancellationToken) =>
            Task.FromResult<CollectionFetchResult>(new CollectionFetchResult.Fetched([.. items]));
    }

    private sealed class NamedDetails(IReadOnlyList<string> names) : IEnrichmentSource
    {
        private readonly List<int> _requested = [];

        public IReadOnlyList<string> Names { get; set; } = names;

        public IReadOnlyList<int> Requested => _requested;

        public Task<EnrichmentFetchResult> FetchDetailsAsync(IReadOnlyList<int> gameIds, CancellationToken cancellationToken)
        {
            _requested.AddRange(gameIds);
            var answered = gameIds.ToDictionary(id => id, _ => Details(Names));

            return Task.FromResult<EnrichmentFetchResult>(new EnrichmentFetchResult.Fetched(answered));
        }
    }
}

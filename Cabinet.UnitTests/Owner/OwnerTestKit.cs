using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.Service.Collection;
using Cabinet.Service.Owner;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Owner;

/// <summary>An owner-data store that lives in memory, so the writer's rules can be tried without a disk.</summary>
internal sealed class InMemoryOwnerDataStore : IOwnerDataStore
{
    /// <summary>What the next load finds.</summary>
    public OwnerDataState State { get; set; } = new OwnerDataState.Ready(OwnerData.Empty);

    /// <summary>Every copy that was saved, oldest first.</summary>
    public List<OwnerData> Saved { get; } = [];

    /// <summary>When true, the next save fails the way a full disk does.</summary>
    public bool FailNextSave { get; set; }

    /// <summary>Whether a file exists to copy; a save creates it, and a test that starts from stored data sets it.</summary>
    public bool FileExists { get; set; }

    /// <summary>The moments earlier versions were kept, oldest first.</summary>
    public List<DateTimeOffset> KeptAt { get; } = [];

    /// <inheritdoc />
    public OwnerDataState Load() => State;

    /// <inheritdoc />
    public void Save(OwnerData data)
    {
        if (FailNextSave)
        {
            FailNextSave = false;

            throw new IOException("The disk is full.");
        }

        Saved.Add(data);
        FileExists = true;
        State = new OwnerDataState.Ready(data);
    }

    /// <inheritdoc />
    public void KeepEarlierVersion(DateTimeOffset now)
    {
        if (FileExists)
        {
            KeptAt.Add(now);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<EarlierVersion> EarlierVersions() =>
        [.. KeptAt.OrderByDescending(moment => moment).Select((moment, index) => new EarlierVersion(index, moment))];

    /// <inheritdoc />
    public byte[]? ReadEarlierVersion(int index) => index >= 0 && index < KeptAt.Count ? [] : null;

    /// <inheritdoc />
    public DateTimeOffset? NewestEarlierVersionAt() => KeptAt.Count == 0 ? null : KeptAt.Max();
}

/// <summary>A writer wired to an in-memory store, a clock the test moves, and a publisher that fills a collection store.</summary>
/// <param name="Store">What the writer keeps its data in.</param>
/// <param name="Clock">The clock the writer stamps saves with.</param>
/// <param name="Service">The single writer.</param>
/// <param name="Collection">The store visitors read from.</param>
/// <param name="Publisher">Builds the collection view from the synced collection and the owner data.</param>
internal sealed record OwnerHarness(
    InMemoryOwnerDataStore Store,
    FakeTimeProvider Clock,
    OwnerDataService Service,
    CollectionStore Collection,
    CollectionPublisher Publisher)
{
    /// <summary>The version of the collection visitors see now.</summary>
    public string? Version => Collection.Current.Version;

    /// <summary>The owner data as the writer holds it now.</summary>
    public OwnerData Data => ((OwnerDataState.Ready)Service.State).Data;

    /// <summary>Adds a location through the writer, from the revision the writer holds now.</summary>
    /// <param name="name">The name to add.</param>
    public Task<OwnerResult> AddAsync(string name) =>
        Service.MutateAsync(Service.Revision, data => LocationEdits.Add(data, name, OwnerDataService.NewId), CancellationToken.None);

    /// <summary>Puts a game in the location of that name through the writer, from the revision the writer holds now.</summary>
    /// <param name="gameId">The game.</param>
    /// <param name="locationName">The name of an existing location.</param>
    public Task<OwnerResult> PlaceAsync(int gameId, string locationName) =>
        Service.MutateAsync(
            Service.Revision,
            data => LocationEdits.Assign(data, gameId, data.Locations.Single(location => location.Name == locationName).Id, $"Game {gameId}"),
            CancellationToken.None);
}

/// <summary>Builds the pieces the owner writer's tests share: a clock, an in-memory store and synthetic collections with invented titles.</summary>
internal static class OwnerTestKit
{
    /// <summary>The moment the clock starts at.</summary>
    public static readonly DateTimeOffset Start = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Builds a writer over a store, on a clock that starts at <see cref="Start"/>.</summary>
    /// <param name="store">The store; a fresh in-memory one when null.</param>
    /// <param name="snapshot">The synced collection to publish; none when null.</param>
    public static OwnerHarness Harness(InMemoryOwnerDataStore? store = null, CollectionSnapshot? snapshot = null)
    {
        var keeping = store ?? new InMemoryOwnerDataStore();
        var clock = new FakeTimeProvider(Start);
        var service = new OwnerDataService(keeping, clock, NullLogger<OwnerDataService>.Instance);
        var collection = new CollectionStore();
        var publisher = new CollectionPublisher(collection, ArtRules.Default, service);

        if (snapshot is not null)
        {
            publisher.Publish(snapshot);
        }

        return new OwnerHarness(keeping, clock, service, collection, publisher);
    }

    /// <summary>A synced collection holding only the invented base games with these game identifiers, named after them.</summary>
    /// <param name="gameIds">The games the collection lists.</param>
    public static CollectionSnapshot Listing(params int[] gameIds) => Snapshot(
        [.. gameIds.Select((gameId, index) => Base(index + 1, gameId, $"Game {gameId}"))]);

    /// <summary>A synced collection of invented games: two base games, an expansion of the first, one of both, and one of a game that is not owned.</summary>
    public static CollectionSnapshot Collection() => Snapshot(
        [
            Base(1, 100, "Lantern Harbour"),
            Base(2, 101, "Quiet Quarry"),
            Expansion(3, 200, "Lantern Harbour: Night Market"),
            Expansion(4, 201, "Quiet Quarry: Copper Canal"),
            Expansion(5, 202, "Orphan Orchard: Extra Rows"),
        ],
        (200, [Ref(100)]),
        (201, [Ref(100), Ref(101)]),
        (202, [Ref(999)]));

    /// <summary>A synced collection with the given items and details.</summary>
    /// <param name="items">The owned items.</param>
    /// <param name="expands">For an expansion's game, the games its details say it expands.</param>
    public static CollectionSnapshot Snapshot(SnapshotItem[] items, params (int GameId, IReadOnlyList<BaseGameRef> Expands)[] expands) =>
        new(
            CollectionSnapshot.CurrentSchemaVersion,
            Start,
            items,
            Games: expands.Length == 0
                ? null
                : expands.ToDictionary(entry => entry.GameId, entry => Details(entry.Expands)));

    /// <summary>An owned base game.</summary>
    /// <param name="collectionId">The collection entry.</param>
    /// <param name="gameId">The game.</param>
    /// <param name="title">The invented title.</param>
    public static SnapshotItem Base(long collectionId, int gameId, string title) =>
        new(collectionId, gameId, title, ItemKind.Base, 2020, null, null);

    /// <summary>An owned expansion.</summary>
    /// <param name="collectionId">The collection entry.</param>
    /// <param name="gameId">The game.</param>
    /// <param name="title">The invented title.</param>
    public static SnapshotItem Expansion(long collectionId, int gameId, string title) =>
        new(collectionId, gameId, title, ItemKind.Expansion, 2021, null, null);

    /// <summary>A reference to a base game.</summary>
    /// <param name="gameId">The game.</param>
    public static BaseGameRef Ref(int gameId) => new(gameId, $"Example Game {gameId}");

    private static GameDetails Details(IReadOnlyList<BaseGameRef> expands) =>
        new(Start, null, null, null, null, null, null, null, null, null, [], [], expands, null);
}

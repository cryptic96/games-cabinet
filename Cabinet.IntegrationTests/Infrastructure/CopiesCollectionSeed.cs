using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.Repository.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>
/// Writes a small synthetic collection to a storage directory before the host starts: one game owned three times, one game owned
/// once, and one expansion that stands beside the game owned three times. It is the smallest collection in which the copies of a game
/// can be told apart, ticked one by one and placed in different locations.
/// </summary>
public static class CopiesCollectionSeed
{
    /// <summary>The identifier of the game that is owned three times.</summary>
    public const int ThreeCopiesGameId = 5101;

    /// <summary>The identifier of the game that is owned once.</summary>
    public const int OneCopyGameId = 5102;

    /// <summary>The identifier of the expansion of the game that is owned three times.</summary>
    public const int ExpansionGameId = 5201;

    /// <summary>The title of the game that is owned three times.</summary>
    public const string ThreeCopiesTitle = "Quiet Quarry";

    /// <summary>The title of the game that is owned once.</summary>
    public const string OneCopyTitle = "Hollow Harbour";

    /// <summary>The title of the expansion.</summary>
    public const string ExpansionTitle = "Hidden Galleries";

    /// <summary>The collection entry of the one copy of the game that is owned once.</summary>
    public const long OneCopyEntry = 7101;

    /// <summary>The collection entry of the expansion.</summary>
    public const long ExpansionEntry = 7201;

    /// <summary>The collection entries of the three copies, in collection order.</summary>
    public static IReadOnlyList<long> ThreeCopyEntries { get; } = [7001, 7002, 7003];

    /// <summary>Writes the collection and, when locations are given, owner data that holds those locations and no placed game.</summary>
    /// <param name="storageDirectory">The storage directory the host will read.</param>
    /// <param name="editions">The edition names of the three copies, null for a copy that has none; null for no edition names at all.</param>
    /// <param name="locations">The locations to create, as an identifier and a name each.</param>
    public static void Write(string storageDirectory, string?[]? editions = null, params OwnerLocation[] locations)
    {
        var moment = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var items = ThreeCopyEntries
            .Select((entry, index) => new SnapshotItem(entry, ThreeCopiesGameId, ThreeCopiesTitle, ItemKind.Base, 2015, null, null, VersionName: editions?[index]))
            .Append(new SnapshotItem(OneCopyEntry, OneCopyGameId, OneCopyTitle, ItemKind.Base, 2018, null, null))
            .Append(new SnapshotItem(ExpansionEntry, ExpansionGameId, ExpansionTitle, ItemKind.Expansion, 2020, null, null));
        var expansionDetails = new GameDetails(
            moment,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            [new BaseGameRef(ThreeCopiesGameId, ThreeCopiesTitle)],
            null);

        new SnapshotStore(storageDirectory, NullLogger<SnapshotStore>.Instance).Save(new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            moment,
            [.. items],
            null,
            new Dictionary<int, GameDetails> { [ExpansionGameId] = expansionDetails }));

        if (locations.Length > 0)
        {
            new OwnerDataStore(storageDirectory, NullLogger<OwnerDataStore>.Instance).Save(OwnerData.Empty with { Locations = locations });
        }
    }
}

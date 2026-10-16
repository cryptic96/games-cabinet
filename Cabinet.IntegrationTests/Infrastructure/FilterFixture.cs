using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.Repository.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>
/// An invented, synced collection and the owner's invented locations for the tests that filter by players, time or place.
/// It holds a game with an expansion stored apart from it, a game owned three times with copies in two places, a game with an
/// expansion for more players that follows it, standalone games, games whose player counts or play time are unknown, and games
/// that are not placed. One location, "Lent out", holds nothing.
/// </summary>
public static class FilterFixture
{
    /// <summary>The identifier of the location named Cabinet A.</summary>
    public const string CabinetAId = "a1b2c3d4";

    /// <summary>The identifier of the location named Shelf B.</summary>
    public const string ShelfBId = "e5f6a7b8";

    /// <summary>The identifier of the location named Lent out, which holds no game.</summary>
    public const string LentOutId = "c0ffee01";

    /// <summary>The game that has an expansion stored in another location.</summary>
    public const int HarbourGameId = 6001;

    /// <summary>The expansion of <see cref="HarbourGameId"/>, kept in a different location from it.</summary>
    public const int HarbourExpansionGameId = 6002;

    /// <summary>The game that is owned three times.</summary>
    public const int QuarryGameId = 6003;

    /// <summary>The game for three to four players.</summary>
    public const int TallyGameId = 6004;

    /// <summary>The expansion of <see cref="TallyGameId"/> for five to six players; it has no location of its own and follows its base.</summary>
    public const int TallyExpansionGameId = 6005;

    /// <summary>A standalone game kept in Shelf B.</summary>
    public const int KettleGameId = 6006;

    /// <summary>A standalone game that is not placed.</summary>
    public const int MeadowGameId = 6007;

    /// <summary>A game with no stored details at all, not placed.</summary>
    public const int MysteryGameId = 6008;

    /// <summary>A game whose details leave out the player counts and the play time, not placed.</summary>
    public const int HalfKnownGameId = 6009;

    /// <summary>The collection entry of <see cref="HarbourGameId"/>.</summary>
    public const long HarbourEntry = 8001;

    /// <summary>The collection entry of <see cref="HarbourExpansionGameId"/>.</summary>
    public const long HarbourExpansionEntry = 8002;

    /// <summary>The three collection entries of <see cref="QuarryGameId"/>: the first and third in Cabinet A, the second in Shelf B.</summary>
    public static IReadOnlyList<long> QuarryEntries { get; } = [8003, 8004, 8005];

    /// <summary>The collection entry of <see cref="TallyGameId"/>.</summary>
    public const long TallyEntry = 8006;

    /// <summary>The collection entry of <see cref="TallyExpansionGameId"/>.</summary>
    public const long TallyExpansionEntry = 8007;

    /// <summary>The collection entry of <see cref="KettleGameId"/>.</summary>
    public const long KettleEntry = 8008;

    /// <summary>The collection entry of <see cref="MeadowGameId"/>.</summary>
    public const long MeadowEntry = 8009;

    /// <summary>The collection entry of <see cref="MysteryGameId"/>.</summary>
    public const long MysteryEntry = 8010;

    /// <summary>The collection entry of <see cref="HalfKnownGameId"/>.</summary>
    public const long HalfKnownEntry = 8011;

    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The owner's locations in the owner's order.</summary>
    public static IReadOnlyList<OwnerLocation> Locations { get; } =
    [
        new OwnerLocation(CabinetAId, "Cabinet A"),
        new OwnerLocation(ShelfBId, "Shelf B"),
        new OwnerLocation(LentOutId, "Lent out"),
    ];

    /// <summary>Builds the invented synced collection, without any location of its own.</summary>
    public static CollectionSnapshot Snapshot()
    {
        var details = new GameDetails(Moment, 2, 4, 60, null, null, 10, 2.4, 7.4, 7.0, ["Invented Designer"], ["Drafting"], [], null);
        var games = new Dictionary<int, GameDetails>
        {
            [HarbourGameId] = details,
            [HarbourExpansionGameId] = details with { ExpandsGames = [new BaseGameRef(HarbourGameId, "Lantern & Harbour")] },
            [QuarryGameId] = details with { MinPlayers = 1, MaxPlayers = 2, PlayingTime = 30 },
            [TallyGameId] = details with { MinPlayers = 3, MaxPlayers = 4, PlayingTime = 90 },
            [TallyExpansionGameId] = details with
            {
                MinPlayers = 5,
                MaxPlayers = 6,
                PlayingTime = 90,
                ExpandsGames = [new BaseGameRef(TallyGameId, "Tide Tally")],
            },
            [KettleGameId] = details with { MinPlayers = 2, MaxPlayers = 5, PlayingTime = 45 },
            [MeadowGameId] = details with { MinPlayers = 1, MaxPlayers = 2, PlayingTime = 20 },
            [HalfKnownGameId] = details with { MinPlayers = null, MaxPlayers = null, PlayingTime = null },
        };

        return new CollectionSnapshot(
            CollectionSnapshot.CurrentSchemaVersion,
            Moment,
            [
                Item(HarbourEntry, HarbourGameId, "Lantern & Harbour", ItemKind.Base),
                Item(HarbourExpansionEntry, HarbourExpansionGameId, "Lantern & Harbour: Far Shore", ItemKind.Expansion),
                .. QuarryEntries.Select(entry => Item(entry, QuarryGameId, "Quiet Quarry", ItemKind.Base)),
                Item(TallyEntry, TallyGameId, "Tide Tally", ItemKind.Base),
                Item(TallyExpansionEntry, TallyExpansionGameId, "Tide Tally: Big Table", ItemKind.Expansion),
                Item(KettleEntry, KettleGameId, "Copper Kettle", ItemKind.Base),
                Item(MeadowEntry, MeadowGameId, "Plain Meadow", ItemKind.Base),
                Item(MysteryEntry, MysteryGameId, "Mystery Crate", ItemKind.Base),
                Item(HalfKnownEntry, HalfKnownGameId, "Half Known", ItemKind.Base),
            ],
            null,
            games);
    }

    /// <summary>
    /// Builds the owner's data: Cabinet A holds the first game, the first and third copy of the game owned three times and the
    /// game for three to four players, whose expansion follows it; Shelf B holds the second copy, the standalone game and the
    /// expansion stored apart from its base; Lent out holds nothing; the other games are not placed.
    /// </summary>
    public static OwnerData Owner() => Owner(placeCopies: true);

    /// <summary>
    /// Builds the owner's data with the same three locations. With <paramref name="placeCopies"/> false the locations exist but no copy
    /// is placed in any of them, so every game is not placed.
    /// </summary>
    /// <param name="placeCopies">Whether to place the copies as <see cref="Owner()"/> describes; false leaves every game unplaced.</param>
    public static OwnerData Owner(bool placeCopies) =>
        OwnerData.Empty with
        {
            Revision = 1,
            Locations = Locations,
            Games = placeCopies ?
            [
                new OwnerGame(HarbourGameId, CabinetAId, null, null),
                new OwnerGame(HarbourExpansionGameId, ShelfBId, null, null),
                new OwnerGame(QuarryGameId, CabinetAId, null, null, QuarryEntries[0]),
                new OwnerGame(QuarryGameId, ShelfBId, null, null, QuarryEntries[1]),
                new OwnerGame(QuarryGameId, CabinetAId, null, null, QuarryEntries[2]),
                new OwnerGame(TallyGameId, CabinetAId, null, null),
                new OwnerGame(KettleGameId, ShelfBId, null, null),
            ] : [],
        };

    /// <summary>Stores the collection in the directory the way the host's own store would, with no owner data.</summary>
    /// <param name="directory">The state directory the host will be given; it must exist.</param>
    public static void WriteCollection(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        new SnapshotStore(directory, NullLogger<SnapshotStore>.Instance).Save(Snapshot());
    }

    /// <summary>Stores the collection and the owner's locations and placements in the directory.</summary>
    /// <param name="directory">The state directory the host will be given; it must exist.</param>
    public static void Write(string directory) => Write(directory, placeCopies: true);

    /// <summary>Stores the collection and the owner's locations in the directory, with or without the placements.</summary>
    /// <param name="directory">The state directory the host will be given; it must exist.</param>
    /// <param name="placeCopies">Whether to store the placements; false stores the three locations and places no copy.</param>
    public static void Write(string directory, bool placeCopies)
    {
        WriteCollection(directory);
        OwnerDataSeed.Write(directory, Owner(placeCopies));
    }

    /// <summary>
    /// Builds a synthetic collection and the owner's data for many storage locations: <paramref name="count"/> invented locations named
    /// Cabinet A, Cabinet B and onward by letter, in that owner order, each holding two invented games, and no game left unplaced.
    /// The names are lettered furniture only. The collection and the placements are separate from the rest of this fixture.
    /// </summary>
    /// <param name="count">How many locations to invent, from 1 to 26.</param>
    public static (CollectionSnapshot Snapshot, OwnerData Owner) ManyLocations(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, 26);

        var details = new GameDetails(Moment, 2, 4, 60, null, null, 10, 2.4, 7.4, 7.0, ["Invented Designer"], ["Drafting"], [], null);
        var locations = new List<OwnerLocation>();
        var items = new List<SnapshotItem>();
        var games = new Dictionary<int, GameDetails>();
        var placements = new List<OwnerGame>();

        for (var index = 0; index < count; index++)
        {
            var locationId = (0xb0000000 + index).ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
            var letter = (char)('A' + index);

            locations.Add(new OwnerLocation(locationId, $"Cabinet {letter}"));

            for (var game = 0; game < 2; game++)
            {
                var gameId = 7000 + (index * 2) + game;

                items.Add(Item(9000 + (index * 2) + game, gameId, $"Invented Game {letter}{game + 1}", ItemKind.Base));
                games[gameId] = details;
                placements.Add(new OwnerGame(gameId, locationId, null, null));
            }
        }

        var snapshot = new CollectionSnapshot(CollectionSnapshot.CurrentSchemaVersion, Moment, items, null, games);
        var owner = OwnerData.Empty with { Revision = 1, Locations = locations, Games = placements };

        return (snapshot, owner);
    }

    /// <summary>Stores the many-locations collection and the owner's placements in the directory.</summary>
    /// <param name="directory">The state directory the host will be given; it must exist.</param>
    /// <param name="count">How many locations to invent, from 1 to 26.</param>
    public static void WriteManyLocations(string directory, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var (snapshot, owner) = ManyLocations(count);

        new SnapshotStore(directory, NullLogger<SnapshotStore>.Instance).Save(snapshot);
        OwnerDataSeed.Write(directory, owner);
    }

    private static SnapshotItem Item(long entry, int gameId, string title, ItemKind kind) =>
        new(entry, gameId, title, kind, 2015, null, null);
}

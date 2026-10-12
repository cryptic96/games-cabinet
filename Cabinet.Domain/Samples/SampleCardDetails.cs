using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;

namespace Cabinet.Domain.Samples;

/// <summary>
/// Invented details and locations for the sample collections, so every row of the detail card can be seen before and
/// without a real collection. Every name, mechanic, number and location here is invented; nothing mirrors a real game, a
/// real designer or a real storage place. A sample always gets the same details, because each value comes from a stable
/// hash of the game identifier.
/// </summary>
public static class SampleCardDetails
{
    private const string EdgeSampleName = "edge";
    private const int FirstYear = 1990;
    private const int YearSpan = 36;
    private const int NoDetailsOneIn = 10;
    private const int LocationOneIn = 2;
    private const int MaxMechanics = 14;
    private const int MaxDesigners = 3;
    private const int WeightSteps = 381;
    private const int AverageSteps = 345;
    private const int MinimumWeightHundredths = 100;
    private const int MinimumAverageHundredths = 550;
    private const int BayesDrop = 40;
    private const int PlayTimeStepMinutes = 15;
    private const int PlayTimeSteps = 12;
    private const int ExtraPlayMinutes = 30;
    private const int StatedOnlyOneIn = 3;
    private const int MaxExtraPlayers = 5;
    private const int MaxFirstMinimum = 3;
    private const int AgeSteps = 5;
    private const int MinimumAge = 6;
    private const int AgeStep = 2;

    private const int YearSalt = 201;
    private const int NoDetailsSalt = 202;
    private const int PlayersSalt = 203;
    private const int PlayTimeSalt = 204;
    private const int AgeSalt = 205;
    private const int WeightSalt = 206;
    private const int AverageSalt = 207;
    private const int DesignerCountSalt = 208;
    private const int DesignerStartSalt = 209;
    private const int MechanicCountSalt = 210;
    private const int MechanicStartSalt = 211;
    private const int LocationPresenceSalt = 212;
    private const int LocationPickSalt = 213;
    private const int ExtraPlayersSalt = 214;
    private const int StatedOnlySalt = 215;

    private static readonly DateTimeOffset InventedTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly string[] Designers =
    [
        "Orla Vendrick",
        "Tamsin Quillon",
        "Bram Holloway-Pike",
        "Ines Marrow",
        "Caspar Delmont",
        "Wren Ashgrove",
        "Lowen Tarkis",
        "Marit Fennick",
        "Odin Brackwater",
        "Sunniva Hesk",
        "Ulrich Pemberly",
        "Zadie Lorne",
    ];

    private static readonly string[] Mechanics =
    [
        "Hand management",
        "Set collection",
        "Dice rolling",
        "Worker placement",
        "Area control",
        "Drafting",
        "Tile placement",
        "Trading",
        "Deck building",
        "Cooperative play",
        "Pattern building",
        "Auction",
        "Variable player powers",
        "Route building",
        "Push your luck",
        "Tech trees",
    ];

    private static readonly string[] Locations =
    [
        "Rack A, row 3",
        "Crate 7",
        "Rack B, left",
        "Bin 2",
        "Rack C, top",
        "Crate 12",
        "Bin 9",
    ];

    private static readonly string[] EdgeDesigners =
    [
        "Zoë Åkerlund-Pike",
        "Ярослав Тестовый",
        "テスト 太郎",
        "สมใจ ทดสอบ",
        "ليلى اختبار",
        "Pat \U0001F3B2 Quill",
    ];

    private static readonly string[] EdgeLocations =
    [
        "Rack Å, row 3",
        "Стеллаж 3, полка 2",
        "棚 B・左",
        "ชั้น 4 ซ้าย",
        "رف 5، يمين",
        "Crate \U0001F3B2 7",
    ];

    /// <summary>
    /// Builds the stored shape of a sample: one stored item per item, with a year and sometimes a location, and invented
    /// details for most games. About one base game in ten has no details at all, and an expansion always has details so the
    /// games it expands are named. The edge sample gives every game a designer and a location in a different script, so
    /// the card is seen with Latin, Cyrillic, Japanese, Thai and Arabic text and an emoji.
    /// </summary>
    /// <param name="sample">The sample name; only the edge sample is treated differently.</param>
    /// <param name="items">The items of the sample.</param>
    public static CollectionSnapshot SnapshotFor(string sample, IReadOnlyList<CabinetItem> items)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(items);

        var edge = string.Equals(sample, EdgeSampleName, StringComparison.Ordinal);
        var stored = items
            .Select((item, index) => new SnapshotItem(
                item.CollectionId,
                item.BggId,
                item.Title,
                item.Kind,
                FirstYear + StableHash.Bucket(item.BggId, YearSalt, YearSpan),
                null,
                edge ? EdgeLocations[index % EdgeLocations.Length] : LocationOf(item),
                null,
                null))
            .ToList();
        var games = new Dictionary<int, GameDetails>();

        foreach (var (item, index) in items.Select((item, index) => (item, index)))
        {
            if (!games.ContainsKey(item.BggId) && (edge || HasDetails(item)))
            {
                games[item.BggId] = DetailsOf(item, edge ? EdgeDesigners[index % EdgeDesigners.Length] : null);
            }
        }

        return new CollectionSnapshot(CollectionSnapshot.CurrentSchemaVersion, InventedTime, stored, null, games);
    }

    private static bool HasDetails(CabinetItem item) =>
        item.Kind == ItemKind.Expansion || StableHash.Bucket(item.BggId, NoDetailsSalt, NoDetailsOneIn) != 0;

    private static string? LocationOf(CabinetItem item) =>
        StableHash.Bucket(item.CollectionId, LocationPresenceSalt, LocationOneIn) == 0
            ? Locations[StableHash.Bucket(item.CollectionId, LocationPickSalt, Locations.Length)]
            : null;

    private static GameDetails DetailsOf(CabinetItem item, string? fixedDesigner)
    {
        var id = item.BggId;
        var minPlayers = 1 + StableHash.Bucket(id, PlayersSalt, MaxFirstMinimum);
        var maxPlayers = minPlayers + StableHash.Bucket(id, ExtraPlayersSalt, MaxExtraPlayers);
        var playTime = PlayTimeStepMinutes * (1 + StableHash.Bucket(id, PlayTimeSalt, PlayTimeSteps));
        var statedOnly = StableHash.Bucket(id, StatedOnlySalt, StatedOnlyOneIn) == 0;
        var average = (MinimumAverageHundredths + StableHash.Bucket(id, AverageSalt, AverageSteps)) / 100.0;

        return new GameDetails(
            InventedTime,
            minPlayers,
            maxPlayers,
            playTime,
            statedOnly ? null : playTime,
            statedOnly ? null : playTime + ExtraPlayMinutes,
            MinimumAge + (AgeStep * StableHash.Bucket(id, AgeSalt, AgeSteps)),
            (MinimumWeightHundredths + StableHash.Bucket(id, WeightSalt, WeightSteps)) / 100.0,
            average,
            average - (BayesDrop / 100.0),
            fixedDesigner is null ? Pick(Designers, id, DesignerStartSalt, 1 + StableHash.Bucket(id, DesignerCountSalt, MaxDesigners)) : [fixedDesigner],
            Pick(Mechanics, id, MechanicStartSalt, StableHash.Bucket(id, MechanicCountSalt, MaxMechanics + 1)),
            item.ExpansionOf,
            null);
    }

    private static List<string> Pick(string[] pool, int id, int startSalt, int count)
    {
        var start = StableHash.Bucket(id, startSalt, pool.Length);

        return Enumerable.Range(0, count).Select(offset => pool[(start + offset) % pool.Length]).ToList();
    }
}

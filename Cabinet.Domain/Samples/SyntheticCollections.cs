using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;

namespace Cabinet.Domain.Samples;

/// <summary>
/// Invented collections for building and reviewing the cabinet before real data exists. Every title is made from
/// short tables of obviously invented syllables; nothing mirrors any real collection. A seed always gives the same
/// collection, with ascending identifiers that have gaps like real data.
/// </summary>
public static class SyntheticCollections
{
    private const int FirstBggId = 10000;
    private const long FirstCollectionId = 500000;
    private const ulong SampleSeedBase = 0x5EED0CABUL << 16;
    private const int MaxBggGap = 40;
    private const int MaxCollectionGap = 30;
    private const int FirstAbsentBaseId = 100;
    private const int MaxFamilySize = 10;
    private const int PopularityFactor = 8;
    private const int OrphanPercent = 15;
    private const int TwoParentPercent = 5;
    private const int LongTitleLength = 58;
    private const int LargeSampleExpansionPercent = 18;
    private const string EdgeSampleName = "edge";
    private const string ReviewSampleName = "65";
    private const string LargeSampleName = "400";
    private const int BigFamilyBase = 10;
    private const int SmallFamilyBase = 20;
    private const int SingleFamilyBase = 27;
    private const int SingleFamilyExpansion = 40;
    private const int TwoParentExpansion = 36;
    private const int TwoParentFirst = 5;
    private const int TwoParentSecond = 30;
    private const int LongTitleOrphan = 29;
    private const int MixExpansionPercent = 25;
    private const int MixMaxFamilySize = 8;
    private const int MixMinSeriesCount = 2;
    private const int MixSeriesPerBaseGames = 60;
    private const int MixSeriesMinLength = 2;
    private const int MixSeriesMaxLength = 4;
    private const ulong MixSeedMask = 0x51E3D1C5UL;

    private static readonly string[] FirstSyllables =
    [
        "Brin", "Voss", "Quay", "Kel", "Tarn", "Zim", "Drel", "Sul", "Orv", "Pell", "Tav", "Grov",
        "Hesk", "Lum", "Nox", "Yar", "Wend", "Cass", "Jor", "Ulm", "Rax", "Bel", "Mor", "Eld",
    ];

    private static readonly string[] SecondSyllables =
    [
        "dle", "mere", "ford", "wyn", "ra", "lo", "than", "vex", "dis", "mont", "ley", "quin", "bar", "sk",
        "zel", "tor", "nia", "ven",
    ];

    private static readonly string[] Connectors = ["of", "the", "and"];

    private static readonly Dictionary<string, int> SampleSizes = new(StringComparer.Ordinal)
    {
        ["0"] = 0,
        ["1"] = 1,
        ["5"] = 5,
        ["12"] = 12,
        [ReviewSampleName] = 65,
        ["400"] = 400,
    };

    private static readonly (int Width, int Height, int Depth)[] OversizeFronts =
    [
        (430, 420, 70),
        (520, 380, 90),
        (610, 500, 60),
        (380, 560, 110),
        (700, 410, 80),
        (450, 450, 140),
    ];

    private const int FirstSeriesFamilyId = 9000;

    private static readonly int[][] ReviewSeriesBases = [[4, 18, 33], [9, 41]];

    private static readonly int[][] LargeSeriesBases =
    [
        [10, 45, 90, 140, 190, 230, 280],
        [5, 55, 120],
        [20, 70],
        [30, 100, 180, 240],
        [15, 65, 135],
        [25, 85],
    ];

    private static readonly (int Share, Func<SplitMix64, BoxDimensions>? Make)[] MixBaseBoxes =
    [
        (30, null),
        (20, SmallCardBox),
        (15, StandardBox),
        (20, SquareBox),
        (10, TallLargeBox),
        (5, LandscapeBox),
    ];

    private static readonly (int Share, Func<SplitMix64, BoxDimensions>? Make)[] MixExpansionBoxes =
    [
        (40, null),
        (40, SquareExpansionBox),
        (20, SmallExpansionBox),
    ];

    private static readonly int[] ReviewOversizeBases = [3, 31];
    private static readonly int[] LargeOversizeBases = [9, 60, 100, 150, 210, 270];

    private static readonly int[] ReviewBigFamilyExpansions = [12, 15, 19, 23, 24, 31, 38, 44, 52];
    private static readonly int[] ReviewSmallFamilyExpansions = [17, 33];
    private static readonly int[] ReviewOrphans = [8, 29, 50];

    /// <summary>
    /// The sample names a visitor may ask for. Numeric names are also their item count; the edge sample is a small
    /// set of games with awkward titles for checking how labels cope.
    /// </summary>
    public static IReadOnlyList<string> SampleNames { get; } = ["0", "1", "5", "12", "65", "400", EdgeSampleName];

    /// <summary>
    /// Finds a named sample by exact, case-sensitive name; anything else finds nothing. The sample of sixty-five items is
    /// made like a real collection of that size: forty-nine base games and sixteen expansions, with one family of nine
    /// expansions, one of two, one of one, one expansion for two owned games and three expansions whose base game is not
    /// owned. The sample of four hundred has about a fifth of its items as expansions in families of up to ten. Both
    /// carry a few base games whose boxes are larger than any section can hold, so the engine's scaling is exercised, and
    /// a few invented series: the sample of sixty-five has one of three base games and one of two, the sample of four
    /// hundred has six, the longest of seven base games.
    /// </summary>
    public static bool TryGetSample(string? name, out IReadOnlyList<CabinetItem> items)
    {
        if (string.Equals(name, EdgeSampleName, StringComparison.Ordinal))
        {
            items = CreateEdgeSample();

            return true;
        }

        if (name is not null && SampleSizes.TryGetValue(name, out var count))
        {
            var generator = new SplitMix64(SampleSeedBase + (ulong)count);
            var titles = new HashSet<string>(StringComparer.Ordinal);

            var roles = name switch
            {
                ReviewSampleName => ScriptedRoles(generator, titles),
                LargeSampleName => RandomRoles(generator, titles, LargeSampleExpansionPercent),
                _ => RandomRoles(generator, titles, 0),
            };

            var assembled = Assemble(generator, titles, count, roles);

            items = name switch
            {
                ReviewSampleName => WithSeries(WithOversizeBoxes(assembled, ReviewOversizeBases), ReviewSeriesBases),
                LargeSampleName => WithSeries(WithOversizeBoxes(assembled, LargeOversizeBases), LargeSeriesBases),
                _ => assembled,
            };

            return true;
        }

        items = [];

        return false;
    }

    /// <summary>
    /// A collection of the given size made from the caller's seed. With an expansion percentage above zero, that share of
    /// the items are expansions: most name an earlier base game they extend, about one in seven names a base game that is
    /// not in the collection, and a few extend two owned base games. A family never grows past ten expansions.
    /// </summary>
    /// <param name="seed">The seed.</param>
    /// <param name="count">The number of items.</param>
    /// <param name="expansionPercent">The share of items that are expansions, from 0 to 100; zero gives base games only.</param>
    public static IReadOnlyList<CabinetItem> Random(int seed, int count, int expansionPercent = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfNegative(expansionPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(expansionPercent, 100);

        var generator = new SplitMix64(unchecked((ulong)seed));
        var titles = new HashSet<string>(StringComparer.Ordinal);

        return Assemble(generator, titles, count, RandomRoles(generator, titles, expansionPercent));
    }

    /// <summary>
    /// A seeded collection shaped like a real hobby collection, built from coarse bands only: about thirty percent of the base
    /// games have no known size so the default box applies, a fifth are small card boxes, a sixth standard portrait boxes,
    /// a fifth large squares, a tenth tall large boxes and one in twenty a wide landscape front; expansions are mostly large
    /// squares or the default box. A quarter of the items are expansions in families of up to eight, a few of them with two
    /// or more, and a few invented series stand among the base games. Sizes are rounded to ten millimetres and shares to
    /// five percentage points, and the same seed always gives the same collection.
    /// </summary>
    /// <param name="seed">The seed.</param>
    /// <param name="count">The number of items.</param>
    public static IReadOnlyList<CabinetItem> SizeMix(int seed, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var generator = new SplitMix64(unchecked((ulong)seed));
        var titles = new HashSet<string>(StringComparer.Ordinal);
        var assembled = Assemble(generator, titles, count, RandomRoles(generator, titles, MixExpansionPercent, MixMaxFamilySize));
        var sizing = new SplitMix64(unchecked((ulong)seed) ^ MixSeedMask);
        var sized = assembled
            .Select(item => item with { Box = MixBoxFor(sizing, item.Kind), PoseHeightMm = null })
            .Select(item => item with { PoseHeightMm = item.Box.WidthMm > item.Box.HeightMm ? item.Box.WidthMm : null })
            .ToList();

        return WithSeries(sized, MixSeries(sizing, sized.Count(item => item.Kind == ItemKind.Base)));
    }

    /// <summary>A new base game whose collection and game identifiers are above every existing one and whose title is unused.</summary>
    public static CabinetItem NextBaseGame(IReadOnlyList<CabinetItem> items, int seed)
    {
        ArgumentNullException.ThrowIfNull(items);

        var generator = new SplitMix64(unchecked((ulong)seed));
        var titles = UsedTitles(items);
        var (bggId, collectionId) = NextIdentifiers(items, generator);

        return CreateItem(generator, bggId, collectionId, UniqueTitle(generator, titles));
    }

    /// <summary>
    /// A new expansion for the given owned base game, whose collection and game identifiers are above every existing one
    /// and whose title is unused, the way a game bought later arrives.
    /// </summary>
    /// <exception cref="ArgumentException">The collection holds no base game with that identifier.</exception>
    public static CabinetItem NextExpansion(IReadOnlyList<CabinetItem> items, int baseBggId, int seed)
    {
        ArgumentNullException.ThrowIfNull(items);

        var parent = items.FirstOrDefault(item => item.BggId == baseBggId && item.Kind == ItemKind.Base)
            ?? throw new ArgumentException($"The collection holds no base game {baseBggId}.", nameof(baseBggId));
        var generator = new SplitMix64(unchecked((ulong)seed));
        var titles = UsedTitles(items);
        var (bggId, collectionId) = NextIdentifiers(items, generator);
        var title = UniqueTitle(generator, titles);

        return new CabinetItem(
            bggId,
            collectionId,
            title,
            ItemKind.Expansion,
            CreateExpansionBox(generator),
            [new BaseGameRef(parent.BggId, parent.Title)]);
    }

    /// <summary>
    /// A new expansion for a base game the collection does not own, whose collection and game identifiers are above every
    /// existing one and whose title and base game title are invented and unused.
    /// </summary>
    public static CabinetItem NextOrphanExpansion(IReadOnlyList<CabinetItem> items, int seed)
    {
        ArgumentNullException.ThrowIfNull(items);

        var generator = new SplitMix64(unchecked((ulong)seed));
        var titles = UsedTitles(items);
        var (bggId, collectionId) = NextIdentifiers(items, generator);
        var title = UniqueTitle(generator, titles);
        var absent = new BaseGameRef(generator.NextInt(FirstAbsentBaseId, FirstBggId), UniqueTitle(generator, titles));

        return new CabinetItem(bggId, collectionId, title, ItemKind.Expansion, CreateExpansionBox(generator), [absent]);
    }

    private static HashSet<string> UsedTitles(IReadOnlyList<CabinetItem> items) =>
        items
            .SelectMany(item => item.ExpansionOf.Select(reference => reference.Title).Append(item.Title))
            .ToHashSet(StringComparer.Ordinal);

    private static (int BggId, long CollectionId) NextIdentifiers(IReadOnlyList<CabinetItem> items, SplitMix64 generator)
    {
        var bggId = items.Count == 0 ? FirstBggId : items.Max(item => item.BggId) + 1 + generator.NextInt(0, MaxBggGap);
        var collectionId = items.Count == 0
            ? FirstCollectionId
            : items.Max(item => item.CollectionId) + 1 + generator.NextInt(0, MaxCollectionGap);

        return (bggId, collectionId);
    }

    private static List<CabinetItem> CreateEdgeSample()
    {
        string[] titles =
        [
            "Brindle Vossmere Quaymont and the Tarnwyn Zimdrel of Orvley Pellford Tavquin Grovmere Heskra Lumthan Noxdle",
            "Kelmont: The Vossmere Accord",
            "Tarnwyn - Brindle Reborn",
            "Zimdrelsulorvpelltavgrovheskluminox",
            "ブリンドルゲーム",
            "Voss \U0001F3B2 Tarn",
            "ברינדל",
            "Orvä Kelmont",
            "",
            "Drel",
            "Pell Tav Grov",
            "Hesk of the Lum",
            "Nox: Yar and Wend",
            "Cassjorulm",
        ];
        int[] heights = [210, 380, 150, 260, 320, 190, 300, 240, 340, 130, 280, 220, 360, 200];
        int[] depths = [45, 100, 25, 60, 80, 35, 55, 40, 90, 22, 70, 50, 105, 30];

        return titles
            .Select((title, index) => new CabinetItem(
                FirstBggId + (index * 7),
                FirstCollectionId + (index * 3),
                title,
                ItemKind.Base,
                new BoxDimensions(heights[index] * 3 / 4, heights[index], depths[index]),
                []))
            .ToList();
    }

    /// <summary>
    /// What an item is when it is an expansion: the positions of the owned base games it extends, or the base game it
    /// names that is not owned.
    /// </summary>
    private sealed record Role(IReadOnlyList<int> ParentPositions, BaseGameRef? Absent);

    private sealed record Draft(int BggId, long CollectionId, string Title, BoxDimensions Box, Role? Role);

    private delegate Role? RoleChooser(int position);

    private static List<CabinetItem> Assemble(SplitMix64 generator, HashSet<string> titles, int count, RoleChooser chooseRole)
    {
        var drafts = new List<Draft>(count);
        var bggId = FirstBggId;
        var collectionId = FirstCollectionId;

        for (var position = 0; position < count; position++)
        {
            var title = UniqueTitle(generator, titles);
            var role = chooseRole(position);
            var box = role is null ? CreateBox(generator) : CreateExpansionBox(generator);

            drafts.Add(new Draft(bggId, collectionId, title, box, role));
            bggId += 1 + generator.NextInt(0, MaxBggGap);
            collectionId += 1 + generator.NextInt(0, MaxCollectionGap);
        }

        return drafts.Select(draft => ToItem(draft, drafts)).ToList();
    }

    /// <summary>
    /// Gives the base games at the listed positions, counting base games only from zero, oversize boxes taken in turn
    /// from a fixed list. Nothing else about the items changes, so the counts and the composition stay the same.
    /// </summary>
    private static List<CabinetItem> WithOversizeBoxes(List<CabinetItem> items, int[] baseOrdinals)
    {
        var baseIndexes = Enumerable.Range(0, items.Count).Where(index => items[index].Kind == ItemKind.Base).ToList();
        var result = new List<CabinetItem>(items);

        for (var turn = 0; turn < baseOrdinals.Length; turn++)
        {
            var index = baseIndexes[baseOrdinals[turn]];
            var (width, height, depth) = OversizeFronts[turn % OversizeFronts.Length];

            result[index] = result[index] with { Box = new BoxDimensions(width, height, depth) };
        }

        return result;
    }

    /// <summary>
    /// Puts the base games at the listed positions, counting base games only from zero, into invented series: each list is
    /// one series family, numbered from 9000. Nothing else about the items changes, so the counts and the composition stay
    /// the same.
    /// </summary>
    private static List<CabinetItem> WithSeries(List<CabinetItem> items, int[][] seriesBaseOrdinals)
    {
        var baseIndexes = Enumerable.Range(0, items.Count).Where(index => items[index].Kind == ItemKind.Base).ToList();
        var result = new List<CabinetItem>(items);

        for (var series = 0; series < seriesBaseOrdinals.Length; series++)
        {
            foreach (var ordinal in seriesBaseOrdinals[series])
            {
                var index = baseIndexes[ordinal];

                result[index] = result[index] with { SeriesFamilies = [FirstSeriesFamilyId + series] };
            }
        }

        return result;
    }

    private static CabinetItem ToItem(Draft draft, List<Draft> drafts)
    {
        if (draft.Role is null)
        {
            return new CabinetItem(draft.BggId, draft.CollectionId, draft.Title, ItemKind.Base, draft.Box, []);
        }

        IReadOnlyList<BaseGameRef> parents = draft.Role.Absent is not null
            ? [draft.Role.Absent]
            : draft.Role.ParentPositions.Select(position => new BaseGameRef(drafts[position].BggId, drafts[position].Title)).ToList();

        return new CabinetItem(draft.BggId, draft.CollectionId, draft.Title, ItemKind.Expansion, draft.Box, parents);
    }

    private static BoxDimensions MixBoxFor(SplitMix64 generator, ItemKind kind)
    {
        var bands = kind == ItemKind.Expansion ? MixExpansionBoxes : MixBaseBoxes;
        var roll = generator.NextInt(0, 100);

        foreach (var (share, make) in bands)
        {
            roll -= share;

            if (roll < 0)
            {
                return make is null ? BoxFromVersion.DefaultFor(kind) : make(generator);
            }
        }

        return BoxFromVersion.DefaultFor(kind);
    }

    private static int[][] MixSeries(SplitMix64 generator, int baseGames)
    {
        var seriesCount = Math.Max(MixMinSeriesCount, baseGames / MixSeriesPerBaseGames);
        var taken = new HashSet<int>();
        var series = new List<int[]>();

        for (var index = 0; index < seriesCount; index++)
        {
            var length = generator.NextInt(MixSeriesMinLength, MixSeriesMaxLength + 1);
            var members = new List<int>();

            while (members.Count < length && taken.Count < baseGames)
            {
                var ordinal = generator.NextInt(0, baseGames);

                if (taken.Add(ordinal))
                {
                    members.Add(ordinal);
                }
            }

            if (members.Count >= MixSeriesMinLength)
            {
                series.Add([.. members]);
            }
        }

        return [.. series];
    }

    private static BoxDimensions SmallCardBox(SplitMix64 generator) =>
        RoundedBox(generator.NextInt(80, 151), generator.NextInt(110, 191), generator.NextInt(20, 61));

    private static BoxDimensions StandardBox(SplitMix64 generator) =>
        RoundedBox(generator.NextInt(150, 201), generator.NextInt(200, 251), generator.NextInt(40, 71));

    private static BoxDimensions SquareBox(SplitMix64 generator)
    {
        var side = generator.NextInt(0, 2) == 0 ? 290 : 300;

        return new BoxDimensions(side, side, RoundTen(generator.NextInt(40, 141)));
    }

    private static BoxDimensions TallLargeBox(SplitMix64 generator) =>
        RoundedBox(generator.NextInt(250, 301), generator.NextInt(300, 441), generator.NextInt(50, 111));

    private static BoxDimensions LandscapeBox(SplitMix64 generator) =>
        RoundedBox(generator.NextInt(350, 411), generator.NextInt(250, 281), generator.NextInt(50, 101));

    private static BoxDimensions SquareExpansionBox(SplitMix64 generator)
    {
        var side = generator.NextInt(0, 2) == 0 ? 290 : 300;

        return new BoxDimensions(side, side, RoundTen(generator.NextInt(20, 81)));
    }

    private static BoxDimensions SmallExpansionBox(SplitMix64 generator) =>
        RoundedBox(generator.NextInt(100, 191), generator.NextInt(150, 251), generator.NextInt(20, 61));

    private static BoxDimensions RoundedBox(int width, int height, int depth) =>
        new(RoundTen(width), RoundTen(height), RoundTen(depth));

    private static int RoundTen(int value) => (value + 5) / 10 * 10;

    private static RoleChooser RandomRoles(SplitMix64 generator, HashSet<string> titles, int expansionPercent, int maxFamilySize = MaxFamilySize)
    {
        var basePositions = new List<int>();
        var familySizes = new Dictionary<int, int>();

        return position =>
        {
            var role = expansionPercent > 0 && basePositions.Count > 0 && generator.NextInt(0, 100) < expansionPercent
                ? ChooseExpansionRole(generator, titles, basePositions, familySizes, maxFamilySize)
                : null;

            if (role is null)
            {
                basePositions.Add(position);
                familySizes[position] = 0;
            }
            else
            {
                foreach (var parent in role.ParentPositions)
                {
                    familySizes[parent]++;
                }
            }

            return role;
        };
    }

    private static Role? ChooseExpansionRole(
        SplitMix64 generator,
        HashSet<string> titles,
        List<int> basePositions,
        Dictionary<int, int> familySizes,
        int maxFamilySize)
    {
        var kindRoll = generator.NextInt(0, 100);

        if (kindRoll < OrphanPercent)
        {
            var absent = new BaseGameRef(generator.NextInt(FirstAbsentBaseId, FirstBggId), UniqueTitle(generator, titles));

            return new Role([], absent);
        }

        var first = PickFamily(generator, basePositions, familySizes, maxFamilySize, excluded: -1);

        if (first < 0)
        {
            return null;
        }

        if (kindRoll < OrphanPercent + TwoParentPercent)
        {
            var second = PickFamily(generator, basePositions, familySizes, maxFamilySize, excluded: first);

            if (second >= 0)
            {
                return new Role([second, first], null);
            }
        }

        return new Role([first], null);
    }

    /// <summary>
    /// Picks the base game a new expansion extends. A game's chance grows with the square of the expansions it already
    /// has, so a few popular games gather big families while most have none, like a real collection. Games with a full
    /// family are never picked; returns -1 when no game can take another expansion.
    /// </summary>
    private static int PickFamily(SplitMix64 generator, List<int> basePositions, Dictionary<int, int> familySizes, int maxFamilySize, int excluded)
    {
        var candidates = basePositions
            .Where(position => position != excluded && familySizes[position] < maxFamilySize)
            .ToList();

        if (candidates.Count == 0)
        {
            return -1;
        }

        var total = candidates.Sum(position => PopularityWeight(familySizes[position]));
        var roll = generator.NextInt(0, total);

        foreach (var position in candidates)
        {
            roll -= PopularityWeight(familySizes[position]);

            if (roll < 0)
            {
                return position;
            }
        }

        return candidates[^1];
    }

    private static int PopularityWeight(int familySize) => 1 + (PopularityFactor * familySize * familySize);

    private static RoleChooser ScriptedRoles(SplitMix64 generator, HashSet<string> titles)
    {
        var roles = new Dictionary<int, Role>();

        foreach (var position in ReviewBigFamilyExpansions)
        {
            roles[position] = new Role([BigFamilyBase], null);
        }

        foreach (var position in ReviewSmallFamilyExpansions)
        {
            roles[position] = new Role([SmallFamilyBase], null);
        }

        roles[SingleFamilyExpansion] = new Role([SingleFamilyBase], null);
        roles[TwoParentExpansion] = new Role([TwoParentSecond, TwoParentFirst], null);

        foreach (var position in ReviewOrphans)
        {
            var absentTitle = position == LongTitleOrphan ? LongTitle(generator, titles) : UniqueTitle(generator, titles);
            roles[position] = new Role([], new BaseGameRef(generator.NextInt(FirstAbsentBaseId, FirstBggId), absentTitle));
        }

        return position => roles.GetValueOrDefault(position);
    }

    private static string LongTitle(SplitMix64 generator, HashSet<string> used)
    {
        string title;

        do
        {
            title = MakeWord(generator);

            while (title.Length < LongTitleLength)
            {
                title += ' ' + MakeWord(generator);
            }
        }
        while (!used.Add(title));

        return title;
    }

    private static CabinetItem CreateItem(SplitMix64 generator, int bggId, long collectionId, string title) =>
        new(bggId, collectionId, title, ItemKind.Base, CreateBox(generator), []);

    private static BoxDimensions CreateBox(SplitMix64 generator)
    {
        var sizeRoll = generator.NextInt(0, 100);
        var (height, depth) = sizeRoll switch
        {
            < 25 => (generator.NextInt(120, 200), generator.NextInt(20, 46)),
            < 75 => (generator.NextInt(200, 320), generator.NextInt(40, 81)),
            _ => (generator.NextInt(320, 391), generator.NextInt(60, 111)),
        };
        var width = height * generator.NextInt(70, 101) / 100;

        return new BoxDimensions(width, height, depth);
    }

    private static BoxDimensions CreateExpansionBox(SplitMix64 generator)
    {
        var height = generator.NextInt(150, 301);
        var depth = generator.NextInt(20, 61);
        var width = height * generator.NextInt(70, 101) / 100;

        return new BoxDimensions(width, height, depth);
    }

    private static string UniqueTitle(SplitMix64 generator, HashSet<string> used)
    {
        string title;

        do
        {
            title = MakeTitle(generator);
        }
        while (!used.Add(title));

        return title;
    }

    private static string MakeTitle(SplitMix64 generator)
    {
        var title = string.Join(' ', Enumerable.Range(0, generator.NextInt(1, 4)).Select(_ => MakeWord(generator)));

        if (generator.NextInt(0, 4) != 0)
        {
            return title;
        }

        var subtitleWords = Enumerable.Range(0, generator.NextInt(2, 5)).Select(_ => MakeSubtitleWord(generator));

        return $"{title}: {string.Join(' ', subtitleWords)}";
    }

    private static string MakeSubtitleWord(SplitMix64 generator) =>
        generator.NextInt(0, 5) == 0 ? Connectors[generator.NextInt(0, Connectors.Length)] : MakeWord(generator);

    private static string MakeWord(SplitMix64 generator)
    {
        var word = FirstSyllables[generator.NextInt(0, FirstSyllables.Length)]
            + SecondSyllables[generator.NextInt(0, SecondSyllables.Length)];

        return generator.NextInt(0, 4) == 0 ? word + SecondSyllables[generator.NextInt(0, SecondSyllables.Length)] : word;
    }
}

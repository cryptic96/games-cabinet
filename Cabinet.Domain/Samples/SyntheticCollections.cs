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
    /// carry a few base games whose boxes are larger than any section can hold, so the engine's scaling is exercised.
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
                ReviewSampleName => WithOversizeBoxes(assembled, ReviewOversizeBases),
                LargeSampleName => WithOversizeBoxes(assembled, LargeOversizeBases),
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

    private static RoleChooser RandomRoles(SplitMix64 generator, HashSet<string> titles, int expansionPercent)
    {
        var basePositions = new List<int>();
        var familySizes = new Dictionary<int, int>();

        return position =>
        {
            var role = expansionPercent > 0 && basePositions.Count > 0 && generator.NextInt(0, 100) < expansionPercent
                ? ChooseExpansionRole(generator, titles, basePositions, familySizes)
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
        Dictionary<int, int> familySizes)
    {
        var kindRoll = generator.NextInt(0, 100);

        if (kindRoll < OrphanPercent)
        {
            var absent = new BaseGameRef(generator.NextInt(FirstAbsentBaseId, FirstBggId), UniqueTitle(generator, titles));

            return new Role([], absent);
        }

        var first = PickFamily(generator, basePositions, familySizes, excluded: -1);

        if (first < 0)
        {
            return null;
        }

        if (kindRoll < OrphanPercent + TwoParentPercent)
        {
            var second = PickFamily(generator, basePositions, familySizes, excluded: first);

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
    private static int PickFamily(SplitMix64 generator, List<int> basePositions, Dictionary<int, int> familySizes, int excluded)
    {
        var candidates = basePositions
            .Where(position => position != excluded && familySizes[position] < MaxFamilySize)
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

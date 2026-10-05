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
    private const string EdgeSampleName = "edge";

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
        ["65"] = 65,
        ["400"] = 400,
    };

    /// <summary>
    /// The sample names a visitor may ask for. Numeric names are also their item count; the edge sample is a small
    /// set of games with awkward titles for checking how labels cope.
    /// </summary>
    public static IReadOnlyList<string> SampleNames { get; } = ["0", "1", "5", "12", "65", "400", EdgeSampleName];

    /// <summary>Finds a named sample by exact, case-sensitive name; anything else finds nothing.</summary>
    public static bool TryGetSample(string? name, out IReadOnlyList<CabinetItem> items)
    {
        if (string.Equals(name, EdgeSampleName, StringComparison.Ordinal))
        {
            items = CreateEdgeSample();

            return true;
        }

        if (name is not null && SampleSizes.TryGetValue(name, out var count))
        {
            items = Generate(new SplitMix64(SampleSeedBase + (ulong)count), count);

            return true;
        }

        items = [];

        return false;
    }

    /// <summary>A collection of the given size made from the caller's seed.</summary>
    public static IReadOnlyList<CabinetItem> Random(int seed, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return Generate(new SplitMix64(unchecked((ulong)seed)), count);
    }

    /// <summary>A new base game whose collection and game identifiers are above every existing one and whose title is unused.</summary>
    public static CabinetItem NextBaseGame(IReadOnlyList<CabinetItem> items, int seed)
    {
        ArgumentNullException.ThrowIfNull(items);

        var generator = new SplitMix64(unchecked((ulong)seed));
        var titles = items.Select(item => item.Title).ToHashSet(StringComparer.Ordinal);
        var bggId = items.Count == 0 ? FirstBggId : items.Max(item => item.BggId) + 1 + generator.NextInt(0, MaxBggGap);
        var collectionId = items.Count == 0
            ? FirstCollectionId
            : items.Max(item => item.CollectionId) + 1 + generator.NextInt(0, MaxCollectionGap);

        return CreateItem(generator, bggId, collectionId, UniqueTitle(generator, titles));
    }

    private static List<CabinetItem> CreateEdgeSample()
    {
        string[] titles =
        [
            "Brindle Vossmere Quaymont and the Tarnwyn Zimdrel of Orvley Pellford Tavquin Grovmere Heskra Lumthan Noxdle",
            "Kelmont: The Vossmere Accord",
            "Tarnwyn - Brindle Reborn",
            "Zimdrelsulorvpelltavgrovheskluminox",
            "\u30D6\u30EA\u30F3\u30C9\u30EB\u30B2\u30FC\u30E0",
            "Voss \U0001F3B2 Tarn",
            "\u05D1\u05E8\u05D9\u05E0\u05D3\u05DC",
            "Orva\u0308 Kelmont",
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

    private static List<CabinetItem> Generate(SplitMix64 generator, int count)
    {
        var titles = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<CabinetItem>(count);
        var bggId = FirstBggId;
        var collectionId = FirstCollectionId;

        for (var index = 0; index < count; index++)
        {
            items.Add(CreateItem(generator, bggId, collectionId, UniqueTitle(generator, titles)));
            bggId += 1 + generator.NextInt(0, MaxBggGap);
            collectionId += 1 + generator.NextInt(0, MaxCollectionGap);
        }

        return items;
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

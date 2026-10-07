namespace Cabinet.FakeBgg;

/// <summary>Box dimensions of an invented game version, in the inches BoardGameGeek reports; zero means never entered.</summary>
/// <param name="Width">The front width of the box.</param>
/// <param name="Length">The standing height of the box.</param>
/// <param name="Depth">The thickness of the box.</param>
public sealed record FakeVersion(double Width, double Length, double Depth);

/// <summary>One invented entry in the fake owner's collection.</summary>
/// <param name="ObjectId">The game's id; a game owned twice shares it across both entries.</param>
/// <param name="CollId">The id of this collection entry; unique per copy.</param>
/// <param name="Title">The primary name, which may be blank or contain characters that need XML escaping.</param>
/// <param name="IsExpansion">Whether the game is an expansion.</param>
/// <param name="Owned">Whether the entry is marked as owned.</param>
/// <param name="Year">The publication year, or null when it is not known.</param>
/// <param name="Version">The selected version's box dimensions, or null when no version is selected.</param>
/// <param name="Location">The private inventory location, or null when none is set.</param>
/// <param name="BaseObjectId">For an expansion, the id of the game it expands; the base game may be absent from the collection.</param>
public sealed record FakeBggItem(
    int ObjectId,
    long CollId,
    string Title,
    bool IsExpansion,
    bool Owned,
    int? Year,
    FakeVersion? Version,
    string? Location,
    int? BaseObjectId = null);

/// <summary>Builds deterministic, entirely invented owner collections of a few fixed sizes.</summary>
public static class SyntheticBggCollection
{
    /// <summary>The collection sizes the fake offers.</summary>
    public static IReadOnlyList<int> Sizes { get; } = [0, 1, 5, 65, 400];

    /// <summary>The first game id handed out.</summary>
    public const int FirstObjectId = 100001;

    /// <summary>The first collection entry id handed out.</summary>
    public const long FirstCollId = 5000001;

    /// <summary>The first game version id handed out.</summary>
    public const int FirstVersionId = 900001;

    private const int MissingBaseObjectId = 190001;

    /// <summary>
    /// Creates a collection in collection-id order. The list holds exactly the requested number of entries, unowned
    /// ones included. A size that is not offered is clamped to the nearest offered size. The first entries are
    /// hand-picked edge cases (a game owned twice, an unowned entry, expansions with and without their base game in
    /// the collection, an escaped character, a non-Latin title, a blank title, versions with and without
    /// dimensions, two locations); the rest follow a regular pattern.
    /// </summary>
    /// <param name="size">The wanted number of entries.</param>
    public static IReadOnlyList<FakeBggItem> Create(int size)
    {
        var clamped = Clamp(size);
        var items = new List<FakeBggItem>(clamped);

        foreach (var edgeCase in EdgeCases())
        {
            if (items.Count == clamped)
            {
                break;
            }

            items.Add(edgeCase);
        }

        var lastBaseObjectId = items.LastOrDefault(item => !item.IsExpansion && item.Owned)?.ObjectId ?? FirstObjectId;
        while (items.Count < clamped)
        {
            var index = items.Count;
            var item = IsGeneratedExpansion(index)
                ? Generated(index, isExpansion: true, lastBaseObjectId)
                : Generated(index, isExpansion: false, baseObjectId: null);
            if (!item.IsExpansion)
            {
                lastBaseObjectId = item.ObjectId;
            }

            items.Add(item);
        }

        return items;
    }

    /// <summary>Returns the offered size nearest to the requested one; a tie goes to the smaller size.</summary>
    /// <param name="size">The wanted number of entries.</param>
    public static int Clamp(int size) =>
        Sizes.OrderBy(offered => Math.Abs(offered - size)).ThenBy(offered => offered).First();

    private static IEnumerable<FakeBggItem> EdgeCases()
    {
        yield return Entry(0, "Example Game 1", false, true, 1998, Dimensions(6.3, 8.27, 2.09), "Shelf A");
        yield return Entry(1, "Lantern & Harbour", false, true, 2004, Dimensions(9.5, 11.75, 3.1), "Shelf B");
        yield return Entry(2, "Example Game 1", false, true, 1998, Dimensions(6.3, 8.27, 2.09), null, objectIndex: 0);
        yield return Entry(3, "Example Game 1: Lantern Extras", true, true, 1999, Dimensions(7.56, 10.0, 1.54), null, baseObjectId: ObjectId(0));
        yield return Entry(4, "Copper Orchard", false, false, 2011, Dimensions(11.61, 11.61, 2.36), null);
        yield return Entry(5, "Distant Orchard: Wind Pack", true, true, 2012, Dimensions(0, 0, 0), null, baseObjectId: MissingBaseObjectId);
        yield return Entry(6, "港の灯台", false, true, 2007, Dimensions(4.5, 5.1, 1.2), null);
        yield return Entry(7, string.Empty, false, true, null, Dimensions(5.25, 7.0, 4.4), null);
        yield return Entry(8, "Quiet Quarry", false, true, 2019, Dimensions(0, 0, 0), null);
        yield return Entry(9, "Ribbon Rally", false, true, 1987, null, null);
    }

    private static FakeBggItem Entry(
        int index,
        string title,
        bool isExpansion,
        bool owned,
        int? year,
        FakeVersion? version,
        string? location,
        int? baseObjectId = null,
        int? objectIndex = null) =>
        new(
            ObjectId(objectIndex ?? index),
            FirstCollId + index,
            title,
            isExpansion,
            owned,
            year,
            version,
            location,
            baseObjectId);

    private static FakeBggItem Generated(int index, bool isExpansion, int? baseObjectId)
    {
        var title = isExpansion
            ? $"Example Game {index + 1}: Extra Pack"
            : $"Example Game {index + 1}";
        var year = index % 11 == 0 ? (int?)null : 1990 + (index % 35);
        return Entry(index, title, isExpansion, true, year, GeneratedVersion(index, isExpansion), null, baseObjectId);
    }

    private static FakeVersion? GeneratedVersion(int index, bool isExpansion)
    {
        if (index % 17 == 5)
        {
            return null;
        }

        if (index % 10 == 3)
        {
            return Dimensions(0, 0, 0);
        }

        if (isExpansion)
        {
            var width = 7.5 + (index * 3 % 41) / 10.0;
            return Dimensions(width, width, 1.5 + (index % 16) / 10.0);
        }

        var baseWidth = 4.0 + (index * 37 % 85) / 10.0;
        return Dimensions(baseWidth, baseWidth + 0.5 + (index * 13 % 60) / 10.0, 1.0 + (index * 7 % 55) / 10.0);
    }

    private static bool IsGeneratedExpansion(int index) => index % 5 == 4;

    private static int ObjectId(int index) => FirstObjectId + index;

    private static FakeVersion Dimensions(double width, double length, double depth) =>
        new(Math.Round(width, 2), Math.Round(length, 2), Math.Round(depth, 2));
}

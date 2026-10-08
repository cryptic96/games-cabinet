namespace Cabinet.Domain.Layout;

/// <summary>One series of top-level games, or a game that belongs to none.</summary>
/// <param name="AnchorGameId">The game identifier of the earliest entry of the series; a game in no series is its own anchor.</param>
/// <param name="Indices">The positions of the series' games in the list that was grouped, in entry order.</param>
public sealed record SeriesGroup(int AnchorGameId, IReadOnlyList<int> Indices);

/// <summary>
/// Finds which top-level games belong to one series. Two games are in one series when they carry the same BGG family that
/// names a series, or when their titles share a title key (see <see cref="TitleKey"/>), and series are joined through the
/// games they share: if one game is linked to a second and the second to a third, all three are one series. A family that
/// only one owned game carries links nothing, so adding a game can only ever join a series and never split one. Which
/// families name a series is decided by <see cref="IsSeriesFamily"/>; broad families such as themes, components or player
/// counts never do.
/// </summary>
public static class SeriesGrouping
{
    /// <summary>The prefixes of the BGG family names that name a series: a franchise of related games, and a named series of separate games.</summary>
    public static IReadOnlyList<string> SeriesFamilyPrefixes { get; } = ["Game: ", "Series: "];

    private static readonly string[] TitleSeparators = [":", " - ", " \u2013 "];

    /// <summary>Whether a BGG family name names a series, comparing the prefix exactly (ordinal, case-sensitive) after trimming the name.</summary>
    /// <param name="name">The family's name.</param>
    public static bool IsSeriesFamily(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var trimmed = name.Trim();

        return SeriesFamilyPrefixes.Any(prefix => trimmed.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// The part of a title that names its series: the text before the first colon, spaced hyphen or spaced en dash,
    /// whichever comes first, or the whole title when it has none of them. It is trimmed, runs of white space become one
    /// space, and letters are upper-cased with the invariant culture so the comparison ignores case. Null when nothing is left.
    /// </summary>
    /// <param name="title">The display title.</param>
    public static string? TitleKey(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var end = TitleSeparators
            .Select(separator => title.IndexOf(separator, StringComparison.Ordinal))
            .Where(position => position >= 0)
            .DefaultIfEmpty(title.Length)
            .Min();
        var words = title[..end].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return words.Length == 0 ? null : string.Join(' ', words).ToUpperInvariant();
    }

    /// <summary>
    /// Groups the top-level games into series and puts the groups in the order they are placed: by the entry of each
    /// group's earliest game, so a series is placed where its first game would be placed, and the games of a group in entry
    /// order. A game in no series is a group of its own. The result depends only on the games, never on the order they are
    /// listed in, as long as the list is in entry order.
    /// </summary>
    /// <param name="topLevel">The top-level games, ordered by collection entry and then game identifier.</param>
    /// <param name="groupSeries">Whether to group at all; when false every game is a group of its own, in entry order.</param>
    public static IReadOnlyList<SeriesGroup> Group(IReadOnlyList<CabinetItem> topLevel, bool groupSeries = true)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        var roots = Enumerable.Range(0, topLevel.Count).ToArray();

        if (groupSeries)
        {
            foreach (var carriers in FamilyCarriers(topLevel).Values.Where(carriers => carriers.Select(index => topLevel[index].BggId).Distinct().Count() >= 2))
            {
                Join(roots, carriers);
            }

            foreach (var carriers in TitleCarriers(topLevel).Values.Where(carriers => carriers.Count >= 2))
            {
                Join(roots, carriers);
            }
        }

        return
        [
            .. Enumerable.Range(0, topLevel.Count)
                .GroupBy(index => Find(roots, index))
                .OrderBy(group => group.Key)
                .Select(group => new SeriesGroup(topLevel[group.Key].BggId, [.. group.Order()])),
        ];
    }

    private static void Join(int[] roots, List<int> carriers)
    {
        foreach (var index in carriers.Skip(1))
        {
            Union(roots, carriers[0], index);
        }
    }

    private static Dictionary<string, List<int>> TitleCarriers(IReadOnlyList<CabinetItem> topLevel)
    {
        var carriers = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        for (var index = 0; index < topLevel.Count; index++)
        {
            if (TitleKey(topLevel[index].Title) is not { } key)
            {
                continue;
            }

            if (!carriers.TryGetValue(key, out var list))
            {
                list = [];
                carriers[key] = list;
            }

            list.Add(index);
        }

        return carriers;
    }

    private static Dictionary<int, List<int>> FamilyCarriers(IReadOnlyList<CabinetItem> topLevel)
    {
        var carriers = new Dictionary<int, List<int>>();

        for (var index = 0; index < topLevel.Count; index++)
        {
            foreach (var family in (topLevel[index].SeriesFamilies ?? []).Distinct())
            {
                if (!carriers.TryGetValue(family, out var list))
                {
                    list = [];
                    carriers[family] = list;
                }

                list.Add(index);
            }
        }

        return carriers;
    }

    private static int Find(int[] roots, int index)
    {
        while (roots[index] != index)
        {
            roots[index] = roots[roots[index]];
            index = roots[index];
        }

        return index;
    }

    private static void Union(int[] roots, int first, int second)
    {
        var firstRoot = Find(roots, first);
        var secondRoot = Find(roots, second);

        if (firstRoot == secondRoot)
        {
            return;
        }

        roots[Math.Max(firstRoot, secondRoot)] = Math.Min(firstRoot, secondRoot);
    }
}

using Cabinet.Domain.Layout;

namespace Cabinet.Domain.Collection;

/// <summary>
/// Works out which base game each owned expansion stands beside, from the base games its details say it expands. Only an
/// item that came from the expansions call is paired, only an inbound expansion link is read, and a base game that is
/// owned only as an expansion entry is not a base game here.
/// </summary>
public static class ExpansionPairing
{
    /// <summary>
    /// Pairs every owned expansion. When one or more of the games it expands are owned as base games, the result is that one
    /// base game whose earliest collection entry has the lowest collection identifier, so adding another base game later
    /// never moves the expansion. When none is owned, the result is every game it expands in the order the source gave them,
    /// so the expansion can be labelled. An expansion without details has no entries. Base items are never paired.
    /// </summary>
    /// <param name="snapshot">The stored collection with the details known so far.</param>
    /// <returns>The games each expansion expands, by the expansion's collection identifier.</returns>
    public static IReadOnlyDictionary<long, IReadOnlyList<BaseGameRef>> Pair(CollectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var expansionEntries = snapshot.Items
            .Where(item => item.Kind == ItemKind.Expansion)
            .Select(item => item.CollectionId)
            .ToHashSet();
        var earliestEntryByBase = snapshot.Items
            .Where(item => item.Kind == ItemKind.Base && !expansionEntries.Contains(item.CollectionId))
            .GroupBy(item => item.GameId)
            .ToDictionary(group => group.Key, group => group.Min(item => item.CollectionId));
        var games = snapshot.Games;
        var paired = new Dictionary<long, IReadOnlyList<BaseGameRef>>();

        foreach (var expansion in snapshot.Items.Where(item => item.Kind == ItemKind.Expansion))
        {
            if (games is null || !games.TryGetValue(expansion.GameId, out var details) || details.ExpandsGames.Count == 0)
            {
                continue;
            }

            var owned = details.ExpandsGames
                .Where(reference => earliestEntryByBase.ContainsKey(reference.BggId))
                .OrderBy(reference => earliestEntryByBase[reference.BggId])
                .ThenBy(reference => reference.BggId)
                .ToList();

            paired[expansion.CollectionId] = owned.Count > 0 ? [owned[0]] : details.ExpandsGames;
        }

        return paired;
    }
}

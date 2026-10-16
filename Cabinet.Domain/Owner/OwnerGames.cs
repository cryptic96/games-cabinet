namespace Cabinet.Domain.Owner;

/// <summary>
/// Finds and arranges what the owner decided for a game. A decision is for one copy of the game, a collection entry, or for
/// every copy at once, which is the form an older owner-data file has. A copy's own decision wins over the one for every copy.
/// </summary>
public static class OwnerGames
{
    /// <summary>Finds the decision that applies to a copy: its own, else the one that holds for every copy of the game, else none.</summary>
    /// <param name="data">The owner data.</param>
    /// <param name="gameId">The game.</param>
    /// <param name="collectionId">The copy, or null to look for the decision that holds for every copy.</param>
    public static OwnerGame? Find(OwnerData data, int gameId, long? collectionId)
    {
        ArgumentNullException.ThrowIfNull(data);

        OwnerGame? everyCopy = null;

        foreach (var game in data.Games)
        {
            if (game.GameId != gameId)
            {
                continue;
            }

            if (collectionId is { } wanted && game.CollectionId == wanted)
            {
                return game;
            }

            if (game.CollectionId is null)
            {
                everyCopy ??= game;
            }
        }

        return everyCopy;
    }

    /// <summary>
    /// Prepares a game for the edit of one copy: a decision that holds for every copy is replaced by one decision per listed copy
    /// that has none of its own, each with the same location, picture and absence, so that editing one copy moves no other.
    /// Copies that already have their own decision keep it. Nothing changes when the game has no decision for every copy.
    /// </summary>
    /// <param name="data">The owner data.</param>
    /// <param name="gameId">The game.</param>
    /// <param name="copies">The collection entries of the game that exist now.</param>
    public static OwnerData SplitForEdit(OwnerData data, int gameId, IReadOnlyList<long> copies)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(copies);

        var everyCopy = data.Games.FirstOrDefault(game => game.GameId == gameId && game.CollectionId is null);

        if (everyCopy is null)
        {
            return data;
        }

        var withOwn = data.Games
            .Where(game => game.GameId == gameId && game.CollectionId is not null)
            .Select(game => game.CollectionId!.Value)
            .ToHashSet();
        var split = copies
            .Distinct()
            .Where(copy => !withOwn.Contains(copy))
            .Select(copy => everyCopy with { CollectionId = copy });
        var rest = data.Games.Where(game => !ReferenceEquals(game, everyCopy));

        return data with { Games = Ordered(rest.Concat(split)) };
    }

    /// <summary>Puts decisions in their stored order: by game, then by copy, the decision that holds for every copy first.</summary>
    /// <param name="games">The decisions.</param>
    public static IReadOnlyList<OwnerGame> Ordered(IEnumerable<OwnerGame> games)
    {
        ArgumentNullException.ThrowIfNull(games);

        return [.. games.OrderBy(game => game.GameId).ThenBy(game => game.CollectionId ?? long.MinValue)];
    }
}

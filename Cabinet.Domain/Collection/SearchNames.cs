using System.Globalization;
using System.Text.Json;
using Cabinet.Domain.Layout;

namespace Cabinet.Domain.Collection;

/// <summary>
/// The other titles of the owned games, as the page's search reads them. It holds nothing but those titles, keyed by game
/// identifier; no other detail of a game travels with it.
/// </summary>
/// <param name="Names">The other titles of each owned game that has any, in the order the source listed them, keyed by game identifier as text.</param>
public sealed record SearchNamesDocument(IReadOnlyDictionary<string, IReadOnlyList<string>> Names)
{
    /// <summary>The document of a collection that has no other titles to offer.</summary>
    public static SearchNamesDocument Empty { get; } = new(new Dictionary<string, IReadOnlyList<string>>());
}

/// <summary>Builds the index of other titles the page's search reads, from stored data only.</summary>
public static class SearchNames
{
    /// <summary>
    /// Lists the other titles of every game the snapshot owns. A game that is not owned is left out, a title equal to the title
    /// the collection shows for the game is dropped, and a game left with no title is left out. Games come in identifier order.
    /// </summary>
    /// <param name="snapshot">The stored collection, or null when there is none yet.</param>
    public static SearchNamesDocument Build(CollectionSnapshot? snapshot)
    {
        if (snapshot?.Games is null || snapshot.Items.Count == 0)
        {
            return SearchNamesDocument.Empty;
        }

        var titlesOf = snapshot.Items
            .GroupBy(item => item.GameId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Title).ToHashSet(StringComparer.Ordinal));
        var names = new SortedDictionary<int, IReadOnlyList<string>>();

        foreach (var (gameId, titles) in titlesOf)
        {
            var other = (snapshot.Games.GetValueOrDefault(gameId)?.AlternateNames ?? [])
                .Where(name => !titles.Contains(name))
                .ToList();

            if (other.Count > 0)
            {
                names[gameId] = other;
            }
        }

        return new SearchNamesDocument(names.ToDictionary(
            entry => entry.Key.ToString(CultureInfo.InvariantCulture),
            entry => entry.Value));
    }

    /// <summary>Serialises the document with the same options as the layout, so the same names always give the same bytes.</summary>
    /// <param name="document">The document to serialise.</param>
    public static string Serialize(SearchNamesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return JsonSerializer.Serialize(document, LayoutJson.Options);
    }
}

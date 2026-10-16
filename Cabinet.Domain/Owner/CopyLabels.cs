using Cabinet.Domain.Collection;

namespace Cabinet.Domain.Owner;

/// <summary>Names the copies of a game the owner has more than once, so both owner pages tell them apart the same way.</summary>
public static class CopyLabels
{
    /// <summary>The words for the position of one copy among the copies of its game.</summary>
    /// <param name="index">The position of the copy, counted from one.</param>
    /// <param name="count">How many copies the game has.</param>
    public static string Tag(int index, int count) => $"Copy {index} of {count}";

    /// <summary>
    /// The label of each copy of one game: the edition name when it is unique among the copies (case ignored), else the position
    /// words. A game owned once has no label, so its entry is null.
    /// </summary>
    /// <param name="copies">The copies of one game, in collection order.</param>
    public static IReadOnlyList<string?> Of(IReadOnlyList<SnapshotItem> copies)
    {
        ArgumentNullException.ThrowIfNull(copies);

        if (copies.Count < 2)
        {
            return [.. copies.Select(_ => (string?)null)];
        }

        var editions = copies
            .Where(copy => copy.VersionName is not null)
            .GroupBy(copy => copy.VersionName!, StringComparer.OrdinalIgnoreCase)
            .Where(sameName => sameName.Count() == 1)
            .Select(sameName => sameName.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return
        [
            .. copies.Select((copy, index) => copy.VersionName is { } name && editions.Contains(name) ? name : Tag(index + 1, copies.Count)),
        ];
    }
}

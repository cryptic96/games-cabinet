using System.Globalization;

namespace Cabinet.Domain.Owner;

/// <summary>
/// The owner's locations in the owner's order and the location every collection entry is in. It is what the public cards carry
/// so the page can group and filter by place, and its <see cref="Text"/> is part of the collection version so a reorder, a rename
/// or a move reaches a page that is already open.
/// </summary>
/// <param name="Order">The owner's locations, in the order the owner set.</param>
/// <param name="EntryLocations">The identifier of the location of each placed collection entry, keyed by collection entry; an entry that is not placed is absent.</param>
public sealed record LocationBook(IReadOnlyList<OwnerLocation> Order, IReadOnlyDictionary<long, string> EntryLocations)
{
    /// <summary>A book with no locations and no placed entries.</summary>
    public static LocationBook Empty { get; } = new([], new Dictionary<long, string>());

    /// <summary>Reads the book of a collection from what the overlay worked out for each of its entries.</summary>
    /// <param name="overlay">Where the owner keeps each copy, counting the base game an expansion follows.</param>
    /// <param name="collectionIds">The collection entries of the collection.</param>
    public static LocationBook From(OwnerOverlay overlay, IEnumerable<long> collectionIds)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(collectionIds);

        var known = overlay.Locations.Select(location => location.Id).ToHashSet(StringComparer.Ordinal);
        var entries = new Dictionary<long, string>();

        foreach (var collectionId in collectionIds)
        {
            if (overlay.EntryLocationId(collectionId) is { } id && OwnerLocation.IsWellFormedId(id) && known.Contains(id))
            {
                entries[collectionId] = id;
            }
        }

        return new LocationBook(overlay.Locations, entries);
    }

    /// <summary>The identifier of the location a collection entry is in, or null when it is not placed.</summary>
    /// <param name="collectionId">The collection entry.</param>
    public string? LocationOf(long collectionId) => EntryLocations.GetValueOrDefault(collectionId);

    /// <summary>How many collection entries are in a location.</summary>
    /// <param name="id">The identifier of the location.</param>
    public int CopiesAt(string id) => EntryLocations.Values.Count(entry => string.Equals(entry, id, StringComparison.Ordinal));

    /// <summary>
    /// A text that changes exactly when the order, a name, an identifier or the place of an entry changes: one line per location
    /// as the identifier and the name in order, then one line per placed entry as the collection entry and the identifier of its
    /// location in ascending entry order. It is empty for a book with no locations.
    /// </summary>
    public string Text =>
        Order.Count == 0
            ? string.Empty
            : string.Join(
                '\n',
                Order.Select(location => $"{location.Id}:{location.Name}")
                    .Concat(EntryLocations.OrderBy(entry => entry.Key)
                        .Select(entry => string.Create(CultureInfo.InvariantCulture, $"{entry.Key}:{entry.Value}"))));
}

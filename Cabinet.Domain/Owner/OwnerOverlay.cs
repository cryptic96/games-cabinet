using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;

namespace Cabinet.Domain.Owner;

/// <summary>The owner's picture choice for one game when it is anything other than Automatic.</summary>
/// <param name="Choice">Which picture the owner chose.</param>
/// <param name="LinkRecord">What was stored of the owner's own picture, or null when there is none; it is drawn only while <paramref name="Choice"/> is <see cref="PictureChoice.Link"/>.</param>
/// <param name="LinkUrl">The address of the owner's own picture, or null when there is none.</param>
public sealed record PictureOverride(PictureChoice Choice, ImageRecord? LinkRecord, string? LinkUrl);

/// <summary>
/// Works out where every owned copy of a game is kept from what the owner entered, and applies that to the synced collection.
/// Locations are resolved per collection entry only, so no answer about a place is ever given for a game as a whole.
/// A copy is in the location the owner gave that copy, else the one the owner gave every copy of its game; an expansion without
/// one of its own follows the first copy of the base game it stands beside (the one <see cref="ExpansionPairing"/> pairs it
/// with, when that base game is owned); every other copy has no location. The synced collection itself carries no location the
/// cabinet uses. It also holds the picture the owner chose for each copy that is not left to the automatic pick, resolved the same way:
/// the copy's own choice, else the one for every copy of its game.
/// </summary>
public sealed class OwnerOverlay
{
    private readonly IReadOnlyDictionary<long, Place> _places;
    private readonly IReadOnlyDictionary<int, long> _firstCopies;
    private readonly IReadOnlyDictionary<string, string> _names;
    private readonly IReadOnlyDictionary<long, int> _pairedBases;
    private readonly IReadOnlyDictionary<long, PictureOverride> _pictures;

    private OwnerOverlay(
        IReadOnlyList<OwnerLocation> locations,
        IReadOnlyDictionary<long, Place> places,
        IReadOnlyDictionary<int, long> firstCopies,
        IReadOnlyDictionary<string, string> names,
        IReadOnlyDictionary<long, int> pairedBases,
        IReadOnlyDictionary<long, PictureOverride> pictures)
    {
        Locations = locations;
        _places = places;
        _firstCopies = firstCopies;
        _names = names;
        _pairedBases = pairedBases;
        _pictures = pictures;
    }

    /// <summary>An overlay that places nothing: every game has no location. It is what applies while the owner data cannot be read.</summary>
    public static OwnerOverlay None { get; } = new(
        [],
        new Dictionary<long, Place>(),
        new Dictionary<int, long>(),
        new Dictionary<string, string>(),
        new Dictionary<long, int>(),
        new Dictionary<long, PictureOverride>());

    /// <summary>The owner's locations in the order the owner set, a location listed twice counted once; empty while the owner data cannot be read.</summary>
    public IReadOnlyList<OwnerLocation> Locations { get; }

    /// <summary>Works out the place of every copy of every game in a collection.</summary>
    /// <param name="snapshot">The synced collection.</param>
    /// <param name="data">What the owner entered; null when it could not be read, which places nothing.</param>
    public static OwnerOverlay From(CollectionSnapshot snapshot, OwnerData? data)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (data is null)
        {
            return None;
        }

        var locations = data.Locations
            .GroupBy(location => location.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        var names = locations.ToDictionary(location => location.Id, location => location.Name, StringComparer.Ordinal);
        var gameWide = data.Games
            .Where(game => game.CollectionId is null && game.LocationId is not null && names.ContainsKey(game.LocationId))
            .GroupBy(game => game.GameId)
            .ToDictionary(group => group.Key, group => group.First().LocationId!);
        var perCopy = data.Games
            .Where(game => game.CollectionId is not null)
            .GroupBy(game => game.CollectionId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.First().LocationId is { } id && names.ContainsKey(id) ? id : null);
        var expansionEntries = snapshot.Items
            .Where(item => item.Kind == ItemKind.Expansion)
            .Select(item => item.CollectionId)
            .ToHashSet();
        var baseCopies = snapshot.Items
            .Where(item => item.Kind == ItemKind.Base && !expansionEntries.Contains(item.CollectionId))
            .GroupBy(item => item.GameId)
            .ToDictionary(group => group.Key, group => group.Min(item => item.CollectionId));
        var firstCopies = snapshot.Items
            .GroupBy(item => item.GameId)
            .ToDictionary(group => group.Key, group => group.Min(item => item.CollectionId));
        var pairing = ExpansionPairing.Pair(snapshot);
        var owns = new Dictionary<long, string?>();

        foreach (var item in snapshot.Items)
        {
            owns[item.CollectionId] = perCopy.TryGetValue(item.CollectionId, out var own)
                ? own
                : gameWide.GetValueOrDefault(item.GameId);
        }

        var places = new Dictionary<long, Place>();
        var pairedBases = new Dictionary<long, int>();

        foreach (var item in snapshot.Items)
        {
            if (OwnedBaseOf(item, baseCopies, pairing) is { } baseGameId)
            {
                pairedBases[item.CollectionId] = baseGameId;
            }

            places[item.CollectionId] = PlaceOf(item, owns, baseCopies, pairedBases);
        }

        var pictures = PicturesOf(snapshot, data);

        return new OwnerOverlay(locations, places, firstCopies, names, pairedBases, pictures);
    }

    /// <summary>
    /// The picture the owner chose for a copy: the choice made for that copy, else the one made for every copy of its game, or
    /// null when the copy is left to the automatic pick. A copy whose own choice is Automatic stays automatic even when its
    /// game has a choice for every copy.
    /// </summary>
    /// <param name="collectionId">The collection entry.</param>
    public PictureOverride? EntryPictureOverride(long collectionId) => _pictures.TryGetValue(collectionId, out var setting) ? setting : null;

    /// <summary>The identifier of the location the owner gave a copy itself, or gave every copy of its game, or null when it has none of its own.</summary>
    /// <param name="collectionId">The collection entry.</param>
    public string? EntryOwnLocationId(long collectionId) => _places.TryGetValue(collectionId, out var place) ? place.OwnLocationId : null;

    /// <summary>The identifier of the location a copy is in, counting the base game an expansion follows, or null when it is not placed.</summary>
    /// <param name="collectionId">The collection entry.</param>
    public string? EntryLocationId(long collectionId) => _places.TryGetValue(collectionId, out var place) ? place.LocationId : null;

    /// <summary>The name of the location a copy is in, or null when it is not placed.</summary>
    /// <param name="collectionId">The collection entry.</param>
    public string? EntryLocationName(long collectionId) =>
        EntryLocationId(collectionId) is { } id && _names.TryGetValue(id, out var name) ? name : null;

    /// <summary>The game an expansion follows because it has no location of its own and an owned base game, or null when it follows none.</summary>
    /// <param name="collectionId">The collection entry of the expansion.</param>
    public int? EntryFollowsBaseGameId(long collectionId) => _places.TryGetValue(collectionId, out var place) ? place.FollowsBaseGameId : null;

    /// <summary>
    /// The owned base game an expansion stands beside, or null for any other game. Unlike <see cref="EntryFollowsBaseGameId"/> it
    /// does not depend on whether the expansion has a location of its own, and it answers for the game, not for one copy.
    /// </summary>
    /// <param name="gameId">The game.</param>
    public int? OwnedBaseGameId(int gameId) =>
        _firstCopies.TryGetValue(gameId, out var first) && _pairedBases.TryGetValue(first, out var baseGameId) ? baseGameId : null;

    /// <summary>The collection with every item's location replaced by the one this overlay gives its entry; nothing else changes.</summary>
    /// <param name="snapshot">The synced collection the overlay was worked out from.</param>
    public CollectionSnapshot Effective(CollectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return snapshot with
        {
            Items = [.. snapshot.Items.Select(item => item with { Location = EntryLocationName(item.CollectionId) })],
        };
    }

    private static Dictionary<long, PictureOverride> PicturesOf(CollectionSnapshot snapshot, OwnerData data)
    {
        var everyCopy = data.Games
            .Where(game => game.CollectionId is null && game.Picture is not null)
            .GroupBy(game => game.GameId)
            .ToDictionary(group => group.Key, group => OverrideOf(group.First().Picture!));
        var ownChoice = data.Games
            .Where(game => game.CollectionId is not null && game.Picture is not null)
            .GroupBy(game => game.CollectionId!.Value)
            .ToDictionary(group => group.Key, group => OverrideOf(group.First().Picture!));
        var pictures = new Dictionary<long, PictureOverride>();

        foreach (var item in snapshot.Items.GroupBy(item => item.CollectionId).Select(entry => SnapshotMapper.InEntryOrder(entry).First()))
        {
            var chosen = ownChoice.TryGetValue(item.CollectionId, out var own) ? own : everyCopy.GetValueOrDefault(item.GameId);

            if (chosen is not null)
            {
                pictures[item.CollectionId] = chosen;
            }
        }

        return pictures;
    }

    private static PictureOverride? OverrideOf(PictureSetting setting) =>
        setting.Choice == PictureChoice.Automatic ? null : new PictureOverride(setting.Choice, setting.Link?.Record, setting.Link?.Url);

    private static Place PlaceOf(
        SnapshotItem item,
        IReadOnlyDictionary<long, string?> owns,
        IReadOnlyDictionary<int, long> baseCopies,
        IReadOnlyDictionary<long, int> pairedBases)
    {
        var own = owns[item.CollectionId];

        if (own is not null)
        {
            return new Place(own, own, null);
        }

        if (pairedBases.TryGetValue(item.CollectionId, out var baseGameId))
        {
            var baseOwn = owns.GetValueOrDefault(baseCopies[baseGameId]);

            return new Place(null, baseOwn, baseGameId);
        }

        return new Place(null, null, null);
    }

    private static int? OwnedBaseOf(
        SnapshotItem item,
        IReadOnlyDictionary<int, long> baseCopies,
        IReadOnlyDictionary<long, IReadOnlyList<BaseGameRef>> pairing) =>
        item.Kind == ItemKind.Expansion
            && pairing.TryGetValue(item.CollectionId, out var references)
            && references is [{ } paired]
            && baseCopies.ContainsKey(paired.BggId)
            ? paired.BggId
            : null;

    private sealed record Place(string? OwnLocationId, string? LocationId, int? FollowsBaseGameId);
}

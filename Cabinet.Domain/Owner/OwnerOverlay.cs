using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;

namespace Cabinet.Domain.Owner;

/// <summary>The owner's picture choice for one game when it is anything other than Automatic.</summary>
/// <param name="Choice">Which picture the owner chose.</param>
/// <param name="LinkRecord">What was stored of the owner's own picture, or null when there is none; it is drawn only while <paramref name="Choice"/> is <see cref="PictureChoice.Link"/>.</param>
/// <param name="LinkUrl">The address of the owner's own picture, or null when there is none.</param>
public sealed record PictureOverride(PictureChoice Choice, ImageRecord? LinkRecord, string? LinkUrl);

/// <summary>
/// Works out where every owned game is kept from what the owner entered, and applies that to the synced collection.
/// A game is in the location the owner gave it; an expansion without one of its own follows the base game it stands beside
/// (the one <see cref="ExpansionPairing"/> pairs it with, when that base game is owned); every other game has no location.
/// The synced collection itself carries no location the cabinet uses. It also holds the picture the owner chose for each game
/// that is not left to the automatic pick.
/// </summary>
public sealed class OwnerOverlay
{
    private readonly IReadOnlyDictionary<int, Place> _places;
    private readonly IReadOnlyDictionary<string, string> _names;
    private readonly IReadOnlyDictionary<int, string> _ownLocations;
    private readonly IReadOnlyDictionary<int, int> _pairedBases;
    private readonly IReadOnlyDictionary<int, PictureOverride> _pictures;

    private OwnerOverlay(
        IReadOnlyDictionary<int, Place> places,
        IReadOnlyDictionary<string, string> names,
        IReadOnlyDictionary<int, string> ownLocations,
        IReadOnlyDictionary<int, int> pairedBases,
        IReadOnlyDictionary<int, PictureOverride> pictures)
    {
        _places = places;
        _names = names;
        _ownLocations = ownLocations;
        _pairedBases = pairedBases;
        _pictures = pictures;
    }

    /// <summary>An overlay that places nothing: every game has no location. It is what applies while the owner data cannot be read.</summary>
    public static OwnerOverlay None { get; } = new(
        new Dictionary<int, Place>(),
        new Dictionary<string, string>(),
        new Dictionary<int, string>(),
        new Dictionary<int, int>(),
        new Dictionary<int, PictureOverride>());

    /// <summary>Works out the place of every game in a collection.</summary>
    /// <param name="snapshot">The synced collection.</param>
    /// <param name="data">What the owner entered; null when it could not be read, which places nothing.</param>
    public static OwnerOverlay From(CollectionSnapshot snapshot, OwnerData? data)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (data is null)
        {
            return None;
        }

        var names = data.Locations
            .GroupBy(location => location.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.Ordinal);
        var own = data.Games
            .Where(game => game.LocationId is not null && names.ContainsKey(game.LocationId))
            .GroupBy(game => game.GameId)
            .ToDictionary(group => group.Key, group => group.First().LocationId!);
        var expansionEntries = snapshot.Items
            .Where(item => item.Kind == ItemKind.Expansion)
            .Select(item => item.CollectionId)
            .ToHashSet();
        var ownedBases = snapshot.Items
            .Where(item => item.Kind == ItemKind.Base && !expansionEntries.Contains(item.CollectionId))
            .Select(item => item.GameId)
            .ToHashSet();
        var pairing = ExpansionPairing.Pair(snapshot);
        var places = new Dictionary<int, Place>();
        var pairedBases = new Dictionary<int, int>();

        foreach (var item in snapshot.Items)
        {
            if (places.ContainsKey(item.GameId))
            {
                continue;
            }

            places[item.GameId] = PlaceOf(item, own, ownedBases, pairing);

            if (OwnedBaseOf(item, ownedBases, pairing) is { } baseGameId)
            {
                pairedBases[item.GameId] = baseGameId;
            }
        }

        var pictures = data.Games
            .Where(game => game.Picture is { Choice: not PictureChoice.Automatic })
            .GroupBy(game => game.GameId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var setting = group.First().Picture!;

                    return new PictureOverride(setting.Choice, setting.Link?.Record, setting.Link?.Url);
                });

        return new OwnerOverlay(places, names, own, pairedBases, pictures);
    }

    /// <summary>The picture the owner chose for a game, or null when the game is left to the automatic pick.</summary>
    /// <param name="gameId">The game.</param>
    public PictureOverride? PictureOverrideOf(int gameId) => _pictures.TryGetValue(gameId, out var setting) ? setting : null;

    /// <summary>The identifier of the location the owner gave a game itself, or null when it has none of its own.</summary>
    /// <param name="gameId">The game.</param>
    public string? OwnLocationId(int gameId) => _ownLocations.TryGetValue(gameId, out var id) ? id : null;

    /// <summary>The identifier of the location a game is in, counting the base game an expansion follows, or null when it is not placed.</summary>
    /// <param name="gameId">The game.</param>
    public string? EffectiveLocationId(int gameId) => _places.TryGetValue(gameId, out var place) ? place.LocationId : null;

    /// <summary>The name of the location a game is in, or null when it is not placed.</summary>
    /// <param name="gameId">The game.</param>
    public string? EffectiveLocationName(int gameId) =>
        EffectiveLocationId(gameId) is { } id && _names.TryGetValue(id, out var name) ? name : null;

    /// <summary>The game an expansion follows because it has no location of its own and an owned base game, or null when it follows none.</summary>
    /// <param name="gameId">The game.</param>
    public int? FollowsBaseGameId(int gameId) => _places.TryGetValue(gameId, out var place) ? place.FollowsBaseGameId : null;

    /// <summary>
    /// The owned base game an expansion stands beside, or null for any other game. Unlike <see cref="FollowsBaseGameId"/> it
    /// does not depend on whether the expansion has a location of its own.
    /// </summary>
    /// <param name="gameId">The game.</param>
    public int? OwnedBaseGameId(int gameId) => _pairedBases.TryGetValue(gameId, out var baseGameId) ? baseGameId : null;

    /// <summary>The collection with every item's location replaced by the one this overlay gives it; nothing else changes.</summary>
    /// <param name="snapshot">The synced collection the overlay was worked out from.</param>
    public CollectionSnapshot Effective(CollectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return snapshot with
        {
            Items = [.. snapshot.Items.Select(item => item with { Location = EffectiveLocationName(item.GameId) })],
        };
    }

    private static Place PlaceOf(
        SnapshotItem item,
        IReadOnlyDictionary<int, string> own,
        IReadOnlySet<int> ownedBases,
        IReadOnlyDictionary<long, IReadOnlyList<BaseGameRef>> pairing)
    {
        if (own.TryGetValue(item.GameId, out var ownId))
        {
            return new Place(ownId, null);
        }

        if (OwnedBaseOf(item, ownedBases, pairing) is { } baseGameId)
        {
            return new Place(own.TryGetValue(baseGameId, out var baseId) ? baseId : null, baseGameId);
        }

        return new Place(null, null);
    }

    private static int? OwnedBaseOf(
        SnapshotItem item,
        IReadOnlySet<int> ownedBases,
        IReadOnlyDictionary<long, IReadOnlyList<BaseGameRef>> pairing) =>
        item.Kind == ItemKind.Expansion
            && pairing.TryGetValue(item.CollectionId, out var references)
            && references is [{ } paired]
            && ownedBases.Contains(paired.BggId)
            ? paired.BggId
            : null;

    private sealed record Place(string? LocationId, int? FollowsBaseGameId);
}

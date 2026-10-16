using System.Globalization;
using System.Text;
using Cabinet.Domain.Collection;

namespace Cabinet.Domain.Owner;

/// <summary>What one edit of the owner data came to.</summary>
/// <param name="Data">The data after the edit; the data that was given when nothing changed.</param>
/// <param name="LogText">The line for the change log, or null when nothing changed.</param>
/// <param name="StatusKey">A short key the page turns into the words it shows the owner.</param>
/// <param name="Changed">Whether the edit changed anything.</param>
public sealed record OwnerChange(OwnerData Data, string? LogText, string StatusKey, bool Changed = true)
{
    private const string NoneStatusKey = "unchanged";

    /// <summary>The answer of an edit that has nothing to do, such as assigning the location a game already has.</summary>
    public static OwnerChange None { get; } = new(OwnerData.Empty, null, NoneStatusKey, false);

    /// <summary>Whether this is the answer of an edit that has nothing to do, as opposed to one that was refused.</summary>
    public bool IsNone => !Changed && StatusKey == NoneStatusKey;

    /// <summary>An edit that was refused and changed nothing.</summary>
    /// <param name="data">The data as it was.</param>
    /// <param name="statusKey">Why the edit was refused.</param>
    public static OwnerChange Refused(OwnerData data, string statusKey) => new(data, null, statusKey, false);

    /// <summary>How many games an edit left not placed, such as the games that were kept in a location that was deleted.</summary>
    public int GamesNotPlaced { get; init; }
}

/// <summary>Why a location name was refused.</summary>
public enum LocationNameProblem
{
    /// <summary>The name is fine.</summary>
    None,

    /// <summary>The name is empty or only spaces.</summary>
    Empty,

    /// <summary>The name is longer than <see cref="OwnerData.MaxNameLength"/> characters.</summary>
    TooLong,

    /// <summary>The name holds a control character or a character that overrides the direction of text.</summary>
    ForbiddenCharacter,

    /// <summary>The list already holds <see cref="OwnerData.MaxLocations"/> locations.</summary>
    TooMany,

    /// <summary>Another location already has this name, ignoring case.</summary>
    Taken,
}

/// <summary>The verdict on a location name.</summary>
/// <param name="Problem">Why the name was refused, or <see cref="LocationNameProblem.None"/>.</param>
/// <param name="Name">The name with its edge spaces removed; this is what is stored when there is no problem.</param>
public readonly record struct LocationNameCheck(LocationNameProblem Problem, string Name)
{
    /// <summary>The short key the page turns into the words it shows the owner.</summary>
    public string StatusKey => Problem switch
    {
        LocationNameProblem.Empty => "name-empty",
        LocationNameProblem.TooLong => "name-too-long",
        LocationNameProblem.ForbiddenCharacter => "name-characters",
        LocationNameProblem.TooMany => "too-many-locations",
        LocationNameProblem.Taken => "name-taken",
        _ => "location-added",
    };
}

/// <summary>The rules a location name has to meet. Every visitor sees the name on a card, so it is plain text of a sensible length.</summary>
public static class LocationNames
{
    /// <summary>Checks a name the owner entered against the locations that exist.</summary>
    /// <param name="existing">The locations that exist now.</param>
    /// <param name="candidate">The name as entered.</param>
    /// <param name="exceptId">The location being renamed, which may keep its own name or change its case; null when adding.</param>
    public static LocationNameCheck Validate(IReadOnlyList<OwnerLocation> existing, string candidate, string? exceptId = null)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(candidate);

        var name = candidate.Trim();

        if (name.Length == 0)
        {
            return new LocationNameCheck(LocationNameProblem.Empty, name);
        }

        if (name.Length > OwnerData.MaxNameLength)
        {
            return new LocationNameCheck(LocationNameProblem.TooLong, name);
        }

        if (name.EnumerateRunes().Any(IsForbidden))
        {
            return new LocationNameCheck(LocationNameProblem.ForbiddenCharacter, name);
        }

        if (exceptId is null && existing.Count >= OwnerData.MaxLocations)
        {
            return new LocationNameCheck(LocationNameProblem.TooMany, name);
        }

        var taken = existing.Any(location =>
            !string.Equals(location.Id, exceptId, StringComparison.Ordinal)
            && string.Equals(location.Name, name, StringComparison.OrdinalIgnoreCase));

        return new LocationNameCheck(taken ? LocationNameProblem.Taken : LocationNameProblem.None, name);
    }

    private static bool IsForbidden(Rune rune) =>
        Rune.IsControl(rune)
        || Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format;
}

/// <summary>Where a move of several games at once puts them.</summary>
public enum MoveTargetKind
{
    /// <summary>Into one of the owner's locations.</summary>
    Location,

    /// <summary>Into no location of their own.</summary>
    NotPlaced,

    /// <summary>Back to following the base game, for the expansions that have an owned base game.</summary>
    SameAsBase,
}

/// <summary>Where a move of several games at once puts them: one location, no location, or back with their base game.</summary>
public sealed record MoveTarget
{
    private MoveTarget(MoveTargetKind kind, string? locationId)
    {
        Kind = kind;
        LocationId = locationId;
    }

    /// <summary>What kind of place this is.</summary>
    public MoveTargetKind Kind { get; }

    /// <summary>The location the games go to, or null unless <see cref="Kind"/> is <see cref="MoveTargetKind.Location"/>.</summary>
    public string? LocationId { get; }

    /// <summary>Not placed: the games keep no location of their own.</summary>
    public static MoveTarget NotPlaced { get; } = new(MoveTargetKind.NotPlaced, null);

    /// <summary>Same as the base game: the expansions that have an owned base game follow it again, and no other game is touched.</summary>
    public static MoveTarget SameAsBase { get; } = new(MoveTargetKind.SameAsBase, null);

    /// <summary>One of the owner's locations.</summary>
    /// <param name="id">The location's identifier.</param>
    public static MoveTarget Location(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new MoveTarget(MoveTargetKind.Location, id);
    }
}

/// <summary>The edits of the location list and of where each game is kept. Every edit is pure: it gives back new data and leaves the old untouched.</summary>
public static class LocationEdits
{
    private const int MaxIdAttempts = 20;

    /// <summary>Adds a location at the end of the list.</summary>
    /// <param name="data">The data to add to.</param>
    /// <param name="name">The name, which every visitor will see.</param>
    /// <param name="newId">Produces a fresh identifier; asked again when it clashes with one in use.</param>
    public static OwnerChange Add(OwnerData data, string name, Func<string> newId)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(newId);

        var check = LocationNames.Validate(data.Locations, name);

        if (check.Problem != LocationNameProblem.None)
        {
            return OwnerChange.Refused(data, check.StatusKey);
        }

        var id = FreshId(data, newId);

        if (id is null)
        {
            return OwnerChange.Refused(data, "id-clash");
        }

        return new OwnerChange(
            data with { Locations = [.. data.Locations, new OwnerLocation(id, check.Name)] },
            $"Added location \"{check.Name}\"",
            "location-added");
    }

    /// <summary>
    /// Puts every copy of a game in a location, or takes every copy out of the one it is in. Every decision of the game gets the
    /// location, and when the game has no decision that holds for every copy, one is added, so a copy the collection gains later
    /// is placed too. Pictures are left as they are.
    /// </summary>
    /// <param name="data">The data to change.</param>
    /// <param name="gameId">The game to move.</param>
    /// <param name="locationId">The location to put it in, or null for no location of its own.</param>
    /// <param name="title">The game's title, for the change log.</param>
    /// <param name="followsBase">Whether the game is an expansion with an owned base game, so that no location of its own means it follows the base.</param>
    public static OwnerChange Assign(OwnerData data, int gameId, string? locationId, string title, bool followsBase = false)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(title);

        OwnerLocation? target = null;

        if (locationId is not null)
        {
            target = data.Locations.FirstOrDefault(location => string.Equals(location.Id, locationId, StringComparison.Ordinal));

            if (target is null)
            {
                return OwnerChange.Refused(data, "unknown-location");
            }
        }

        if (PlacedEverywhere(data, gameId, target?.Id) is not { } games)
        {
            return OwnerChange.None;
        }

        return new OwnerChange(
            data with { Games = games },
            LogLine(title, target, followsBase),
            target is null ? "game-unplaced" : "game-moved");
    }

    /// <summary>
    /// Puts one copy of a game in a location, or takes it out of the one it is in, and moves no other copy: a decision that holds
    /// for every copy of the game is first turned into one decision per copy that exists, so the other copies keep their place.
    /// </summary>
    /// <param name="data">The data to change.</param>
    /// <param name="gameId">The game the copy belongs to.</param>
    /// <param name="collectionId">The copy to move.</param>
    /// <param name="copies">The collection entries of the game that exist now, the copy included.</param>
    /// <param name="locationId">The location to put it in, or null for no location of its own.</param>
    /// <param name="title">The game's title, for the change log.</param>
    /// <param name="followsBase">Whether the game is an expansion with an owned base game, so that no location of its own means it follows the base.</param>
    public static OwnerChange Assign(
        OwnerData data,
        int gameId,
        long collectionId,
        IReadOnlyList<long> copies,
        string? locationId,
        string title,
        bool followsBase = false)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(copies);
        ArgumentNullException.ThrowIfNull(title);

        OwnerLocation? target = null;

        if (locationId is not null)
        {
            target = data.Locations.FirstOrDefault(location => string.Equals(location.Id, locationId, StringComparison.Ordinal));

            if (target is null)
            {
                return OwnerChange.Refused(data, "unknown-location");
            }
        }

        if (PlacedCopy(data, gameId, collectionId, copies, target?.Id) is not { } placed)
        {
            return OwnerChange.None;
        }

        return new OwnerChange(
            placed,
            LogLine(title, target, followsBase),
            target is null ? "game-unplaced" : "game-moved");
    }

    /// <summary>
    /// Moves several copies at once: into one location, to no location of their own, or, for the copies of expansions that have an
    /// owned base game, back to following it. Only the listed copies move; a decision that holds for every copy of a touched game
    /// is first turned into one decision per copy, so the copies that were not listed keep their place. The whole move is one
    /// change with one log line, so one Undo takes all of it back.
    /// </summary>
    /// <param name="data">The data to change.</param>
    /// <param name="copies">The copies to move, each as its game and its collection entry; a copy listed twice counts once.</param>
    /// <param name="target">Where they go.</param>
    /// <param name="titleOf">Gives the title of an owned game, or null when the game is not in the collection; any such game refuses the whole move.</param>
    /// <param name="followsBase">Whether a game is an expansion with an owned base game, so that no location of its own means it follows the base.</param>
    /// <param name="copiesOf">Gives the collection entries a game has now; a listed copy that is not among them refuses the whole move.</param>
    public static OwnerChange MoveMany(
        OwnerData data,
        IReadOnlyCollection<(int GameId, long CollectionId)> copies,
        MoveTarget target,
        Func<int, string?> titleOf,
        Func<int, bool> followsBase,
        Func<int, IReadOnlyList<long>> copiesOf)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(copies);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(titleOf);
        ArgumentNullException.ThrowIfNull(followsBase);
        ArgumentNullException.ThrowIfNull(copiesOf);

        var listed = copies.Distinct().ToList();

        if (listed.Count == 0)
        {
            return OwnerChange.Refused(data, "nothing-selected");
        }

        if (listed.Any(copy => titleOf(copy.GameId) is null || !copiesOf(copy.GameId).Contains(copy.CollectionId)))
        {
            return OwnerChange.Refused(data, "unknown-game");
        }

        var destination = target.Kind == MoveTargetKind.Location
            ? data.Locations.FirstOrDefault(location => string.Equals(location.Id, target.LocationId, StringComparison.Ordinal))
            : null;

        if (target.Kind == MoveTargetKind.Location && destination is null)
        {
            return OwnerChange.Refused(data, "unknown-location");
        }

        var affected = target.Kind == MoveTargetKind.SameAsBase ? listed.Where(copy => followsBase(copy.GameId)).ToList() : listed;
        var moved = data;
        var changed = false;

        foreach (var copy in affected)
        {
            if (PlacedCopy(moved, copy.GameId, copy.CollectionId, copiesOf(copy.GameId), destination?.Id) is { } next)
            {
                moved = next;
                changed = true;
            }
        }

        if (!changed)
        {
            return OwnerChange.None;
        }

        return new OwnerChange(
            moved,
            MoveManyLog(destination, affected.Count, affected.Count(copy => followsBase(copy.GameId))),
            destination is null ? "game-unplaced" : "game-moved");
    }

    /// <summary>Renames a location in place. Its identifier and its place in the list stay, so every game kept there follows the new name.</summary>
    /// <param name="data">The data to change.</param>
    /// <param name="id">The location to rename.</param>
    /// <param name="name">The new name, held to the same rules as the name of a new location.</param>
    public static OwnerChange Rename(OwnerData data, string id, string name)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(name);

        var index = IndexOf(data, id);

        if (index < 0)
        {
            return OwnerChange.Refused(data, "unknown-location");
        }

        var check = LocationNames.Validate(data.Locations, name, id);

        if (check.Problem != LocationNameProblem.None)
        {
            return OwnerChange.Refused(data, check.StatusKey);
        }

        var current = data.Locations[index];

        if (string.Equals(current.Name, check.Name, StringComparison.Ordinal))
        {
            return OwnerChange.None;
        }

        var locations = data.Locations.ToList();
        locations[index] = current with { Name = check.Name };

        return new OwnerChange(
            data with { Locations = locations },
            $"Renamed \"{current.Name}\" to \"{check.Name}\"",
            "location-renamed");
    }

    /// <summary>Moves a location one place up or down the list by swapping it with its neighbour.</summary>
    /// <param name="data">The data to change.</param>
    /// <param name="id">The location to move.</param>
    /// <param name="step">Minus one to move it up, plus one to move it down.</param>
    public static OwnerChange Move(OwnerData data, string id, int step)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(id);

        var index = IndexOf(data, id);

        if (index < 0 || step is not (-1 or 1))
        {
            return OwnerChange.Refused(data, "unknown-location");
        }

        var target = index + step;

        if (target < 0 || target >= data.Locations.Count)
        {
            return OwnerChange.None;
        }

        var locations = data.Locations.ToList();
        (locations[index], locations[target]) = (locations[target], locations[index]);

        return new OwnerChange(
            data with { Locations = locations },
            $"Moved \"{data.Locations[index].Name}\" {(step < 0 ? "up" : "down")}",
            "location-moved");
    }

    /// <summary>
    /// Deletes a location. Every game kept there is not placed afterwards, and an expansion that has an owned base game goes back
    /// to following its base game; nothing is moved to another location. The change says how many games ended up not placed.
    /// </summary>
    /// <param name="data">The data to change.</param>
    /// <param name="snapshot">The collection the games are in, or null when there is none yet, which counts no games.</param>
    /// <param name="id">The location to delete.</param>
    public static OwnerChange Delete(OwnerData data, CollectionSnapshot? snapshot, string id)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(id);

        var index = IndexOf(data, id);

        if (index < 0)
        {
            return OwnerChange.Refused(data, "unknown-location");
        }

        var name = data.Locations[index].Name;
        var notPlaced = CountLeftNotPlaced(data, snapshot, id);
        var log = notPlaced == 0
            ? $"Deleted location \"{name}\""
            : $"Deleted location \"{name}\" ({notPlaced} {(notPlaced == 1 ? "game" : "games")} now not placed)";

        return new OwnerChange(Without(data, id), log, "location-deleted") { GamesNotPlaced = notPlaced };
    }

    /// <summary>Counts the copies that would be not placed if a location were deleted: those shown there now that would be shown nowhere afterwards.</summary>
    /// <param name="data">The data as it is now.</param>
    /// <param name="snapshot">The collection the games are in, or null when there is none yet.</param>
    /// <param name="id">The location that would be deleted.</param>
    public static int CountLeftNotPlaced(OwnerData data, CollectionSnapshot? snapshot, string id)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(id);

        if (snapshot is null || IndexOf(data, id) < 0)
        {
            return 0;
        }

        var before = OwnerOverlay.From(snapshot, data);
        var after = OwnerOverlay.From(snapshot, Without(data, id));

        return snapshot.Items.Count(item =>
            string.Equals(before.EntryLocationId(item.CollectionId), id, StringComparison.Ordinal) && after.EntryLocationId(item.CollectionId) is null);
    }

    /// <summary>
    /// The data after one copy is put in a location, or out of every location when the target is null; null when the copy is there
    /// already. A decision that holds for every copy of the game is first turned into one decision per copy, so no other copy moves.
    /// </summary>
    /// <param name="data">The data to change.</param>
    /// <param name="gameId">The game the copy belongs to.</param>
    /// <param name="collectionId">The copy.</param>
    /// <param name="copies">The collection entries of the game that exist now.</param>
    /// <param name="targetId">The location identifier, already known to exist, or null.</param>
    private static OwnerData? PlacedCopy(OwnerData data, int gameId, long collectionId, IReadOnlyList<long> copies, string? targetId)
    {
        var current = OwnerGames.Find(data, gameId, collectionId);
        var currentLocation = current?.LocationId is { } held && data.Locations.Any(location => location.Id == held) ? held : null;

        if (string.Equals(currentLocation, targetId, StringComparison.Ordinal))
        {
            return null;
        }

        var split = OwnerGames.SplitForEdit(data, gameId, [.. copies.Append(collectionId)]);
        var own = split.Games.FirstOrDefault(game => game.GameId == gameId && game.CollectionId == collectionId);
        var updated = (own ?? new OwnerGame(gameId, null, null, null, collectionId)) with { LocationId = targetId };
        var others = split.Games.Where(game => !ReferenceEquals(game, own));
        var games = IsEmpty(updated) ? others : others.Append(updated);

        return split with { Games = OwnerGames.Ordered(games) };
    }

    /// <summary>
    /// The decisions after every copy of a game is put in a location, or out of every location when the target is null; null when
    /// every copy is there already. Every decision of the game gets the location, and a target location adds the decision that
    /// holds for every copy when the game has none.
    /// </summary>
    /// <param name="data">The data to change.</param>
    /// <param name="gameId">The game.</param>
    /// <param name="targetId">The location identifier, already known to exist, or null.</param>
    private static IReadOnlyList<OwnerGame>? PlacedEverywhere(OwnerData data, int gameId, string? targetId)
    {
        var entries = data.Games.Where(game => game.GameId == gameId).ToList();
        var known = data.Locations.Select(location => location.Id).ToHashSet(StringComparer.Ordinal);
        var allThere = entries.All(game => string.Equals(game.LocationId is { } held && known.Contains(held) ? held : null, targetId, StringComparison.Ordinal));
        var coversEveryCopy = targetId is null || entries.Any(game => game.CollectionId is null);

        if (allThere && coversEveryCopy)
        {
            return null;
        }

        var placed = entries.Select(game => game with { LocationId = targetId });
        var added = coversEveryCopy ? [] : new List<OwnerGame> { new(gameId, targetId, null, null) };
        var others = data.Games.Where(game => game.GameId != gameId);

        return OwnerGames.Ordered(others.Concat(placed.Concat(added).Where(game => !IsEmpty(game))));
    }

    private static string MoveManyLog(OwnerLocation? destination, int moved, int following)
    {
        if (destination is not null)
        {
            return $"Moved {moved} {(moved == 1 ? "game" : "games")} to {destination.Name}";
        }

        var notPlaced = moved - following;
        var parts = new List<string>();

        if (notPlaced > 0)
        {
            parts.Add(notPlaced == 1 ? "1 game is now not placed" : $"{notPlaced} games are now not placed");
        }

        if (following > 0)
        {
            parts.Add(following == 1 ? "1 expansion now follows its base game" : $"{following} expansions now follow their base game");
        }

        return string.Join(" and ", parts);
    }

    private static int IndexOf(OwnerData data, string id)
    {
        for (var index = 0; index < data.Locations.Count; index++)
        {
            if (string.Equals(data.Locations[index].Id, id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static OwnerData Without(OwnerData data, string id) =>
        data with
        {
            Locations = [.. data.Locations.Where(location => !string.Equals(location.Id, id, StringComparison.Ordinal))],
            Games =
            [
                .. data.Games
                    .Select(game => string.Equals(game.LocationId, id, StringComparison.Ordinal) ? game with { LocationId = null } : game)
                    .Where(game => !IsEmpty(game)),
            ],
        };

    private static string? FreshId(OwnerData data, Func<string> newId)
    {
        for (var attempt = 0; attempt < MaxIdAttempts; attempt++)
        {
            var id = newId();

            if (!string.IsNullOrEmpty(id) && data.Locations.All(location => !string.Equals(location.Id, id, StringComparison.Ordinal)))
            {
                return id;
            }
        }

        return null;
    }

    internal static bool IsEmpty(OwnerGame game) =>
        game.LocationId is null && game.AbsentSinceUtc is null && game.Picture is null;

    private static string LogLine(string title, OwnerLocation? target, bool followsBase)
    {
        if (target is not null)
        {
            return $"Moved {title} to {target.Name}";
        }

        return followsBase ? $"{title} now follows its base game" : $"{title} is now not placed";
    }
}

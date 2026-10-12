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

        if (name.Any(IsForbidden))
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

    private static bool IsForbidden(char character) =>
        char.IsControl(character)
        || character is >= '‪' and <= '‮'
        || character is >= '⁦' and <= '⁩';
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

    /// <summary>Puts a game in a location, or takes it out of the one it is in.</summary>
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

        var current = data.Games.FirstOrDefault(game => game.GameId == gameId);
        var currentLocation = current?.LocationId is { } held && data.Locations.Any(location => location.Id == held) ? held : null;

        if (string.Equals(currentLocation, target?.Id, StringComparison.Ordinal))
        {
            return OwnerChange.None;
        }

        var updated = (current ?? new OwnerGame(gameId, null, null, null)) with { LocationId = target?.Id };
        var others = data.Games.Where(game => game.GameId != gameId);
        var games = IsEmpty(updated) ? others : others.Append(updated);

        return new OwnerChange(
            data with { Games = [.. games.OrderBy(game => game.GameId)] },
            LogLine(title, target, followsBase),
            target is null ? "game-unplaced" : "game-moved");
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

    /// <summary>Counts the games that would be not placed if a location were deleted: those shown there now that would be shown nowhere afterwards.</summary>
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

        return snapshot.Items
            .Select(item => item.GameId)
            .Distinct()
            .Count(gameId => string.Equals(before.EffectiveLocationId(gameId), id, StringComparison.Ordinal) && after.EffectiveLocationId(gameId) is null);
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

    private static bool IsEmpty(OwnerGame game) =>
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

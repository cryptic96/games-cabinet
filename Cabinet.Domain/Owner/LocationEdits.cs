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

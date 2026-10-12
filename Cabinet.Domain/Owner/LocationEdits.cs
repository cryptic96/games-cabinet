namespace Cabinet.Domain.Owner;

/// <summary>What one edit of the owner data came to.</summary>
/// <param name="Data">The data after the edit; the data that was given when nothing changed.</param>
/// <param name="LogText">The line for the change log, or null when nothing changed.</param>
/// <param name="StatusKey">A short key the page turns into the words it shows the owner.</param>
/// <param name="Changed">Whether the edit changed anything.</param>
public sealed record OwnerChange(OwnerData Data, string? LogText, string StatusKey, bool Changed = true)
{
    /// <summary>The answer of an edit that has nothing to do, such as assigning the location a game already has.</summary>
    public static OwnerChange None { get; } = new(OwnerData.Empty, null, "unchanged", false);

    /// <summary>Whether this is the answer of an edit that has nothing to do.</summary>
    public bool IsNone => !Changed;

    /// <summary>An edit that was refused and changed nothing.</summary>
    /// <param name="data">The data as it was.</param>
    /// <param name="statusKey">Why the edit was refused.</param>
    public static OwnerChange Refused(OwnerData data, string statusKey) => new(data, null, statusKey, false);
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

        var trimmed = name.Trim();

        if (trimmed.Length == 0)
        {
            return OwnerChange.Refused(data, "name-empty");
        }

        if (trimmed.Length > OwnerData.MaxNameLength)
        {
            return OwnerChange.Refused(data, "name-too-long");
        }

        if (data.Locations.Count >= OwnerData.MaxLocations)
        {
            return OwnerChange.Refused(data, "too-many-locations");
        }

        if (data.Locations.Any(location => string.Equals(location.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return OwnerChange.Refused(data, "name-taken");
        }

        var id = FreshId(data, newId);

        if (id is null)
        {
            return OwnerChange.Refused(data, "id-clash");
        }

        return new OwnerChange(
            data with { Locations = [.. data.Locations, new OwnerLocation(id, trimmed)] },
            $"Added location \"{trimmed}\"",
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

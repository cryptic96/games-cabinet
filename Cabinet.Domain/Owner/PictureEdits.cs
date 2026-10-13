using Cabinet.Domain.Collection;

namespace Cabinet.Domain.Owner;

/// <summary>The edits of the owner's picture decisions. Every edit is pure: it gives back new data and leaves the old untouched.</summary>
public static class PictureEdits
{
    private const string ChosenStatusKey = "picture-chosen";
    private const string NoPictureStatusKey = "no-picture";
    private const string NoLinkStatusKey = "no-link";
    private const string CheckedStatusKey = "picture-checked";
    private const string RemovedStatusKey = "link-removed";

    /// <summary>
    /// Sets which picture a game is drawn from. A choice other than Automatic marks the game as checked, and every choice
    /// records the addresses of the two pictures BGG offers right now, so a later change of either can be noticed. The kinds
    /// follow BGG: nothing but the kind is stored, never an address to pin. A link the owner entered earlier stays stored when
    /// another kind is chosen, so it can be chosen again.
    /// </summary>
    /// <param name="data">The data to change.</param>
    /// <param name="gameId">The game.</param>
    /// <param name="choice">The kind of picture to draw.</param>
    /// <param name="title">The game's title, for the change log.</param>
    /// <param name="currentOwnedUrl">The address of the owned edition's picture now, or null when BGG offers none.</param>
    /// <param name="currentMainUrl">The address of the game's main picture now, or null when BGG offers none.</param>
    /// <param name="now">The moment of the choice. A link that counts as dead then is accepted by the choice, so it stops asking for a look until it fails again; leave it out to keep the link's record as it is.</param>
    public static OwnerChange Choose(
        OwnerData data,
        int gameId,
        PictureChoice choice,
        string title,
        string? currentOwnedUrl,
        string? currentMainUrl,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(title);

        if (!Enum.IsDefined(choice))
        {
            return OwnerChange.Refused(data, NoPictureStatusKey);
        }

        var current = data.Games.FirstOrDefault(game => game.GameId == gameId);
        var setting = current?.Picture;

        if ((setting?.Choice ?? PictureChoice.Automatic) == choice)
        {
            return OwnerChange.None;
        }

        if ((choice == PictureChoice.OwnedEdition && currentOwnedUrl is null) || (choice == PictureChoice.MainPicture && currentMainUrl is null))
        {
            return OwnerChange.Refused(data, NoPictureStatusKey);
        }

        if (choice == PictureChoice.Link && setting?.Link is null)
        {
            return OwnerChange.Refused(data, NoLinkStatusKey);
        }

        var next = new PictureSetting(
            choice,
            setting?.Checked == true || choice != PictureChoice.Automatic,
            currentOwnedUrl,
            currentMainUrl,
            AcceptedLink(setting?.Link, now));
        var kept = choice == PictureChoice.Automatic && !next.Checked && next.Link is null ? null : next;
        var updated = (current ?? new OwnerGame(gameId, null, null, null)) with { Picture = kept };
        var others = data.Games.Where(game => game.GameId != gameId);
        var games = LocationEdits.IsEmpty(updated) ? others : others.Append(updated);

        return new OwnerChange(
            data with { Games = [.. games.OrderBy(game => game.GameId)] },
            $"Picture of {title}: {Words(choice)}",
            ChosenStatusKey);
    }

    /// <summary>
    /// Stores a picture the owner linked and chooses it: the address and its processed record become the game's link, replacing
    /// any link the game had before, the choice becomes the link and the game counts as checked. Both picture addresses BGG
    /// offers right now are remembered so a later change of either is noticed. The pictures themselves are written before this
    /// edit is applied, so a record never refers to files that are not there.
    /// </summary>
    /// <param name="data">The data to change.</param>
    /// <param name="gameId">The game.</param>
    /// <param name="url">The canonical address of the linked picture.</param>
    /// <param name="record">The processed record of the picture.</param>
    /// <param name="title">The game's title, for the change log.</param>
    /// <param name="currentOwnedUrl">The address of the owned edition's picture now, or null when BGG offers none.</param>
    /// <param name="currentMainUrl">The address of the game's main picture now, or null when BGG offers none.</param>
    public static OwnerChange UseLink(
        OwnerData data,
        int gameId,
        string url,
        ImageRecord record,
        string title,
        string? currentOwnedUrl,
        string? currentMainUrl)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(title);

        if (string.IsNullOrWhiteSpace(url) || record.Status != ImageStatus.Ok || record.Files is not { Count: > 0 })
        {
            return OwnerChange.Refused(data, NoLinkStatusKey);
        }

        var current = data.Games.FirstOrDefault(game => game.GameId == gameId);
        var next = new PictureSetting(
            PictureChoice.Link,
            true,
            currentOwnedUrl,
            currentMainUrl,
            new PictureLink(url, record, 0, null));
        var updated = (current ?? new OwnerGame(gameId, null, null, null)) with { Picture = next };
        var games = data.Games.Where(game => game.GameId != gameId).Append(updated);

        return new OwnerChange(
            data with { Games = [.. games.OrderBy(game => game.GameId)] },
            $"Picture of {title}: {Words(PictureChoice.Link)}",
            ChosenStatusKey);
    }

    /// <summary>
    /// Removes the picture link a game has, and with it the record of the stored copy; the files are tidied away later by the
    /// clean-up, which keeps what the owner data refers to. When the link was the choice, the choice becomes Automatic and the
    /// game no longer counts as checked, so it asks for a look again by the normal rule; any other choice stays as it is. A game
    /// without a link has nothing to remove.
    /// </summary>
    /// <param name="data">The data to change.</param>
    /// <param name="gameId">The game.</param>
    /// <param name="title">The game's title, for the change log.</param>
    public static OwnerChange RemoveLink(OwnerData data, int gameId, string title)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(title);

        var current = data.Games.FirstOrDefault(game => game.GameId == gameId);

        if (current?.Picture is not { Link: not null } setting)
        {
            return OwnerChange.None;
        }

        var wasChoice = setting.Choice == PictureChoice.Link;
        var next = setting with
        {
            Link = null,
            Choice = wasChoice ? PictureChoice.Automatic : setting.Choice,
            Checked = !wasChoice && setting.Checked,
        };
        var kept = next.Choice == PictureChoice.Automatic && !next.Checked ? null : next;
        var updated = current with { Picture = kept };
        var others = data.Games.Where(game => game.GameId != gameId);
        var games = LocationEdits.IsEmpty(updated) ? others : others.Append(updated);

        return new OwnerChange(
            data with { Games = [.. games.OrderBy(game => game.GameId)] },
            $"Removed the picture link of {title}",
            RemovedStatusKey);
    }

    /// <summary>
    /// Records that the owner looked at a game's pictures and they look right. The choice stays as it is, both picture addresses
    /// BGG offers right now are remembered so a later change of either is noticed, and a link that counts as dead is accepted:
    /// its stored copy stays, its failures are forgotten, and it asks for a look again only after failing again.
    /// </summary>
    /// <param name="data">The data to change.</param>
    /// <param name="gameId">The game.</param>
    /// <param name="title">The game's title, for the change log.</param>
    /// <param name="currentOwnedUrl">The address of the owned edition's picture now, or null when BGG offers none.</param>
    /// <param name="currentMainUrl">The address of the game's main picture now, or null when BGG offers none.</param>
    /// <param name="now">The moment of the check, which decides whether the game's link counts as dead.</param>
    public static OwnerChange LooksRight(
        OwnerData data,
        int gameId,
        string title,
        string? currentOwnedUrl,
        string? currentMainUrl,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(title);

        var current = data.Games.FirstOrDefault(game => game.GameId == gameId);
        var setting = current?.Picture ?? new PictureSetting(PictureChoice.Automatic, false, null, null, null);
        var next = setting with
        {
            Checked = true,
            SeenOwnedUrl = currentOwnedUrl,
            SeenMainUrl = currentMainUrl,
            Link = AcceptedLink(setting.Link, now),
        };

        if (current?.Picture is not null && next == setting)
        {
            return OwnerChange.None;
        }

        var updated = (current ?? new OwnerGame(gameId, null, null, null)) with { Picture = next };
        var games = data.Games.Where(game => game.GameId != gameId).Append(updated);

        return new OwnerChange(
            data with { Games = [.. games.OrderBy(game => game.GameId)] },
            $"Marked {title} as checked",
            CheckedStatusKey);
    }

    private static PictureLink? AcceptedLink(PictureLink? link, DateTimeOffset? now) =>
        link is not null && now is { } moment && LinkHealth.IsDead(link, moment)
            ? link with { FailedChecks = 0, FailedSinceUtc = null }
            : link;

    private static string Words(PictureChoice choice) => choice switch
    {
        PictureChoice.OwnedEdition => "owned edition",
        PictureChoice.MainPicture => "main picture",
        PictureChoice.Link => "your link",
        _ => "back to Automatic",
    };
}

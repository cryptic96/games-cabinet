namespace Cabinet.Domain.Owner;

/// <summary>The edits of the owner's picture decisions. Every edit is pure: it gives back new data and leaves the old untouched.</summary>
public static class PictureEdits
{
    private const string ChosenStatusKey = "picture-chosen";
    private const string NoPictureStatusKey = "no-picture";
    private const string NoLinkStatusKey = "no-link";

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
    public static OwnerChange Choose(
        OwnerData data,
        int gameId,
        PictureChoice choice,
        string title,
        string? currentOwnedUrl,
        string? currentMainUrl)
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
            setting?.Link);
        var kept = choice == PictureChoice.Automatic && !next.Checked && next.Link is null ? null : next;
        var updated = (current ?? new OwnerGame(gameId, null, null, null)) with { Picture = kept };
        var others = data.Games.Where(game => game.GameId != gameId);
        var games = LocationEdits.IsEmpty(updated) ? others : others.Append(updated);

        return new OwnerChange(
            data with { Games = [.. games.OrderBy(game => game.GameId)] },
            $"Picture of {title}: {Words(choice)}",
            ChosenStatusKey);
    }

    private static string Words(PictureChoice choice) => choice switch
    {
        PictureChoice.OwnedEdition => "owned edition",
        PictureChoice.MainPicture => "main picture",
        PictureChoice.Link => "your link",
        _ => "back to Automatic",
    };
}

using Cabinet.Domain.Collection;

namespace Cabinet.Domain.Owner;

/// <summary>
/// The edits of the owner's picture decisions. Every edit is pure: it gives back new data and leaves the old untouched. The forms
/// that take a game mean every copy of that game; the forms that take a copy change that copy alone. No edit touches a location,
/// and no edit drops another entry of the game: only the pictures of the entries it names change.
/// </summary>
public static class PictureEdits
{
    private const string ChosenStatusKey = "picture-chosen";
    private const string NoPictureStatusKey = "no-picture";
    private const string NoLinkStatusKey = "no-link";
    private const string CheckedStatusKey = "picture-checked";
    private const string RemovedStatusKey = "link-removed";

    /// <summary>
    /// Sets which picture every copy of a game is drawn from: every entry of the game gets the choice, and the entry that holds
    /// for every copy is added when the game has none, so a copy the collection gains later follows too. Locations are left as
    /// they are. A choice other than Automatic marks the game as checked, and every choice records the addresses of the two
    /// pictures BGG offers right now, so a later change of either can be noticed. The kinds follow BGG: nothing but the kind is
    /// stored, never an address to pin. A link the owner entered earlier stays stored when another kind is chosen, so it can be
    /// chosen again. An entry that has no link cannot take the choice of the link and keeps its picture as it is.
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

        return EditEveryCopy(
            data,
            gameId,
            setting => ChooseStep(setting, choice, currentOwnedUrl, currentMainUrl, now),
            $"Picture of {title}: {Words(choice)}",
            ChosenStatusKey);
    }

    /// <summary>
    /// Sets which picture one copy of a game is drawn from and no other copy: a choice that holds for every copy of the game is
    /// first turned into one choice per copy that exists, so the other copies keep their picture and their location. The rules
    /// of the choice are those of the form that takes a game, judged against what applies to the copy now.
    /// </summary>
    /// <param name="data">The data to change.</param>
    /// <param name="gameId">The game the copy belongs to.</param>
    /// <param name="collectionId">The copy.</param>
    /// <param name="copies">The collection entries of the game that exist now, the copy included.</param>
    /// <param name="choice">The kind of picture to draw.</param>
    /// <param name="title">The game's title, for the change log.</param>
    /// <param name="currentOwnedUrl">The address of the owned edition's picture now, or null when BGG offers none.</param>
    /// <param name="currentMainUrl">The address of the game's main picture now, or null when BGG offers none.</param>
    /// <param name="now">The moment of the choice, as for the form that takes a game.</param>
    public static OwnerChange Choose(
        OwnerData data,
        int gameId,
        long collectionId,
        IReadOnlyList<long> copies,
        PictureChoice choice,
        string title,
        string? currentOwnedUrl,
        string? currentMainUrl,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(copies);
        ArgumentNullException.ThrowIfNull(title);

        if (!Enum.IsDefined(choice))
        {
            return OwnerChange.Refused(data, NoPictureStatusKey);
        }

        return EditOneCopy(
            data,
            gameId,
            collectionId,
            copies,
            setting => ChooseStep(setting, choice, currentOwnedUrl, currentMainUrl, now),
            $"Picture of {NameOf(title, collectionId, copies)}: {Words(choice)}",
            ChosenStatusKey);
    }

    /// <summary>
    /// Stores a picture the owner linked and chooses it for every copy of the game: the address and its processed record become
    /// the link of every entry of the game, replacing any link it had before, the choice becomes the link and the game counts
    /// as checked. The entry that holds for every copy is added when the game has none. Both picture addresses BGG offers right
    /// now are remembered so a later change of either is noticed. The pictures themselves are written before this edit is
    /// applied, so a record never refers to files that are not there. Locations and the other entries are left as they are.
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

        var linked = new PictureSetting(
            PictureChoice.Link,
            true,
            currentOwnedUrl,
            currentMainUrl,
            new PictureLink(url, record, 0, null));

        return EditEveryCopy(
            data,
            gameId,
            _ => Step.Changed(linked),
            $"Picture of {title}: {Words(PictureChoice.Link)}",
            ChosenStatusKey);
    }

    /// <summary>
    /// Stores a picture the owner linked for one copy of a game and chooses it for that copy alone: a choice that holds for every
    /// copy of the game is first turned into one choice per copy that exists, so the other copies keep their picture, their link
    /// and their location. The copy's link is replaced, its choice becomes the link and it counts as checked.
    /// </summary>
    /// <param name="data">The data to change.</param>
    /// <param name="gameId">The game the copy belongs to.</param>
    /// <param name="collectionId">The copy.</param>
    /// <param name="copies">The collection entries of the game that exist now, the copy included.</param>
    /// <param name="url">The canonical address of the linked picture.</param>
    /// <param name="record">The processed record of the picture.</param>
    /// <param name="title">The game's title, for the change log.</param>
    /// <param name="currentOwnedUrl">The address of the owned edition's picture now, or null when BGG offers none.</param>
    /// <param name="currentMainUrl">The address of the game's main picture now, or null when BGG offers none.</param>
    public static OwnerChange UseLink(
        OwnerData data,
        int gameId,
        long collectionId,
        IReadOnlyList<long> copies,
        string url,
        ImageRecord record,
        string title,
        string? currentOwnedUrl,
        string? currentMainUrl)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(copies);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(title);

        if (string.IsNullOrWhiteSpace(url) || record.Status != ImageStatus.Ok || record.Files is not { Count: > 0 })
        {
            return OwnerChange.Refused(data, NoLinkStatusKey);
        }

        var linked = new PictureSetting(
            PictureChoice.Link,
            true,
            currentOwnedUrl,
            currentMainUrl,
            new PictureLink(url, record, 0, null));

        return EditOneCopy(
            data,
            gameId,
            collectionId,
            copies,
            _ => Step.Changed(linked),
            $"Picture of {NameOf(title, collectionId, copies)}: {Words(PictureChoice.Link)}",
            ChosenStatusKey);
    }

    /// <summary>
    /// Removes the picture link of every copy of a game, and with it the record of the stored copy; the files are tidied away
    /// later by the clean-up, which keeps what the owner data refers to. Where the link was the choice, the choice becomes
    /// Automatic and the copy no longer counts as checked, so it asks for a look again by the normal rule; any other choice
    /// stays as it is. Entries without a link, locations and every other entry are left as they are, and a game without a link
    /// has nothing to remove.
    /// </summary>
    /// <param name="data">The data to change.</param>
    /// <param name="gameId">The game.</param>
    /// <param name="title">The game's title, for the change log.</param>
    public static OwnerChange RemoveLink(OwnerData data, int gameId, string title)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(title);

        return EditEveryCopy(data, gameId, RemoveLinkStep, $"Removed the picture link of {title}", RemovedStatusKey);
    }

    /// <summary>
    /// Removes the picture link of one copy of a game and with it the record of the stored copy, and no other copy's link: a
    /// choice that holds for every copy of the game is first turned into one choice per copy that exists. The rules are those of
    /// the form that takes a game, judged against what applies to the copy now; a copy without a link has nothing to remove.
    /// </summary>
    /// <param name="data">The data to change.</param>
    /// <param name="gameId">The game the copy belongs to.</param>
    /// <param name="collectionId">The copy.</param>
    /// <param name="copies">The collection entries of the game that exist now, the copy included.</param>
    /// <param name="title">The game's title, for the change log.</param>
    public static OwnerChange RemoveLink(OwnerData data, int gameId, long collectionId, IReadOnlyList<long> copies, string title)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(copies);
        ArgumentNullException.ThrowIfNull(title);

        return EditOneCopy(
            data,
            gameId,
            collectionId,
            copies,
            RemoveLinkStep,
            $"Removed the picture link of {NameOf(title, collectionId, copies)}",
            RemovedStatusKey);
    }

    /// <summary>
    /// Records that the owner looked at the pictures of every copy of a game and they look right. The choices stay as they are,
    /// both picture addresses BGG offers right now are remembered so a later change of either is noticed, and a link that counts
    /// as dead is accepted: its stored copy stays, its failures are forgotten, and it asks for a look again only after failing
    /// again. The entry that holds for every copy is added when the game has none; locations are left as they are.
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

        return EditEveryCopy(
            data,
            gameId,
            setting => LooksRightStep(setting, currentOwnedUrl, currentMainUrl, now),
            $"Marked {title} as checked",
            CheckedStatusKey);
    }

    /// <summary>
    /// Records that the owner looked at one copy's picture and it looks right, and no other copy's: a choice that holds for every
    /// copy of the game is first turned into one choice per copy that exists. The rules are those of the form that takes a game.
    /// </summary>
    /// <param name="data">The data to change.</param>
    /// <param name="gameId">The game the copy belongs to.</param>
    /// <param name="collectionId">The copy.</param>
    /// <param name="copies">The collection entries of the game that exist now, the copy included.</param>
    /// <param name="title">The game's title, for the change log.</param>
    /// <param name="currentOwnedUrl">The address of the owned edition's picture now, or null when BGG offers none.</param>
    /// <param name="currentMainUrl">The address of the game's main picture now, or null when BGG offers none.</param>
    /// <param name="now">The moment of the check, which decides whether the copy's link counts as dead.</param>
    public static OwnerChange LooksRight(
        OwnerData data,
        int gameId,
        long collectionId,
        IReadOnlyList<long> copies,
        string title,
        string? currentOwnedUrl,
        string? currentMainUrl,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(copies);
        ArgumentNullException.ThrowIfNull(title);

        return EditOneCopy(
            data,
            gameId,
            collectionId,
            copies,
            setting => LooksRightStep(setting, currentOwnedUrl, currentMainUrl, now),
            $"Marked {NameOf(title, collectionId, copies)} as checked",
            CheckedStatusKey);
    }

    private static Step ChooseStep(PictureSetting? setting, PictureChoice choice, string? currentOwnedUrl, string? currentMainUrl, DateTimeOffset? now)
    {
        if ((setting?.Choice ?? PictureChoice.Automatic) == choice)
        {
            return Step.Unchanged;
        }

        if ((choice == PictureChoice.OwnedEdition && currentOwnedUrl is null) || (choice == PictureChoice.MainPicture && currentMainUrl is null))
        {
            return Step.Refused(NoPictureStatusKey);
        }

        if (choice == PictureChoice.Link && setting?.Link is null)
        {
            return Step.Refused(NoLinkStatusKey);
        }

        var next = new PictureSetting(
            choice,
            setting?.Checked == true || choice != PictureChoice.Automatic,
            currentOwnedUrl,
            currentMainUrl,
            AcceptedLink(setting?.Link, now));

        return Step.Changed(choice == PictureChoice.Automatic && !next.Checked && next.Link is null ? null : next);
    }

    private static Step RemoveLinkStep(PictureSetting? current)
    {
        if (current is not { Link: not null } setting)
        {
            return Step.Unchanged;
        }

        var wasChoice = setting.Choice == PictureChoice.Link;
        var next = setting with
        {
            Link = null,
            Choice = wasChoice ? PictureChoice.Automatic : setting.Choice,
            Checked = !wasChoice && setting.Checked,
        };

        return Step.Changed(next.Choice == PictureChoice.Automatic && !next.Checked ? null : next);
    }

    private static Step LooksRightStep(PictureSetting? current, string? currentOwnedUrl, string? currentMainUrl, DateTimeOffset now)
    {
        var setting = current ?? new PictureSetting(PictureChoice.Automatic, false, null, null, null);
        var next = setting with
        {
            Checked = true,
            SeenOwnedUrl = currentOwnedUrl,
            SeenMainUrl = currentMainUrl,
            Link = AcceptedLink(setting.Link, now),
        };

        return current is not null && next == setting ? Step.Unchanged : Step.Changed(next);
    }

    /// <summary>
    /// Applies an edit of a picture to every entry of a game, adding the entry that holds for every copy when the game has none.
    /// It is the one way the game-level forms rewrite the owner data, and it changes pictures only: every entry keeps its
    /// location, its absence and its collection entry, and an entry the edit leaves as it is stays.
    /// </summary>
    private static OwnerChange EditEveryCopy(OwnerData data, int gameId, Func<PictureSetting?, Step> edit, string log, string statusKey)
    {
        var entries = data.Games.Where(game => game.GameId == gameId).ToList();

        if (!entries.Any(game => game.CollectionId is null))
        {
            entries.Add(new OwnerGame(gameId, null, null, null));
        }

        var steps = entries.Select(entry => (Entry: entry, Step: edit(entry.Picture))).ToList();

        if (steps.All(pair => pair.Step.Kind != StepKind.Changed))
        {
            var refusal = steps.Select(pair => pair.Step).FirstOrDefault(step => step.Kind == StepKind.Refused);

            return refusal.Kind == StepKind.Refused ? OwnerChange.Refused(data, refusal.StatusKey) : OwnerChange.None;
        }

        var after = steps.Select(pair => pair.Step.Kind == StepKind.Changed ? pair.Entry with { Picture = pair.Step.Setting } : pair.Entry);

        return new OwnerChange(Rewrite(data, gameId, after), log, statusKey);
    }

    /// <summary>
    /// Applies an edit of a picture to one copy of a game after a choice that holds for every copy is turned into one choice per
    /// copy, and keeps every other entry of the game as it was. The edit is judged against what applies to the copy now: its own
    /// picture, else the one that holds for every copy.
    /// </summary>
    private static OwnerChange EditOneCopy(
        OwnerData data,
        int gameId,
        long collectionId,
        IReadOnlyList<long> copies,
        Func<PictureSetting?, Step> edit,
        string log,
        string statusKey)
    {
        var applies = data.Games.FirstOrDefault(game => game.GameId == gameId && game.CollectionId == collectionId)?.Picture
            ?? OwnerGames.Find(data, gameId, null)?.Picture;
        var step = edit(applies);

        if (step.Kind != StepKind.Changed)
        {
            return step.Kind == StepKind.Refused ? OwnerChange.Refused(data, step.StatusKey) : OwnerChange.None;
        }

        var split = OwnerGames.SplitForEdit(data, gameId, [.. copies.Append(collectionId)]);
        var entries = split.Games.Where(game => game.GameId == gameId).ToList();
        var own = entries.FirstOrDefault(game => game.CollectionId == collectionId);
        var updated = (own ?? new OwnerGame(gameId, null, null, null, collectionId)) with { Picture = step.Setting };
        var after = entries.Where(game => !ReferenceEquals(game, own)).Append(updated);

        return new OwnerChange(Rewrite(split, gameId, after), log, statusKey);
    }

    /// <summary>Replaces the entries of one game with the given complete list, drops the ones that hold nothing, and keeps every other game's entries as they are.</summary>
    private static OwnerData Rewrite(OwnerData data, int gameId, IEnumerable<OwnerGame> entriesOfGame)
    {
        var others = data.Games.Where(game => game.GameId != gameId);

        return data with { Games = OwnerGames.Ordered(others.Concat(entriesOfGame.Where(game => !LocationEdits.IsEmpty(game)))) };
    }

    private static string NameOf(string title, long collectionId, IReadOnlyList<long> copies)
    {
        var all = copies.Append(collectionId).Distinct().Order().ToList();

        return all.Count > 1 ? $"{title} (copy {all.IndexOf(collectionId) + 1} of {all.Count})" : title;
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

    private enum StepKind
    {
        Unchanged,
        Refused,
        Changed,
    }

    /// <summary>What an edit comes to for one entry: nothing, a refusal with the key that says why, or the picture the entry holds afterwards, which is null when it holds none.</summary>
    private readonly record struct Step(StepKind Kind, string StatusKey, PictureSetting? Setting)
    {
        public static Step Unchanged { get; } = new(StepKind.Unchanged, string.Empty, null);

        public static Step Refused(string statusKey) => new(StepKind.Refused, statusKey, null);

        public static Step Changed(PictureSetting? setting) => new(StepKind.Changed, string.Empty, setting);
    }
}

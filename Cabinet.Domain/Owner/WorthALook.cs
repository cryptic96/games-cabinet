using Cabinet.Domain.Collection;

namespace Cabinet.Domain.Owner;

/// <summary>Which of the two pictures BGG offers changed since the owner last looked.</summary>
public enum ChangedPicture
{
    /// <summary>The picture of the owned edition has another address now.</summary>
    OwnedEdition,

    /// <summary>The game's main picture has another address now.</summary>
    MainPicture,
}

/// <summary>
/// What the owner's worklist knows about one game: the reasons it is worth a look, whether the owner has dealt with it, and the
/// marks the pictures page shows. It is worked out from stored facts only, never from what the browser could load.
/// </summary>
/// <param name="AutomaticSkipsOwned">Whether the owned edition has a usable picture that the automatic pick does not draw.</param>
/// <param name="NoPicture">Whether the picture that is drawn is the generated cover and no candidate has a stored picture that failed.</param>
/// <param name="WontLoad">Whether a picture that should be drawn has a stored record that is unusable, so a generated cover is drawn instead.</param>
/// <param name="Changed">Which picture BGG replaced since the owner last looked, or null.</param>
/// <param name="LinkDead">Whether the owner's linked picture failed often enough, over enough time, to count as dead.</param>
/// <param name="Fetching">Whether the owner's link is stored but its picture has not been fetched yet.</param>
/// <param name="Resolved">Whether the owner dealt with the game and nothing has changed since.</param>
public sealed record WorthALookResult(
    bool AutomaticSkipsOwned,
    bool NoPicture,
    bool WontLoad,
    ChangedPicture? Changed,
    bool LinkDead,
    bool Fetching,
    bool Resolved)
{
    /// <summary>Whether the game is worth a look for any of the reasons.</summary>
    public bool Candidate => AutomaticSkipsOwned || NoPicture || WontLoad || Changed is not null || LinkDead;

    /// <summary>Whether the game is a candidate the owner has not dealt with yet.</summary>
    public bool ToCheck => Candidate && !Resolved;

    /// <summary>
    /// Where the game sorts among the games to check: 0 for a missing or broken picture, 1 for a link that no longer works,
    /// 2 for a picture BGG changed, and 3 for a game that is only worth a look because the automatic pick skips the owned edition.
    /// </summary>
    public int AttentionRank => NoPicture || WontLoad ? 0 : LinkDead ? 1 : Changed is not null ? 2 : 3;

    /// <summary>Whether the game carries a mark that asks for attention, as opposed to a quiet one.</summary>
    public bool NeedsAttention => AttentionRank < 3;
}

/// <summary>
/// The one rule that says which games the owner's pictures page opens on. A game is a candidate when the automatic pick skips
/// a usable owned-edition picture, when the picture drawn is missing or broken, when BGG replaced a picture the owner's decision
/// follows, or when the owner's link no longer works. The owner deals with a candidate by marking it as looking right or by
/// choosing a picture; either is undone by a picture change on BGG or a dead link. Pictures are compared by address, never by
/// the names of the files stored from them, so re-encoding a picture never raises a false alarm.
/// </summary>
public static class WorthALook
{
    /// <summary>Works out what the worklist knows about one game.</summary>
    /// <param name="trace">The automatic mapping of the game, made without the owner's decisions.</param>
    /// <param name="mainUrl">The address of the game's main picture now, or null when BGG offers none.</param>
    /// <param name="setting">The owner's picture decision, or null when there is none.</param>
    /// <param name="images">The stored picture records by source address.</param>
    /// <param name="now">The moment the answer is wanted for, which decides whether a link counts as dead.</param>
    public static WorthALookResult Evaluate(
        MappedItemTrace trace,
        string? mainUrl,
        PictureSetting? setting,
        IReadOnlyDictionary<string, ImageRecord> images,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(images);

        var ownedUrl = trace.Item.VersionImageUrl;
        var choice = setting?.Choice ?? PictureChoice.Automatic;
        var link = setting?.Link;
        var skipsOwned = trace.VersionImage is not null && trace.Pick != ArtPick.VersionImage;
        var (noPicture, wontLoad) = MissingOrBroken(trace, mainUrl, choice, link, images);
        var changed = ChangedOf(setting, choice, ownedUrl, mainUrl);
        var dead = link is not null && LinkHealth.IsDead(link, now);
        var dealtWith = (setting?.Checked ?? false) || choice != PictureChoice.Automatic;

        return new WorthALookResult(
            skipsOwned,
            noPicture,
            wontLoad,
            changed,
            dead,
            link is { Record: null } && !dead,
            dealtWith && changed is null && !dead);
    }

    private static (bool NoPicture, bool WontLoad) MissingOrBroken(
        MappedItemTrace trace,
        string? mainUrl,
        PictureChoice choice,
        PictureLink? link,
        IReadOnlyDictionary<string, ImageRecord> images)
    {
        var ownedUrl = trace.Item.VersionImageUrl;

        return choice switch
        {
            PictureChoice.OwnedEdition => Candidate(ownedUrl, trace.VersionImage, images),
            PictureChoice.MainPicture => Candidate(mainUrl, trace.MainImage, images),
            PictureChoice.Link => (false, link?.Record is { } record && !IsUsable(record)),
            _ => trace.Pick == ArtPick.GeneratedCover ? Generated(ownedUrl, mainUrl, images) : (false, false),
        };
    }

    private static (bool NoPicture, bool WontLoad) Candidate(string? url, ImageRecord? usable, IReadOnlyDictionary<string, ImageRecord> images)
    {
        if (usable is not null)
        {
            return (false, false);
        }

        return url is null ? (true, false) : (false, images.ContainsKey(url));
    }

    private static (bool NoPicture, bool WontLoad) Generated(string? ownedUrl, string? mainUrl, IReadOnlyDictionary<string, ImageRecord> images)
    {
        var failed = new[] { ownedUrl, mainUrl }.Any(url => url is not null && images.ContainsKey(url));

        return (!failed, failed);
    }

    private static ChangedPicture? ChangedOf(PictureSetting? setting, PictureChoice choice, string? ownedUrl, string? mainUrl)
    {
        if (setting is null || !(setting.Checked || choice != PictureChoice.Automatic) || choice == PictureChoice.Link)
        {
            return null;
        }

        var ownedChanged = !string.Equals(setting.SeenOwnedUrl, ownedUrl, StringComparison.Ordinal);
        var mainChanged = !string.Equals(setting.SeenMainUrl, mainUrl, StringComparison.Ordinal);

        return choice switch
        {
            PictureChoice.OwnedEdition => ownedChanged ? ChangedPicture.OwnedEdition : null,
            PictureChoice.MainPicture => mainChanged ? ChangedPicture.MainPicture : null,
            _ => ownedChanged ? ChangedPicture.OwnedEdition : mainChanged ? ChangedPicture.MainPicture : null,
        };
    }

    private static bool IsUsable(ImageRecord record) => record is { Status: ImageStatus.Ok, Files.Count: > 0 };
}

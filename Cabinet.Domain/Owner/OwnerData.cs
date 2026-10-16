using Cabinet.Domain.Collection;

namespace Cabinet.Domain.Owner;

/// <summary>One place the owner keeps games, such as a cabinet or a shelf. Games refer to it by its identifier, so a rename moves nothing.</summary>
/// <param name="Id">The stable identifier the server generated when the location was added.</param>
/// <param name="Name">The name every visitor sees on a card.</param>
public sealed record OwnerLocation(string Id, string Name)
{
    /// <summary>How many characters an identifier has.</summary>
    public const int IdLength = 8;

    /// <summary>Tells whether a text has the shape of an identifier the server generates: <see cref="IdLength"/> lowercase hexadecimal digits.</summary>
    /// <param name="text">The text, or null.</param>
    public static bool IsWellFormedId(string? text) =>
        text is { Length: IdLength } && text.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

/// <summary>Which of the available pictures the owner wants a game drawn from.</summary>
public enum PictureChoice
{
    /// <summary>The cabinet picks the picture by its own rules.</summary>
    Automatic,

    /// <summary>The picture of the owned edition.</summary>
    OwnedEdition,

    /// <summary>The game's main picture.</summary>
    MainPicture,

    /// <summary>A picture at an address the owner entered.</summary>
    Link,
}

/// <summary>A picture the owner pointed to by address, and what is known about fetching it.</summary>
/// <param name="Url">The address the owner entered.</param>
/// <param name="Record">What was stored of the fetched picture, or null when it has not been fetched.</param>
/// <param name="FailedChecks">How many checks in a row found the address unusable.</param>
/// <param name="FailedSinceUtc">When the address first stopped being usable, or null while it works.</param>
public sealed record PictureLink(string Url, ImageRecord? Record, int FailedChecks, DateTimeOffset? FailedSinceUtc);

/// <summary>The owner's picture decision for one game.</summary>
/// <param name="Choice">Which picture to draw.</param>
/// <param name="Checked">Whether the owner has looked at this game's picture and marked it as seen.</param>
/// <param name="SeenOwnedUrl">The address of the owned edition's picture when the owner last looked, or null.</param>
/// <param name="SeenMainUrl">The address of the main picture when the owner last looked, or null.</param>
/// <param name="Link">The owner's own picture address and what is known about it, or null when the owner has none. It is kept when another picture is chosen, so the owner can pick it again.</param>
public sealed record PictureSetting(
    PictureChoice Choice,
    bool Checked,
    string? SeenOwnedUrl,
    string? SeenMainUrl,
    PictureLink? Link);

/// <summary>What the owner decided for one game.</summary>
/// <param name="GameId">The game the decisions are for.</param>
/// <param name="LocationId">The location the game is kept in, or null when it has no location of its own.</param>
/// <param name="AbsentSinceUtc">When the game stopped being owned, or null while it is owned; its decisions are kept for a while.</param>
/// <param name="Picture">The picture decision, or null when the owner made none.</param>
/// <param name="CollectionId">The collection entry this choice is for, or null for every copy of the game, the form older files have.</param>
public sealed record OwnerGame(int GameId, string? LocationId, DateTimeOffset? AbsentSinceUtc, PictureSetting? Picture, long? CollectionId = null);

/// <summary>The three counts that say what a copy of the owner data holds, which the owner compares before a copy replaces another.</summary>
/// <param name="Locations">How many locations there are.</param>
/// <param name="PlacedGames">How many games are kept in a location.</param>
/// <param name="PictureChoices">How many games have a picture other than the automatic one.</param>
public readonly record struct OwnerDataFigures(int Locations, int PlacedGames, int PictureChoices)
{
    /// <summary>Counts what a copy of the owner data holds.</summary>
    /// <param name="data">The owner data.</param>
    public static OwnerDataFigures From(OwnerData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return new OwnerDataFigures(
            data.Locations.Count,
            data.Games.Where(game => game.LocationId is not null).Select(game => game.GameId).Distinct().Count(),
            data.Games.Count(game => game.Picture is { } picture && picture.Choice != PictureChoice.Automatic));
    }
}

/// <summary>One line of the owner's change log.</summary>
/// <param name="AtUtc">When the change was made.</param>
/// <param name="Text">What changed, in words.</param>
public sealed record ChangeEntry(DateTimeOffset AtUtc, string Text);

/// <summary>
/// Everything the owner entered by hand: the ordered list of locations, where each game is kept, the picture decisions, and
/// the log of changes. It is stored apart from the synced collection and applied on top of it, so a sync never touches it.
/// </summary>
/// <param name="Kind">The marker that says this is an owner-data file.</param>
/// <param name="SchemaVersion">The version of the stored shape; a reader refuses a version newer than it knows.</param>
/// <param name="Revision">Counts the saves, so a writer working from an old copy is told it is out of date.</param>
/// <param name="SavedAtUtc">When the data was last saved, or null before the first save.</param>
/// <param name="Look">The look of the cabinet for every visitor.</param>
/// <param name="Locations">The locations in the order the owner set.</param>
/// <param name="Games">The per-game and per-copy decisions, sorted by game identifier and then by collection entry, the choice that holds for every copy first.</param>
/// <param name="LastDownloadAtUtc">When the owner last downloaded a backup, or null when never.</param>
/// <param name="ChangesSinceDownload">How many changes were saved since that download.</param>
/// <param name="Changes">The change log, newest first.</param>
public sealed record OwnerData(
    string Kind,
    int SchemaVersion,
    long Revision,
    DateTimeOffset? SavedAtUtc,
    CabinetLook Look,
    IReadOnlyList<OwnerLocation> Locations,
    IReadOnlyList<OwnerGame> Games,
    DateTimeOffset? LastDownloadAtUtc,
    int ChangesSinceDownload,
    IReadOnlyList<ChangeEntry> Changes)
{
    /// <summary>The marker every owner-data file carries.</summary>
    public const string KindMarker = "games-cabinet-owner-data";

    /// <summary>The stored shape this build writes and understands.</summary>
    public const int CurrentSchemaVersion = 3;

    /// <summary>The most locations the owner can have.</summary>
    public const int MaxLocations = 50;

    /// <summary>The longest location name, in characters.</summary>
    public const int MaxNameLength = 60;

    /// <summary>How many change log lines are kept in the file.</summary>
    public const int MaxStoredChanges = 200;

    /// <summary>How many change log lines the owner page shows.</summary>
    public const int VisibleChanges = 20;

    /// <summary>Owner data with nothing entered yet.</summary>
    public static OwnerData Empty { get; } = new(KindMarker, CurrentSchemaVersion, 0, null, CabinetLook.Default, [], [], null, 0, []);
}

/// <summary>What a load of the owner data found.</summary>
public abstract record OwnerDataState
{
    /// <summary>The data was read, or there was none yet and it is empty.</summary>
    /// <param name="Data">The owner data.</param>
    public sealed record Ready(OwnerData Data) : OwnerDataState;

    /// <summary>A file exists that this build cannot read, so it was left exactly as it is and nothing may be written over it.</summary>
    /// <param name="Reason">A short category of why, never the content.</param>
    public sealed record Unreadable(string Reason) : OwnerDataState;
}

/// <summary>One earlier copy of the owner-data file, kept so a wrong edit can be undone by hand.</summary>
/// <param name="Index">The place of the copy in the listing, newest first and counting from zero; it is how a copy is read back.</param>
/// <param name="SavedAtUtc">When the copy was taken.</param>
public sealed record EarlierVersion(int Index, DateTimeOffset SavedAtUtc);

/// <summary>Keeps the owner data in a file that this build never renames, moves or overwrites unless it wrote it.</summary>
public interface IOwnerDataStore
{
    /// <summary>Reads the stored owner data.</summary>
    OwnerDataState Load();

    /// <summary>Stores the data so that a reader sees either the previous copy or this one, never a mixture.</summary>
    /// <param name="data">The data to store.</param>
    void Save(OwnerData data);

    /// <summary>Copies the stored file, as it is and readable or not, to the earlier versions under a name every earlier release lists, and drops the oldest ordinary copies beyond the number kept. Only a copy in the current format is ordinary: a copy in an older format is never replaced, counted or dropped, whatever its name. It does nothing when there is no file yet.</summary>
    /// <param name="now">The moment the copy is stamped with.</param>
    void KeepEarlierVersion(DateTimeOffset now);

    /// <summary>The kept copies, newest first.</summary>
    IReadOnlyList<EarlierVersion> EarlierVersions();

    /// <summary>Reads a kept copy by its place in <see cref="EarlierVersions"/>; null when there is no copy at that place.</summary>
    /// <param name="index">The place in the listing.</param>
    byte[]? ReadEarlierVersion(int index);

    /// <summary>When the newest kept copy was taken, or null when there is none.</summary>
    DateTimeOffset? NewestEarlierVersionAt();

    /// <summary>The schema version written in the file on disk, or null when there is no file or the file or its header cannot be read; null therefore never means the current format. It never writes.</summary>
    int? StoredSchemaVersion();
}

/// <summary>
/// The thirty-day rule: what the owner decided for a game is kept for a while after the game leaves the collection, in case
/// it was only a hiccup or the owner takes it back, and forgotten once the game stayed away for thirty days of accepted collections.
/// </summary>
public static class OwnerExpiry
{
    /// <summary>How long a game may be missing from accepted collections before its owner data is removed.</summary>
    public static readonly TimeSpan AbsentAfter = TimeSpan.FromDays(30);

    /// <summary>Works out the owner data after an accepted collection that lists these games.</summary>
    /// <param name="data">The owner data as it is.</param>
    /// <param name="listedGameIds">Every game the accepted collection lists, base games and expansions.</param>
    /// <param name="now">The moment the collection was accepted.</param>
    /// <returns>The new owner data, or null when nothing needs to change.</returns>
    public static OwnerData? Reconcile(OwnerData data, IReadOnlySet<int> listedGameIds, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(listedGameIds);

        var changed = false;
        var games = new List<OwnerGame>(data.Games.Count);

        foreach (var game in data.Games)
        {
            var next = Next(game, listedGameIds.Contains(game.GameId), now);
            changed |= !ReferenceEquals(next, game);

            if (next is not null)
            {
                games.Add(next);
            }
        }

        return changed ? data with { Games = games } : null;
    }

    private static OwnerGame? Next(OwnerGame game, bool listed, DateTimeOffset now)
    {
        if (listed)
        {
            return game.AbsentSinceUtc is null ? game : game with { AbsentSinceUtc = null };
        }

        if (game.AbsentSinceUtc is not { } since)
        {
            return game with { AbsentSinceUtc = now };
        }

        return now - since >= AbsentAfter ? null : game;
    }
}

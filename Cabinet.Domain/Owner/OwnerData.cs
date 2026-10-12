using Cabinet.Domain.Collection;

namespace Cabinet.Domain.Owner;

/// <summary>One place the owner keeps games, such as a cabinet or a shelf. Games refer to it by its identifier, so a rename moves nothing.</summary>
/// <param name="Id">The stable identifier the server generated when the location was added.</param>
/// <param name="Name">The name every visitor sees on a card.</param>
public sealed record OwnerLocation(string Id, string Name);

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
/// <param name="Link">The owner's own picture address when <see cref="Choice"/> is <see cref="PictureChoice.Link"/>, otherwise null.</param>
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
public sealed record OwnerGame(int GameId, string? LocationId, DateTimeOffset? AbsentSinceUtc, PictureSetting? Picture);

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
/// <param name="Finish">The wood finish of the cabinet.</param>
/// <param name="Locations">The locations in the order the owner set.</param>
/// <param name="Games">The per-game decisions, sorted by game identifier.</param>
/// <param name="LastDownloadAtUtc">When the owner last downloaded a backup, or null when never.</param>
/// <param name="ChangesSinceDownload">How many changes were saved since that download.</param>
/// <param name="Changes">The change log, newest first.</param>
public sealed record OwnerData(
    string Kind,
    int SchemaVersion,
    long Revision,
    DateTimeOffset? SavedAtUtc,
    string Finish,
    IReadOnlyList<OwnerLocation> Locations,
    IReadOnlyList<OwnerGame> Games,
    DateTimeOffset? LastDownloadAtUtc,
    int ChangesSinceDownload,
    IReadOnlyList<ChangeEntry> Changes)
{
    /// <summary>The marker every owner-data file carries.</summary>
    public const string KindMarker = "games-cabinet-owner-data";

    /// <summary>The stored shape this build writes and understands.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The most locations the owner can have.</summary>
    public const int MaxLocations = 50;

    /// <summary>The longest location name, in characters.</summary>
    public const int MaxNameLength = 60;

    /// <summary>How many change log lines are kept in the file.</summary>
    public const int MaxStoredChanges = 200;

    /// <summary>How many change log lines the owner page shows.</summary>
    public const int VisibleChanges = 20;

    /// <summary>The wood finish used until the owner picks another.</summary>
    public const string DefaultFinish = "classic";

    /// <summary>Owner data with nothing entered yet.</summary>
    public static OwnerData Empty { get; } = new(KindMarker, CurrentSchemaVersion, 0, null, DefaultFinish, [], [], null, 0, []);
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

/// <summary>Keeps the owner data in a file that this build never renames, moves or overwrites unless it wrote it.</summary>
public interface IOwnerDataStore
{
    /// <summary>Reads the stored owner data.</summary>
    OwnerDataState Load();

    /// <summary>Stores the data so that a reader sees either the previous copy or this one, never a mixture.</summary>
    /// <param name="data">The data to store.</param>
    void Save(OwnerData data);
}

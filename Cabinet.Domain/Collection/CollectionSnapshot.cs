using Cabinet.Domain.Layout;

namespace Cabinet.Domain.Collection;

/// <summary>The box dimensions BoardGameGeek reports for a game version, in the unit it reports them in.</summary>
/// <param name="Width">The front width of the box.</param>
/// <param name="Length">The standing height of the box.</param>
/// <param name="Depth">The thickness of the box.</param>
public sealed record VersionDimensions(double Width, double Length, double Depth);

/// <summary>One owned item as it is stored: what was read from the source, before it is turned into something to draw.</summary>
/// <param name="CollectionId">The identifier of the collection entry.</param>
/// <param name="GameId">The identifier of the game.</param>
/// <param name="Title">The title as the source gave it; it may be blank.</param>
/// <param name="Kind">Whether this is a standalone game or an expansion.</param>
/// <param name="Year">The publication year, or null when the source gave none.</param>
/// <param name="Dimensions">The box dimensions of the selected version, or null when the source gave none.</param>
/// <param name="Location">Where the owner keeps the item, or null when it is not known.</param>
/// <param name="VersionImageUrl">The canonical address of the picture of the owned version, or null when the source gave none.</param>
/// <param name="ImageUrl">The canonical address of the game's main picture, or null when the source gave none.</param>
public sealed record SnapshotItem(
    long CollectionId,
    int GameId,
    string Title,
    ItemKind Kind,
    int? Year,
    VersionDimensions? Dimensions,
    string? Location,
    string? VersionImageUrl = null,
    string? ImageUrl = null);

/// <summary>How the last attempt to fetch and store a picture ended.</summary>
public enum ImageStatus
{
    /// <summary>The picture was fetched, resized and stored.</summary>
    Ok,

    /// <summary>The picture could not be fetched: the host was unreachable or answered with an error.</summary>
    Failed,

    /// <summary>The bytes were fetched but could not be read as a picture.</summary>
    Undecodable,

    /// <summary>The picture was not fetched or not used because it broke a rule: an address, a size or a type.</summary>
    Refused,
}

/// <summary>One stored size of a picture.</summary>
/// <param name="Width">The width of the stored file in pixels.</param>
/// <param name="Height">The height of the stored file in pixels.</param>
/// <param name="Name">The file name inside the art directory; it carries a hash of the file's content.</param>
public sealed record ArtFile(int Width, int Height, string Name);

/// <summary>What is known about one picture address: when it was last tried, how that ended and which files came of it.</summary>
/// <param name="SourceUrl">The canonical address the picture was fetched from.</param>
/// <param name="Status">How the last attempt ended.</param>
/// <param name="AttemptedAtUtc">When the last attempt was made.</param>
/// <param name="Files">The stored sizes, widest first, when the status is <see cref="ImageStatus.Ok"/>; otherwise null.</param>
public sealed record ImageRecord(
    string SourceUrl,
    ImageStatus Status,
    DateTimeOffset AttemptedAtUtc,
    IReadOnlyList<ArtFile>? Files = null);

/// <summary>The last good copy of the owned collection, as it is written to disk.</summary>
/// <param name="SchemaVersion">The version of the stored shape; a reader refuses a version newer than it knows.</param>
/// <param name="CapturedAtUtc">When the collection was read from its source.</param>
/// <param name="Items">The owned items.</param>
/// <param name="Images">What is known about each picture address the items name, keyed by the canonical address; null when nothing is known.</param>
public sealed record CollectionSnapshot(
    int SchemaVersion,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<SnapshotItem> Items,
    IReadOnlyDictionary<string, ImageRecord>? Images = null)
{
    /// <summary>The stored shape this build writes and understands.</summary>
    public const int CurrentSchemaVersion = 2;
}

/// <summary>Keeps the last good copy of the collection so a restart shows the same cabinet.</summary>
public interface ISnapshotStore
{
    /// <summary>Reads the stored collection, or returns null when there is none that can be used.</summary>
    CollectionSnapshot? Load();

    /// <summary>
    /// True when a stored collection exists that the last read could not use, so it is still on disk and a store would replace
    /// it. A store that has no such notion never reports it.
    /// </summary>
    bool HasUnreadStoredCollection => false;

    /// <summary>Stores the collection so that a reader sees either the previous copy or this one, never a mixture.</summary>
    /// <param name="snapshot">The collection to store.</param>
    void Save(CollectionSnapshot snapshot);
}

/// <summary>Why a sync did not produce a collection.</summary>
public enum SyncFailure
{
    /// <summary>There was no failure.</summary>
    None,

    /// <summary>The username or token is not configured, so nothing was requested.</summary>
    NotConfigured,

    /// <summary>The source refused the credentials.</summary>
    Unauthorized,

    /// <summary>The source could not be reached or answered with an error.</summary>
    Unavailable,

    /// <summary>The source asked to be called less often.</summary>
    Throttled,

    /// <summary>The source did not answer in time.</summary>
    Timeout,

    /// <summary>The answer was not in the expected shape.</summary>
    BadAnswer,

    /// <summary>The source was still preparing the answer when the waiting ran out.</summary>
    Queued,
}

/// <summary>How a finished sync ended.</summary>
public enum SyncResult
{
    /// <summary>The collection changed and the new one is shown.</summary>
    Changed,

    /// <summary>The collection is the same as the one already shown.</summary>
    Unchanged,

    /// <summary>The sync failed and the previous collection stays.</summary>
    Failed,

    /// <summary>The fetched collection was not applied because it looked wrong and the previous collection stays.</summary>
    HeldBack,
}

/// <summary>The answer of a collection source: the owned items or the reason there are none.</summary>
public abstract record CollectionFetchResult
{
    /// <summary>The source answered with the owned items.</summary>
    /// <param name="Items">The owned items.</param>
    public sealed record Fetched(IReadOnlyList<SnapshotItem> Items) : CollectionFetchResult;

    /// <summary>The source could not provide the collection.</summary>
    /// <param name="Failure">Why not.</param>
    public sealed record Failed(SyncFailure Failure) : CollectionFetchResult;
}

/// <summary>Reads the owned collection from wherever it lives.</summary>
public interface ICollectionSource
{
    /// <summary>Fetches the whole owned collection, or says why it could not.</summary>
    /// <param name="cancellationToken">Cancels the fetch.</param>
    Task<CollectionFetchResult> FetchOwnedAsync(CancellationToken cancellationToken);
}

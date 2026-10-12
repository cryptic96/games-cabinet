using System.Text.Json;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Microsoft.Extensions.Logging;

namespace Cabinet.Repository.Storage;

/// <summary>What a read-only load found.</summary>
/// <param name="Snapshot">The stored collection, or null when there is none to use.</param>
/// <param name="Reason">A one-line reason when there is no collection, otherwise null.</param>
public sealed record SnapshotReadResult(CollectionSnapshot? Snapshot, string? Reason);

/// <summary>Keeps the last good collection as <c>snapshot.json</c> in the storage directory.</summary>
public sealed class SnapshotStore : ISnapshotStore
{
    /// <summary>The name of the stored file.</summary>
    public const string FileName = "snapshot.json";

    /// <summary>The name a damaged or too-new file is renamed to. A file that could not be read at all is left in place.</summary>
    public const string SetAsideFileName = "snapshot.json.bad";

    private const string MalformedProblem = "malformed";
    private const string NewerSchemaProblem = "newer schema";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly string _path;
    private readonly ILogger<SnapshotStore> _logger;
    private volatile bool _unread;

    /// <summary>Creates a store over a directory.</summary>
    /// <param name="directory">The storage directory; it must exist.</param>
    /// <param name="logger">Receives one line when a stored file has to be set aside or could not be read.</param>
    public SnapshotStore(string directory, ILogger<SnapshotStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(logger);

        _path = Path.Combine(directory, FileName);
        _logger = logger;
    }

    /// <inheritdoc />
    public bool HasUnreadStoredCollection => _unread;

    /// <inheritdoc />
    public CollectionSnapshot? Load()
    {
        byte[] content;

        try
        {
            if (!File.Exists(_path))
            {
                _unread = false;

                return null;
            }

            content = File.ReadAllBytes(_path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _unread = true;
            _logger.LogWarning("The stored collection could not be read ({Reason}) and was left in place.", "unreadable");

            return null;
        }

        var snapshot = Parse(content);

        _unread = snapshot is null && File.Exists(_path);

        return snapshot;
    }

    /// <inheritdoc />
    public void Save(CollectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        AtomicJsonFile.WriteAtomically(_path, JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions));
        _unread = false;
    }

    /// <summary>
    /// Reads the stored collection from a directory without changing anything in it. A damaged or too-new file is left in
    /// place under its own name, unlike <see cref="Load"/>, so a tool that only reads can never alter the stored state.
    /// </summary>
    /// <param name="directory">The storage directory.</param>
    /// <returns>The collection, or no collection and a one-line reason.</returns>
    public static SnapshotReadResult LoadReadOnly(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var path = Path.Combine(directory, FileName);
        byte[] content;

        try
        {
            if (!File.Exists(path))
            {
                return new SnapshotReadResult(null, "no stored collection in the state directory");
            }

            content = File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new SnapshotReadResult(null, "the stored collection could not be read and was left in place");
        }

        var (snapshot, problem) = Classify(content);

        return problem switch
        {
            null => new SnapshotReadResult(snapshot, null),
            NewerSchemaProblem => new SnapshotReadResult(null, "the stored collection is in a newer format than this version reads and was left in place"),
            _ => new SnapshotReadResult(null, "the stored collection is malformed and was left in place"),
        };
    }

    private static (CollectionSnapshot? Snapshot, string? Problem) Classify(byte[] content)
    {
        CollectionSnapshot? snapshot;

        try
        {
            snapshot = JsonSerializer.Deserialize<CollectionSnapshot>(content, JsonOptions);
        }
        catch (JsonException)
        {
            return (null, MalformedProblem);
        }

        if (snapshot is null || snapshot.Items is null || snapshot.Items.Any(item => item is null))
        {
            return (null, MalformedProblem);
        }

        if (snapshot.SchemaVersion > CollectionSnapshot.CurrentSchemaVersion)
        {
            return (null, NewerSchemaProblem);
        }

        return snapshot.SchemaVersion < 1 ? (null, MalformedProblem) : (snapshot, null);
    }

    private CollectionSnapshot? Parse(byte[] content)
    {
        var (snapshot, problem) = Classify(content);

        return problem is null ? snapshot : SetAside(problem);
    }

    private CollectionSnapshot? SetAside(string reason)
    {
        try
        {
            File.Move(_path, Path.Combine(Path.GetDirectoryName(_path)!, SetAsideFileName), overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("The stored collection could not be read ({Reason}) and could not be set aside.", reason);

            return null;
        }

        _logger.LogWarning("The stored collection could not be read ({Reason}) and was set aside; the next sync rebuilds it.", reason);

        return null;
    }

    private static JsonSerializerOptions CreateJsonOptions() => StoredJson.CreateOptions(
        new Dictionary<Type, string[]>
        {
            [typeof(CollectionSnapshot)] = ["schemaVersion", "capturedAtUtc", "items"],
            [typeof(SnapshotItem)] = ["collectionId", "gameId", "title", "kind"],
            [typeof(ImageRecord)] = ["sourceUrl", "status", "attemptedAtUtc"],
            [typeof(ArtFile)] = ["width", "height", "name"],
            [typeof(ArtFeatures)] = ["backdropShare", "fill", "corner1", "corner2", "sidesTouched"],
            [typeof(PaletteTone)] = ["background", "text"],
            [typeof(ArtEdges)] = ["top", "right", "bottom", "left"],
            [typeof(GameDetails)] = ["enrichedAtUtc", "designers", "mechanics", "expandsGames"],
            [typeof(BaseGameRef)] = ["bggId", "title"],
            [typeof(FamilyLink)] = ["id", "name"],
        });
}

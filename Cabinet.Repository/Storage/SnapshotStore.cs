using System.Text.Json;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Microsoft.Extensions.Logging;

namespace Cabinet.Repository.Storage;

/// <summary>Keeps the last good collection as <c>snapshot.json</c> in the storage directory.</summary>
public sealed class SnapshotStore : ISnapshotStore
{
    /// <summary>The name of the stored file.</summary>
    public const string FileName = "snapshot.json";

    /// <summary>The name a damaged or too-new file is renamed to. A file that could not be read at all is left in place.</summary>
    public const string SetAsideFileName = "snapshot.json.bad";

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

    private CollectionSnapshot? Parse(byte[] content)
    {
        CollectionSnapshot? snapshot;

        try
        {
            snapshot = JsonSerializer.Deserialize<CollectionSnapshot>(content, JsonOptions);
        }
        catch (JsonException)
        {
            return SetAside("malformed");
        }

        if (snapshot is null || snapshot.Items is null || snapshot.Items.Any(item => item is null))
        {
            return SetAside("malformed");
        }

        if (snapshot.SchemaVersion > CollectionSnapshot.CurrentSchemaVersion)
        {
            return SetAside("newer schema");
        }

        return snapshot.SchemaVersion < 1 ? SetAside("malformed") : snapshot;
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

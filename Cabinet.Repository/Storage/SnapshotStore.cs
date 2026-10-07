using System.Text.Json;
using System.Text.Json.Serialization;
using Cabinet.Domain.Collection;
using Microsoft.Extensions.Logging;

namespace Cabinet.Repository.Storage;

/// <summary>Keeps the last good collection as <c>snapshot.json</c> in the storage directory.</summary>
public sealed class SnapshotStore : ISnapshotStore
{
    /// <summary>The name of the stored file.</summary>
    public const string FileName = "snapshot.json";

    /// <summary>The name a damaged or too-new file is renamed to.</summary>
    public const string SetAsideFileName = "snapshot.json.bad";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly string _path;
    private readonly ILogger<SnapshotStore> _logger;

    /// <summary>Creates a store over a directory.</summary>
    /// <param name="directory">The storage directory; it must exist.</param>
    /// <param name="logger">Receives one line when a stored file has to be set aside.</param>
    public SnapshotStore(string directory, ILogger<SnapshotStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(logger);

        _path = Path.Combine(directory, FileName);
        _logger = logger;
    }

    /// <inheritdoc />
    public CollectionSnapshot? Load()
    {
        byte[] content;

        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            content = File.ReadAllBytes(_path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return SetAside("unreadable");
        }

        return Parse(content);
    }

    /// <inheritdoc />
    public void Save(CollectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        AtomicJsonFile.WriteAtomically(_path, JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions));
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

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        return options;
    }
}

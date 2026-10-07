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
        if (!File.Exists(_path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<CollectionSnapshot>(File.ReadAllBytes(_path), JsonOptions);
    }

    /// <inheritdoc />
    public void Save(CollectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        AtomicJsonFile.WriteAtomically(_path, JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions));
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

using System.Text.Json;
using System.Text.Json.Serialization;
using Cabinet.Domain.Collection;
using Microsoft.Extensions.Logging;

namespace Cabinet.Repository.Storage;

/// <summary>Keeps the sync bookkeeping as <c>sync-state.json</c> in the storage directory.</summary>
public sealed class SyncStateStore : ISyncStateStore
{
    /// <summary>The name of the stored file.</summary>
    public const string FileName = "sync-state.json";

    /// <summary>The name a damaged or too-new file is renamed to.</summary>
    public const string SetAsideFileName = "sync-state.json.bad";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly string _path;
    private readonly ILogger<SyncStateStore> _logger;

    /// <summary>Creates a store over a directory.</summary>
    /// <param name="directory">The storage directory; it must exist.</param>
    /// <param name="logger">Receives one line when a stored file has to be set aside.</param>
    public SyncStateStore(string directory, ILogger<SyncStateStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(logger);

        _path = Path.Combine(directory, FileName);
        _logger = logger;
    }

    /// <inheritdoc />
    public SyncState Load()
    {
        byte[] content;

        try
        {
            if (!File.Exists(_path))
            {
                return SyncState.Initial;
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
    public void Save(SyncState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        AtomicJsonFile.WriteAtomically(_path, JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions));
    }

    private SyncState Parse(byte[] content)
    {
        SyncState? state;

        try
        {
            state = JsonSerializer.Deserialize<SyncState>(content, JsonOptions);
        }
        catch (JsonException)
        {
            return SetAside("malformed");
        }

        if (state is null || state.SchemaVersion < 1 || state.ConsecutiveFailures < 0)
        {
            return SetAside("malformed");
        }

        return state.SchemaVersion > SyncState.CurrentSchemaVersion ? SetAside("newer schema") : state;
    }

    private SyncState SetAside(string reason)
    {
        try
        {
            File.Move(_path, Path.Combine(Path.GetDirectoryName(_path)!, SetAsideFileName), overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("The stored sync state could not be read ({Reason}) and could not be set aside.", reason);

            return SyncState.Initial;
        }

        _logger.LogWarning("The stored sync state could not be read ({Reason}) and was set aside; it starts afresh.", reason);

        return SyncState.Initial;
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

using System.Text.Json;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Microsoft.Extensions.Logging;

namespace Cabinet.Repository.Storage;

/// <summary>
/// Keeps the owner data as <c>owner-data.json</c> beside the synced collection. The file holds hand-entered data that exists
/// nowhere else, so a file that cannot be read, or that a newer release wrote, is reported as unreadable and left exactly
/// where it is and as it is: it is never renamed, moved, truncated or replaced.
/// </summary>
public sealed class OwnerDataStore : IOwnerDataStore
{
    /// <summary>The name of the stored file.</summary>
    public const string FileName = "owner-data.json";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly string _path;
    private readonly ILogger<OwnerDataStore> _logger;

    /// <summary>Creates a store over a directory.</summary>
    /// <param name="directory">The storage directory; it must exist.</param>
    /// <param name="logger">Receives one line with a category when the file cannot be read; never its content.</param>
    public OwnerDataStore(string directory, ILogger<OwnerDataStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(logger);

        _path = Path.Combine(directory, FileName);
        _logger = logger;
    }

    /// <inheritdoc />
    public OwnerDataState Load()
    {
        byte[] content;

        try
        {
            if (!File.Exists(_path))
            {
                return new OwnerDataState.Ready(OwnerData.Empty);
            }

            content = File.ReadAllBytes(_path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Unreadable("unreadable");
        }

        return Parse(content);
    }

    /// <inheritdoc />
    public void Save(OwnerData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        AtomicJsonFile.WriteAtomically(_path, JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions));
    }

    private OwnerDataState Parse(byte[] content)
    {
        var header = ReadHeader(content);

        if (header is not { } known || known.Kind != OwnerData.KindMarker)
        {
            return Unreadable("malformed");
        }

        if (known.SchemaVersion > OwnerData.CurrentSchemaVersion)
        {
            return Unreadable("newer format");
        }

        OwnerData? data;

        try
        {
            data = JsonSerializer.Deserialize<OwnerData>(content, JsonOptions);
        }
        catch (JsonException)
        {
            return Unreadable("malformed");
        }

        return data is null || data.SchemaVersion < 1 || HasNullMember(data)
            ? Unreadable("malformed")
            : new OwnerDataState.Ready(data);
    }

    private static (string? Kind, int SchemaVersion)? ReadHeader(byte[] content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("kind", out var kind)
                || kind.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("schemaVersion", out var version)
                || !version.TryGetInt32(out var schemaVersion))
            {
                return null;
            }

            return (kind.GetString(), schemaVersion);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool HasNullMember(OwnerData data) =>
        data.Locations.Any(location => location is null)
        || data.Games.Any(game => game is null)
        || data.Changes.Any(change => change is null);

    private OwnerDataState Unreadable(string reason)
    {
        _logger.LogWarning("The owner data could not be read ({Reason}) and was left in place; owner changes are refused until it is restored.", reason);

        return new OwnerDataState.Unreadable(reason);
    }

    private static JsonSerializerOptions CreateJsonOptions() => StoredJson.CreateOptions(
        new Dictionary<Type, string[]>
        {
            [typeof(OwnerData)] = ["kind", "schemaVersion", "revision", "finish", "locations", "games", "changesSinceDownload", "changes"],
            [typeof(OwnerLocation)] = ["id", "name"],
            [typeof(OwnerGame)] = ["gameId"],
            [typeof(PictureSetting)] = ["choice", "checked"],
            [typeof(PictureLink)] = ["url", "failedChecks"],
            [typeof(ChangeEntry)] = ["atUtc", "text"],
            [typeof(ImageRecord)] = ["sourceUrl", "status", "attemptedAtUtc"],
            [typeof(ArtFile)] = ["width", "height", "name"],
            [typeof(ArtFeatures)] = ["backdropShare", "fill", "corner1", "corner2", "sidesTouched"],
            [typeof(PaletteTone)] = ["background", "text"],
            [typeof(ArtEdges)] = ["top", "right", "bottom", "left"],
        });
}

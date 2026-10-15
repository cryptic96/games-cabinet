using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    /// <summary>How many ordinary earlier copies of the file are kept; older ones are removed. The copies of a file in an older format are never counted or removed.</summary>
    public const int KeptVersions = 10;

    private const string VersionsDirectoryName = "owner-data-versions";
    private const string VersionNamePrefix = "owner-data.";
    private const string VersionNameSuffix = ".json";
    private const string OlderFormatMark = "format-";
    private const string VersionTimeFormat = "yyyyMMdd'T'HHmmss'Z'";
    private const string OlderFormatTimeFormat = "yyyyMMdd'T'HHmmssfff'Z'";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly string _path;
    private readonly string _versionsDirectory;
    private readonly ILogger<OwnerDataStore> _logger;

    /// <summary>Creates a store over a directory.</summary>
    /// <param name="directory">The storage directory; it must exist.</param>
    /// <param name="logger">Receives one line with a category when the file cannot be read; never its content.</param>
    public OwnerDataStore(string directory, ILogger<OwnerDataStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(logger);

        _path = Path.Combine(directory, FileName);
        _versionsDirectory = Path.Combine(directory, VersionsDirectoryName);
        _logger = logger;
        AtomicJsonFile.RemoveStrayTemporaryFiles(_versionsDirectory);
    }

    /// <summary>The strict serializer settings of the stored file, which a readable copy of the same document is written with as well.</summary>
    internal static JsonSerializerOptions StoredOptions => JsonOptions;

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

    /// <inheritdoc />
    /// <remarks>
    /// A copy of a file in an older format is kept under its own name, which carries the format and a millisecond stamp: it is
    /// never replaced by a later copy, never counted against <see cref="KeptVersions"/> and never removed, because it is the
    /// only copy a release that can read that format is able to use. A file already copied byte for byte is not copied again.
    /// </remarks>
    public void KeepEarlierVersion(DateTimeOffset now)
    {
        if (!File.Exists(_path))
        {
            return;
        }

        var content = File.ReadAllBytes(_path);
        Directory.CreateDirectory(_versionsDirectory);

        if (ReadHeader(content) is { SchemaVersion: var format } && format >= 1 && format < OwnerData.CurrentSchemaVersion)
        {
            KeepOlderFormatCopy(content, format, now);

            return;
        }

        AtomicJsonFile.WriteAtomically(Path.Combine(_versionsDirectory, VersionFileName(now)), content);

        foreach (var (path, _) in ListVersionFiles().Where(version => !IsOlderFormatCopy(version.Path)).Skip(KeptVersions))
        {
            File.Delete(path);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<EarlierVersion> EarlierVersions() =>
        [.. ListVersionFiles().Select((version, index) => new EarlierVersion(index, version.SavedAtUtc))];

    /// <inheritdoc />
    public byte[]? ReadEarlierVersion(int index)
    {
        var files = ListVersionFiles();

        return index >= 0 && index < files.Count ? File.ReadAllBytes(files[index].Path) : null;
    }

    /// <inheritdoc />
    public DateTimeOffset? NewestEarlierVersionAt() => ListVersionFiles() is [var newest, ..] ? newest.SavedAtUtc : null;

    /// <inheritdoc />
    public int? StoredSchemaVersion()
    {
        try
        {
            return File.Exists(_path) && ReadHeader(File.ReadAllBytes(_path)) is { } header ? header.SchemaVersion : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private List<(string Path, DateTimeOffset SavedAtUtc)> ListVersionFiles()
    {
        if (!Directory.Exists(_versionsDirectory))
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(_versionsDirectory)
            .Select(path => (Path: path, SavedAtUtc: ParseVersionTime(System.IO.Path.GetFileName(path))))
            .Where(version => version.SavedAtUtc is not null)
            .Select(version => (version.Path, SavedAtUtc: version.SavedAtUtc!.Value))
            .OrderByDescending(version => version.SavedAtUtc)
            .ThenByDescending(version => version.Path, StringComparer.Ordinal)];
    }

    private void KeepOlderFormatCopy(byte[] content, int format, DateTimeOffset now)
    {
        var mark = OlderFormatMark + format.ToString(CultureInfo.InvariantCulture) + ".";

        if (Directory.EnumerateFiles(_versionsDirectory)
            .Where(path => System.IO.Path.GetFileName(path).StartsWith(VersionNamePrefix + mark, StringComparison.Ordinal))
            .Any(path => File.ReadAllBytes(path).AsSpan().SequenceEqual(content)))
        {
            return;
        }

        var moment = now;
        string target;

        while (File.Exists(target = System.IO.Path.Combine(_versionsDirectory, VersionNamePrefix + mark + moment.UtcDateTime.ToString(OlderFormatTimeFormat, CultureInfo.InvariantCulture) + VersionNameSuffix)))
        {
            moment = moment.AddMilliseconds(1);
        }

        AtomicJsonFile.WriteAtomically(target, content);
    }

    private static bool IsOlderFormatCopy(string path) =>
        System.IO.Path.GetFileName(path).StartsWith(VersionNamePrefix + OlderFormatMark, StringComparison.Ordinal);

    private static string VersionFileName(DateTimeOffset moment) =>
        VersionNamePrefix + moment.UtcDateTime.ToString(VersionTimeFormat, CultureInfo.InvariantCulture) + VersionNameSuffix;

    private static DateTimeOffset? ParseVersionTime(string fileName)
    {
        if (!fileName.StartsWith(VersionNamePrefix, StringComparison.Ordinal) || !fileName.EndsWith(VersionNameSuffix, StringComparison.Ordinal))
        {
            return null;
        }

        var stamp = fileName[VersionNamePrefix.Length..^VersionNameSuffix.Length];
        var timeFormat = VersionTimeFormat;

        if (stamp.StartsWith(OlderFormatMark, StringComparison.Ordinal))
        {
            var dot = stamp.IndexOf('.', StringComparison.Ordinal);

            if (dot < 0)
            {
                return null;
            }

            stamp = stamp[(dot + 1)..];
            timeFormat = OlderFormatTimeFormat;
        }

        return DateTimeOffset.TryParseExact(stamp, timeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var moment)
            ? moment
            : null;
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

        if (known.SchemaVersion < 1)
        {
            return Unreadable("malformed");
        }

        var upgraded = UpgradeToCurrent(content, known.SchemaVersion);

        if (upgraded is null)
        {
            return Unreadable("malformed");
        }

        OwnerData? data;

        try
        {
            data = JsonSerializer.Deserialize<OwnerData>(upgraded, JsonOptions);
        }
        catch (JsonException)
        {
            return Unreadable("malformed");
        }

        return data is null || HasNullMember(data)
            ? Unreadable("malformed")
            : new OwnerDataState.Ready(data with { Look = data.Look.Normalized() });
    }

    /// <summary>
    /// Brings the bytes of a stored or downloaded owner-data file in an older format to the current one, in memory only. A file
    /// of the first format held one finish name, which is dropped for the default look, because no earlier finish was ever
    /// anything but the default. A file that is already current is returned as it is.
    /// </summary>
    /// <param name="content">The content of the file, whose header was already read.</param>
    /// <param name="schemaVersion">The format version the header names.</param>
    /// <returns>The content in the current format, or null when an older file cannot be brought to it.</returns>
    internal static byte[]? UpgradeToCurrent(byte[] content, int schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (schemaVersion >= OwnerData.CurrentSchemaVersion)
        {
            return content;
        }

        try
        {
            if (JsonNode.Parse(content) is not JsonObject root)
            {
                return null;
            }

            var look = CabinetLook.Default;
            root.Remove("finish");
            root["look"] = new JsonObject
            {
                ["design"] = look.Design,
                ["wood"] = look.Wood,
                ["backs"] = look.Backs,
                ["lights"] = look.Lights,
                ["depth"] = look.Depth,
            };
            root["schemaVersion"] = OwnerData.CurrentSchemaVersion;

            return JsonSerializer.SerializeToUtf8Bytes(root);
        }
        catch (JsonException)
        {
            return null;
        }
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
        data.Look is null
        || data.Locations.Any(location => location is null)
        || data.Games.Any(game => game is null)
        || data.Changes.Any(change => change is null)
        || data.Games.Any(game => game.Picture?.Link?.Record?.Files?.Any(file => file is null) == true);

    private OwnerDataState Unreadable(string reason)
    {
        _logger.LogWarning("The owner data could not be read ({Reason}) and was left in place; owner changes are refused until it is restored.", reason);

        return new OwnerDataState.Unreadable(reason);
    }

    private static JsonSerializerOptions CreateJsonOptions() => StoredJson.CreateOptions(
        new Dictionary<Type, string[]>
        {
            [typeof(OwnerData)] = ["kind", "schemaVersion", "revision", "look", "locations", "games", "changesSinceDownload", "changes"],
            [typeof(CabinetLook)] = ["design", "wood", "backs", "lights", "depth"],
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

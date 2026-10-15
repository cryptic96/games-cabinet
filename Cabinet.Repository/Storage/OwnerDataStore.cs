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

    /// <summary>
    /// How many ordinary earlier copies of the file are kept; older ones are removed. Only a copy whose header names the current
    /// format is ordinary: a copy in an older format, in a newer one or with a header that cannot be read is never counted or removed.
    /// </summary>
    public const int KeptVersions = 10;

    private const string VersionsDirectoryName = "owner-data-versions";
    private const string VersionNamePrefix = "owner-data.";
    private const string VersionNameSuffix = ".json";
    private const string OlderFormatMark = "format-";
    private const string VersionTimeFormat = "yyyyMMdd'T'HHmmss'Z'";
    private const string OlderFormatTimeFormat = "yyyyMMdd'T'HHmmssfff'Z'";
    private const int HeaderBytes = 4096;

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
    /// Every copy is named after the second it was taken, <c>owner-data.&lt;yyyyMMddTHHmmssZ&gt;.json</c>, the name every earlier
    /// release lists, so a release that is rolled back to finds the copy of the file it can read. A copy of a file in an older
    /// format never replaces another copy: when its second is taken it moves on to the next free one, and a file already copied
    /// byte for byte is not copied again. An ordinary copy replaces only an ordinary copy of the same second. Removing copies
    /// beyond <see cref="KeptVersions"/> goes by what each copy holds, not by its name, and reads only the start of each copy.
    /// A copy written under the older <c>owner-data.format-&lt;n&gt;.&lt;stamp&gt;.json</c> name is still listed, read and kept.
    /// </remarks>
    public void KeepEarlierVersion(DateTimeOffset now)
    {
        byte[] content;

        try
        {
            content = File.ReadAllBytes(_path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return;
        }

        Directory.CreateDirectory(_versionsDirectory);

        if (ReadHeader(content) is { SchemaVersion: var format } && format >= 1 && format < OwnerData.CurrentSchemaVersion)
        {
            KeepOlderFormatCopy(content, now);

            return;
        }

        AtomicJsonFile.WriteAtomically(FreeVersionPath(now, IsOrdinaryCopy), content);

        foreach (var (path, _) in ListVersionFiles().Where(version => IsOrdinaryCopy(version.Path)).Skip(KeptVersions))
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

    private void KeepOlderFormatCopy(byte[] content, DateTimeOffset now)
    {
        if (ListVersionFiles().Any(version => HasContent(version.Path, content)))
        {
            return;
        }

        AtomicJsonFile.WriteAtomically(FreeVersionPath(now, _ => false), content);
    }

    /// <summary>The path of the copy for a moment, moved on a second at a time past every taken name that may not be replaced.</summary>
    /// <param name="now">The moment the copy is taken.</param>
    /// <param name="mayReplace">Whether the copy at a taken name may be replaced.</param>
    private string FreeVersionPath(DateTimeOffset now, Func<string, bool> mayReplace)
    {
        var moment = now;
        string target;

        while (File.Exists(target = Path.Combine(_versionsDirectory, VersionFileName(moment))) && !mayReplace(target))
        {
            moment = moment.AddSeconds(1);
        }

        return target;
    }

    private static bool HasContent(string path, byte[] content) =>
        new FileInfo(path).Length == content.Length && File.ReadAllBytes(path).AsSpan().SequenceEqual(content);

    /// <summary>
    /// Whether a copy is an ordinary one, which may be replaced and removed: its header names the current format and its name is
    /// not that of a copy of an older format. A copy whose header cannot be read is not ordinary, so it is kept.
    /// </summary>
    /// <param name="path">The copy.</param>
    private static bool IsOrdinaryCopy(string path) =>
        !System.IO.Path.GetFileName(path).StartsWith(VersionNamePrefix + OlderFormatMark, StringComparison.Ordinal)
        && ReadStoredFormat(path) == OwnerData.CurrentSchemaVersion;

    /// <summary>The format a stored copy names in its header, read from its first few kilobytes only; null when that cannot be told.</summary>
    /// <param name="path">The copy.</param>
    private static int? ReadStoredFormat(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var start = new byte[HeaderBytes];
            var length = stream.ReadAtLeast(start, start.Length, throwOnEndOfStream: false);

            return FormatInHeader(start.AsSpan(0, length), isWholeFile: length < start.Length);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The <c>schemaVersion</c> among the top-level members at the start of a document, or null when it is not there.</summary>
    /// <param name="start">The start of the document.</param>
    /// <param name="isWholeFile">Whether the start is the whole document.</param>
    private static int? FormatInHeader(ReadOnlySpan<byte> start, bool isWholeFile)
    {
        var reader = new Utf8JsonReader(start, isWholeFile, default);

        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return null;
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var isFormat = reader.ValueTextEquals("schemaVersion"u8);

                if (!reader.Read())
                {
                    return null;
                }

                if (isFormat)
                {
                    return reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var format) ? format : null;
                }

                if (!reader.TrySkip())
                {
                    return null;
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

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

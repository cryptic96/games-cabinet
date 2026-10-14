using System.Globalization;
using System.Text.Json;
using Cabinet.Domain.Owner;
using Cabinet.Repository.Images;

namespace Cabinet.Repository.Storage;

/// <summary>Why a file chosen for a restore was refused. Each one changes nothing and has its own words for the owner.</summary>
public enum RestoreProblem
{
    /// <summary>No file was chosen, or it holds nothing.</summary>
    NoFile,

    /// <summary>The file is larger than <see cref="OwnerDataExport.MaxRestoreBytes"/>.</summary>
    TooLarge,

    /// <summary>The file is not JSON, or is JSON that is not an owner-data file.</summary>
    NotOwnerData,

    /// <summary>The file is an owner-data file that is cut short, nested too deeply or otherwise not in the shape the site writes.</summary>
    Damaged,

    /// <summary>The file was made by a newer version of the site than this one.</summary>
    Newer,

    /// <summary>The file holds more, or other, than the site accepts: too many items, a name or a link that breaks a rule, or a reference to nothing.</summary>
    OverLimits,
}

/// <summary>What the check of a file chosen for a restore found: the document when it can be accepted whole, or why it cannot.</summary>
public sealed record RestoreCheck
{
    private RestoreCheck()
    {
    }

    /// <summary>Why the file was refused, or null when it was accepted.</summary>
    public RestoreProblem? Refusal { get; private init; }

    /// <summary>The accepted document, with the fetched records of its links left out, or null when the file was refused.</summary>
    public OwnerData? Data { get; private init; }

    /// <summary>The stored format the file was written in: for an accepted file, the one it came in; for a file from a newer site, the newer one.</summary>
    public int FormatVersion { get; private init; }

    /// <summary>The size of a file that was too large, in bytes.</summary>
    public long SizeBytes { get; private init; }

    /// <summary>Whether the file was accepted.</summary>
    public bool IsOk => Refusal is null;

    /// <summary>An accepted file.</summary>
    /// <param name="data">The document, ready to be stored.</param>
    /// <param name="formatVersion">The stored format the file was written in.</param>
    public static RestoreCheck Ok(OwnerData data, int formatVersion) => new() { Data = data, FormatVersion = formatVersion };

    /// <summary>A refused file.</summary>
    /// <param name="problem">Why it was refused.</param>
    /// <param name="formatVersion">For a file from a newer site, its format; otherwise zero.</param>
    /// <param name="sizeBytes">For a file that was too large, its size; otherwise zero.</param>
    public static RestoreCheck Problem(RestoreProblem problem, int formatVersion = 0, long sizeBytes = 0) =>
        new() { Refusal = problem, FormatVersion = formatVersion, SizeBytes = sizeBytes };
}

/// <summary>
/// Writes the owner data as the readable file the owner downloads as a backup, and checks such a file when it comes back. The
/// file is the stored document in the same strict shape, so reading it back needs no other reader, but indented so a person
/// can open it, and without the records of the pictures the owner linked: what was fetched from an address is fetched again
/// when the file is restored, so only the address is kept. A file that is restored is untrusted input: it is read as a whole
/// or not at all.
/// </summary>
public static class OwnerDataExport
{
    /// <summary>The largest file a restore accepts, in bytes.</summary>
    public const int MaxRestoreBytes = 2 * 1024 * 1024;

    /// <summary>The most games with decisions a restored file may hold.</summary>
    public const int MaxGames = 5000;

    /// <summary>The deepest nesting of JSON a restored file may have.</summary>
    public const int MaxDepth = 16;

    /// <summary>The highest revision a restored file may carry; a script has to be able to send it back exactly.</summary>
    public const int MaxRevision = int.MaxValue / 2;

    /// <summary>The most changes since the last download a restored file may report.</summary>
    public const int MaxChangesSinceDownload = OwnerData.MaxStoredChanges * 1000;

    private const int MaxChangeTextLength = 1000;
    private const string FileNamePrefix = "cabinet-owner-data-";
    private const string FileNameSuffix = ".json";
    private const string FileNameDateFormat = "yyyy-MM-dd";

    private static readonly JsonSerializerOptions ReadableOptions = new(OwnerDataStore.StoredOptions) { WriteIndented = true };

    /// <summary>Writes the owner data as UTF-8 JSON as it will be after this download: the download time is the last download and no change is left since it.</summary>
    /// <param name="data">The owner data to export.</param>
    /// <param name="downloadedAt">The moment of the download.</param>
    public static byte[] Write(OwnerData data, DateTimeOffset downloadedAt)
    {
        ArgumentNullException.ThrowIfNull(data);

        var document = data with
        {
            LastDownloadAtUtc = downloadedAt,
            ChangesSinceDownload = 0,
            Games = [.. data.Games.Select(WithoutFetchedRecord)],
        };

        return JsonSerializer.SerializeToUtf8Bytes(document, ReadableOptions);
    }

    /// <summary>
    /// Checks the bytes of a file chosen for a restore. The file is accepted only whole: it has to be an owner-data file in the
    /// strict shape the site writes, within the size, count, name and link rules, with every reference pointing at something in
    /// it. Its fetched records are left out so every linked picture is fetched again, and its games are put in order of
    /// identifier. A file from an older format is upgraded here, in memory, and reports the format it came in.
    /// </summary>
    /// <param name="bytes">The content of the file, or null when none was chosen.</param>
    /// <param name="policy">Says which addresses a linked picture may be fetched from; every link of the file must meet it.</param>
    public static RestoreCheck Check(byte[]? bytes, ArtSourcePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (bytes is null || bytes.Length == 0)
        {
            return RestoreCheck.Problem(RestoreProblem.NoFile);
        }

        if (bytes.Length > MaxRestoreBytes)
        {
            return RestoreCheck.Problem(RestoreProblem.TooLarge, sizeBytes: bytes.Length);
        }

        var header = ReadHeader(bytes);

        if (header.Refusal is { } headerProblem)
        {
            return RestoreCheck.Problem(headerProblem);
        }

        if (header.SchemaVersion > OwnerData.CurrentSchemaVersion)
        {
            return RestoreCheck.Problem(RestoreProblem.Newer, formatVersion: header.SchemaVersion);
        }

        if (header.SchemaVersion < 1 || OwnerDataStore.UpgradeToCurrent(bytes, header.SchemaVersion) is not { } upgraded || Deserialize(upgraded) is not { } data)
        {
            return RestoreCheck.Problem(RestoreProblem.Damaged);
        }

        return WithinLimits(data, policy)
            ? RestoreCheck.Ok(Accepted(data), header.SchemaVersion)
            : RestoreCheck.Problem(RestoreProblem.OverLimits);
    }

    /// <summary>The name the downloaded file is saved under: <c>cabinet-owner-data-</c> and the date of the download in UTC.</summary>
    /// <param name="downloadedAt">The moment of the download.</param>
    public static string FileName(DateTimeOffset downloadedAt) =>
        FileNamePrefix + downloadedAt.UtcDateTime.ToString(FileNameDateFormat, CultureInfo.InvariantCulture) + FileNameSuffix;

    private static (RestoreProblem? Refusal, int SchemaVersion) ReadHeader(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = MaxDepth });
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("kind", out var kind)
                || kind.ValueKind != JsonValueKind.String
                || kind.GetString() != OwnerData.KindMarker)
            {
                return (RestoreProblem.NotOwnerData, 0);
            }

            return root.TryGetProperty("schemaVersion", out var version) && version.TryGetInt32(out var schemaVersion)
                ? (null, schemaVersion)
                : (RestoreProblem.Damaged, 0);
        }
        catch (JsonException)
        {
            return (IsJsonOfAnyDepth(bytes) || ClaimsToBeOwnerData(bytes) ? RestoreProblem.Damaged : RestoreProblem.NotOwnerData, 0);
        }
    }

    private static bool IsJsonOfAnyDepth(byte[] bytes)
    {
        try
        {
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = int.MaxValue });

            while (reader.Read())
            {
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ClaimsToBeOwnerData(byte[] bytes) =>
        bytes.AsSpan().IndexOf(System.Text.Encoding.UTF8.GetBytes($"\"{OwnerData.KindMarker}\"")) >= 0;

    private static OwnerData? Deserialize(byte[] bytes)
    {
        try
        {
            var data = JsonSerializer.Deserialize<OwnerData>(bytes, OwnerDataStore.StoredOptions);

            return data is null || data.Look is null || data.Locations.Any(location => location is null) || data.Games.Any(game => game is null) || data.Changes.Any(change => change is null)
                ? null
                : data;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool WithinLimits(OwnerData data, ArtSourcePolicy policy)
    {
        if (data.Revision < 0
            || data.Revision > MaxRevision
            || data.ChangesSinceDownload < 0
            || data.ChangesSinceDownload > MaxChangesSinceDownload
            || LookIsOverLimit(data.Look)
            || data.Changes.Count > OwnerData.MaxStoredChanges
            || data.Changes.Any(change => change.Text.Length > MaxChangeTextLength)
            || data.Games.Count > MaxGames)
        {
            return false;
        }

        return LocationsAreAcceptable(data.Locations) && GamesAreAcceptable(data, policy);
    }

    private static bool LookIsOverLimit(CabinetLook look) =>
        new[] { look.Design, look.Wood, look.Backs, look.Lights, look.Depth }.Any(value => value.Length > CabinetLook.MaxValueLength);

    private static bool LocationsAreAcceptable(IReadOnlyList<OwnerLocation> locations)
    {
        var accepted = new List<OwnerLocation>(locations.Count);

        foreach (var location in locations)
        {
            var check = LocationNames.Validate(accepted, location.Name);

            if (!OwnerLocation.IsWellFormedId(location.Id)
                || accepted.Any(other => other.Id == location.Id)
                || check.Problem != LocationNameProblem.None
                || check.Name != location.Name)
            {
                return false;
            }

            accepted.Add(location);
        }

        return true;
    }

    private static bool GamesAreAcceptable(OwnerData data, ArtSourcePolicy policy)
    {
        var locationIds = data.Locations.Select(location => location.Id).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<int>();

        foreach (var game in data.Games)
        {
            if (game.GameId <= 0
                || !seen.Add(game.GameId)
                || (game.LocationId is not null && !locationIds.Contains(game.LocationId))
                || !PictureIsAcceptable(game.Picture, policy))
            {
                return false;
            }
        }

        return true;
    }

    private static bool PictureIsAcceptable(PictureSetting? picture, ArtSourcePolicy policy)
    {
        if (picture is null)
        {
            return true;
        }

        if (picture.SeenOwnedUrl?.Length > ArtUrl.MaxLength || picture.SeenMainUrl?.Length > ArtUrl.MaxLength)
        {
            return false;
        }

        if (picture.Choice == PictureChoice.Link && picture.Link is null)
        {
            return false;
        }

        return picture.Link is not { } link
            || (link.FailedChecks >= 0
                && ArtUrl.Canonical(link.Url) == link.Url
                && Uri.TryCreate(link.Url, UriKind.Absolute, out var uri)
                && policy.Allows(uri));
    }

    private static OwnerData Accepted(OwnerData data) =>
        data with
        {
            Look = data.Look.Normalized(),
            Games = [.. data.Games.Select(WithoutFetchedRecord).OrderBy(game => game.GameId)],
        };

    private static OwnerGame WithoutFetchedRecord(OwnerGame game) =>
        game.Picture?.Link is { } link
            ? game with { Picture = game.Picture with { Link = link with { Record = null } } }
            : game;
}

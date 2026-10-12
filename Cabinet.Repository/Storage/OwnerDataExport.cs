using System.Globalization;
using System.Text.Json;
using Cabinet.Domain.Owner;

namespace Cabinet.Repository.Storage;

/// <summary>
/// Writes the owner data as the readable file the owner downloads as a backup. It is the stored document in the same strict
/// shape, so reading it back needs no other reader, but indented so a person can open it, and without the records of the
/// pictures the owner linked: what was fetched from an address is fetched again when the file is restored, so only the
/// address is kept.
/// </summary>
public static class OwnerDataExport
{
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

    /// <summary>The name the downloaded file is saved under: <c>cabinet-owner-data-</c> and the date of the download in UTC.</summary>
    /// <param name="downloadedAt">The moment of the download.</param>
    public static string FileName(DateTimeOffset downloadedAt) =>
        FileNamePrefix + downloadedAt.UtcDateTime.ToString(FileNameDateFormat, CultureInfo.InvariantCulture) + FileNameSuffix;

    private static OwnerGame WithoutFetchedRecord(OwnerGame game) =>
        game.Picture?.Link is { } link
            ? game with { Picture = game.Picture with { Link = link with { Record = null } } }
            : game;
}

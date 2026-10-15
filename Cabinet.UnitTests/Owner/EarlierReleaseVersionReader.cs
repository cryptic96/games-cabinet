using System.Globalization;

namespace Cabinet.UnitTests.Owner;

/// <summary>
/// Mirrors the earlier release's reader of the earlier-versions folder, frozen as that release shipped it: it lists only files
/// named <c>owner-data.&lt;yyyyMMddTHHmmssZ&gt;.json</c>, newest first. A release that is rolled back to sees exactly what this
/// lists, so a copy it cannot list is a copy it cannot restore. It must not follow later changes to the store.
/// </summary>
internal static class EarlierReleaseVersionReader
{
    private const string VersionNamePrefix = "owner-data.";
    private const string VersionNameSuffix = ".json";
    private const string VersionTimeFormat = "yyyyMMdd'T'HHmmss'Z'";

    /// <summary>The copies the earlier release lists in a versions folder, newest first, as it orders them.</summary>
    /// <param name="versionsDirectory">The folder of earlier versions.</param>
    public static List<(string Path, DateTimeOffset SavedAtUtc)> List(string versionsDirectory)
    {
        if (!Directory.Exists(versionsDirectory))
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(versionsDirectory)
            .Select(path => (Path: path, SavedAtUtc: ParseVersionTime(Path.GetFileName(path))))
            .Where(version => version.SavedAtUtc is not null)
            .Select(version => (version.Path, SavedAtUtc: version.SavedAtUtc!.Value))
            .OrderByDescending(version => version.SavedAtUtc)
            .ThenByDescending(version => version.Path, StringComparer.Ordinal)];
    }

    /// <summary>The moment the earlier release reads from a copy's file name, or null when it does not list that file.</summary>
    /// <param name="fileName">The file name, without a directory.</param>
    public static DateTimeOffset? ParseVersionTime(string fileName)
    {
        if (!fileName.StartsWith(VersionNamePrefix, StringComparison.Ordinal) || !fileName.EndsWith(VersionNameSuffix, StringComparison.Ordinal))
        {
            return null;
        }

        var stamp = fileName[VersionNamePrefix.Length..^VersionNameSuffix.Length];

        return DateTimeOffset.TryParseExact(stamp, VersionTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var moment)
            ? moment
            : null;
    }
}

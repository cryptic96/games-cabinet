using System.Globalization;
using Cabinet.Repository.Images;

namespace Cabinet.Service.Sync;

/// <summary>The rules for fetching, sizing and keeping box pictures.</summary>
/// <param name="AllowedHosts">The only host names pictures are fetched from.</param>
/// <param name="MaxBytes">The most bytes a picture download may have.</param>
/// <param name="MaxPixels">The most pixels a picture may hold before it is refused undecoded.</param>
/// <param name="DownloadGap">The least time between two picture requests.</param>
/// <param name="MaxDownloadsPerRun">The most pictures one sync run fetches.</param>
/// <param name="RetryFailedAfter">How long a picture that failed, was refused or could not be read waits before it is tried again.</param>
/// <param name="PruneGrace">How long a stored file that nothing refers to is kept before it is deleted.</param>
/// <param name="DevelopmentOrigin">A local origin pictures may also come from in development; null otherwise.</param>
/// <param name="DevelopmentOriginIgnored">Whether a development origin was configured outside development and therefore left out.</param>
public sealed record ImageOptions(
    IReadOnlySet<string> AllowedHosts,
    long MaxBytes,
    long MaxPixels,
    TimeSpan DownloadGap,
    int MaxDownloadsPerRun,
    TimeSpan RetryFailedAfter,
    TimeSpan PruneGrace,
    Uri? DevelopmentOrigin,
    bool DevelopmentOriginIgnored = false)
{
    /// <summary>The source policy these options describe.</summary>
    public ArtSourcePolicy Policy => new(AllowedHosts, DevelopmentOrigin);

    /// <summary>The download limits these options describe.</summary>
    public ArtLimits Limits => new(MaxBytes, MaxPixels);
}

/// <summary>
/// Reads the picture settings from the Images configuration section. Every value is checked here, so a typo in the server
/// env file stops the app at startup with a message naming the key.
/// </summary>
public static class ImageSettings
{
    /// <summary>The host pictures come from unless the settings name others.</summary>
    public const string DefaultHost = "cf.geekdo-images.com";

    private const string HostsKey = "Images:AllowedHosts";
    private const string MegabytesKey = "Images:MaxMegabytes";
    private const string MegapixelsKey = "Images:MaxMegapixels";
    private const string GapKey = "Images:DownloadGapMilliseconds";
    private const string PerRunKey = "Images:MaxDownloadsPerRun";
    private const string RetryKey = "Images:RetryFailedAfterHours";
    private const string GraceKey = "Images:PruneGraceDays";
    private const string DevelopmentOriginKey = "Images:DevelopmentOrigin";
    private const long BytesPerMegabyte = 1_048_576;
    private const long PixelsPerMegapixel = 1_000_000;

    /// <summary>
    /// Reads the Images keys. A key that is absent takes the default; a key that is present but out of range, not a
    /// whole number, or naming no usable host throws. The development origin is read only in the Development environment;
    /// anywhere else it is left out and reported through <see cref="ImageOptions.DevelopmentOriginIgnored"/>, so a server
    /// settings file can never widen where pictures are fetched from.
    /// </summary>
    /// <param name="configuration">The configuration to read.</param>
    /// <param name="environment">The hosting environment the app runs in.</param>
    /// <exception cref="InvalidOperationException">A value is invalid; the message names the full key.</exception>
    public static ImageOptions FromConfiguration(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var isDevelopment = environment.IsDevelopment();

        return new ImageOptions(
            ReadHosts(configuration),
            ReadWholeNumber(configuration, MegabytesKey, 1, 50, 12) * BytesPerMegabyte,
            ReadWholeNumber(configuration, MegapixelsKey, 1, 100, 36) * PixelsPerMegapixel,
            TimeSpan.FromMilliseconds(ReadWholeNumber(configuration, GapKey, 500, 60_000, 1000)),
            ReadWholeNumber(configuration, PerRunKey, 1, 1000, 80),
            TimeSpan.FromHours(ReadWholeNumber(configuration, RetryKey, 1, 720, 24)),
            TimeSpan.FromDays(ReadWholeNumber(configuration, GraceKey, 1, 365, 7)),
            isDevelopment ? ReadDevelopmentOrigin(configuration) : null,
            !isDevelopment && !string.IsNullOrWhiteSpace(configuration[DevelopmentOriginKey]));
    }

    private static Uri? ReadDevelopmentOrigin(IConfiguration configuration)
    {
        var text = configuration[DevelopmentOriginKey];

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var origin)
            || origin.Scheme is not ("http" or "https")
            || origin.UserInfo.Length != 0
            || origin.Query.Length != 0
            || origin.Fragment.Length != 0
            || origin.AbsolutePath != "/"
            || text.Trim().Contains('#', StringComparison.Ordinal)
            || text.Trim().Contains('?', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{DevelopmentOriginKey} must be an absolute http or https origin without a path, query or user information.");
        }

        return origin;
    }

    private static HashSet<string> ReadHosts(IConfiguration configuration)
    {
        var text = configuration[HostsKey] ?? DefaultHost;
        var hosts = text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(host => host.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        if (hosts.Count == 0 || hosts.Any(host => Uri.CheckHostName(host) != UriHostNameType.Dns))
        {
            throw new InvalidOperationException($"{HostsKey} must list at least one host name, separated by commas.");
        }

        return hosts;
    }

    private static int ReadWholeNumber(IConfiguration configuration, string key, int min, int max, int fallback)
    {
        var text = configuration[key];

        if (text is null)
        {
            return fallback;
        }

        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            || value < min
            || value > max)
        {
            throw new InvalidOperationException($"{key} must be a whole number between {min} and {max}.");
        }

        return value;
    }
}

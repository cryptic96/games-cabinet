using System.Globalization;
using Cabinet.Repository.Bgg;

namespace Cabinet.Service.Sync;

/// <summary>
/// Reads the BGG settings from the Bgg configuration section. A missing username or token is allowed, because the app
/// serves pages without them and a sync then reports that it is not configured; every other value is checked here, so a
/// typo in the server env file stops the app at startup with a message naming the key.
/// </summary>
public static class BggSettings
{
    private const string UsernameKey = "Bgg:Username";
    private const string TokenKey = "Bgg:Token";
    private const string ContactUrlKey = "Bgg:ContactUrl";
    private const string GapKey = "Bgg:MinRequestGapSeconds";
    private const string PrivateInfoKey = "Bgg:IncludePrivateInfo";
    private const string BaseUriKey = "Bgg:BaseUri";
    private const int MaxGapSeconds = 3600;

    /// <summary>Reads and validates the settings.</summary>
    /// <param name="configuration">The configuration to read.</param>
    /// <param name="environment">The hosting environment; the API address can only be overridden in Development.</param>
    /// <param name="productVersion">The running build's version, for the User-Agent.</param>
    /// <exception cref="InvalidOperationException">A value is invalid; the message names the full key.</exception>
    public static BggOptions FromConfiguration(IConfiguration configuration, IHostEnvironment environment, string productVersion)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(productVersion);

        var isDevelopment = environment.IsDevelopment();

        return new BggOptions(
            isDevelopment ? ReadBaseUri(configuration) : BggOptions.DefaultBaseUri,
            ReadText(configuration, UsernameKey),
            ReadText(configuration, TokenKey),
            ReadText(configuration, ContactUrlKey),
            TimeSpan.FromSeconds(ReadGapSeconds(configuration)),
            ReadSwitch(configuration, PrivateInfoKey),
            productVersion,
            BaseUriOverrideIgnored: !isDevelopment && !string.IsNullOrWhiteSpace(configuration[BaseUriKey]));
    }

    private static string? ReadText(IConfiguration configuration, string key)
    {
        var text = configuration[key]?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.Any(char.IsControl))
        {
            throw new InvalidOperationException($"{key} must not contain control characters.");
        }

        return text;
    }

    private static Uri ReadBaseUri(IConfiguration configuration)
    {
        var text = configuration[BaseUriKey];

        if (string.IsNullOrWhiteSpace(text))
        {
            return BggOptions.DefaultBaseUri;
        }

        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || !uri.AbsolutePath.EndsWith('/'))
        {
            throw new InvalidOperationException($"{BaseUriKey} must be an absolute http or https address that ends with a slash.");
        }

        return uri;
    }

    private static double ReadGapSeconds(IConfiguration configuration)
    {
        var text = configuration[GapKey];
        var minimum = (int)BggOptions.MinimumRequestGap.TotalSeconds;

        if (text is null)
        {
            return minimum;
        }

        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            || value < minimum
            || value > MaxGapSeconds)
        {
            throw new InvalidOperationException($"{GapKey} must be a whole number between {minimum} and {MaxGapSeconds}.");
        }

        return value;
    }

    private static bool ReadSwitch(IConfiguration configuration, string key)
    {
        var text = configuration[key];

        if (text is null)
        {
            return false;
        }

        return text.Trim().ToLowerInvariant() switch
        {
            "true" => true,
            "false" => false,
            _ => throw new InvalidOperationException($"{key} must be true or false."),
        };
    }
}

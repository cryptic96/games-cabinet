using System.Globalization;
using Cabinet.Domain.Collection;

namespace Cabinet.Service.Sync;

/// <summary>
/// Reads the game details settings from the Enrichment configuration section. Every value is checked here, so a typo in
/// the server env file stops the app at startup with a message naming the key.
/// </summary>
public static class EnrichmentSettings
{
    private const string RequestsKey = "Enrichment:MaxThingRequestsPerRun";
    private const string RefreshBatchesKey = "Enrichment:RefreshBatchesPerRun";
    private const string RefreshDaysKey = "Enrichment:RefreshAfterDays";

    /// <summary>
    /// Reads the three Enrichment keys. A key that is absent takes the default; a key that is present but out of range or
    /// not a whole number throws.
    /// </summary>
    /// <param name="configuration">The configuration to read.</param>
    /// <exception cref="InvalidOperationException">A value is invalid; the message names the full key.</exception>
    public static EnrichmentOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var defaults = EnrichmentOptions.Default;

        return new EnrichmentOptions(
            ReadWholeNumber(configuration, RequestsKey, 1, 100, defaults.MaxThingRequestsPerRun),
            ReadWholeNumber(configuration, RefreshBatchesKey, 0, 25, defaults.RefreshBatchesPerRun),
            TimeSpan.FromDays(ReadWholeNumber(configuration, RefreshDaysKey, 1, 90, (int)defaults.RefreshAfter.TotalDays)));
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

using System.Globalization;

namespace Cabinet.Service.Sync;

/// <summary>The timing rules of the sync.</summary>
/// <param name="BackgroundEnabled">Whether the hourly and start-up syncs run on their own.</param>
/// <param name="Interval">How often the hourly sync runs.</param>
/// <param name="ManualCooldown">How long after any sync starts a visitor's request for one is refused.</param>
/// <param name="StartupJitterMax">The latest the start-up sync may begin after the app starts.</param>
/// <param name="StaleAfter">How old the collection may get before a page treats it as out of date.</param>
public sealed record SyncOptions(
    bool BackgroundEnabled,
    TimeSpan Interval,
    TimeSpan ManualCooldown,
    TimeSpan StartupJitterMax,
    TimeSpan StaleAfter)
{
    /// <summary>The shortest interval allowed between two background syncs, and the least time between two starts of the app that may both sync.</summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(15);

    /// <summary>The earliest the start-up sync may begin after the app starts.</summary>
    public static readonly TimeSpan StartupJitterMin = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Reads the sync timing settings from the Sync configuration section. Every value is checked here, so a typo in the server
/// env file stops the app at startup with a message naming the key.
/// </summary>
public static class SyncSettings
{
    private const string BackgroundKey = "Sync:BackgroundEnabled";
    private const string IntervalKey = "Sync:IntervalMinutes";
    private const string CooldownKey = "Sync:ManualCooldownMinutes";
    private const string JitterKey = "Sync:StartupJitterMaxSeconds";
    private const string StaleKey = "Sync:StaleAfterHours";

    /// <summary>
    /// Reads the five Sync keys. A key that is absent takes the default; a key that is present but out of range, not a
    /// whole number, or not true or false where a switch is expected throws.
    /// </summary>
    /// <param name="configuration">The configuration to read.</param>
    /// <exception cref="InvalidOperationException">A value is invalid; the message names the full key.</exception>
    public static SyncOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new SyncOptions(
            ReadSwitch(configuration, BackgroundKey, true),
            TimeSpan.FromMinutes(ReadWholeNumber(configuration, IntervalKey, (int)SyncOptions.MinimumInterval.TotalMinutes, 1440, 60)),
            TimeSpan.FromMinutes(ReadWholeNumber(configuration, CooldownKey, 1, 120, 10)),
            TimeSpan.FromSeconds(ReadWholeNumber(configuration, JitterKey, (int)SyncOptions.StartupJitterMin.TotalSeconds, 3600, 120)),
            TimeSpan.FromHours(ReadWholeNumber(configuration, StaleKey, 1, 168, 3)));
    }

    private static bool ReadSwitch(IConfiguration configuration, string key, bool fallback)
    {
        var text = configuration[key];

        if (text is null)
        {
            return fallback;
        }

        return text.Trim().ToLowerInvariant() switch
        {
            "true" => true,
            "false" => false,
            _ => throw new InvalidOperationException($"{key} must be true or false."),
        };
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

using System.Globalization;
using Cabinet.Domain.Layout;

namespace Cabinet.Service.Layout;

/// <summary>
/// Reads the layout settings from the Layout configuration section. Every value is checked here, so a typo in the server
/// env file stops the app at startup with a message naming the key instead of silently producing an odd cabinet.
/// </summary>
public static class LayoutSettings
{
    private const string SharePercentKey = "Layout:CoverSharePercent";
    private const string StrategyKey = "Layout:CoverStrategy";
    private const string StackMaxKey = "Layout:ExpansionStackMax";
    private const string ThresholdKey = "Layout:FewGamesThreshold";
    private const string LieFlatKey = "Layout:LieFlatBeforeNewSection";
    private const string GroupSeriesKey = "Layout:GroupSeries";
    private const string CoverFromExpansionsKey = "Layout:CoverFromExpansions";

    /// <summary>
    /// Reads the seven Layout keys. A key that is absent takes the default; a key that is present but out of range, not a
    /// whole number, not a known strategy name, or not true or false where a switch is expected throws.
    /// </summary>
    /// <exception cref="InvalidOperationException">A value is invalid; the message names the full key.</exception>
    public static LayoutOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var defaults = LayoutOptions.Default;

        return new LayoutOptions(
            ReadWholeNumber(configuration, SharePercentKey, 0, 100, defaults.CoverSharePercent),
            ReadStrategy(configuration, defaults.CoverStrategy),
            ReadWholeNumber(configuration, StackMaxKey, 1, 20, defaults.ExpansionStackMax),
            ReadWholeNumber(configuration, ThresholdKey, 0, 100, defaults.FewGamesThreshold),
            ReadSwitch(configuration, LieFlatKey, defaults.LieFlatBeforeNewSection),
            ReadSwitch(configuration, GroupSeriesKey, defaults.GroupSeries),
            ReadWholeNumber(configuration, CoverFromExpansionsKey, 0, 20, defaults.CoverFromExpansions));
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

    private static CoverStrategy ReadStrategy(IConfiguration configuration, CoverStrategy fallback)
    {
        var text = configuration[StrategyKey];

        if (text is null)
        {
            return fallback;
        }

        var names = string.Join(", ", Enum.GetNames<CoverStrategy>());
        var trimmed = text.Trim();

        if (trimmed.Length == 0
            || char.IsDigit(trimmed[0])
            || trimmed[0] is '+' or '-'
            || trimmed.Contains(',')
            || !Enum.TryParse<CoverStrategy>(trimmed, ignoreCase: true, out var strategy)
            || !Enum.IsDefined(strategy))
        {
            throw new InvalidOperationException($"{StrategyKey} must be one of {names}.");
        }

        return strategy;
    }
}

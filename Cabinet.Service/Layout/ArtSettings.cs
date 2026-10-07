using System.Globalization;
using Cabinet.Domain.Collection;

namespace Cabinet.Service.Layout;

/// <summary>
/// Reads the rules that choose between the owned edition's picture and the game's main picture from the Art
/// configuration section. Every value is checked here, so a typo in the server env file stops the app at startup with a
/// message naming the key instead of silently choosing odd pictures. The values are percentages; they only decide how
/// stored measurements are read, so changing them needs a restart and no download.
/// </summary>
public static class ArtSettings
{
    private const string FlatMinFillKey = "Art:FlatMinFillPercent";
    private const string FlatMaxCornerKey = "Art:FlatMaxCornerPercent";
    private const string ThreeDMaxFillKey = "Art:ThreeDMaxFillPercent";
    private const string ThreeDMinCornerKey = "Art:ThreeDMinCornerPercent";
    private const string ShapeMarginKey = "Art:ShapeMarginPercent";
    private const string OrientFromCoverKey = "Art:OrientFromCover";
    private const int MinShapeMarginPercent = 1;
    private const int MaxShapeMarginPercent = 50;
    private const double PercentDivisor = 100.0;

    /// <summary>
    /// Reads the Art keys. A key that is absent takes the default; a key that is present but out of range or not a
    /// whole number throws, and so does a photographed-box fill limit above the flat-cover fill minimum, because no picture
    /// could then be judged both ways in a way that makes sense.
    /// </summary>
    /// <param name="configuration">The configuration to read.</param>
    /// <exception cref="InvalidOperationException">A value is invalid; the message names the full key.</exception>
    public static ArtRules FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var defaults = ArtThresholds.Default;
        var flatMinFill = ReadPercent(configuration, FlatMinFillKey, 50, 100, defaults.FlatMinFill);
        var flatMaxCorner = ReadPercent(configuration, FlatMaxCornerKey, 0, 100, defaults.FlatMaxCorner);
        var threeDMaxFill = ReadPercent(configuration, ThreeDMaxFillKey, 0, 100, defaults.ThreeDMaxFill);
        var threeDMinCorner = ReadPercent(configuration, ThreeDMinCornerKey, 0, 100, defaults.ThreeDMinCorner);

        if (threeDMaxFill > flatMinFill)
        {
            throw new InvalidOperationException($"{ThreeDMaxFillKey} must not be above {FlatMinFillKey}.");
        }

        return new ArtRules(
            new ArtThresholds(flatMinFill, flatMaxCorner, threeDMaxFill, threeDMinCorner, defaults.DegenerateBackdropShare),
            ReadWholeNumber(configuration, ShapeMarginKey, MinShapeMarginPercent, MaxShapeMarginPercent, ArtRules.DefaultShapeMarginPercent),
            ReadSwitch(configuration, OrientFromCoverKey, fallback: true));
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

    private static bool ReadSwitch(IConfiguration configuration, string key, bool fallback)
    {
        var text = configuration[key];

        if (text is null)
        {
            return fallback;
        }

        return bool.TryParse(text, out var value)
            ? value
            : throw new InvalidOperationException($"{key} must be true or false.");
    }

    private static double ReadPercent(IConfiguration configuration, string key, int min, int max, double fallback)
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

        return value / PercentDivisor;
    }
}

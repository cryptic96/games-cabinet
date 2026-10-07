namespace Cabinet.Domain.Layout;

/// <summary>
/// Turns a colour taken from box art into a background and text pair that reads under the furniture shade. The art colour
/// is kept whenever white or black text already reaches the minimum contrast, both with no shade and under the strongest
/// shade the furniture may lay across a box. Otherwise only its lightness moves, by the smallest amount that lets one of
/// the two text colours pass, so the spine stays recognisably the colour of the box.
/// </summary>
public static class SpineColour
{
    /// <summary>The contrast ratio a title must reach against its background.</summary>
    public const double MinimumContrast = 4.5;

    /// <summary>The step by which OKLab lightness is moved while searching for a legible background.</summary>
    public const double LightnessStep = 0.005;

    /// <summary>The most steps the search takes in each direction, enough to reach pure black and pure white.</summary>
    public const int MaxLightnessSteps = 200;

    private const double ByteMax = 255.0;
    private const double PercentDivisor = 100.0;
    private const double LuminanceBreakpoint = 0.04045;
    private const double LuminanceLinearDivisor = 12.92;
    private const double LuminanceOffset = 0.055;
    private const double LuminanceScale = 1.055;
    private const double LuminanceExponent = 2.4;
    private const double RedWeight = 0.2126;
    private const double GreenWeight = 0.7152;
    private const double BlueWeight = 0.0722;
    private const double ContrastOffset = 0.05;

    /// <summary>Pure white, the lighter of the two text colours.</summary>
    public static RgbColour White { get; } = new(byte.MaxValue, byte.MaxValue, byte.MaxValue);

    /// <summary>Pure black, the darker of the two text colours.</summary>
    public static RgbColour Black { get; } = new(0, 0, 0);

    /// <summary>The contrast ratio between two colours, from 1 for identical luminance to 21 for black on white.</summary>
    public static double ContrastRatio(RgbColour first, RgbColour second) =>
        ContrastRatio(Channels(first), Channels(second));

    /// <summary>Whether a contrast ratio reaches the minimum. A ratio of exactly the minimum passes.</summary>
    public static bool MeetsMinimum(double ratio) => ratio >= MinimumContrast;

    /// <summary>
    /// Whether text on a background reaches the minimum contrast both with no shade and under the strongest shade, where
    /// both colours are blended toward the shade colour on their gamma-encoded channels.
    /// </summary>
    public static bool PassesUnderShade(RgbColour background, RgbColour text) =>
        MeetsMinimum(ContrastRatio(background, text))
        && MeetsMinimum(ContrastRatio(Shade(background, SpinePalette.MaxShadePercent), Shade(text, SpinePalette.MaxShadePercent)));

    /// <summary>
    /// The background and text for a colour taken from box art. The colour itself is the background when white or black
    /// text passes under the shade, white being preferred. Otherwise the OKLab lightness moves in 0.005 steps, lighter
    /// before darker at the same step, keeping hue and chroma, and chroma shrinks only when the result would leave the
    /// sRGB gamut. The search covers the whole lightness range, so it always ends with a legible pair.
    /// </summary>
    /// <param name="extracted">The colour taken from the art.</param>
    public static PaletteTone PairFor(RgbColour extracted)
    {
        if (TextFor(extracted) is { } text)
        {
            return Tone(extracted, text);
        }

        var lab = Oklab.From(extracted);

        for (var step = 1; step <= MaxLightnessSteps; step++)
        {
            foreach (var direction in new[] { 1, -1 })
            {
                var candidate = Oklab.ToSrgb(lab.Lightness + (direction * step * LightnessStep), lab.Chroma, lab.Hue);

                if (candidate is { } background && TextFor(background) is { } candidateText)
                {
                    return Tone(background, candidateText);
                }
            }
        }

        return Tone(Black, White);
    }

    /// <summary>
    /// Whether a stored pair is safe to use: both colours are lowercase six-digit hexadecimal, the text is white or black,
    /// and the pair passes under the shade. A null or damaged pair is refused so the caller can use a palette tone.
    /// </summary>
    /// <param name="pair">The stored pair, or null when none was stored.</param>
    public static bool IsValidPair(PaletteTone? pair)
    {
        if (pair is null
            || !RgbColour.TryParseHex(pair.Background, out var background)
            || !RgbColour.TryParseHex(pair.Text, out var text))
        {
            return false;
        }

        return (text == White || text == Black) && PassesUnderShade(background, text);
    }

    /// <summary>The OKLab lightness of a colour, 0 for black to about 1 for white.</summary>
    public static double LightnessOf(RgbColour colour) => Oklab.From(colour).Lightness;

    private static RgbColour? TextFor(RgbColour background)
    {
        if (PassesUnderShade(background, White))
        {
            return White;
        }

        return PassesUnderShade(background, Black) ? Black : null;
    }

    private static PaletteTone Tone(RgbColour background, RgbColour text) => new(background.ToHex(), text.ToHex());

    private static double[] Channels(RgbColour colour) => [colour.R, colour.G, colour.B];

    private static double[] Shade(RgbColour colour, int percent)
    {
        RgbColour.TryParseHex(SpinePalette.ShadeColour, out var shade);

        double[] shadeChannels = [shade.R, shade.G, shade.B];

        return Channels(colour).Select((channel, index) =>
            (channel * (PercentDivisor - percent) / PercentDivisor) + (shadeChannels[index] * percent / PercentDivisor)).ToArray();
    }

    private static double ContrastRatio(double[] first, double[] second)
    {
        var a = Luminance(first);
        var b = Luminance(second);
        var (lighter, darker) = a >= b ? (a, b) : (b, a);

        return (lighter + ContrastOffset) / (darker + ContrastOffset);
    }

    private static double Luminance(double[] channels)
    {
        var red = Linear(channels[0]);
        var green = Linear(channels[1]);
        var blue = Linear(channels[2]);

        return (RedWeight * red) + (GreenWeight * green) + (BlueWeight * blue);
    }

    private static double Linear(double channel)
    {
        var value = channel / ByteMax;

        return value <= LuminanceBreakpoint
            ? value / LuminanceLinearDivisor
            : Math.Pow((value + LuminanceOffset) / LuminanceScale, LuminanceExponent);
    }
}

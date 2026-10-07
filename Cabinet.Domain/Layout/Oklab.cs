namespace Cabinet.Domain.Layout;

/// <summary>
/// The OKLab perceptual colour space: conversion from and to sRGB bytes, used to change only how light a colour is while
/// its hue and chroma stay put.
/// </summary>
internal static class Oklab
{
    private const double ByteMax = 255.0;
    private const double SrgbBreakpoint = 0.04045;
    private const double SrgbLinearBreakpoint = 0.0031308;
    private const double SrgbLinearDivisor = 12.92;
    private const double SrgbOffset = 0.055;
    private const double SrgbScale = 1.055;
    private const double SrgbExponent = 2.4;

    /// <summary>How far past 0 or 1 an encoded channel may land and still round to a valid byte without visible change.</summary>
    private const double GamutTolerance = 0.0005;

    /// <summary>The share of chroma kept on each attempt to bring a colour back inside the sRGB gamut.</summary>
    private const double ChromaKeep = 0.97;

    private const int MaxChromaAttempts = 400;

    /// <summary>The lightness, green-red axis and blue-yellow axis of an sRGB colour.</summary>
    /// <param name="Lightness">Perceived lightness, 0 for black to about 1 for white.</param>
    /// <param name="A">The green-red axis.</param>
    /// <param name="B">The blue-yellow axis.</param>
    internal readonly record struct Lab(double Lightness, double A, double B)
    {
        /// <summary>The distance from the grey axis.</summary>
        public double Chroma => Math.Sqrt((A * A) + (B * B));

        /// <summary>The angle around the grey axis in radians.</summary>
        public double Hue => Math.Atan2(B, A);
    }

    /// <summary>Converts an sRGB colour to OKLab.</summary>
    public static Lab From(RgbColour colour)
    {
        var r = ToLinear(colour.R);
        var g = ToLinear(colour.G);
        var b = ToLinear(colour.B);

        var l = Math.Cbrt((0.4122214708 * r) + (0.5363325363 * g) + (0.0514459929 * b));
        var m = Math.Cbrt((0.2119034982 * r) + (0.6806995451 * g) + (0.1073969566 * b));
        var s = Math.Cbrt((0.0883024619 * r) + (0.2817188376 * g) + (0.6299787005 * b));

        return new Lab(
            (0.2104542553 * l) + (0.7936177850 * m) - (0.0040720468 * s),
            (1.9779984951 * l) - (2.4285922050 * m) + (0.4505937099 * s),
            (0.0259040371 * l) + (0.7827717662 * m) - (0.8086757660 * s));
    }

    /// <summary>
    /// Converts a lightness, chroma and hue to the nearest sRGB colour at that exact lightness, or null when no valid
    /// colour exists at it. Chroma shrinks in 3 percent steps only as far as needed to fit the sRGB gamut.
    /// </summary>
    public static RgbColour? ToSrgb(double lightness, double chroma, double hue)
    {
        var current = chroma;

        for (var attempt = 0; attempt < MaxChromaAttempts; attempt++)
        {
            var colour = TryConvert(lightness, current * Math.Cos(hue), current * Math.Sin(hue));

            if (colour is not null)
            {
                return colour;
            }

            current *= ChromaKeep;
        }

        return TryConvert(lightness, 0, 0);
    }

    private static RgbColour? TryConvert(double lightness, double a, double b)
    {
        var l = Cube(lightness + (0.3963377774 * a) + (0.2158037573 * b));
        var m = Cube(lightness - (0.1055613458 * a) - (0.0638541728 * b));
        var s = Cube(lightness - (0.0894841775 * a) - (1.2914855480 * b));

        var red = ToEncoded((+4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s));
        var green = ToEncoded((-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s));
        var blue = ToEncoded((-0.0041960863 * l) - (0.7034186147 * m) + (1.7076147010 * s));

        if (!InGamut(red) || !InGamut(green) || !InGamut(blue))
        {
            return null;
        }

        return new RgbColour(ToByte(red), ToByte(green), ToByte(blue));
    }

    private static double Cube(double value) => value * value * value;

    private static bool InGamut(double encoded) => encoded >= -GamutTolerance && encoded <= 1 + GamutTolerance;

    private static byte ToByte(double encoded) => (byte)Math.Round(Math.Clamp(encoded, 0, 1) * ByteMax, MidpointRounding.AwayFromZero);

    private static double ToLinear(byte channel)
    {
        var value = channel / ByteMax;

        return value <= SrgbBreakpoint ? value / SrgbLinearDivisor : Math.Pow((value + SrgbOffset) / SrgbScale, SrgbExponent);
    }

    private static double ToEncoded(double linear)
    {
        if (double.IsNaN(linear))
        {
            return double.NaN;
        }

        var magnitude = Math.Abs(linear);
        var encoded = magnitude <= SrgbLinearBreakpoint
            ? magnitude * SrgbLinearDivisor
            : (SrgbScale * Math.Pow(magnitude, 1 / SrgbExponent)) - SrgbOffset;

        return Math.CopySign(encoded, linear);
    }
}

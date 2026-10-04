using System.Runtime.InteropServices;
using System.Text;
using SkiaSharp;

namespace Cabinet.Repository.Images;

/// <summary>
/// Exercises the native imaging stack end to end on synthetic data: decode, resize and WebP encode.
/// It reads no input and writes nothing, so it is safe to run on any host the release lands on.
/// </summary>
public static class ImageSmoke
{
    private const int FlatSourceWidth = 640;
    private const int FlatSourceHeight = 480;
    private const int FlatTargetWidth = 240;
    private const int FlatTargetHeight = 180;
    private const int NoiseSourceWidth = 2400;
    private const int NoiseSourceHeight = 1800;
    private const int NoiseTargetWidth = 480;
    private const int NoiseTargetHeight = 360;
    private const int NoiseSeed = 20240607;
    private const int WebpQuality = 80;
    private const int ContainerHeaderLength = 12;
    private const int OpaqueAlpha = 255;
    private const int BytesPerPixel = 4;

    /// <summary>
    /// Runs both round trips and returns the width, height and encoded byte count of the larger one.
    /// Throws <see cref="InvalidOperationException"/> when any step does not behave as expected.
    /// </summary>
    public static (int Width, int Height, int Bytes) Run()
    {
        var flat = EncodeFlatSample();
        Verify(flat, FlatTargetWidth, FlatTargetHeight);

        var noise = EncodeSample();
        Verify(noise, NoiseTargetWidth, NoiseTargetHeight);

        return (NoiseTargetWidth, NoiseTargetHeight, noise.Length);
    }

    /// <summary>
    /// Builds a seeded-noise bitmap, round trips it through PNG so the decoder runs, resizes it
    /// and returns the WebP encoding of the resized image.
    /// </summary>
    public static byte[] EncodeSample()
    {
        using var source = CreateNoiseBitmap();
        using var decoded = DecodeThroughPng(source);

        return ResizeAndEncode(decoded, NoiseTargetWidth, NoiseTargetHeight);
    }

    /// <summary>
    /// Checks that the payload is a WebP container that decodes to the expected dimensions.
    /// Throws <see cref="InvalidOperationException"/> with a plain message on any mismatch.
    /// </summary>
    public static void Verify(byte[] webp, int expectedWidth, int expectedHeight)
    {
        if (webp.Length < ContainerHeaderLength
            || Encoding.ASCII.GetString(webp, 0, 4) != "RIFF"
            || Encoding.ASCII.GetString(webp, 8, 4) != "WEBP")
        {
            throw new InvalidOperationException("The encoded image is not a WebP container.");
        }

        using var decoded = Decode(webp, "The encoded image could not be decoded.");

        if (decoded.Width != expectedWidth || decoded.Height != expectedHeight)
        {
            throw new InvalidOperationException(
                $"The decoded image is {decoded.Width}x{decoded.Height}, expected {expectedWidth}x{expectedHeight}.");
        }
    }

    private static byte[] EncodeFlatSample()
    {
        using var source = new SKBitmap(new SKImageInfo(FlatSourceWidth, FlatSourceHeight));
        using (var canvas = new SKCanvas(source))
        {
            canvas.Clear(new SKColor(120, 80, 40));
        }

        return ResizeAndEncode(source, FlatTargetWidth, FlatTargetHeight);
    }

    private static SKBitmap CreateNoiseBitmap()
    {
        var info = new SKImageInfo(NoiseSourceWidth, NoiseSourceHeight, SKColorType.Rgba8888, SKAlphaType.Opaque);
        var pixels = new byte[NoiseSourceWidth * NoiseSourceHeight * BytesPerPixel];
        new Random(NoiseSeed).NextBytes(pixels);

        for (var alpha = BytesPerPixel - 1; alpha < pixels.Length; alpha += BytesPerPixel)
        {
            pixels[alpha] = OpaqueAlpha;
        }

        var bitmap = new SKBitmap(info);
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);

        return bitmap;
    }

    private static SKBitmap DecodeThroughPng(SKBitmap source)
    {
        using var image = SKImage.FromBitmap(source);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);

        return Decode(png.ToArray(), "The PNG round trip could not be decoded.");
    }

    private static SKBitmap Decode(byte[] payload, string failureMessage)
    {
        try
        {
            return SKBitmap.Decode(payload) ?? throw new InvalidOperationException(failureMessage);
        }
        catch (ArgumentNullException exception)
        {
            throw new InvalidOperationException(failureMessage, exception);
        }
    }

    private static byte[] ResizeAndEncode(SKBitmap source, int width, int height)
    {
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        using var resized = source.Resize(new SKImageInfo(width, height), sampling)
            ?? throw new InvalidOperationException("The image could not be resized.");
        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, WebpQuality)
            ?? throw new InvalidOperationException("The image could not be encoded as WebP.");

        return encoded.ToArray();
    }
}

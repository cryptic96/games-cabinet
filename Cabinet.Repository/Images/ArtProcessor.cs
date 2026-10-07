using System.Security.Cryptography;
using SkiaSharp;

namespace Cabinet.Repository.Images;

/// <summary>One resized picture, encoded and named by its content.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Name">The file name: sixteen lowercase hexadecimal characters of the content hash, a dash, the width and <c>.webp</c>.</param>
/// <param name="Bytes">The encoded WebP file.</param>
public sealed record EncodedArt(int Width, int Height, string Name, byte[] Bytes);

/// <summary>How a picture came out of processing.</summary>
public abstract record ArtProcessing
{
    /// <summary>The picture was resized; the sizes are widest first.</summary>
    /// <param name="Variants">The encoded sizes.</param>
    public sealed record Done(IReadOnlyList<EncodedArt> Variants) : ArtProcessing;

    /// <summary>The bytes could not be read as a picture.</summary>
    public sealed record Unreadable : ArtProcessing;

    /// <summary>The picture was not decoded because it broke a rule.</summary>
    /// <param name="Reason">One category word that says which rule.</param>
    public sealed record Turned(string Reason) : ArtProcessing;

    /// <summary>The picture was resized.</summary>
    /// <param name="variants">The encoded sizes, widest first.</param>
    public static ArtProcessing Processed(IReadOnlyList<EncodedArt> variants) => new Done(variants);

    /// <summary>The bytes could not be read as a picture.</summary>
    public static ArtProcessing Undecodable { get; } = new Unreadable();

    /// <summary>The picture was refused by a rule.</summary>
    /// <param name="reason">One category word that says which rule.</param>
    public static ArtProcessing Refused(string reason) => new Turned(reason);
}

/// <summary>
/// Turns downloaded bytes into the two small pictures the site serves. The picture is only ever scaled down, never
/// cropped or recoloured; the original bytes are not kept. The pixel count is read from the file header and checked
/// before anything is decoded, and the decode itself is scaled so a large picture never fills memory.
/// </summary>
public static class ArtProcessor
{
    /// <summary>The widths of the stored sizes, widest first.</summary>
    public static readonly IReadOnlyList<int> VariantWidths = [480, 240];

    private const int DecodeWidth = 960;
    private const int WebpQuality = 80;
    private const int HashCharacters = 16;

    /// <summary>
    /// Reads, checks, decodes and resizes the picture. A source at least 480 pixels wide gives a 480 and a 240 pixel wide
    /// file; a source between 240 and 480 gives a 240 pixel file only; a narrower one gives one file at its own width.
    /// </summary>
    /// <param name="bytes">The downloaded bytes.</param>
    /// <param name="limits">The limits; only the pixel cap is used here.</param>
    public static ArtProcessing Process(byte[] bytes, ArtLimits limits)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(limits);

        try
        {
            using var stream = new SKMemoryStream(bytes);
            using var codec = SKCodec.Create(stream);

            if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0)
            {
                return ArtProcessing.Undecodable;
            }

            if ((long)codec.Info.Width * codec.Info.Height > limits.MaxPixels)
            {
                return ArtProcessing.Refused("pixels");
            }

            using var decoded = Decode(codec);

            return decoded is null ? ArtProcessing.Undecodable : ArtProcessing.Processed(Resize(decoded));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        {
            return ArtProcessing.Undecodable;
        }
    }

    private static SKBitmap? Decode(SKCodec codec)
    {
        var scale = Math.Min(1f, (float)DecodeWidth / codec.Info.Width);
        var size = codec.GetScaledDimensions(scale);

        if (size.Width <= 0 || size.Height <= 0)
        {
            return null;
        }

        return SKBitmap.Decode(codec, new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
    }

    private static List<EncodedArt> Resize(SKBitmap decoded)
    {
        var widths = VariantWidths.Where(width => width <= decoded.Width).ToList();

        if (widths.Count == 0)
        {
            widths.Add(decoded.Width);
        }

        var variants = new List<EncodedArt>(widths.Count);

        foreach (var width in widths)
        {
            var height = Math.Max(1, (int)Math.Round((double)width * decoded.Height / decoded.Width, MidpointRounding.AwayFromZero));
            variants.Add(Encode(decoded, width, height));
        }

        return variants;
    }

    private static EncodedArt Encode(SKBitmap decoded, int width, int height)
    {
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);

        using var resized = decoded.Resize(info, sampling) ?? throw new InvalidOperationException("The picture could not be resized.");
        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, WebpQuality)
            ?? throw new InvalidOperationException("The picture could not be encoded.");
        var bytes = encoded.ToArray();
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes))[..HashCharacters];

        return new EncodedArt(width, height, $"{hash}-{width}.webp", bytes);
    }
}

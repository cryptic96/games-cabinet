using SkiaSharp;

namespace Cabinet.BrowserTests.Infrastructure;

/// <summary>
/// Compares two PNG screenshots pixel for pixel, so a test can prove that two renders of the same page look exactly the same
/// and, when they do not, keep a picture that shows where they differ.
/// </summary>
public static class PixelComparison
{
    private const string ScreenshotDirectoryVariable = "CABINET_SCREENSHOT_DIR";

    private static readonly SKColor MarkerColour = new(255, 0, 255, 255);

    /// <summary>
    /// Counts the pixels whose red, green, blue or alpha value differs between the two images. Images of different sizes are
    /// never counted as equal: the result then reports the sizes and counts every pixel of the larger area as different.
    /// </summary>
    /// <param name="first">The first PNG.</param>
    /// <param name="second">The second PNG.</param>
    /// <returns>The sizes of both images and the number of differing pixels.</returns>
    public static PixelDifference Compare(byte[] first, byte[] second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        using var left = SKBitmap.Decode(first) ?? throw new InvalidOperationException("The first image is not a readable picture.");
        using var right = SKBitmap.Decode(second) ?? throw new InvalidOperationException("The second image is not a readable picture.");

        var leftSize = new PixelSize(left.Width, left.Height);
        var rightSize = new PixelSize(right.Width, right.Height);

        if (leftSize != rightSize)
        {
            var larger = Math.Max((long)left.Width * left.Height, (long)right.Width * right.Height);
            return new PixelDifference(leftSize, rightSize, larger);
        }

        return new PixelDifference(leftSize, rightSize, CountDifferences(left, right));
    }

    /// <summary>
    /// Writes a picture of where two images differ as <c>{name}.png</c> in the directory named by the
    /// <c>CABINET_SCREENSHOT_DIR</c> environment variable: a dimmed copy of the first image with every differing pixel in a solid
    /// marker colour. Does nothing when the variable is not set or the images differ in size.
    /// </summary>
    /// <param name="first">The first PNG.</param>
    /// <param name="second">The second PNG.</param>
    /// <param name="name">The file name without extension.</param>
    /// <returns>The path that was written, or null when nothing was written.</returns>
    public static string? WriteDiffImage(byte[] first, byte[] second, string name)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var directory = Environment.GetEnvironmentVariable(ScreenshotDirectoryVariable);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        using var left = SKBitmap.Decode(first) ?? throw new InvalidOperationException("The first image is not a readable picture.");
        using var right = SKBitmap.Decode(second) ?? throw new InvalidOperationException("The second image is not a readable picture.");

        if (left.Width != right.Width || left.Height != right.Height)
        {
            return null;
        }

        using var diff = new SKBitmap(new SKImageInfo(left.Width, left.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        for (var y = 0; y < left.Height; y++)
        {
            for (var x = 0; x < left.Width; x++)
            {
                var a = left.GetPixel(x, y);
                var b = right.GetPixel(x, y);
                diff.SetPixel(x, y, a == b ? Dim(a) : MarkerColour);
            }
        }

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{name}.png");
        using var image = SKImage.FromBitmap(diff);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static long CountDifferences(SKBitmap left, SKBitmap right)
    {
        var leftPixels = left.Pixels;
        var rightPixels = right.Pixels;
        var count = 0L;

        for (var index = 0; index < leftPixels.Length; index++)
        {
            if (leftPixels[index] != rightPixels[index])
            {
                count++;
            }
        }

        return count;
    }

    private static SKColor Dim(SKColor colour) =>
        new((byte)(colour.Red / 3 + 170), (byte)(colour.Green / 3 + 170), (byte)(colour.Blue / 3 + 170), 255);
}

/// <summary>The width and height of an image in pixels.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
public readonly record struct PixelSize(int Width, int Height);

/// <summary>The outcome of comparing two images.</summary>
/// <param name="FirstSize">The size of the first image.</param>
/// <param name="SecondSize">The size of the second image.</param>
/// <param name="DifferingPixels">How many pixels differ; the whole larger area when the sizes differ.</param>
public readonly record struct PixelDifference(PixelSize FirstSize, PixelSize SecondSize, long DifferingPixels);

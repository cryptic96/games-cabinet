using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using SkiaSharp;

namespace Cabinet.Repository.Images;

/// <summary>Everything one analysis pass learns about a picture.</summary>
/// <param name="Features">The measurements the art detector turns into a flat-or-3D verdict.</param>
/// <param name="Main">The picture's main colour with plain backgrounds ignored.</param>
/// <param name="Top">The colour that fills the bar above the picture.</param>
/// <param name="Right">The colour that fills the bar to the right of the picture.</param>
/// <param name="Bottom">The colour that fills the bar below the picture.</param>
/// <param name="Left">The colour that fills the bar to the left of the picture.</param>
public sealed record ArtFacts(ArtFeatures Features, RgbColour Main, RgbColour Top, RgbColour Right, RgbColour Bottom, RgbColour Left);

/// <summary>
/// Measures a decoded picture once: whether it looks like a photographed 3D box, its main colour with plain backgrounds
/// ignored, and the four colours that fill the bars around fitted art. All per-pixel work runs on a small copy, so the
/// cost never depends on the size of the original.
/// </summary>
public static class ArtAnalysis
{
    /// <summary>The width of the working copy in pixels.</summary>
    public const int AnalysisWidth = 96;

    /// <summary>The share of the border a colour must hold to count as a backdrop colour.</summary>
    public const double RingClusterMinShare = 0.10;

    /// <summary>The share of the border the kept colours must hold together for the picture to have a backdrop at all.</summary>
    public const double BackdropMinRingShare = 0.55;

    /// <summary>The share of the border that must be transparent for the picture to count as a cut-out product picture; a starting value.</summary>
    public const double CutOutMinRingShare = BackdropMinRingShare;

    /// <summary>The share of the border a near-white backdrop must hold, when it also fills most picture corners, to count as a backdrop although the kept colours hold less than the usual share; a starting value.</summary>
    public const double LightBackdropMinRingShare = 0.30;

    /// <summary>How many of the picture's four corners a near-white backdrop that holds less than the usual share must fill; a starting value.</summary>
    public const int LightBackdropMinCorners = 3;

    /// <summary>The least value on its brightest channel for a border colour to count as clearly coloured; a starting value.</summary>
    public const int ColouredBackdropMinValue = 32;

    /// <summary>The least saturation, from 0 for grey to 1 for a pure hue, for a border colour to count as clearly coloured; a starting value.</summary>
    public const double ColouredBackdropMinSaturation = 0.35;

    /// <summary>The share of its convex outline the largest piece of the subject must cover for a plain backdrop around it to stand; a starting value.</summary>
    public const double BoxOutlineMinSolidity = 0.85;

    /// <summary>The share of all subject pixels the largest connected piece must hold for a plain backdrop around it to stand; a starting value.</summary>
    public const double SubjectMinLargestShare = 0.90;

    /// <summary>The largest colour distance, in the red, green, blue cube, at which a pixel still matches a backdrop colour.</summary>
    public const int BackdropTolerance = 18;

    /// <summary>The alpha below which a pixel counts as transparent backdrop.</summary>
    public const int TransparentAlpha = 20;

    /// <summary>The side of each corner square, as a share of the shorter side of the subject's bounding box.</summary>
    public const double CornerSquareShare = 0.20;

    /// <summary>The share of all pixels the subject must cover for the main colour to be taken from it alone.</summary>
    public const double MinForegroundShare = 0.03;

    /// <summary>The depth of each edge strip, as a share of the picture's size across that edge.</summary>
    public const double EdgeStripShare = 0.02;

    /// <summary>The least depth of an edge strip in pixels of the working copy.</summary>
    public const int MinEdgeStripPx = 2;

    /// <summary>The fill at which a subject counts as rectangular when it is also free of empty corners.</summary>
    public const double RectangularFill = 0.97;

    /// <summary>The emptiest corner share up to which a subject counts as rectangular when it also fills its box.</summary>
    public const double RectangularCorner = 0.15;

    /// <summary>The least value on every channel for a backdrop colour to count as a near-white frame.</summary>
    public const int NearWhiteMin = 224;

    /// <summary>The widest spread between the highest and the lowest channel for a near-white pixel; a starting value.</summary>
    public const int NearWhiteMaxSpread = 24;

    /// <summary>The most two insets of one side may differ by, in pixels of the working copy, and still count as the same border; a starting value.</summary>
    public const int FrameInsetTolerancePx = 1;

    /// <summary>The share of the rows or columns of a side that must hold the same inset for that side to count as straight; a starting value.</summary>
    public const double FrameStraightShare = 0.50;

    /// <summary>The colour used when a picture has no opaque pixel at all.</summary>
    public static readonly RgbColour FallbackColour = new(128, 128, 128);

    private const int ChannelBits = 4;
    private const int ChannelShift = 4;
    private const int BucketsPerChannel = 1 << ChannelBits;
    private const int BucketCount = BucketsPerChannel * BucketsPerChannel * BucketsPerChannel;
    private const double SaturationWeight = 0.75;
    private const double CountWeight = 0.25;
    private const int OpaqueAlpha = 255;
    private const int CornerCount = 4;
    private const int SideCount = 4;

    /// <summary>
    /// Measures the picture. A picture that is entirely backdrop uses its overall mean colour, and a picture with no
    /// opaque pixel uses <see cref="FallbackColour"/>; neither is an error.
    /// </summary>
    public static ArtFacts Analyse(SKBitmap source)
    {
        var (pixels, width, height) = WorkingCopy(source);
        var masks = BackdropMask.Analyse(pixels, width, height);
        var features = Measure(pixels, masks, width, height);
        var main = MainColour(pixels, masks, features, width, height);

        return new ArtFacts(
            features,
            main,
            EdgeColour(pixels, width, height, Edge.Top, main),
            EdgeColour(pixels, width, height, Edge.Right, main),
            EdgeColour(pixels, width, height, Edge.Bottom, main),
            EdgeColour(pixels, width, height, Edge.Left, main));
    }

    private enum Edge
    {
        Top,
        Right,
        Bottom,
        Left,
    }

    private static (SKColor[] Pixels, int Width, int Height) WorkingCopy(SKBitmap source)
    {
        var width = AnalysisWidth;
        var height = Math.Max(1, (int)Math.Round((double)source.Height * AnalysisWidth / Math.Max(1, source.Width), MidpointRounding.AwayFromZero));
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        using var resized = source.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul), sampling)
            ?? throw new InvalidOperationException("The picture could not be scaled for analysis.");

        return (resized.Pixels, width, height);
    }

    private static bool IsOpaque(SKColor colour) => colour.Alpha >= TransparentAlpha;

    private static ArtFeatures Measure(SKColor[] pixels, BackdropMaskResult masks, int width, int height)
    {
        var backdrop = masks.Backdrop;
        var cutOut = masks.CutOut;
        var backdropShare = (double)backdrop.Count(isBackdrop => isBackdrop) / pixels.Length;
        var subject = new bool[pixels.Length];
        var subjectCount = 0;
        int minX = width, minY = height, maxX = -1, maxY = -1;

        for (var index = 0; index < pixels.Length; index++)
        {
            if (backdrop[index] || !IsOpaque(pixels[index]))
            {
                continue;
            }

            subject[index] = true;
            subjectCount++;
            var x = index % width;
            var y = index / width;
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }

        if (subjectCount == 0)
        {
            return new ArtFeatures(backdropShare, 1, 0, 0, 0, cutOut, false);
        }

        var boxWidth = maxX - minX + 1;
        var boxHeight = maxY - minY + 1;
        var fill = (double)subjectCount / (boxWidth * boxHeight);
        var side = Math.Max(1, (int)Math.Round(CornerSquareShare * Math.Min(boxWidth, boxHeight), MidpointRounding.AwayFromZero));
        var corners = new[]
        {
            EmptyShare(subject, width, minX, minY, side),
            EmptyShare(subject, width, maxX - side + 1, minY, side),
            EmptyShare(subject, width, minX, maxY - side + 1, side),
            EmptyShare(subject, width, maxX - side + 1, maxY - side + 1, side),
        };
        Array.Sort(corners);
        var sidesTouched = (minX == 0 ? 1 : 0) + (maxX == width - 1 ? 1 : 0) + (minY == 0 ? 1 : 0) + (maxY == height - 1 ? 1 : 0);

        var tightCrop = masks.LightCornerBackdrop && sidesTouched == SideCount;

        return new ArtFeatures(backdropShare, fill, corners[CornerCount - 1], corners[CornerCount - 2], sidesTouched, cutOut, tightCrop);
    }

    private static double EmptyShare(bool[] subject, int width, int left, int top, int side)
    {
        var empty = 0;
        for (var y = top; y < top + side; y++)
        {
            for (var x = left; x < left + side; x++)
            {
                if (!subject[(y * width) + x])
                {
                    empty++;
                }
            }
        }

        return (double)empty / (side * side);
    }

    private static RgbColour MainColour(SKColor[] pixels, BackdropMaskResult masks, ArtFeatures features, int width, int height)
    {
        var rectangular = features.Fill >= RectangularFill && features.Corner1 <= RectangularCorner;
        var counted = new List<SKColor>();
        var opaque = new List<SKColor>();

        for (var index = 0; index < pixels.Length; index++)
        {
            if (!IsOpaque(pixels[index]))
            {
                continue;
            }

            opaque.Add(pixels[index]);
            var excluded = rectangular ? masks.LightBackdrop[index] : masks.Backdrop[index];
            if (!excluded)
            {
                counted.Add(pixels[index]);
            }
        }

        if (opaque.Count == 0)
        {
            return FallbackColour;
        }

        if (counted.Count < MinForegroundShare * (width * height))
        {
            return MeanOf(opaque);
        }

        return DominantColour(counted);
    }

    private static RgbColour MeanOf(List<SKColor> colours)
    {
        long red = 0, green = 0, blue = 0;
        foreach (var colour in colours)
        {
            red += colour.Red;
            green += colour.Green;
            blue += colour.Blue;
        }

        return new RgbColour(Mean(red, colours.Count), Mean(green, colours.Count), Mean(blue, colours.Count));
    }

    private static byte Mean(long sum, long count) => (byte)((sum + (count / 2)) / count);

    private static RgbColour DominantColour(List<SKColor> colours)
    {
        var tallies = new Tally[BucketCount];
        foreach (var colour in colours)
        {
            var bucket = BucketOf(colour);
            tallies[bucket] = tallies[bucket].Add(colour);
        }

        var bestScore = double.MinValue;
        var best = default(Tally);
        for (var centre = 0; centre < BucketCount; centre++)
        {
            var neighbourhood = Neighbourhood(centre, tallies);
            if (neighbourhood.Count == 0)
            {
                continue;
            }

            var score = (CountWeight * neighbourhood.Count) + (SaturationWeight * neighbourhood.Saturation);
            if (score > bestScore)
            {
                bestScore = score;
                best = neighbourhood;
            }
        }

        return new RgbColour(Mean(best.Red, best.Count), Mean(best.Green, best.Count), Mean(best.Blue, best.Count));
    }

    private static Tally Neighbourhood(int centre, Tally[] tallies)
    {
        var total = default(Tally);
        var centreRed = centre >> (2 * ChannelBits);
        var centreGreen = (centre >> ChannelBits) & (BucketsPerChannel - 1);
        var centreBlue = centre & (BucketsPerChannel - 1);

        for (var r = Math.Max(0, centreRed - 1); r <= Math.Min(BucketsPerChannel - 1, centreRed + 1); r++)
        {
            for (var g = Math.Max(0, centreGreen - 1); g <= Math.Min(BucketsPerChannel - 1, centreGreen + 1); g++)
            {
                for (var b = Math.Max(0, centreBlue - 1); b <= Math.Min(BucketsPerChannel - 1, centreBlue + 1); b++)
                {
                    total = total.Plus(tallies[(r << (2 * ChannelBits)) | (g << ChannelBits) | b]);
                }
            }
        }

        return total;
    }

    private readonly record struct Tally(long Count, double Saturation, long Red, long Green, long Blue)
    {
        public Tally Add(SKColor colour) =>
            new(Count + 1, Saturation + SaturationOf(colour), Red + colour.Red, Green + colour.Green, Blue + colour.Blue);

        public Tally Plus(Tally other) =>
            new(Count + other.Count, Saturation + other.Saturation, Red + other.Red, Green + other.Green, Blue + other.Blue);
    }

    private static int BucketOf(SKColor colour) =>
        ((colour.Red >> ChannelShift) << (2 * ChannelBits)) | ((colour.Green >> ChannelShift) << ChannelBits) | (colour.Blue >> ChannelShift);

    private static double SaturationOf(SKColor colour)
    {
        var highest = Math.Max(colour.Red, Math.Max(colour.Green, colour.Blue));
        var lowest = Math.Min(colour.Red, Math.Min(colour.Green, colour.Blue));

        return highest == 0 ? 0 : (double)(highest - lowest) / highest;
    }

    private static RgbColour EdgeColour(SKColor[] pixels, int width, int height, Edge edge, RgbColour main)
    {
        var horizontal = edge is Edge.Top or Edge.Bottom;
        var across = horizontal ? height : width;
        var depth = Math.Min(across, Math.Max(MinEdgeStripPx, (int)Math.Round(EdgeStripShare * across, MidpointRounding.AwayFromZero)));
        long red = 0, green = 0, blue = 0, alpha = 0, count = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!InStrip(edge, x, y, width, height, depth))
                {
                    continue;
                }

                var colour = pixels[(y * width) + x];
                red += colour.Red * colour.Alpha;
                green += colour.Green * colour.Alpha;
                blue += colour.Blue * colour.Alpha;
                alpha += colour.Alpha;
                count++;
            }
        }

        if (alpha == 0 || 2 * alpha < count * OpaqueAlpha)
        {
            return main;
        }

        return new RgbColour(Mean(red, alpha), Mean(green, alpha), Mean(blue, alpha));
    }

    private static bool InStrip(Edge edge, int x, int y, int width, int height, int depth) => edge switch
    {
        Edge.Top => y < depth,
        Edge.Bottom => y >= height - depth,
        Edge.Left => x < depth,
        _ => x >= width - depth,
    };
}

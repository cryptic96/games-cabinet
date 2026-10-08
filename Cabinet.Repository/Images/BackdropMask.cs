using SkiaSharp;

namespace Cabinet.Repository.Images;

/// <summary>Which pixels of a picture are plain backdrop, and which of those belong to a near-white backdrop.</summary>
/// <param name="Backdrop">True for every pixel that is plain backdrop or nearly transparent.</param>
/// <param name="LightBackdrop">True for the backdrop pixels whose nearest backdrop colour is near white, such as a white frame.</param>
internal sealed record BackdropMaskResult(bool[] Backdrop, bool[] LightBackdrop);

/// <summary>
/// Finds the plain backdrop around a picture's subject from the colours along its outer border. A backdrop colour must
/// hold a real share of the border; a picture whose border is a mix of many colours, such as a full-bleed illustration,
/// has no backdrop at all. Every pixel is compared with fixed kept colours, so a smooth gradient never drifts into the
/// mask one step at a time. Every near-white border pixel counts as one colour whatever its bucket, and any near-white
/// pixel matches it, so a white margin around a studio photo with an off-white backdrop is one backdrop. When all four
/// sides of the subject sit behind a straight border, the subject is the whole rectangle inside that border.
/// </summary>
internal static class BackdropMask
{
    private const int QuantiseShift = 4;
    private const int TransparentBucket = -1;
    private const int ChannelBits = 4;

    /// <summary>Returns the backdrop mask of the picture, row by row, with no backdrop when the border does not agree on one.</summary>
    public static bool[] Build(SKColor[] pixels, int width, int height) => Analyse(pixels, width, height).Backdrop;

    /// <summary>Returns the backdrop mask and the near-white part of it.</summary>
    public static BackdropMaskResult Analyse(SKColor[] pixels, int width, int height)
    {
        var backdrop = new bool[pixels.Length];
        var light = new bool[pixels.Length];
        var ring = RingIndexes(width, height);
        var kept = KeptColours(pixels, ring);

        if (kept.Count == 0 || KeptShare(kept, ring.Count) < ArtAnalysis.BackdropMinRingShare)
        {
            return new BackdropMaskResult(backdrop, light);
        }

        var queue = new Queue<int>();
        foreach (var index in ring)
        {
            if (Matches(pixels[index], kept))
            {
                backdrop[index] = true;
                queue.Enqueue(index);
            }
        }

        while (queue.Count > 0)
        {
            var index = queue.Dequeue();
            var x = index % width;
            var y = index / width;

            Visit(x - 1, y, width, height, pixels, kept, backdrop, queue);
            Visit(x + 1, y, width, height, pixels, kept, backdrop, queue);
            Visit(x, y - 1, width, height, pixels, kept, backdrop, queue);
            Visit(x, y + 1, width, height, pixels, kept, backdrop, queue);
        }

        for (var index = 0; index < pixels.Length; index++)
        {
            light[index] = backdrop[index] && IsNearestNearWhite(pixels[index], kept);
        }

        return new BackdropMaskResult(FramedSubject(backdrop, width, height), light);
    }

    private static bool[] FramedSubject(bool[] backdrop, int width, int height)
    {
        var left = new List<int>();
        var right = new List<int>();
        var top = new List<int>();
        var bottom = new List<int>();

        for (var y = 0; y < height; y++)
        {
            var first = FirstSubject(backdrop, y * width, 1, width);

            if (first >= 0)
            {
                left.Add(first);
                right.Add(FirstSubject(backdrop, (y * width) + width - 1, -1, width));
            }
        }

        for (var x = 0; x < width; x++)
        {
            var first = FirstSubject(backdrop, x, width, height);

            if (first >= 0)
            {
                top.Add(first);
                bottom.Add(FirstSubject(backdrop, ((height - 1) * width) + x, -width, height));
            }
        }

        if (!TryStraightInset(left, out var leftInset) || !TryStraightInset(right, out var rightInset)
            || !TryStraightInset(top, out var topInset) || !TryStraightInset(bottom, out var bottomInset)
            || leftInset + rightInset >= width || topInset + bottomInset >= height)
        {
            return backdrop;
        }

        var framed = new bool[backdrop.Length];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                framed[(y * width) + x] = x < leftInset || x >= width - rightInset || y < topInset || y >= height - bottomInset;
            }
        }

        return framed;
    }

    private static int FirstSubject(bool[] backdrop, int start, int step, int length)
    {
        for (var offset = 0; offset < length; offset++)
        {
            if (!backdrop[start + (offset * step)])
            {
                return offset;
            }
        }

        return -1;
    }

    private static bool TryStraightInset(List<int> insets, out int inset)
    {
        inset = 0;
        if (insets.Count == 0)
        {
            return false;
        }

        var bestCount = 0;
        foreach (var candidate in insets.Distinct().Order())
        {
            var count = insets.Count(value => Math.Abs(value - candidate) <= ArtAnalysis.FrameInsetTolerancePx);

            if (count > bestCount)
            {
                bestCount = count;
                inset = candidate;
            }
        }

        return bestCount >= ArtAnalysis.FrameStraightShare * insets.Count;
    }

    private static void Visit(int x, int y, int width, int height, SKColor[] pixels, List<KeptColour> kept, bool[] backdrop, Queue<int> queue)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }

        var index = (y * width) + x;
        if (backdrop[index] || !Matches(pixels[index], kept))
        {
            return;
        }

        backdrop[index] = true;
        queue.Enqueue(index);
    }

    private static List<int> RingIndexes(int width, int height)
    {
        var ring = new List<int>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                {
                    ring.Add((y * width) + x);
                }
            }
        }

        return ring;
    }

    private static List<KeptColour> KeptColours(SKColor[] pixels, List<int> ring)
    {
        var buckets = new SortedDictionary<int, (int Count, long Red, long Green, long Blue)>();
        var light = (Count: 0, Red: 0L, Green: 0L, Blue: 0L);
        foreach (var index in ring)
        {
            var colour = pixels[index];

            if (IsNearWhitePixel(colour))
            {
                light = (light.Count + 1, light.Red + colour.Red, light.Green + colour.Green, light.Blue + colour.Blue);

                continue;
            }

            var key = BucketOf(colour);
            buckets.TryGetValue(key, out var bucket);
            buckets[key] = (bucket.Count + 1, bucket.Red + colour.Red, bucket.Green + colour.Green, bucket.Blue + colour.Blue);
        }

        var kept = new List<KeptColour>();
        foreach (var (key, bucket) in buckets)
        {
            if (bucket.Count < ArtAnalysis.RingClusterMinShare * ring.Count)
            {
                continue;
            }

            kept.Add(new KeptColour(
                key == TransparentBucket,
                bucket.Count,
                (double)bucket.Red / bucket.Count,
                (double)bucket.Green / bucket.Count,
                (double)bucket.Blue / bucket.Count));
        }

        if (light.Count >= ArtAnalysis.RingClusterMinShare * ring.Count)
        {
            kept.Add(new KeptColour(false, light.Count, (double)light.Red / light.Count, (double)light.Green / light.Count, (double)light.Blue / light.Count));
        }

        return kept;
    }

    private static bool IsNearWhitePixel(SKColor colour)
    {
        var lowest = Math.Min(colour.Red, Math.Min(colour.Green, colour.Blue));
        var highest = Math.Max(colour.Red, Math.Max(colour.Green, colour.Blue));

        return colour.Alpha >= ArtAnalysis.TransparentAlpha && lowest >= ArtAnalysis.NearWhiteMin && highest - lowest <= ArtAnalysis.NearWhiteMaxSpread;
    }

    private static double KeptShare(List<KeptColour> kept, int ringCount) => (double)kept.Sum(colour => colour.Count) / ringCount;

    private static int BucketOf(SKColor colour) => colour.Alpha < ArtAnalysis.TransparentAlpha
        ? TransparentBucket
        : ((colour.Red >> QuantiseShift) << (2 * ChannelBits)) | ((colour.Green >> QuantiseShift) << ChannelBits) | (colour.Blue >> QuantiseShift);

    private static bool Matches(SKColor colour, List<KeptColour> kept)
    {
        if (colour.Alpha < ArtAnalysis.TransparentAlpha)
        {
            return kept.Any(candidate => candidate.Transparent);
        }

        return kept.Any(candidate => !candidate.Transparent
            && (DistanceSquared(colour, candidate) <= ArtAnalysis.BackdropTolerance * ArtAnalysis.BackdropTolerance
                || (candidate.IsNearWhite && IsNearWhitePixel(colour))));
    }

    private static bool IsNearestNearWhite(SKColor colour, List<KeptColour> kept)
    {
        if (colour.Alpha < ArtAnalysis.TransparentAlpha)
        {
            return false;
        }

        KeptColour? nearest = null;
        var nearestDistance = double.MaxValue;
        foreach (var candidate in kept.Where(candidate => !candidate.Transparent))
        {
            var distance = DistanceSquared(colour, candidate);
            if (distance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        return nearest is not null && nearest.IsNearWhite;
    }

    private static double DistanceSquared(SKColor colour, KeptColour candidate)
    {
        var red = colour.Red - candidate.Red;
        var green = colour.Green - candidate.Green;
        var blue = colour.Blue - candidate.Blue;

        return (red * red) + (green * green) + (blue * blue);
    }

    private sealed record KeptColour(bool Transparent, int Count, double Red, double Green, double Blue)
    {
        public bool IsNearWhite => Red >= ArtAnalysis.NearWhiteMin && Green >= ArtAnalysis.NearWhiteMin && Blue >= ArtAnalysis.NearWhiteMin;
    }
}

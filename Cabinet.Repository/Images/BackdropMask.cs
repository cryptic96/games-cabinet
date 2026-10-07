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
/// mask one step at a time.
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

        return new BackdropMaskResult(backdrop, light);
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
        foreach (var index in ring)
        {
            var colour = pixels[index];
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

        return kept;
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

        return kept.Any(candidate => !candidate.Transparent && DistanceSquared(colour, candidate) <= ArtAnalysis.BackdropTolerance * ArtAnalysis.BackdropTolerance);
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

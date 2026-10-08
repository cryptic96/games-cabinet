using SkiaSharp;

namespace Cabinet.Repository.Images;

/// <summary>
/// Tells whether what is left of a picture once its backdrop is taken away is one connected piece with the outline of a
/// box, or an irregular or scattered picture whose dark or plain field was never a backdrop. All work is linear in the
/// pixels of the small working copy.
/// </summary>
internal static class SubjectShape
{
    private const int NoLabel = 0;

    /// <summary>
    /// True when the subject is empty, or when its largest 8-connected piece holds at least
    /// <see cref="ArtAnalysis.SubjectMinLargestShare"/> of its pixels and covers at least
    /// <see cref="ArtAnalysis.BoxOutlineMinSolidity"/> of its own convex outline.
    /// </summary>
    public static bool IsOnePieceWithSolidOutline(SKColor[] pixels, bool[] backdrop, int width, int height)
    {
        var labels = new int[pixels.Length];
        var sizes = new List<int>();

        for (var start = 0; start < pixels.Length; start++)
        {
            if (!IsSubject(pixels, backdrop, start) || labels[start] != NoLabel)
            {
                continue;
            }

            sizes.Add(Label(pixels, backdrop, labels, start, sizes.Count + 1, width, height));
        }

        if (sizes.Count == 0)
        {
            return true;
        }

        var largest = sizes.Max();
        var largestLabel = sizes.IndexOf(largest) + 1;

        return largest >= ArtAnalysis.SubjectMinLargestShare * sizes.Sum()
            && largest >= ArtAnalysis.BoxOutlineMinSolidity * HullArea(labels, largestLabel, width, height);
    }

    private static bool IsSubject(SKColor[] pixels, bool[] backdrop, int index) =>
        !backdrop[index] && pixels[index].Alpha >= ArtAnalysis.TransparentAlpha;

    private static int Label(SKColor[] pixels, bool[] backdrop, int[] labels, int start, int label, int width, int height)
    {
        var count = 0;
        var queue = new Queue<int>();
        queue.Enqueue(start);
        labels[start] = label;

        while (queue.Count > 0)
        {
            var index = queue.Dequeue();
            count++;
            var x = index % width;
            var y = index / width;

            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var nx = x + dx;
                    var ny = y + dy;

                    if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    {
                        continue;
                    }

                    var neighbour = (ny * width) + nx;
                    if (labels[neighbour] == NoLabel && IsSubject(pixels, backdrop, neighbour))
                    {
                        labels[neighbour] = label;
                        queue.Enqueue(neighbour);
                    }
                }
            }
        }

        return count;
    }

    private static double HullArea(int[] labels, int label, int width, int height)
    {
        var points = new List<(long X, long Y)>();

        for (var y = 0; y < height; y++)
        {
            var first = -1;
            var last = -1;

            for (var x = 0; x < width; x++)
            {
                if (labels[(y * width) + x] != label)
                {
                    continue;
                }

                first = first < 0 ? x : first;
                last = x;
            }

            if (first >= 0)
            {
                points.Add((first, y));
                points.Add((first, y + 1));
                points.Add((last + 1, y));
                points.Add((last + 1, y + 1));
            }
        }

        var hull = ConvexHull(points.Distinct().OrderBy(point => point.X).ThenBy(point => point.Y).ToList());
        double twiceArea = 0;

        for (var index = 0; index < hull.Count; index++)
        {
            var current = hull[index];
            var next = hull[(index + 1) % hull.Count];
            twiceArea += (current.X * next.Y) - (next.X * current.Y);
        }

        return Math.Abs(twiceArea) / 2;
    }

    private static List<(long X, long Y)> ConvexHull(List<(long X, long Y)> sorted)
    {
        var hull = new List<(long X, long Y)>();

        foreach (var point in sorted)
        {
            while (hull.Count >= 2 && Cross(hull[^2], hull[^1], point) <= 0)
            {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(point);
        }

        var lowerCount = hull.Count + 1;
        for (var index = sorted.Count - 2; index >= 0; index--)
        {
            var point = sorted[index];

            while (hull.Count >= lowerCount && Cross(hull[^2], hull[^1], point) <= 0)
            {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(point);
        }

        hull.RemoveAt(hull.Count - 1);

        return hull;
    }

    private static long Cross((long X, long Y) origin, (long X, long Y) a, (long X, long Y) b) =>
        ((a.X - origin.X) * (b.Y - origin.Y)) - ((a.Y - origin.Y) * (b.X - origin.X));
}

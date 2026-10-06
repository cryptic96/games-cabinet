namespace Cabinet.Domain.Layout;

/// <summary>How many expansion layers of a stack are drawn and how many are summed up in the marker.</summary>
/// <param name="Visible">The number of layers drawn, counted from the floor up.</param>
/// <param name="Hidden">The number of expansions that are not drawn and are counted by the marker instead.</param>
public sealed record StackResult(int Visible, int Hidden);

/// <summary>
/// Chooses which expansions of a family are drawn as layers and which are collapsed into the marker on top. The choice
/// is a pure function of the layer heights, the cubby height and the settings, using whole numbers only.
/// </summary>
public static class StackLayout
{
    /// <summary>
    /// Returns the most layers, counted from the floor up, that fit under the cubby's ceiling together with the marker when
    /// any expansion is left out, never more than <paramref name="maxVisible"/>. A stack that shows everything needs no
    /// marker. Once the marker is needed, adding more expansions never changes how many layers are drawn, only how many
    /// are counted by the marker.
    /// </summary>
    /// <param name="layerHeightsMm">The height of every layer in stacking order, floor first.</param>
    /// <param name="cubbyHeightMm">The height of the cubby the stack stands in.</param>
    /// <param name="markerHeightMm">The height of the marker.</param>
    /// <param name="maxVisible">The most layers that may be drawn.</param>
    public static StackResult Layout(IReadOnlyList<int> layerHeightsMm, int cubbyHeightMm, int markerHeightMm, int maxVisible)
    {
        ArgumentNullException.ThrowIfNull(layerHeightsMm);
        ArgumentOutOfRangeException.ThrowIfNegative(maxVisible);

        var count = layerHeightsMm.Count;
        var limit = Math.Min(count, maxVisible);
        var used = new int[limit + 1];

        for (var index = 0; index < limit; index++)
        {
            used[index + 1] = used[index] + layerHeightsMm[index];
        }

        for (var visible = limit; visible >= 0; visible--)
        {
            var needed = used[visible] + (visible < count ? markerHeightMm : 0);

            if (needed <= cubbyHeightMm)
            {
                return new StackResult(visible, count - visible);
            }
        }

        return new StackResult(0, count);
    }
}

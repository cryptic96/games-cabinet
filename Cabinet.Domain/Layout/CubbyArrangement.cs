namespace Cabinet.Domain.Layout;

/// <summary>
/// Decides how the members of one cubby stand. The result depends only on the set of members and the cubby, never on the
/// order they arrived in, so a cubby can be re-arranged freely when it receives a game without touching any other cubby.
/// </summary>
public static class CubbyArrangement
{
    /// <summary>The most flat boxes in one stack. A starting value for review.</summary>
    public const int MaxFlatStackCount = 4;

    /// <summary>
    /// Orders the members by a stable hash of their game identifier salted with the cubby, so the order looks varied but
    /// changes only when the cubby's members change, and packs them from the left with no gap, bottom-aligned on the
    /// cubby floor. A box facing out is as wide as its front and a spine is as wide as its depth. Flat boxes lie with the
    /// spine out, as wide as the box is tall and as tall as it is deep, and gather into short stacks of up to four that
    /// start on the cubby floor; a stack sits where its first box falls in the order and is as wide as its widest box.
    /// Returns null when the members are wider than the cubby or any member or stack is taller than it; a total width or
    /// height exactly equal to the cubby's still fits.
    /// </summary>
    /// <param name="design">The section design the cubby belongs to.</param>
    /// <param name="cubby">The cubby to arrange.</param>
    /// <param name="members">The games in the cubby and how each stands.</param>
    /// <param name="options">The layout settings.</param>
    /// <param name="orderSalt">The salt that orders the boxes, which differs for every cubby of the cabinet.</param>
    public static IReadOnlyList<Placement>? TryArrange(
        SectionDesign design,
        CubbyDesign cubby,
        IReadOnlyList<LayoutMember> members,
        LayoutOptions options,
        int orderSalt)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(cubby);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(options);

        var ordered = members
            .OrderBy(member => StableHash.Hash(member.Item.BggId, orderSalt))
            .ThenBy(member => member.Item.CollectionId)
            .ThenBy(member => member.Item.BggId)
            .ToList();
        var columns = BuildFlatColumns(ordered, cubby.HeightMm);

        if (columns is null)
        {
            return null;
        }

        var placements = new List<Placement>(ordered.Count);
        var x = 0;

        for (var index = 0; index < ordered.Count; index++)
        {
            if (ordered[index].Pose == BoxPose.Flat)
            {
                if (!columns.TryGetValue(index, out var column))
                {
                    continue;
                }

                var columnWidth = column.Max(box => box.Item.Box.HeightMm);

                if (x + columnWidth > cubby.WidthMm)
                {
                    return null;
                }

                PlaceColumn(design, column, x, placements);
                x += columnWidth;

                continue;
            }

            var placed = PlaceStanding(design, ordered[index], x, cubby);

            if (placed is null)
            {
                return null;
            }

            placements.Add(placed);
            x += placed.WidthMm;
        }

        return placements;
    }

    /// <summary>
    /// Groups the flat members, in slot order, into columns: a box joins the current column while it holds fewer than
    /// <see cref="MaxFlatStackCount"/> boxes and the stack stays within the cubby height. Each column is keyed by the slot
    /// index of its first box, which is where the column sits. Returns null when one flat box alone is taller than the cubby.
    /// </summary>
    private static Dictionary<int, List<LayoutMember>>? BuildFlatColumns(List<LayoutMember> ordered, int cubbyHeightMm)
    {
        var columns = new Dictionary<int, List<LayoutMember>>();
        var currentStart = -1;
        var currentHeight = 0;

        for (var index = 0; index < ordered.Count; index++)
        {
            if (ordered[index].Pose != BoxPose.Flat)
            {
                continue;
            }

            var depth = ordered[index].Item.Box.DepthMm;

            if (depth > cubbyHeightMm)
            {
                return null;
            }

            var opensColumn = currentStart < 0
                || columns[currentStart].Count >= MaxFlatStackCount
                || currentHeight + depth > cubbyHeightMm;

            if (opensColumn)
            {
                currentStart = index;
                currentHeight = 0;
                columns[currentStart] = [];
            }

            columns[currentStart].Add(ordered[index]);
            currentHeight += depth;
        }

        return columns;
    }

    private static void PlaceColumn(SectionDesign design, List<LayoutMember> column, int x, List<Placement> placements)
    {
        var y = 0;

        foreach (var box in column)
        {
            var depth = box.Item.Box.DepthMm;

            placements.Add(Place(box.Item, PlacementKind.FlatBox, x, y, box.Item.Box.HeightMm, depth, design));
            y += depth;
        }
    }

    private static Placement? PlaceStanding(SectionDesign design, LayoutMember member, int x, CubbyDesign cubby)
    {
        var box = member.Item.Box;
        var (kind, width) = member.Pose == BoxPose.Cover
            ? (PlacementKind.Cover, box.WidthMm)
            : (PlacementKind.Spine, box.DepthMm);

        return x + width > cubby.WidthMm || box.HeightMm > cubby.HeightMm
            ? null
            : Place(member.Item, kind, x, 0, width, box.HeightMm, design);
    }

    private static string LabelFor(SectionDesign design, CabinetItem item, PlacementKind kind, int width, int height)
    {
        var pitch = Math.Max(1, design.LabelCharPitchMm);

        return kind switch
        {
            PlacementKind.Spine => SpineLabel.Shorten(item.Title, height / pitch),
            PlacementKind.FlatBox => SpineLabel.Shorten(item.Title, width / pitch),
            _ => item.Title,
        };
    }

    private static Placement Place(CabinetItem item, PlacementKind kind, int x, int y, int width, int height, SectionDesign design) =>
        new(
            GameId: item.BggId,
            Kind: kind,
            XMm: x,
            YMm: y,
            WidthMm: width,
            HeightMm: height,
            Title: item.Title,
            Label: LabelFor(design, item, kind, width, height),
            BaseTitle: null,
            ToneIndex: SpinePalette.ToneFor(item.BggId),
            PatternIndex: SpinePalette.PatternFor(item.BggId),
            FamilyId: null,
            MoreCount: null);
}

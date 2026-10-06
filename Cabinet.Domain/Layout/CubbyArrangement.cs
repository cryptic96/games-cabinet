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
    /// cubby floor. A box facing out is as wide as its front and a spine is as wide as its depth. A base game with
    /// expansions is followed immediately by a column of fixed width that holds its expansions as thin layers stacked up
    /// from the floor, with a marker on top counting the ones that did not fit; the column is as wide with one expansion
    /// as with many. Flat boxes lie with the spine out, as wide as the box is tall and as tall as it is deep, and gather
    /// into short piles of up to four that start on the cubby floor; a pile sits where its first box falls in the order
    /// and is as wide as its widest box. Inside a pile the widest box lies at the bottom and the thicker box lower among
    /// boxes of the same width, so no box overhangs the one beneath it. An expansion without an owned base game lies flat like a flat box but is never
    /// drawn lower than the design's orphan minimum. Returns null when the members are wider than the cubby or any member
    /// or stack is taller than it; a total width or height exactly equal to the cubby's still fits.
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
        var columns = BuildFlatColumns(design, ordered, cubby.HeightMm);

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

            var width = PlaceStanding(design, ordered[index], x, cubby, options, placements);

            if (width is null)
            {
                return null;
            }

            x += width.Value;
        }

        return placements;
    }

    /// <summary>
    /// Groups the flat members, in slot order, into columns: a box joins the current column while it holds fewer than
    /// <see cref="MaxFlatStackCount"/> boxes and the stack stays within the cubby height. Each column is keyed by the slot
    /// index of its first box, which is where the column sits. Which boxes share a column never depends on their size;
    /// afterwards each column is ordered from the floor up by drawn length, then drawn thickness, both largest first, with
    /// the slot order breaking the remaining ties. Returns null when one flat box alone is taller than the cubby.
    /// </summary>
    private static Dictionary<int, List<LayoutMember>>? BuildFlatColumns(
        SectionDesign design,
        List<LayoutMember> ordered,
        int cubbyHeightMm)
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

            var height = FlatHeight(design, ordered[index]);

            if (height > cubbyHeightMm)
            {
                return null;
            }

            var opensColumn = currentStart < 0
                || columns[currentStart].Count >= MaxFlatStackCount
                || currentHeight + height > cubbyHeightMm;

            if (opensColumn)
            {
                currentStart = index;
                currentHeight = 0;
                columns[currentStart] = [];
            }

            columns[currentStart].Add(ordered[index]);
            currentHeight += height;
        }

        foreach (var start in columns.Keys.ToList())
        {
            columns[start] = columns[start]
                .OrderByDescending(box => box.Item.Box.HeightMm)
                .ThenByDescending(box => FlatHeight(design, box))
                .ToList();
        }

        return columns;
    }

    private static int FlatHeight(SectionDesign design, LayoutMember member) =>
        member.IsOrphanExpansion ? Math.Max(member.Item.Box.DepthMm, design.MinOrphanHeightMm) : member.Item.Box.DepthMm;

    private static void PlaceColumn(SectionDesign design, List<LayoutMember> column, int x, List<Placement> placements)
    {
        var y = 0;

        foreach (var box in column)
        {
            var height = FlatHeight(design, box);
            var kind = box.IsOrphanExpansion ? PlacementKind.OrphanExpansion : PlacementKind.FlatBox;
            var placement = Place(box.Item, kind, x, y, box.Item.Box.HeightMm, height, design);

            placements.Add(box.IsOrphanExpansion ? placement with { BaseTitle = box.BaseTitle } : placement);
            y += height;
        }
    }

    /// <summary>
    /// Places a box that faces out or stands as a spine, followed by the column of its expansions when it has any, and
    /// returns the width used, or null when the box and its column do not fit.
    /// </summary>
    private static int? PlaceStanding(
        SectionDesign design,
        LayoutMember member,
        int x,
        CubbyDesign cubby,
        LayoutOptions options,
        List<Placement> placements)
    {
        var box = member.Item.Box;
        var (kind, width) = member.Pose == BoxPose.Cover
            ? (PlacementKind.Cover, box.WidthMm)
            : (PlacementKind.Spine, box.DepthMm);
        var columnWidth = member.Expansions.Count > 0 ? design.StackColumnWidthMm : 0;

        if (x + width + columnWidth > cubby.WidthMm || box.HeightMm > cubby.HeightMm)
        {
            return null;
        }

        var placed = Place(member.Item, kind, x, 0, width, box.HeightMm, design);

        if (member.Expansions.Count == 0)
        {
            placements.Add(member.BaseTitle is null ? placed : placed with { BaseTitle = member.BaseTitle });

            return width;
        }

        placements.Add(placed with { FamilyId = member.Item.BggId });
        PlaceStack(design, member, x + width, cubby, options, placements);

        return width + columnWidth;
    }

    /// <summary>
    /// Places the layers of a family from the floor up in the column that starts at <paramref name="columnX"/>, then the
    /// marker when some expansions do not fit. The layers touch each other and the base game's right edge.
    /// </summary>
    private static void PlaceStack(
        SectionDesign design,
        LayoutMember member,
        int columnX,
        CubbyDesign cubby,
        LayoutOptions options,
        List<Placement> placements)
    {
        var baseItem = member.Item;
        var heights = member.Expansions
            .Select(expansion => Math.Clamp(expansion.Box.DepthMm, design.MinLayerHeightMm, design.MaxLayerHeightMm))
            .ToList();
        var stack = StackLayout.Layout(heights, cubby.HeightMm, design.MarkerHeightMm, options.ExpansionStackMax);
        var pitch = Math.Max(1, design.LabelCharPitchMm);
        var y = 0;

        for (var index = 0; index < stack.Visible; index++)
        {
            var expansion = member.Expansions[index];

            placements.Add(new Placement(
                GameId: expansion.BggId,
                Kind: PlacementKind.ExpansionLayer,
                XMm: columnX,
                YMm: y,
                WidthMm: design.StackColumnWidthMm,
                HeightMm: heights[index],
                Title: expansion.Title,
                Label: SpineLabel.Shorten(expansion.Title, design.StackColumnWidthMm / pitch),
                BaseTitle: baseItem.Title,
                ToneIndex: SpinePalette.ToneFor(expansion.BggId),
                PatternIndex: SpinePalette.PatternFor(expansion.BggId),
                FamilyId: baseItem.BggId,
                MoreCount: null));
            y += heights[index];
        }

        if (stack.Hidden > 0)
        {
            placements.Add(new Placement(
                GameId: baseItem.BggId,
                Kind: PlacementKind.MoreMarker,
                XMm: columnX,
                YMm: y,
                WidthMm: design.StackColumnWidthMm,
                HeightMm: design.MarkerHeightMm,
                Title: baseItem.Title,
                Label: string.Empty,
                BaseTitle: baseItem.Title,
                ToneIndex: SpinePalette.ToneFor(baseItem.BggId),
                PatternIndex: SpinePalette.PatternFor(baseItem.BggId),
                FamilyId: baseItem.BggId,
                MoreCount: stack.Hidden));
        }
    }

    private static string LabelFor(SectionDesign design, CabinetItem item, PlacementKind kind, int width, int height)
    {
        var pitch = Math.Max(1, design.LabelCharPitchMm);

        return kind switch
        {
            PlacementKind.Spine => SpineLabel.Shorten(item.Title, height / pitch),
            PlacementKind.FlatBox or PlacementKind.OrphanExpansion => SpineLabel.Shorten(item.Title, width / pitch),
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

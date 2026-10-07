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
    /// cubby floor. A box facing out is as wide as its front and a spine is as wide as its depth. A base game is followed
    /// immediately by its thick expansions, which stand upright on the floor in collection order, and then by a column of
    /// fixed width that holds its remaining expansions as thin layers stacked up from the floor, thickest at the bottom,
    /// with a marker on top counting the ones that did not fit; the column is as wide with one expansion as with many and
    /// exists only when some expansion lies in it. Flat boxes lie with the spine out, as wide as the box is tall and as
    /// tall as it is deep, and gather into short piles of up to four that start on the cubby floor; a pile sits where its
    /// first box falls in the order and is as wide as its widest box. Inside a pile the widest box lies at the bottom and
    /// the thicker box lower among boxes of the same width, so no box overhangs the one beneath it. An expansion without
    /// an owned base game lies flat like a flat box but is never drawn lower than the design's orphan minimum. Returns
    /// null when the members are wider than the cubby or any member or stack is taller than it; a total width or height
    /// exactly equal to the cubby's still fits.
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

    private static int FlatHeight(SectionDesign design, LayoutMember member)
    {
        var height = Math.Max(member.Item.Box.DepthMm, design.MinBoxThicknessMm);

        return member.IsOrphanExpansion ? Math.Max(height, design.MinOrphanHeightMm) : height;
    }

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
    /// The width a box takes standing: its front when it faces out, its depth when it stands as a spine, and a spine is
    /// never narrower than the design's least box thickness. The engine measures with this same value when it reserves
    /// room for a family, so the room reserved is the room drawn.
    /// </summary>
    internal static int StandingWidthMm(SectionDesign design, LayoutMember member)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(member);

        return member.Pose == BoxPose.Cover
            ? member.Item.Box.WidthMm
            : Math.Max(member.Item.Box.DepthMm, design.MinBoxThicknessMm);
    }

    /// <summary>
    /// The width an upright expansion is drawn at: its depth, but never less than the design's least upright width, so
    /// its two lines of text stay readable, nor less than its least box thickness.
    /// </summary>
    internal static int UprightWidthMm(SectionDesign design, CabinetItem expansion)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(expansion);

        return Math.Max(Math.Max(expansion.Box.DepthMm, design.MinUprightExpansionWidthMm), design.MinBoxThicknessMm);
    }

    /// <summary>
    /// Places a box that faces out or stands as a spine, followed by its upright expansions and then the column of its
    /// stacked expansions when it has any, and returns the width used, or null when the box with all of them does not fit.
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
        var kind = member.Pose == BoxPose.Cover ? PlacementKind.Cover : PlacementKind.Spine;
        var width = StandingWidthMm(design, member);
        var uprightsWidth = member.Uprights.Sum(upright => UprightWidthMm(design, upright));
        var columnWidth = member.Expansions.Count > 0 ? design.StackColumnWidthMm : 0;
        var tallest = member.Uprights.Select(upright => upright.Box.HeightMm).Append(box.HeightMm).Max();

        if (x + width + uprightsWidth + columnWidth > cubby.WidthMm || tallest > cubby.HeightMm)
        {
            return null;
        }

        var placed = Place(member.Item, kind, x, 0, width, box.HeightMm, design);

        if (!member.HasFamily)
        {
            placements.Add(member.BaseTitle is null ? placed : placed with { BaseTitle = member.BaseTitle });

            return width;
        }

        placements.Add(placed with { FamilyId = member.Item.BggId });

        var next = x + width;

        foreach (var upright in member.Uprights)
        {
            var uprightWidth = UprightWidthMm(design, upright);

            placements.Add(Place(upright, PlacementKind.ExpansionSpine, next, 0, uprightWidth, upright.Box.HeightMm, design)
                with { BaseTitle = member.Item.Title, FamilyId = member.Item.BggId });
            next += uprightWidth;
        }

        if (member.Expansions.Count > 0)
        {
            PlaceStack(design, member, next, cubby, options, placements);
        }

        return width + uprightsWidth + columnWidth;
    }

    /// <summary>
    /// Places the layers of a family from the floor up in the column that starts at <paramref name="columnX"/>, then the
    /// marker when some expansions do not fit. The stack layout works on the expansions in collection order and decides how
    /// many are shown, so the shown ones are always the earliest arrivals; they are drawn thickest at the bottom, equal
    /// thicknesses in collection order. The layers touch each other and the box on the column's left, and the marker
    /// sits on the top layer.
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
        var minLayerHeight = Math.Max(design.MinLayerHeightMm, design.MinBoxThicknessMm);
        var heights = member.Expansions
            .Select(expansion => Math.Clamp(expansion.Box.DepthMm, minLayerHeight, design.MaxLayerHeightMm))
            .ToList();
        var markerHeight = Math.Max(design.MarkerHeightMm, design.MinBoxThicknessMm);
        var stack = StackLayout.Layout(heights, cubby.HeightMm, markerHeight, options.ExpansionStackMax);
        var pitch = Math.Max(1, design.LabelCharPitchMm);
        var drawOrder = Enumerable.Range(0, stack.Visible)
            .OrderByDescending(index => heights[index])
            .ThenBy(index => index)
            .ToList();
        var y = 0;

        foreach (var index in drawOrder)
        {
            var expansion = member.Expansions[index];

            placements.Add(new Placement(
                GameId: expansion.BggId,
                EntryId: expansion.CollectionId,
                Kind: PlacementKind.ExpansionLayer,
                XMm: columnX,
                YMm: y,
                WidthMm: design.StackColumnWidthMm,
                HeightMm: heights[index],
                Title: expansion.Title,
                Label: SpineLabel.Shorten(expansion.Title, design.StackColumnWidthMm / pitch),
                BaseTitle: baseItem.Title,
                IsExpansion: true,
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
                EntryId: baseItem.CollectionId,
                Kind: PlacementKind.MoreMarker,
                XMm: columnX,
                YMm: y,
                WidthMm: design.StackColumnWidthMm,
                HeightMm: markerHeight,
                Title: baseItem.Title,
                Label: string.Empty,
                BaseTitle: baseItem.Title,
                IsExpansion: null,
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
            PlacementKind.Spine or PlacementKind.ExpansionSpine => SpineLabel.Shorten(item.Title, height / pitch),
            PlacementKind.FlatBox or PlacementKind.OrphanExpansion => SpineLabel.Shorten(item.Title, width / pitch),
            _ => item.Title,
        };
    }

    private static Placement Place(CabinetItem item, PlacementKind kind, int x, int y, int width, int height, SectionDesign design) =>
        new(
            GameId: item.BggId,
            EntryId: item.CollectionId,
            Kind: kind,
            XMm: x,
            YMm: y,
            WidthMm: width,
            HeightMm: height,
            Title: item.Title,
            Label: LabelFor(design, item, kind, width, height),
            BaseTitle: null,
            IsExpansion: item.Kind == ItemKind.Expansion ? true : null,
            ToneIndex: SpinePalette.ToneFor(item.BggId),
            PatternIndex: SpinePalette.PatternFor(item.BggId),
            FamilyId: null,
            MoreCount: null);
}

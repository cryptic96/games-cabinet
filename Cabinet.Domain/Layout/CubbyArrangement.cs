namespace Cabinet.Domain.Layout;

/// <summary>
/// Decides how the members of one cubby stand. The result depends only on the set of members and the cubby, never on the
/// order they arrived in, so a cubby can be re-arranged freely when it receives a game without touching any other cubby.
/// </summary>
public static class CubbyArrangement
{
    /// <summary>
    /// Orders the members by a stable hash of their game identifier salted with the cubby, so the order looks varied but
    /// changes only when the cubby's members change, and packs them from the left with no gap, bottom-aligned on the
    /// cubby floor. A box facing out is as wide as its front and a spine is as wide as its depth. Returns null when the
    /// members are wider than the cubby or any member is taller than it; a total width or height exactly equal to the
    /// cubby's still fits.
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
        var placements = new List<Placement>(ordered.Count);
        var x = 0;

        foreach (var member in ordered)
        {
            var (kind, width, height) = member.Pose == BoxPose.Cover
                ? (PlacementKind.Cover, member.Item.Box.WidthMm, member.Item.Box.HeightMm)
                : (PlacementKind.Spine, member.Item.Box.DepthMm, member.Item.Box.HeightMm);

            if (x + width > cubby.WidthMm || height > cubby.HeightMm)
            {
                return null;
            }

            placements.Add(Place(member.Item, kind, x, 0, width, height));

            x += width;
        }

        return placements;
    }

    private static Placement Place(CabinetItem item, PlacementKind kind, int x, int y, int width, int height) =>
        new(
            GameId: item.BggId,
            Kind: kind,
            XMm: x,
            YMm: y,
            WidthMm: width,
            HeightMm: height,
            Title: item.Title,
            Label: item.Title,
            BaseTitle: null,
            ToneIndex: 0,
            PatternIndex: 0,
            FamilyId: null,
            MoreCount: null);
}

namespace Cabinet.Domain.Layout;

/// <summary>
/// Decides how the members of one cubby stand. The result depends only on the set of members, never on the order
/// they arrived in, so a cubby can be re-arranged freely when it receives a game without touching any other cubby.
/// </summary>
public static class CubbyArrangement
{
    /// <summary>
    /// Stands the members upright and packs them from the left with no gap, in collection then game identifier order,
    /// bottom-aligned on the cubby floor. Returns null when the members are wider than the cubby or any member is taller
    /// than it; a total width or height exactly equal to the cubby's still fits.
    /// </summary>
    public static IReadOnlyList<Placement>? TryArrange(CubbyDesign cubby, IReadOnlyList<CabinetItem> members)
    {
        ArgumentNullException.ThrowIfNull(cubby);
        ArgumentNullException.ThrowIfNull(members);

        var ordered = members.OrderBy(member => member.CollectionId).ThenBy(member => member.BggId).ToList();
        var placements = new List<Placement>(ordered.Count);
        var x = 0;

        foreach (var member in ordered)
        {
            var width = member.Box.DepthMm;
            var height = member.Box.HeightMm;

            if (x + width > cubby.WidthMm || height > cubby.HeightMm)
            {
                return null;
            }

            placements.Add(new Placement(
                GameId: member.BggId,
                Kind: PlacementKind.Spine,
                XMm: x,
                YMm: 0,
                WidthMm: width,
                HeightMm: height,
                Title: member.Title,
                Label: member.Title,
                BaseTitle: null,
                ToneIndex: 0,
                PatternIndex: 0,
                FamilyId: null,
                MoreCount: null));

            x += width;
        }

        return placements;
    }
}

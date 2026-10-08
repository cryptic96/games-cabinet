namespace Cabinet.Domain.Layout;

/// <summary>A game together with how it stands and the expansions that go beside it, which is what a cubby needs to arrange its contents.</summary>
/// <param name="Item">The game, at the size it is drawn at.</param>
/// <param name="Pose">How the game stands.</param>
/// <param name="Expansions">The owned expansions that lie in the stack beside this game, in collection order; empty for a game without any.</param>
/// <param name="BaseTitle">For an expansion whose base game is not owned, the title of the base game it names; otherwise absent.</param>
public sealed record LayoutMember(CabinetItem Item, BoxPose Pose, IReadOnlyList<CabinetItem> Expansions, string? BaseTitle)
{
    /// <summary>A plain game: no expansions beside it and no base game to name.</summary>
    public LayoutMember(CabinetItem item, BoxPose pose)
        : this(item, pose, [], null)
    {
    }

    /// <summary>The thick expansions that stand upright right beside this game, in collection order; empty when there are none.</summary>
    public IReadOnlyList<CabinetItem> Uprights { get; init; } = [];

    /// <summary>
    /// The game identifier of the earliest game of the series this game belongs to; a game in no series is its own anchor.
    /// It is fixed when the game is placed and orders the cubby's boxes, so a series stands together.
    /// </summary>
    public int SeriesAnchor { get; init; } = Item.BggId;

    /// <summary>
    /// Whether this game is part of a series that continues from an earlier cubby, so it stands first in its cubby.
    /// It is fixed when the game is placed.
    /// </summary>
    public bool FromPreviousCubby { get; init; }

    /// <summary>
    /// Whether this family's only stack column stands at the left edge of the next cubby on the same shelf row, because it
    /// does not fit beside the game. The family then stands last in its own cubby. It is fixed when the family is placed.
    /// </summary>
    public bool ColumnNextDoor { get; init; }

    /// <summary>
    /// Whether this family's own column shows only the layers that fit under its shelf, without a marker, and the stack
    /// continues in a second column at the left edge of the next cubby on the same shelf row. The family then stands last
    /// in its own cubby. It is fixed when the family is placed.
    /// </summary>
    public bool ContinuesNextDoor { get; init; }

    /// <summary>
    /// Whether this member is a stack column of a family whose game stands in the cubby before it, not a game of its own.
    /// The item is the family's base game and the expansions are the ones this column shows. It stands first in its cubby,
    /// at the left edge, and carries the marker for what fits nowhere.
    /// </summary>
    public bool IsColumnOnly { get; init; }

    /// <summary>Whether this family has a column in the next cubby, so it must stand last in its own cubby.</summary>
    public bool StandsLast => ColumnNextDoor || ContinuesNextDoor;

    /// <summary>The column that carries the given expansions of a family into the next cubby.</summary>
    /// <param name="family">The family whose game stands in the cubby before.</param>
    /// <param name="expansions">The expansions the column shows, in collection order.</param>
    public static LayoutMember ColumnFor(LayoutMember family, IReadOnlyList<CabinetItem> expansions)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(expansions);

        return new LayoutMember(family.Item, BoxPose.Spine, expansions, null)
        {
            IsColumnOnly = true,
            SeriesAnchor = family.SeriesAnchor,
        };
    }

    /// <summary>Whether any expansion stands upright or lies in the stack beside this game.</summary>
    public bool HasFamily => Uprights.Count > 0 || Expansions.Count > 0;

    /// <summary>Whether this member is an expansion that stands on its own because no owned base game claims it.</summary>
    public bool IsOrphanExpansion => Item.Kind == ItemKind.Expansion;
}

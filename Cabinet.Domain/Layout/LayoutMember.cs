namespace Cabinet.Domain.Layout;

/// <summary>A game together with how it stands and the expansions that go beside it, which is what a cubby needs to arrange its contents.</summary>
/// <param name="Item">The game, at the size it is drawn at.</param>
/// <param name="Pose">How the game stands.</param>
/// <param name="Expansions">The owned expansions that stand in a stack beside this game, in stacking order from the floor up; empty for a game without any.</param>
/// <param name="BaseTitle">For an expansion whose base game is not owned, the title of the base game it names; otherwise absent.</param>
public sealed record LayoutMember(CabinetItem Item, BoxPose Pose, IReadOnlyList<CabinetItem> Expansions, string? BaseTitle)
{
    /// <summary>A plain game: no expansions beside it and no base game to name.</summary>
    public LayoutMember(CabinetItem item, BoxPose pose)
        : this(item, pose, [], null)
    {
    }

    /// <summary>Whether this member is an expansion that stands on its own because no owned base game claims it.</summary>
    public bool IsOrphanExpansion => Item.Kind == ItemKind.Expansion;
}

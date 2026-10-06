namespace Cabinet.Domain.Layout;

/// <summary>A game together with how it stands and the expansions that go beside it, which is what a cubby needs to arrange its contents.</summary>
/// <param name="Item">The game, at the size it is drawn at.</param>
/// <param name="Pose">How the game stands.</param>
/// <param name="Expansions">The owned expansions that stand in a stack beside this game, in stacking order from the floor up; empty for a game without any.</param>
public sealed record LayoutMember(CabinetItem Item, BoxPose Pose, IReadOnlyList<CabinetItem> Expansions)
{
    /// <summary>A plain game: no expansions beside it.</summary>
    public LayoutMember(CabinetItem item, BoxPose pose)
        : this(item, pose, [])
    {
    }
}

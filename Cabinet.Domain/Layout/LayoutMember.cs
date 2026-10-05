namespace Cabinet.Domain.Layout;

/// <summary>A game together with how it stands, which is what a cubby needs to arrange its contents.</summary>
/// <param name="Item">The game.</param>
/// <param name="Pose">How the game stands.</param>
public sealed record LayoutMember(CabinetItem Item, BoxPose Pose);

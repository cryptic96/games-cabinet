namespace Cabinet.Domain.Layout;

/// <summary>Whether an owned item is a standalone game or an expansion for another game.</summary>
public enum ItemKind
{
    /// <summary>A standalone game.</summary>
    Base,

    /// <summary>An expansion that extends one or more base games.</summary>
    Expansion,
}

/// <summary>
/// Physical size of a box in whole millimetres. The convention is fixed for every item:
/// the box is described the way it stands upright on a shelf.
/// </summary>
/// <param name="WidthMm">The front edge that is not vertical when the box stands upright; the cover is this wide.</param>
/// <param name="HeightMm">The standing height, which is the length of the spine.</param>
/// <param name="DepthMm">The spine thickness; an upright spine is this wide and a flat box is this tall.</param>
public sealed record BoxDimensions(int WidthMm, int HeightMm, int DepthMm);

/// <summary>A reference to the base game an expansion extends, carrying the title so an orphan can still be labelled.</summary>
/// <param name="BggId">The identifier of the base game.</param>
/// <param name="Title">The title of the base game.</param>
public sealed record BaseGameRef(int BggId, string Title);

/// <summary>One owned item as the layout engine sees it. Nothing here depends on where the collection came from.</summary>
/// <param name="BggId">The game identifier.</param>
/// <param name="CollectionId">The identifier of the collection entry; with the game identifier it orders the items.</param>
/// <param name="Title">The display title; it may be blank.</param>
/// <param name="Kind">Whether this is a base game or an expansion.</param>
/// <param name="Box">The box size in millimetres.</param>
/// <param name="ExpansionOf">The base games this item expands; empty for a base game.</param>
public sealed record CabinetItem(
    int BggId,
    long CollectionId,
    string Title,
    ItemKind Kind,
    BoxDimensions Box,
    IReadOnlyList<BaseGameRef> ExpansionOf);

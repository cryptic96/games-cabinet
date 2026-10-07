namespace Cabinet.Domain.Layout;

/// <summary>How a placed item is drawn.</summary>
public enum PlacementKind
{
    /// <summary>A box facing out so the whole front shows.</summary>
    Cover,

    /// <summary>A box standing upright with only its spine showing.</summary>
    Spine,

    /// <summary>A box lying flat with its spine facing out.</summary>
    FlatBox,

    /// <summary>One expansion lying in the stack beside its base game.</summary>
    ExpansionLayer,

    /// <summary>An expansion standing upright beside its base game, with a second line naming that game.</summary>
    ExpansionSpine,

    /// <summary>A marker on top of a stack counting the expansions that did not fit.</summary>
    MoreMarker,

    /// <summary>An expansion whose base game is not owned, drawn as a flat box labelled with that base game.</summary>
    OrphanExpansion,
}

/// <summary>How a picture sits inside the box front it is drawn in; the picture is always shown whole.</summary>
public enum ArtFit
{
    /// <summary>The picture fills the width and the box shows bars above and below it.</summary>
    Width,

    /// <summary>The picture fills the height and the box shows bars on both sides of it.</summary>
    Height,

    /// <summary>The picture and the box front have the same shape to within a hair, so no bars show.</summary>
    Exact,
}

/// <summary>The picture a face-out cover shows.</summary>
/// <param name="Url">The path on the site's own origin the file is served from.</param>
/// <param name="Width">The width of the served file in pixels.</param>
/// <param name="Height">The height of the served file in pixels.</param>
/// <param name="Fit">How the picture sits inside the box front.</param>
/// <param name="Edges">The colours along the picture's edges, or null when they are not known.</param>
public sealed record PlacementArt(string Url, int Width, int Height, ArtFit Fit, ArtEdges? Edges = null);

/// <summary>One drawn item inside a cubby. Coordinates are millimetres from the cubby's left edge and floor.</summary>
/// <param name="GameId">The game identifier, kept on every placement so later features can attach to it.</param>
/// <param name="EntryId">The collection entry the drawn box belongs to; two copies of one game have different entries.</param>
/// <param name="Kind">How the item is drawn.</param>
/// <param name="XMm">Distance from the cubby's left edge to the left edge of the item.</param>
/// <param name="YMm">Distance from the cubby floor to the bottom edge of the item.</param>
/// <param name="WidthMm">Drawn width.</param>
/// <param name="HeightMm">Drawn height.</param>
/// <param name="Title">The full title, used for the accessible name.</param>
/// <param name="Label">The text drawn on the item.</param>
/// <param name="BaseTitle">The base game title for expansions; otherwise absent.</param>
/// <param name="IsExpansion">True when the drawn box is an expansion, even when no base game is known; otherwise absent.</param>
/// <param name="ToneIndex">Index into the colour table; stable per game.</param>
/// <param name="PatternIndex">Index of the cover pattern; stable per game.</param>
/// <param name="FamilyId">The base game identifier when the item belongs to a family; otherwise absent.</param>
/// <param name="MoreCount">The hidden expansion count on a marker; otherwise absent.</param>
/// <param name="Art">The picture of a face-out cover that has one; absent for every other placement.</param>
/// <param name="ShowBaseLine">
/// On an upright expansion or an expansion without an owned base game: whether the drawn size has room for the second
/// line naming the base game. Decided from millimetres, so it never depends on the visitor's screen; absent for every
/// other placement.
/// </param>
/// <param name="Colour">The background and text colours taken from the game's picture; absent when the game has none and for the marker.</param>
public sealed record Placement(
    int GameId,
    long EntryId,
    PlacementKind Kind,
    int XMm,
    int YMm,
    int WidthMm,
    int HeightMm,
    string Title,
    string Label,
    string? BaseTitle,
    bool? IsExpansion,
    int ToneIndex,
    int PatternIndex,
    int? FamilyId,
    int? MoreCount,
    PlacementArt? Art = null,
    bool? ShowBaseLine = null,
    PaletteTone? Colour = null);

/// <summary>A compartment of a section. Position is measured from the top-left corner of the section interior.</summary>
/// <param name="Index">The cubby's reading-order index within its section, counted from zero.</param>
/// <param name="XMm">Distance from the interior's left edge to the cubby's left edge.</param>
/// <param name="YMm">Distance from the interior's top edge to the cubby's top edge.</param>
/// <param name="WidthMm">Cubby width.</param>
/// <param name="HeightMm">Cubby height.</param>
/// <param name="Placements">The items standing in the cubby, left to right; empty for a bare cubby.</param>
public sealed record LayoutCubby(
    int Index,
    int XMm,
    int YMm,
    int WidthMm,
    int HeightMm,
    IReadOnlyList<Placement> Placements);

/// <summary>One piece of cabinet furniture. Sizes are the interior; the outer size adds one frame on each side.</summary>
/// <param name="Index">The section's position in the cabinet, counted from zero.</param>
/// <param name="WidthMm">Interior width.</param>
/// <param name="HeightMm">Interior height.</param>
/// <param name="FrameMm">Thickness of the frame, shelves and dividers.</param>
/// <param name="Cubbies">Every cubby of the section in reading order, empty or not.</param>
public sealed record LayoutSection(
    int Index,
    int WidthMm,
    int HeightMm,
    int FrameMm,
    IReadOnlyList<LayoutCubby> Cubbies);

/// <summary>One entry of the colour table: the box background and the text colour that reads on it.</summary>
/// <param name="Background">The background as a lowercase hexadecimal colour.</param>
/// <param name="Text">The text colour as a lowercase hexadecimal colour.</param>
public sealed record PaletteTone(string Background, string Text);

/// <summary>The whole cabinet for one collection and one section design.</summary>
/// <param name="LayoutVersion">Bumped whenever the algorithm or a design changes on purpose.</param>
/// <param name="Profile">The name of the section design the layout was built for.</param>
/// <param name="OptionsFingerprint">Identifies the layout settings the cabinet was built with, so a change of settings is visible.</param>
/// <param name="Palette">The colour table that every placement's tone index points into.</param>
/// <param name="Sections">The sections in order; there is always at least one.</param>
public sealed record CabinetLayout(
    int LayoutVersion,
    string Profile,
    string OptionsFingerprint,
    IReadOnlyList<PaletteTone> Palette,
    IReadOnlyList<LayoutSection> Sections);

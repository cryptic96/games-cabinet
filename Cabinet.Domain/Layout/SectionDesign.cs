namespace Cabinet.Domain.Layout;

/// <summary>One horizontal band of a section: a height and the widths of the cubbies side by side in it.</summary>
/// <param name="HeightMm">Height of every cubby in the row.</param>
/// <param name="CubbyWidthsMm">Cubby widths from left to right.</param>
public sealed record ShelfRow(int HeightMm, IReadOnlyList<int> CubbyWidthsMm);

/// <summary>A cubby rectangle of a design. Position is measured from the top-left corner of the section interior.</summary>
/// <param name="Index">Reading-order index across the whole section, counted from zero.</param>
/// <param name="XMm">Distance from the interior's left edge.</param>
/// <param name="YMm">Distance from the interior's top edge.</param>
/// <param name="WidthMm">Cubby width.</param>
/// <param name="HeightMm">Cubby height.</param>
public sealed record CubbyDesign(int Index, int XMm, int YMm, int WidthMm, int HeightMm);

/// <summary>
/// The largest box a design can hold, derived from its biggest cubby. Items beyond these limits are scaled down on entry
/// so every item always fits an empty section.
/// </summary>
/// <param name="MaxWidthMm">The widest front a box may have.</param>
/// <param name="MaxHeightMm">The tallest front a box may have.</param>
/// <param name="MaxFamilyBaseWidthMm">The widest front a base game may have when expansions stand beside it in a stack column.</param>
/// <param name="MaxDepthMm">The deepest a box may be.</param>
public sealed record BoxLimits(int MaxWidthMm, int MaxHeightMm, int MaxFamilyBaseWidthMm, int MaxDepthMm);

/// <summary>
/// The fixed furniture: a hand-designed section of irregular cubbies. Frame, shelf and divider thickness are all
/// <see cref="FrameMm"/>, so a gap of that size separates neighbouring cubbies and rows.
/// </summary>
/// <param name="Name">The design's name, used as the layout profile.</param>
/// <param name="InteriorWidthMm">Interior width, which every row fills exactly.</param>
/// <param name="FrameMm">Thickness of the frame, shelves and dividers.</param>
/// <param name="Rows">The shelf rows from top to bottom.</param>
public sealed record SectionDesign(string Name, int InteriorWidthMm, int FrameMm, IReadOnlyList<ShelfRow> Rows)
{
    /// <summary>
    /// The tallest box that may stand upright, in millimetres; the oversize-only strategy faces out every box taller than
    /// this. The default suits a design whose tallest cubby is about as high as the usual large box.
    /// </summary>
    public int MaxSpineHeightMm { get; init; } = 330;

    /// <summary>
    /// Millimetres of box length that one label character takes at the smallest width the box is drawn at. Spine and flat
    /// box labels are shortened to the box length divided by this, and the page's ellipsis stays as the backstop when the
    /// estimate is generous.
    /// </summary>
    public int LabelCharPitchMm { get; init; } = 14;

    /// <summary>The deepest a box may be, in millimetres, whatever the cubbies; deeper boxes are drawn at this depth.</summary>
    public const int MaxBoxDepthMm = 150;

    /// <summary>
    /// Width of the column beside a base game that holds its expansions. It never depends on how many expansions there
    /// are, so a growing stack cannot push the neighbours in its cubby.
    /// </summary>
    public int StackColumnWidthMm { get; init; } = 190;

    /// <summary>The least height one expansion layer is drawn at, in millimetres.</summary>
    public int MinLayerHeightMm { get; init; } = 40;

    /// <summary>The most height one expansion layer is drawn at, in millimetres.</summary>
    public int MaxLayerHeightMm { get; init; } = 70;

    /// <summary>Height of the marker that counts the expansions that did not fit the stack, in millimetres.</summary>
    public int MarkerHeightMm { get; init; } = 40;

    /// <summary>
    /// The least height an expansion box without an owned base game is drawn at, in millimetres, so its two label lines
    /// stay readable at the smallest width the section is shown at.
    /// </summary>
    public int MinOrphanHeightMm { get; init; } = 80;

    /// <summary>
    /// The least width an upright expansion is drawn at, in millimetres, so its title and the line naming its base game
    /// fit side by side at the smallest width the section is shown at.
    /// </summary>
    public int MinUprightExpansionWidthMm { get; init; } = 64;

    /// <summary>Interior height: the row heights plus one frame between each pair of rows.</summary>
    public int InteriorHeightMm => Rows.Sum(row => row.HeightMm) + (FrameMm * Math.Max(0, Rows.Count - 1));

    /// <summary>
    /// The largest box the design can hold, taken from its anchor cubby: the tallest cubby, and among equally tall cubbies
    /// the widest. A base game with a stack column beside it must leave room for the column.
    /// </summary>
    public BoxLimits Limits
    {
        get
        {
            var anchor = Cubbies
                .OrderByDescending(cubby => cubby.HeightMm)
                .ThenByDescending(cubby => cubby.WidthMm)
                .FirstOrDefault();

            return anchor is null
                ? new BoxLimits(0, 0, 0, MaxBoxDepthMm)
                : new BoxLimits(anchor.WidthMm, anchor.HeightMm, Math.Max(0, anchor.WidthMm - StackColumnWidthMm), MaxBoxDepthMm);
        }
    }

    /// <summary>Every cubby in reading order: rows top to bottom, cubbies left to right.</summary>
    public IReadOnlyList<CubbyDesign> Cubbies => BuildCubbies();

    private List<CubbyDesign> BuildCubbies()
    {
        var cubbies = new List<CubbyDesign>();
        var y = 0;

        foreach (var row in Rows)
        {
            var x = 0;

            foreach (var width in row.CubbyWidthsMm)
            {
                cubbies.Add(new CubbyDesign(cubbies.Count, x, y, width, row.HeightMm));
                x += width + FrameMm;
            }

            y += row.HeightMm + FrameMm;
        }

        return cubbies;
    }
}

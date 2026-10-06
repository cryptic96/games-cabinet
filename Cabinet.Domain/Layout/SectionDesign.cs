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

    /// <summary>
    /// The space, in millimetres, that the stylesheet reserves beside the frame on each side of a section for the side
    /// boards of the furniture. It must stay equal to the stylesheet's side allowance: the section is drawn across the
    /// outer width plus this space on both sides, which is what the readability floors are measured against.
    /// </summary>
    public int FurnitureSideMm { get; init; } = DefaultFurnitureSideMm;

    /// <summary>The side allowance the stylesheet reserves beside the frame, in millimetres.</summary>
    public const int DefaultFurnitureSideMm = 32;

    /// <summary>
    /// The narrowest width, in screen pixels, that the whole section is ever drawn at. The readability floors of the
    /// design are derived from it, so it changes together with the page's gutters and grid.
    /// </summary>
    public int SmallestRenderedWidthPx { get; init; } = 304;

    /// <summary>
    /// The least width of a spine, the least height of a flat box and of an expansion layer, and the least height of the
    /// marker, in millimetres, so every box that can be tapped stays large enough to tap at the narrowest drawn width.
    /// </summary>
    public int MinBoxThicknessMm { get; init; } = 1;

    /// <summary>The width of the whole section including both frames, in millimetres.</summary>
    public int OuterWidthMm => InteriorWidthMm + (2 * FrameMm);

    /// <summary>The width the section is drawn across: the outer width plus the furniture's side space on both sides.</summary>
    public int RenderedWidthMm => OuterWidthMm + (2 * FurnitureSideMm);

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

    /// <summary>
    /// Checks the design and throws one exception that lists every problem found. A design that passes can hold every
    /// box the engine ever draws: a row's cubbies fill the interior exactly, a box lying flat at its tallest fits the
    /// anchor cubby, the deepest spine and the widest base game fit it together with the stack column, and the floors
    /// and layer sizes are consistent, so no game can fail to find a place in an empty section.
    /// </summary>
    /// <exception cref="InvalidOperationException">The design has one or more problems.</exception>
    public void Validate()
    {
        var problems = new List<string>();

        CheckSizes(problems);
        CheckRows(problems);

        if (problems.Count == 0)
        {
            CheckBoxFit(problems);
            CheckFloors(problems);
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException($"The '{Name}' section design is not valid: {string.Join("; ", problems)}.");
        }
    }

    private void CheckSizes(List<string> problems)
    {
        if (Rows.Count == 0)
        {
            problems.Add("it has no rows");
        }

        var sizes = new (string Name, int Value)[]
        {
            ("interior width", InteriorWidthMm),
            ("frame", FrameMm),
            ("stack column width", StackColumnWidthMm),
            ("minimum layer height", MinLayerHeightMm),
            ("maximum layer height", MaxLayerHeightMm),
            ("marker height", MarkerHeightMm),
            ("minimum orphan height", MinOrphanHeightMm),
            ("minimum upright width", MinUprightExpansionWidthMm),
            ("minimum box thickness", MinBoxThicknessMm),
            ("label character pitch", LabelCharPitchMm),
            ("smallest rendered width", SmallestRenderedWidthPx),
        };

        problems.AddRange(sizes.Where(size => size.Value <= 0).Select(size => $"the {size.Name} must be positive"));

        if (FurnitureSideMm < 0)
        {
            problems.Add("the furniture side space must not be negative");
        }
    }

    private void CheckRows(List<string> problems)
    {
        for (var index = 0; index < Rows.Count; index++)
        {
            var row = Rows[index];
            var position = index + 1;

            if (row.HeightMm <= 0 || row.CubbyWidthsMm.Count == 0 || row.CubbyWidthsMm.Any(width => width <= 0))
            {
                problems.Add($"row {position} needs a positive height and at least one cubby of positive width");

                continue;
            }

            var used = row.CubbyWidthsMm.Sum() + (FrameMm * (row.CubbyWidthsMm.Count - 1));

            if (used != InteriorWidthMm)
            {
                problems.Add($"row {position} is {used} mm wide with its gaps but the interior is {InteriorWidthMm} mm");
            }
        }
    }

    private void CheckBoxFit(List<string> problems)
    {
        var limits = Limits;

        if (limits.MaxWidthMm < limits.MaxHeightMm)
        {
            problems.Add($"the anchor cubby is {limits.MaxWidthMm} mm wide, narrower than its {limits.MaxHeightMm} mm height, so a tall box lying flat does not fit");
        }

        if (limits.MaxFamilyBaseWidthMm < MinBoxThicknessMm)
        {
            problems.Add($"a base game with a stack column of {StackColumnWidthMm} mm has only {limits.MaxFamilyBaseWidthMm} mm left in the anchor cubby");
        }

        var deepestSpine = Math.Max(limits.MaxDepthMm, MinBoxThicknessMm);

        if (deepestSpine + StackColumnWidthMm > limits.MaxWidthMm)
        {
            problems.Add($"the deepest spine of {deepestSpine} mm with the stack column does not fit the {limits.MaxWidthMm} mm anchor cubby");
        }

        var thickestFlat = Math.Max(Math.Max(limits.MaxDepthMm, MinOrphanHeightMm), MinBoxThicknessMm);

        if (thickestFlat > limits.MaxHeightMm)
        {
            problems.Add($"the thickest flat box of {thickestFlat} mm is taller than the {limits.MaxHeightMm} mm anchor cubby");
        }
    }

    private void CheckFloors(List<string> problems)
    {
        if (MinBoxThicknessMm > MaxLayerHeightMm)
        {
            problems.Add($"the minimum box thickness of {MinBoxThicknessMm} mm is above the maximum layer height of {MaxLayerHeightMm} mm");
        }

        if (MinLayerHeightMm > MaxLayerHeightMm)
        {
            problems.Add($"the minimum layer height of {MinLayerHeightMm} mm is above the maximum layer height of {MaxLayerHeightMm} mm");
        }

        var shortest = Cubbies.Min(cubby => cubby.HeightMm);
        var tallestFloor = Math.Max(Math.Max(MinBoxThicknessMm, MarkerHeightMm), MinOrphanHeightMm);

        if (tallestFloor > shortest)
        {
            problems.Add($"a floor or marker of {tallestFloor} mm is taller than the shortest cubby of {shortest} mm");
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

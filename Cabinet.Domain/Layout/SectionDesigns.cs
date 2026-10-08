using System.Diagnostics.CodeAnalysis;

namespace Cabinet.Domain.Layout;

/// <summary>
/// The editable section designs, kept apart from the engine so tuning the furniture only touches this file.
/// The interior height of a design is derived from its rows, never typed.
/// </summary>
public static class SectionDesigns
{
    /// <summary>The name of the wide design used on desktop screens.</summary>
    public const string DesktopName = "desktop";

    /// <summary>The name of the narrow design used on phone screens.</summary>
    public const string PhoneName = "phone";

    private const int DesktopInteriorWidthMm = 1200;
    private const int PhoneInteriorWidthMm = 640;
    private const int FrameMm = 20;
    private const int DesktopSmallestRenderedWidthPx = 592;
    private const int PhoneSmallestRenderedWidthPx = 304;
    private const int DesktopMinBoxThicknessMm = 34;

    /// <summary>
    /// The wide design: five shelf rows of irregular cubbies across 1200 mm, 1850 mm tall inside. Every row is 310 mm or
    /// taller, so a standard box stands with air above it in every cubby; the cubbies are 170 to 550 mm wide with wide ones
    /// in every row, so the largest face-out boxes, base games that face out beside several expansions and landscape fronts
    /// have cubbies to go to wherever the shelves are. The largest box a section holds is therefore 430 by 430 mm. The big
    /// boxes no longer pile up in a few cubbies and open a near-empty section at the end, and no section but the last
    /// keeps a bare shelf row. The numbers came from a seeded search over valid designs, run outside the repository on
    /// the samples, the fake collections and seeded collections with a realistic mix of box sizes, with every layout
    /// rule on and at two cover shares, then confirmed on collections the search never saw.
    /// </summary>
    public static readonly SectionDesign Desktop = new(
        DesktopName,
        InteriorWidthMm: DesktopInteriorWidthMm,
        FrameMm: FrameMm,
        Rows:
        [
            new ShelfRow(310, [190, 240, 290, 230, 170]),
            new ShelfRow(430, [430, 390, 340]),
            new ShelfRow(390, [280, 550, 330]),
            new ShelfRow(330, [480, 380, 300]),
            new ShelfRow(310, [310, 350, 500]),
        ])
    {
        MaxSpineHeightMm = 330,
        LabelCharPitchMm = 14,
        StackColumnWidthMm = 190,
        MinLayerHeightMm = ReadabilityFloor.Millimetres(ReadabilityFloor.OneLineLabelPx, DesktopSmallestRenderedWidthPx, RenderedWidthMm(DesktopInteriorWidthMm)),
        MaxLayerHeightMm = 70,
        MarkerHeightMm = 40,
        SmallestRenderedWidthPx = DesktopSmallestRenderedWidthPx,
        MinBoxThicknessMm = DesktopMinBoxThicknessMm,
        MinOrphanHeightMm = ReadabilityFloor.Millimetres(ReadabilityFloor.OneLineLabelPx, DesktopSmallestRenderedWidthPx, RenderedWidthMm(DesktopInteriorWidthMm)),
        MinUprightExpansionWidthMm = ReadabilityFloor.Millimetres(ReadabilityFloor.OneLineLabelPx, DesktopSmallestRenderedWidthPx, RenderedWidthMm(DesktopInteriorWidthMm)),
        TwoLineOrphanHeightMm = ReadabilityFloor.Millimetres(ReadabilityFloor.TwoLineLabelPx, DesktopSmallestRenderedWidthPx, RenderedWidthMm(DesktopInteriorWidthMm)),
        TwoLineUprightWidthMm = ReadabilityFloor.Millimetres(ReadabilityFloor.TwoLineSpinePx, DesktopSmallestRenderedWidthPx, RenderedWidthMm(DesktopInteriorWidthMm)),
    };

    /// <summary>
    /// The narrow design: seven shelf rows of irregular cubbies across 640 mm, so a phone shows fewer cubbies side by
    /// side and the sections stack. The rows are tuned to real box heights: most are tall enough for a standard box to
    /// stand with air above it, only one is short, and the wide cubbies sit where a face-out box needs them, so earlier
    /// sections stay full instead of leaving whole rows bare. Its floors are derived from the narrowest phone, a 320
    /// pixel screen with a gutter of 8 pixels on each side.
    /// </summary>
    public static readonly SectionDesign Phone = new(
        PhoneName,
        InteriorWidthMm: PhoneInteriorWidthMm,
        FrameMm: FrameMm,
        Rows:
        [
            new ShelfRow(300, [270, 350]),
            new ShelfRow(280, [260, 180, 160]),
            new ShelfRow(380, [290, 330]),
            new ShelfRow(340, [290, 330]),
            new ShelfRow(340, [440, 180]),
            new ShelfRow(420, [170, 450]),
            new ShelfRow(380, [420, 200]),
        ])
    {
        MaxSpineHeightMm = 340,
        LabelCharPitchMm = 18,
        StackColumnWidthMm = 190,
        MinLayerHeightMm = ReadabilityFloor.Millimetres(ReadabilityFloor.OneLineLabelPx, PhoneSmallestRenderedWidthPx, RenderedWidthMm(PhoneInteriorWidthMm)),
        MaxLayerHeightMm = 70,
        MarkerHeightMm = 40,
        SmallestRenderedWidthPx = PhoneSmallestRenderedWidthPx,
        MinBoxThicknessMm = ReadabilityFloor.Millimetres(ReadabilityFloor.TapTargetPx, PhoneSmallestRenderedWidthPx, RenderedWidthMm(PhoneInteriorWidthMm)),
        MinOrphanHeightMm = ReadabilityFloor.Millimetres(ReadabilityFloor.OneLineLabelPx, PhoneSmallestRenderedWidthPx, RenderedWidthMm(PhoneInteriorWidthMm)),
        MinUprightExpansionWidthMm = ReadabilityFloor.Millimetres(ReadabilityFloor.OneLineLabelPx, PhoneSmallestRenderedWidthPx, RenderedWidthMm(PhoneInteriorWidthMm)),
        TwoLineOrphanHeightMm = ReadabilityFloor.Millimetres(ReadabilityFloor.TwoLineLabelPx, PhoneSmallestRenderedWidthPx, RenderedWidthMm(PhoneInteriorWidthMm)),
        TwoLineUprightWidthMm = ReadabilityFloor.Millimetres(ReadabilityFloor.TwoLineSpinePx, PhoneSmallestRenderedWidthPx, RenderedWidthMm(PhoneInteriorWidthMm)),
    };

    /// <summary>Every design the engine can build for.</summary>
    public static IReadOnlyList<SectionDesign> All { get; } = [Desktop, Phone];

    /// <summary>Finds a design by exact, case-sensitive name; anything else, including null, finds nothing.</summary>
    public static bool TryGet(string? name, [NotNullWhen(true)] out SectionDesign? design)
    {
        design = All.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));

        return design is not null;
    }

    private static int RenderedWidthMm(int interiorWidthMm) =>
        interiorWidthMm + (2 * FrameMm) + (2 * SectionDesign.DefaultFurnitureSideMm);
}

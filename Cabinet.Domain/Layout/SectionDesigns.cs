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

    /// <summary>The wide design: five shelf rows of irregular cubbies across 1200 mm.</summary>
    public static readonly SectionDesign Desktop = new(
        DesktopName,
        InteriorWidthMm: DesktopInteriorWidthMm,
        FrameMm: FrameMm,
        Rows:
        [
            new ShelfRow(360, [380, 220, 560]),
            new ShelfRow(300, [260, 340, 200, 340]),
            new ShelfRow(400, [460, 300, 400]),
            new ShelfRow(260, [300, 220, 300, 320]),
            new ShelfRow(330, [420, 340, 400]),
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
    /// The narrow design: six shelf rows of irregular cubbies across 640 mm, so a phone shows fewer cubbies side by
    /// side and the sections stack. Its floors are derived from the narrowest phone, a 320 pixel screen with a gutter of
    /// 8 pixels on each side.
    /// </summary>
    public static readonly SectionDesign Phone = new(
        PhoneName,
        InteriorWidthMm: PhoneInteriorWidthMm,
        FrameMm: FrameMm,
        Rows:
        [
            new ShelfRow(360, [300, 320]),
            new ShelfRow(300, [200, 200, 200]),
            new ShelfRow(400, [420, 200]),
            new ShelfRow(260, [150, 230, 220]),
            new ShelfRow(340, [310, 310]),
            new ShelfRow(300, [200, 420]),
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

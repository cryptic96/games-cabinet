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

    /// <summary>The wide design: five shelf rows of irregular cubbies across 1200 mm.</summary>
    public static readonly SectionDesign Desktop = new(
        DesktopName,
        InteriorWidthMm: 1200,
        FrameMm: 20,
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
        MinLayerHeightMm = 40,
        MaxLayerHeightMm = 70,
        MarkerHeightMm = 40,
        MinOrphanHeightMm = 80,
        MinUprightExpansionWidthMm = 64,
    };

    /// <summary>Every design the engine can build for.</summary>
    public static IReadOnlyList<SectionDesign> All { get; } = [Desktop];

    /// <summary>Finds a design by exact, case-sensitive name; anything else, including null, finds nothing.</summary>
    public static bool TryGet(string? name, [NotNullWhen(true)] out SectionDesign? design)
    {
        design = All.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));

        return design is not null;
    }
}

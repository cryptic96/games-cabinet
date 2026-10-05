namespace Cabinet.Domain.Layout;

/// <summary>
/// Builds the cabinet for a collection. The result is a pure function of the items and the section design:
/// nothing is remembered between calls, so the same collection always gives the same cabinet. Games are taken in
/// collection then game identifier order and each goes into the first cubby, in reading order section by section,
/// that can still take it; a new section opens only when no existing cubby can.
/// </summary>
public static class CabinetLayoutEngine
{
    /// <summary>Bumped whenever the algorithm or a design changes on purpose, so a rearrangement is always a conscious change.</summary>
    public const int LayoutVersion = 1;

    /// <summary>
    /// Lays the items out in the design's sections. Every cubby of every section is emitted, empty or not, and there is
    /// always at least one section.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">A collection and game identifier pair appears twice.</exception>
    /// <exception cref="InvalidOperationException">An item cannot fit even an empty section.</exception>
    public static CabinetLayout Build(IReadOnlyList<CabinetItem> items, SectionDesign design)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(design);

        var ordered = items.OrderBy(item => item.CollectionId).ThenBy(item => item.BggId).ToList();
        RejectDuplicates(ordered);

        var cubbyDesigns = design.Cubbies;
        var sections = new List<List<List<CabinetItem>>> { NewSection(cubbyDesigns.Count) };

        foreach (var item in ordered)
        {
            if (!TryPlaceInExistingSection(sections, cubbyDesigns, item))
            {
                sections.Add(NewSection(cubbyDesigns.Count));

                if (!TryPlaceInSection(sections[^1], cubbyDesigns, item))
                {
                    throw new InvalidOperationException(
                        $"Game {item.BggId} does not fit an empty section of the '{design.Name}' design.");
                }
            }
        }

        var layoutSections = sections
            .Select((section, sectionIndex) => ToLayoutSection(sectionIndex, section, design, cubbyDesigns))
            .ToList();

        return new CabinetLayout(LayoutVersion, design.Name, layoutSections);
    }

    private static void RejectDuplicates(List<CabinetItem> ordered)
    {
        for (var index = 1; index < ordered.Count; index++)
        {
            var previous = ordered[index - 1];
            var current = ordered[index];

            if (previous.CollectionId == current.CollectionId && previous.BggId == current.BggId)
            {
                throw new ArgumentException(
                    $"Collection entry {current.CollectionId} for game {current.BggId} appears more than once.");
            }
        }
    }

    private static List<List<CabinetItem>> NewSection(int cubbyCount) =>
        Enumerable.Range(0, cubbyCount).Select(_ => new List<CabinetItem>()).ToList();

    private static bool TryPlaceInExistingSection(
        List<List<List<CabinetItem>>> sections,
        IReadOnlyList<CubbyDesign> cubbyDesigns,
        CabinetItem item) =>
        sections.Any(section => TryPlaceInSection(section, cubbyDesigns, item));

    private static bool TryPlaceInSection(
        List<List<CabinetItem>> section,
        IReadOnlyList<CubbyDesign> cubbyDesigns,
        CabinetItem item)
    {
        for (var cubbyIndex = 0; cubbyIndex < section.Count; cubbyIndex++)
        {
            var candidate = new List<CabinetItem>(section[cubbyIndex]) { item };

            if (CubbyArrangement.TryArrange(cubbyDesigns[cubbyIndex], candidate) is not null)
            {
                section[cubbyIndex].Add(item);

                return true;
            }
        }

        return false;
    }

    private static LayoutSection ToLayoutSection(
        int sectionIndex,
        List<List<CabinetItem>> section,
        SectionDesign design,
        IReadOnlyList<CubbyDesign> cubbyDesigns)
    {
        var cubbies = section
            .Select((members, cubbyIndex) =>
            {
                var cubby = cubbyDesigns[cubbyIndex];
                var placements = CubbyArrangement.TryArrange(cubby, members) ?? [];

                return new LayoutCubby(cubby.Index, cubby.XMm, cubby.YMm, cubby.WidthMm, cubby.HeightMm, placements);
            })
            .ToList();

        return new LayoutSection(sectionIndex, design.InteriorWidthMm, design.InteriorHeightMm, design.FrameMm, cubbies);
    }
}

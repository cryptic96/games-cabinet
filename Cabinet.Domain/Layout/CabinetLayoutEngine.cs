namespace Cabinet.Domain.Layout;

/// <summary>
/// Builds the cabinet for a collection. The result is a pure function of the items, the section design and the layout
/// options: nothing is remembered between calls, so the same collection always gives the same cabinet. How each game
/// stands is decided from that game alone. Games are taken in collection then game identifier order and each goes into
/// the first cubby, in reading order section by section, that can still take it; a new section opens only when no
/// existing cubby can.
/// </summary>
public static class CabinetLayoutEngine
{
    /// <summary>Bumped whenever the algorithm or a design changes on purpose, so a rearrangement is always a conscious change.</summary>
    public const int LayoutVersion = 3;

    /// <summary>Lays the items out with the default layout options; see the overload that takes options.</summary>
    public static CabinetLayout Build(IReadOnlyList<CabinetItem> items, SectionDesign design) =>
        Build(items, design, LayoutOptions.Default);

    /// <summary>
    /// Lays the items out in the design's sections. Every cubby of every section is emitted, empty or not, and there is
    /// always at least one section. When there are fewer top-level games than the few-games threshold every box faces out.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">A collection and game identifier pair appears twice.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A layout option is out of range.</exception>
    /// <exception cref="InvalidOperationException">An item cannot fit even an empty section.</exception>
    public static CabinetLayout Build(IReadOnlyList<CabinetItem> items, SectionDesign design, LayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var ordered = items.OrderBy(item => item.CollectionId).ThenBy(item => item.BggId).ToList();
        RejectDuplicates(ordered);

        var fewGames = ordered.Count < options.FewGamesThreshold;
        var members = ordered
            .Select(item => new LayoutMember(item, Orientation.Decide(item, options, design, fewGames)))
            .ToList();
        var context = new BuildContext(design, design.Cubbies, options);
        var sections = new List<List<List<LayoutMember>>> { NewSection(context.Cubbies.Count) };

        foreach (var member in members)
        {
            if (!TryPlaceInExistingSection(sections, context, member))
            {
                sections.Add(NewSection(context.Cubbies.Count));

                if (!TryPlaceInSection(sections, sections.Count - 1, context, member))
                {
                    throw new InvalidOperationException(
                        $"Game {member.Item.BggId} does not fit an empty section of the '{design.Name}' design.");
                }
            }
        }

        var layoutSections = sections
            .Select((section, sectionIndex) => ToLayoutSection(sectionIndex, section, context))
            .ToList();

        return new CabinetLayout(LayoutVersion, design.Name, options.Fingerprint, SpinePalette.Tones, layoutSections);
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

    private static List<List<LayoutMember>> NewSection(int cubbyCount) =>
        Enumerable.Range(0, cubbyCount).Select(_ => new List<LayoutMember>()).ToList();

    private static bool TryPlaceInExistingSection(
        List<List<List<LayoutMember>>> sections,
        BuildContext context,
        LayoutMember member)
    {
        for (var sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            if (TryPlaceInSection(sections, sectionIndex, context, member))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryPlaceInSection(
        List<List<List<LayoutMember>>> sections,
        int sectionIndex,
        BuildContext context,
        LayoutMember member)
    {
        var section = sections[sectionIndex];

        for (var cubbyIndex = 0; cubbyIndex < section.Count; cubbyIndex++)
        {
            var candidate = new List<LayoutMember>(section[cubbyIndex]) { member };
            var arrangement = CubbyArrangement.TryArrange(
                context.Design,
                context.Cubbies[cubbyIndex],
                candidate,
                context.Options,
                context.OrderSalt(sectionIndex, cubbyIndex));

            if (arrangement is not null)
            {
                section[cubbyIndex].Add(member);

                return true;
            }
        }

        return false;
    }

    private static LayoutSection ToLayoutSection(int sectionIndex, List<List<LayoutMember>> section, BuildContext context)
    {
        var cubbies = section
            .Select((members, cubbyIndex) =>
            {
                var cubby = context.Cubbies[cubbyIndex];
                var placements = CubbyArrangement.TryArrange(
                    context.Design,
                    cubby,
                    members,
                    context.Options,
                    context.OrderSalt(sectionIndex, cubbyIndex)) ?? [];

                return new LayoutCubby(cubby.Index, cubby.XMm, cubby.YMm, cubby.WidthMm, cubby.HeightMm, placements);
            })
            .ToList();

        return new LayoutSection(
            sectionIndex,
            context.Design.InteriorWidthMm,
            context.Design.InteriorHeightMm,
            context.Design.FrameMm,
            cubbies);
    }

    private sealed record BuildContext(SectionDesign Design, IReadOnlyList<CubbyDesign> Cubbies, LayoutOptions Options)
    {
        public int OrderSalt(int sectionIndex, int cubbyIndex) =>
            StableHash.CubbyOrderSaltBase + (sectionIndex * Cubbies.Count) + cubbyIndex;
    }
}

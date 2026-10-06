namespace Cabinet.Domain.Layout;

/// <summary>
/// Builds the cabinet for a collection. The result is a pure function of the items, the section design and the layout
/// options: nothing is remembered between calls, so the same collection always gives the same cabinet. How each game
/// stands is decided from that game alone. Base games, and expansions whose base game is not owned, are taken in
/// collection then game identifier order and each goes into the first cubby, in reading order section by section, that
/// can still take it; a new section opens only when no existing cubby can. An owned expansion never takes a place of its
/// own: it stands in a stack beside its base game, and the base game reserves the column for that stack in its cubby
/// when it is placed, whether the expansion arrived before or after it.
/// </summary>
public static class CabinetLayoutEngine
{
    /// <summary>Bumped whenever the algorithm or a design changes on purpose, so a rearrangement is always a conscious change.</summary>
    public const int LayoutVersion = 4;

    private const int MinBoxSideMm = 10;

    /// <summary>Lays the items out with the default layout options; see the overload that takes options.</summary>
    public static CabinetLayout Build(IReadOnlyList<CabinetItem> items, SectionDesign design) =>
        Build(items, design, LayoutOptions.Default);

    /// <summary>
    /// Lays the items out in the design's sections. Every cubby of every section is emitted, empty or not, and there is
    /// always at least one section. When there are fewer top-level games than the few-games threshold every box faces out.
    /// Top-level games are the base games and the expansions whose base game is not owned. An expansion that names several
    /// owned base games joins the one with the lowest game identifier. Boxes larger than the design can hold are scaled
    /// down on entry, keeping their proportions.
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

        var families = FamilyIndex.Create(ordered);
        var fewGames = families.TopLevel.Count < options.FewGamesThreshold;
        var limits = design.Limits;
        var members = families.TopLevel
            .Select(unit => ToMember(unit, families, options, design, limits, fewGames))
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

    private static LayoutMember ToMember(
        TopLevelUnit unit,
        FamilyIndex families,
        LayoutOptions options,
        SectionDesign design,
        BoxLimits limits,
        bool fewGames)
    {
        var item = unit.Item;

        if (item.Kind == ItemKind.Expansion)
        {
            var orphanPose = fewGames ? BoxPose.Cover : BoxPose.Flat;
            var orphanItem = item with { Box = Clamp(item.Box, limits, hasFamily: false) };

            return new LayoutMember(orphanItem, orphanPose, [], unit.BaseTitle);
        }

        var expansions = families.ExpansionsOf(item);
        var pose = Orientation.Decide(item, options, design, fewGames);

        if (expansions.Count > 0 && pose == BoxPose.Flat)
        {
            pose = BoxPose.Spine;
        }

        var baseItem = item with { Box = Clamp(item.Box, limits, expansions.Count > 0) };

        return new LayoutMember(baseItem, pose, expansions, null);
    }

    /// <summary>
    /// Scales a box down to the design's limits with whole numbers only: width and height shrink together, keeping their
    /// proportions, until the front fits; depth is capped on its own; no side ends up under the least size.
    /// </summary>
    private static BoxDimensions Clamp(BoxDimensions box, BoxLimits limits, bool hasFamily)
    {
        var maxWidth = Math.Max(MinBoxSideMm, hasFamily ? limits.MaxFamilyBaseWidthMm : limits.MaxWidthMm);
        var maxHeight = Math.Max(MinBoxSideMm, limits.MaxHeightMm);
        var width = Math.Max(MinBoxSideMm, box.WidthMm);
        var height = Math.Max(MinBoxSideMm, box.HeightMm);

        if (width > maxWidth || height > maxHeight)
        {
            if ((long)width * maxHeight > (long)height * maxWidth)
            {
                height = (int)((long)height * maxWidth / width);
                width = maxWidth;
            }
            else
            {
                width = (int)((long)width * maxHeight / height);
                height = maxHeight;
            }
        }

        var depth = Math.Clamp(box.DepthMm, MinBoxSideMm, Math.Max(MinBoxSideMm, limits.MaxDepthMm));

        return new BoxDimensions(Math.Max(MinBoxSideMm, width), Math.Max(MinBoxSideMm, height), depth);
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

    private sealed record TopLevelUnit(CabinetItem Item, string? BaseTitle);

    private sealed record BuildContext(SectionDesign Design, IReadOnlyList<CubbyDesign> Cubbies, LayoutOptions Options)
    {
        public int OrderSalt(int sectionIndex, int cubbyIndex) =>
            StableHash.CubbyOrderSaltBase + (sectionIndex * Cubbies.Count) + cubbyIndex;
    }

    /// <summary>
    /// Sorts the ordered items into top-level units and families. Only a base game can be a parent, so an expansion that
    /// names an expansion, itself or a game that is not owned has no parent and stands on its own. When the same base game
    /// is owned twice, its first entry takes the family.
    /// </summary>
    private sealed class FamilyIndex
    {
        private readonly Dictionary<int, CabinetItem> _firstBaseById;
        private readonly Dictionary<int, List<CabinetItem>> _expansionsByBase;

        private FamilyIndex(
            List<TopLevelUnit> topLevel,
            Dictionary<int, CabinetItem> firstBaseById,
            Dictionary<int, List<CabinetItem>> expansionsByBase)
        {
            TopLevel = topLevel;
            _firstBaseById = firstBaseById;
            _expansionsByBase = expansionsByBase;
        }

        public List<TopLevelUnit> TopLevel { get; }

        public static FamilyIndex Create(List<CabinetItem> ordered)
        {
            var firstBaseById = new Dictionary<int, CabinetItem>();

            foreach (var item in ordered.Where(item => item.Kind == ItemKind.Base))
            {
                firstBaseById.TryAdd(item.BggId, item);
            }

            var topLevel = new List<TopLevelUnit>();
            var expansionsByBase = new Dictionary<int, List<CabinetItem>>();

            foreach (var item in ordered)
            {
                if (item.Kind == ItemKind.Base)
                {
                    topLevel.Add(new TopLevelUnit(item, null));

                    continue;
                }

                var parentId = item.ExpansionOf
                    .Select(reference => reference.BggId)
                    .Where(firstBaseById.ContainsKey)
                    .Order()
                    .Select(id => (int?)id)
                    .FirstOrDefault();

                if (parentId is null)
                {
                    var named = item.ExpansionOf.OrderBy(reference => reference.BggId).FirstOrDefault();
                    topLevel.Add(new TopLevelUnit(item, named?.Title));

                    continue;
                }

                if (!expansionsByBase.TryGetValue(parentId.Value, out var family))
                {
                    family = [];
                    expansionsByBase[parentId.Value] = family;
                }

                family.Add(item);
            }

            return new FamilyIndex(topLevel, firstBaseById, expansionsByBase);
        }

        public IReadOnlyList<CabinetItem> ExpansionsOf(CabinetItem item) =>
            _firstBaseById.TryGetValue(item.BggId, out var first)
                && first == item
                && _expansionsByBase.TryGetValue(item.BggId, out var family)
                    ? family
                    : [];
    }
}

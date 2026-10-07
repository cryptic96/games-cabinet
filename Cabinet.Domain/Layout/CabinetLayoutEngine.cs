namespace Cabinet.Domain.Layout;

/// <summary>
/// Builds the cabinet for a collection. The result is a pure function of the items, the section design and the layout
/// options: nothing is remembered between calls, so the same collection always gives the same cabinet. How each game
/// stands is decided from that game alone. Base games, and expansions whose base game is not owned, are taken in
/// collection then game identifier order and each goes into the first cubby, in reading order section by section, that
/// can still take it the way it was chosen; failing that a plain game may lie flat in the first cubby that can take it
/// lying down; a new section opens only when neither fits. An owned expansion never takes a place of its own: a thick
/// one stands upright right beside its base game and a thinner one lies in a stack beside it, and the base game reserves
/// the room for both in its cubby when it is placed, whether the expansion arrived before or after it. Every section
/// is drawn whole except the last, which is drawn only down to its last used shelf row (at least
/// <see cref="MinTrimmedRows"/> rows); that is a matter of drawing alone, so no placement depends on it.
/// </summary>
public static class CabinetLayoutEngine
{
    /// <summary>Bumped whenever the algorithm or a design changes on purpose, so a rearrangement is always a conscious change.</summary>
    public const int LayoutVersion = 12;

    /// <summary>
    /// The fewest shelf rows the last section is drawn with, so a nearly empty cabinet still reads as a piece of furniture.
    /// A design with fewer rows keeps all of them.
    /// </summary>
    public const int MinTrimmedRows = 2;

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
    /// <exception cref="InvalidOperationException">
    /// An item cannot fit even an empty section, or a cubby that accepted its games cannot arrange them when it is drawn.
    /// </exception>
    public static CabinetLayout Build(IReadOnlyList<CabinetItem> items, SectionDesign design, LayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        design.Validate();

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
            if (!TryPlaceInExistingSection(sections, context, member) && !TryPlaceLyingFlat(sections, context, member, fewGames))
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
            .Select((section, sectionIndex) => ToLayoutSection(sectionIndex, section, context, sectionIndex == sections.Count - 1))
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
            var orphanItem = item with { Box = Clamp(item.Box, limits, facesOutBesideExpansions: false) };

            return new LayoutMember(orphanItem, orphanPose, [], unit.BaseTitle);
        }

        var expansions = families.ExpansionsOf(item);
        var pose = Orientation.Decide(item, options, design, fewGames);

        if (expansions.Count > 0 && pose == BoxPose.Flat)
        {
            pose = BoxPose.Spine;
        }

        var facesOutBesideExpansions = expansions.Count > 0 && pose == BoxPose.Cover;
        var baseItem = item with { Box = Clamp(item.Box, limits, facesOutBesideExpansions) };
        var plain = new LayoutMember(baseItem, pose);
        var room = limits.MaxWidthMm - CubbyArrangement.StandingWidthMm(design, plain) - design.StackColumnWidthMm;
        var (uprights, stacked) = SplitExpansions(expansions, design, limits, room);

        return plain with { Expansions = stacked, Uprights = uprights };
    }

    /// <summary>
    /// Splits the expansions of a family, in collection order, into those that stand upright beside the base game and
    /// those that lie in its stack. A thick expansion stands upright while fewer than the allowed number stand and it still
    /// fits the room left beside the base game, which always keeps the stack column's width free; the first thick one that
    /// does not fit, and every expansion after it, lies in the stack, so a later arrival never displaces an earlier upright.
    /// Every expansion is scaled to the design's limits like a plain box.
    /// </summary>
    private static (List<CabinetItem> Uprights, List<CabinetItem> Stacked) SplitExpansions(
        IReadOnlyList<CabinetItem> expansions,
        SectionDesign design,
        BoxLimits limits,
        int room)
    {
        var uprights = new List<CabinetItem>();
        var stacked = new List<CabinetItem>();
        var roomLeft = room;
        var standingOpen = true;

        foreach (var expansion in expansions)
        {
            var scaled = expansion with { Box = Clamp(expansion.Box, limits, facesOutBesideExpansions: false) };

            if (standingOpen && Orientation.StandsUpright(expansion))
            {
                var width = CubbyArrangement.UprightWidthMm(design, scaled);

                if (uprights.Count < Orientation.MaxUprightExpansions && width <= roomLeft)
                {
                    uprights.Add(scaled);
                    roomLeft -= width;

                    continue;
                }

                standingOpen = false;
            }

            stacked.Add(scaled);
        }

        return (uprights, stacked);
    }

    /// <summary>
    /// Scales a box down to the design's limits with whole numbers only: width and height shrink together, keeping their
    /// proportions, until the front fits; depth is capped on its own; no side ends up under the least size. The narrower
    /// family width applies only to a base game that faces out beside a stack column, because a box standing as a spine
    /// shows its depth, not its front, so its front width must not shorten it.
    /// </summary>
    private static BoxDimensions Clamp(BoxDimensions box, BoxLimits limits, bool facesOutBesideExpansions)
    {
        var maxWidth = Math.Max(MinBoxSideMm, facesOutBesideExpansions ? limits.MaxFamilyBaseWidthMm : limits.MaxWidthMm);
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

    /// <summary>
    /// Tries the member lying flat across the existing sections in reading order. Only a plain game that was chosen to face
    /// out or stand is eligible, and only when the setting is on and the few-games look is off: a game with expansions
    /// always stands and an expansion without an owned base game already lies flat.
    /// </summary>
    private static bool TryPlaceLyingFlat(
        List<List<List<LayoutMember>>> sections,
        BuildContext context,
        LayoutMember member,
        bool fewGames)
    {
        var eligible = context.Options.LieFlatBeforeNewSection
            && !fewGames
            && member.Pose != BoxPose.Flat
            && !member.IsOrphanExpansion
            && !member.HasFamily;

        return eligible && TryPlaceInExistingSection(sections, context, member with { Pose = BoxPose.Flat });
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

    /// <summary>
    /// Draws one section by arranging each of its shown cubbies again. Placement already arranged every cubby with the same
    /// members and the same order salt, so an arrangement that fails here means an engine invariant broke; that throws
    /// instead of drawing the cubby empty, so a game can never silently disappear from the cabinet.
    /// </summary>
    private static LayoutSection ToLayoutSection(
        int sectionIndex,
        List<List<LayoutMember>> section,
        BuildContext context,
        bool trimUnusedRows)
    {
        var rowCount = trimUnusedRows ? DrawnRowCount(section, context) : context.Design.Rows.Count;
        var drawnBottom = context.Design.Rows.Take(rowCount).Sum(row => row.HeightMm) + (context.Design.FrameMm * (rowCount - 1));
        var cubbies = section
            .Select((members, cubbyIndex) => (members, cubbyIndex))
            .Where(entry => context.Cubbies[entry.cubbyIndex].YMm < drawnBottom)
            .Select(entry =>
            {
                var cubby = context.Cubbies[entry.cubbyIndex];
                var placements = CubbyArrangement.TryArrange(
                    context.Design,
                    cubby,
                    entry.members,
                    context.Options,
                    context.OrderSalt(sectionIndex, entry.cubbyIndex))
                    ?? throw new InvalidOperationException(
                        $"Cubby {cubby.Index} of section {sectionIndex} of the '{context.Design.Name}' design accepted its games during placement but cannot arrange them.");

                return new LayoutCubby(cubby.Index, cubby.XMm, cubby.YMm, cubby.WidthMm, cubby.HeightMm, placements);
            })
            .ToList();

        return new LayoutSection(
            sectionIndex,
            context.Design.InteriorWidthMm,
            drawnBottom,
            context.Design.FrameMm,
            cubbies);
    }

    /// <summary>
    /// The number of shelf rows the last section is drawn with: down to the lowest row that holds a game, and never fewer
    /// than <see cref="MinTrimmedRows"/> or more than the design has.
    /// </summary>
    private static int DrawnRowCount(List<List<LayoutMember>> section, BuildContext context)
    {
        var lowestUsedTop = section
            .Select((members, cubbyIndex) => (members, cubbyIndex))
            .Where(entry => entry.members.Count > 0)
            .Select(entry => context.Cubbies[entry.cubbyIndex].YMm)
            .DefaultIfEmpty(-1)
            .Max();
        var rowTops = context.Cubbies.Select(cubby => cubby.YMm).Distinct().Order().ToList();
        var usedRows = lowestUsedTop < 0 ? 0 : rowTops.IndexOf(lowestUsedTop) + 1;

        return Math.Min(rowTops.Count, Math.Max(MinTrimmedRows, usedRows));
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

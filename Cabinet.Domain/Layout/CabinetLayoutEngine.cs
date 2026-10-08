namespace Cabinet.Domain.Layout;

/// <summary>
/// Builds the cabinet for a collection. The result is a pure function of the items, the section design and the layout
/// options: nothing is remembered between calls, so the same collection always gives the same cabinet. How each game
/// stands is decided from that game and its own owned expansions alone: a base game with enough of them faces out. Base
/// games, and expansions whose base game is not owned, are taken in collection then game identifier order, except that
/// the games of one series (see <see cref="SeriesGrouping"/>) are taken together where the earliest of them would be
/// taken: a series that fits one cubby stands in the first cubby, in reading order, that can take all of it, and a longer
/// series runs on from the first cubby from which each of its games can stand in the cubby of the game before it or in
/// the very next cubby, so it never leaves a cubby out. Only a series whose games cannot stand side by side even in an
/// empty section goes game by game to the first cubby that takes each. A game in no series goes into the first cubby, in
/// reading order section by section, that can still take it the way it was chosen; failing that a plain game may lie flat
/// in the first cubby that can take it lying down; a new section opens only when neither fits. An owned expansion never
/// takes a place of its own: a thick one stands upright right beside its base game and a thinner one lies in a stack
/// beside it, and the base game reserves the room for both in its cubby when it is placed, whether the expansion arrived
/// before or after it. A family may use one more cubby: the next one to the right on the same shelf row. When the whole
/// family does not fit a cubby but its game and uprights do, the stack column stands at the left edge of that next cubby,
/// and the family stands last in its own cubby so the column is right beside it. When a whole family would hide
/// expansions behind a marker, it shows the layers that fit under its shelf and continues in a second column at the left
/// edge of the next cubby when that cubby has room, with the marker only for what fits nowhere, unless the next game of
/// its series needs that room to run on. Which of these applies is fixed when the family is placed. A family never
/// reaches a third cubby, another row or another section.
/// Every section is drawn whole except the last, which is drawn only down to its last used shelf row (at least
/// <see cref="MinTrimmedRows"/> rows); that is a matter of drawing alone, so no placement depends on it.
/// </summary>
public static class CabinetLayoutEngine
{
    /// <summary>Bumped whenever the algorithm or a design changes on purpose, so a rearrangement is always a conscious change.</summary>
    public const int LayoutVersion = 14;

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

        foreach (var group in SeriesGrouping.Group([.. members.Select(member => member.Item)], options.GroupSeries))
        {
            var series = group.Indices.Select(index => members[index] with { SeriesAnchor = group.AnchorGameId }).ToList();

            PlaceSeries(sections, context, series, fewGames);
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
        var pose = Orientation.Decide(item, options, design, fewGames, expansions.Count);

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

    /// <summary>
    /// Places the games of one series, which are in entry order; a game in no series is a series of one and is placed on
    /// its own, see <see cref="PlaceOne"/>. A series of several games is placed so it never leaves a cubby out, see
    /// <see cref="TryPlaceRun"/>. Only when not even a new section can hold it that way does each game go, as a plain game
    /// is placed, to the first cubby that takes it from the cubby of the game before it on.
    /// </summary>
    private static void PlaceSeries(
        List<List<List<LayoutMember>>> sections,
        BuildContext context,
        IReadOnlyList<LayoutMember> series,
        bool fewGames)
    {
        if (series.Count > 1 && TryPlaceRun(sections, context, series, fewGames))
        {
            return;
        }

        Position? previous = null;

        foreach (var member in series)
        {
            previous = PlaceOne(sections, context, member, fewGames, previous);
        }
    }

    /// <summary>
    /// Places a series of several games so it never leaves a cubby out, and says whether it could. It tries, in turn: the
    /// whole series as one block in one cubby; a run, in which every later game stands in the cubby of the game before it
    /// or in the very next cubby in reading order, across shelf rows and from the last cubby of a section on into the first
    /// of the next; such a run in which a plain game may lie flat; and such a run in which a family that is not the last
    /// game of the series keeps its whole stack in its own column, with the marker, instead of continuing it in the next
    /// cubby, so the next game has room there. Each way is tried from every cubby in reading order and the first that works
    /// wins: first within the existing sections, then letting a run go on into a new section, and only then from every
    /// cubby of a new section. Returns false, leaving the sections as they were, when nothing works even then.
    /// </summary>
    private static bool TryPlaceRun(
        List<List<List<LayoutMember>>> sections,
        BuildContext context,
        IReadOnlyList<LayoutMember> series,
        bool fewGames)
    {
        var checkpoint = Checkpoint.Take(sections);
        RunMode[] runs = context.Options.LieFlatBeforeNewSection && !fewGames
            ? [RunMode.Standing, RunMode.LyingFlat, RunMode.KeepingStacks]
            : [RunMode.Standing, RunMode.KeepingStacks];
        RunMode[] all = [RunMode.Block, .. runs];
        (RunMode[] Modes, bool Fresh, bool MayOpen)[] phases = [(all, false, false), (runs, false, true), (all, true, true)];

        foreach (var (modes, fresh, mayOpen) in phases)
        {
            int[] sectionIndexes = fresh ? [checkpoint.SectionCount] : [.. Enumerable.Range(0, checkpoint.SectionCount)];

            foreach (var mode in modes)
            {
                foreach (var sectionIndex in sectionIndexes)
                {
                    for (var cubbyIndex = 0; cubbyIndex < context.Cubbies.Count; cubbyIndex++)
                    {
                        if (fresh)
                        {
                            sections.Add(NewSection(context.Cubbies.Count));
                        }

                        if (TryRunFrom(sections, context, series, new Position(sectionIndex, cubbyIndex), mode, mayOpen, fewGames))
                        {
                            return true;
                        }

                        checkpoint.Restore(sections);
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Places the series from the start cubby in the given way and says whether every game found a place; the caller undoes
    /// what was added when one did not. A block puts every game in the start cubby at once. A run puts the first game in the
    /// start cubby and every later game in the cubby of the game before it or else in the very next cubby in reading order,
    /// standing the way it was chosen before lying flat when the mode allows that; a game that lands in a later cubby than
    /// the game before it is marked so it stands first there. The cubby after the last one of the last section is the first
    /// of a new section, which is opened only when <paramref name="mayOpen"/> allows it.
    /// </summary>
    private static bool TryRunFrom(
        List<List<List<LayoutMember>>> sections,
        BuildContext context,
        IReadOnlyList<LayoutMember> series,
        Position start,
        RunMode mode,
        bool mayOpen,
        bool fewGames)
    {
        if (mode == RunMode.Block)
        {
            return TryAdd(sections, context, start.Section, start.Cubby, series);
        }

        Position? previous = null;

        for (var index = 0; index < series.Count; index++)
        {
            var member = series[index];
            Position[] cubbies = previous is { } before ? [before, Following(context, before)] : [start];
            LayoutMember[] poses = mode is RunMode.LyingFlat or RunMode.KeepingStacks && MayLieFlatInstead(context, member, fewGames)
                ? [member, member with { Pose = BoxPose.Flat }]
                : [member];
            var mayContinue = mode != RunMode.KeepingStacks || index == series.Count - 1;

            if (TryPlaceAmong(sections, context, poses, cubbies, previous, new Permits(mayOpen, mayContinue)) is not { } placed)
            {
                return false;
            }

            previous = placed;
        }

        return true;
    }

    /// <summary>
    /// Tries each pose in turn in each of the cubbies in turn and returns the cubby that took the game, or null when none
    /// did. A game that lands in a later cubby than the game of its series before it is marked so it stands first there.
    /// </summary>
    private static Position? TryPlaceAmong(
        List<List<List<LayoutMember>>> sections,
        BuildContext context,
        IReadOnlyList<LayoutMember> poses,
        IReadOnlyList<Position> cubbies,
        Position? previous,
        Permits permits)
    {
        foreach (var pose in poses)
        {
            foreach (var cubby in cubbies)
            {
                var candidate = previous is { } before && cubby.IsAfter(before) ? pose with { FromPreviousCubby = true } : pose;

                if (TryAddOpening(sections, context, cubby, candidate, permits))
                {
                    return cubby;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Adds the game to the cubby as <see cref="TryAdd"/> does. When the cubby is the first of the section after the last
    /// one, a new section is opened for it first if the permits allow that, and taken away again when the game does not fit.
    /// </summary>
    private static bool TryAddOpening(
        List<List<List<LayoutMember>>> sections,
        BuildContext context,
        Position cubby,
        LayoutMember member,
        Permits permits)
    {
        var opens = cubby.Section == sections.Count;

        if (opens && !permits.MayOpen)
        {
            return false;
        }

        if (opens)
        {
            sections.Add(NewSection(context.Cubbies.Count));
        }

        if (TryAdd(sections, context, cubby.Section, cubby.Cubby, [member], permits.MayContinue))
        {
            return true;
        }

        if (opens)
        {
            sections.RemoveAt(sections.Count - 1);
        }

        return false;
    }

    /// <summary>The next cubby in reading order: the next one of the section, or the first of the next section after its last.</summary>
    private static Position Following(BuildContext context, Position cubby) =>
        cubby.Cubby + 1 < context.Cubbies.Count ? cubby with { Cubby = cubby.Cubby + 1 } : new Position(cubby.Section + 1, 0);

    /// <summary>
    /// Adds the members to one cubby when it can take them, and says whether it did. A family goes there whole, its column
    /// beside it, when it fits. When the whole family does not fit, a family with stacked expansions may stand in the
    /// cubby without its column when the next cubby of the same shelf row takes the column at its left edge. And when a
    /// whole family would hide expansions behind a marker, it continues in a second column at the left edge of that next
    /// cubby instead, when the next cubby has room and <paramref name="mayContinue"/> allows it. Nothing ever goes beyond
    /// the next cubby of the row.
    /// </summary>
    private static bool TryAdd(
        List<List<List<LayoutMember>>> sections,
        BuildContext context,
        int sectionIndex,
        int cubbyIndex,
        IReadOnlyList<LayoutMember> batch,
        bool mayContinue = true)
    {
        var section = sections[sectionIndex];
        var here = section[cubbyIndex];
        var nextIndex = NextOnShelfRow(context, cubbyIndex);

        if (CanArrange(context, sectionIndex, cubbyIndex, [.. here, .. batch]))
        {
            if (mayContinue && nextIndex is { } overflowIndex && TryContinue(context, sectionIndex, cubbyIndex, overflowIndex, section, batch) is { } split)
            {
                here.AddRange(split.Batch);
                section[overflowIndex].Add(split.Column);

                return true;
            }

            here.AddRange(batch);

            return true;
        }

        if (nextIndex is not { } neighbourIndex)
        {
            return false;
        }

        for (var index = 0; index < batch.Count; index++)
        {
            if (!HasStackedExpansions(batch[index]))
            {
                continue;
            }

            var column = LayoutMember.ColumnFor(batch[index], batch[index].Expansions);
            var standing = Replace(batch, index, batch[index] with { ColumnNextDoor = true });

            if (CanArrange(context, sectionIndex, cubbyIndex, [.. here, .. standing])
                && CanArrange(context, sectionIndex, neighbourIndex, [.. section[neighbourIndex], column]))
            {
                here.AddRange(standing);
                section[neighbourIndex].Add(column);

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds a family among the members whose stack would hide expansions behind a marker and that can continue in a second
    /// column in the next cubby, and returns the members with that family marked and the column to add there; null when no
    /// family can.
    /// </summary>
    private static (List<LayoutMember> Batch, LayoutMember Column)? TryContinue(
        BuildContext context,
        int sectionIndex,
        int cubbyIndex,
        int nextIndex,
        List<List<LayoutMember>> section,
        IReadOnlyList<LayoutMember> batch)
    {
        for (var index = 0; index < batch.Count; index++)
        {
            if (!HasStackedExpansions(batch[index])
                || CubbyArrangement.SplitStack(context.Design, batch[index], context.Cubbies[cubbyIndex], context.Options) is not { } split)
            {
                continue;
            }

            var column = LayoutMember.ColumnFor(batch[index], split.Remaining);
            var standing = Replace(batch, index, batch[index] with { ContinuesNextDoor = true });

            if (CanArrange(context, sectionIndex, cubbyIndex, [.. section[cubbyIndex], .. standing])
                && CanArrange(context, sectionIndex, nextIndex, [.. section[nextIndex], column]))
            {
                return (standing, column);
            }
        }

        return null;
    }

    private static bool HasStackedExpansions(LayoutMember member) => member.Expansions.Count > 0 && !member.IsColumnOnly;

    private static List<LayoutMember> Replace(IReadOnlyList<LayoutMember> members, int index, LayoutMember replacement)
    {
        var copy = new List<LayoutMember>(members) { [index] = replacement };

        return copy;
    }

    /// <summary>The index of the next cubby to the right on the same shelf row, or null when this cubby is the last of its row.</summary>
    private static int? NextOnShelfRow(BuildContext context, int cubbyIndex) =>
        cubbyIndex + 1 < context.Cubbies.Count && context.Cubbies[cubbyIndex + 1].YMm == context.Cubbies[cubbyIndex].YMm
            ? cubbyIndex + 1
            : null;

    /// <summary>
    /// Places one game in the first cubby, in reading order, that can take it the way it was chosen; failing that lying
    /// flat; failing that in a new section. A game that continues a series starts looking at the cubby of the game before
    /// it, and is marked when it ends up in a later cubby than that game, so it stands first there. Returns where it went.
    /// </summary>
    private static Position PlaceOne(
        List<List<List<LayoutMember>>> sections,
        BuildContext context,
        LayoutMember member,
        bool fewGames,
        Position? previous)
    {
        var start = previous ?? new Position(0, 0);
        var placed = TryPlaceFrom(sections, context, member, start, previous)
            ?? TryPlaceLyingFlat(sections, context, member, fewGames, start, previous);

        if (placed is { } position)
        {
            return position;
        }

        sections.Add(NewSection(context.Cubbies.Count));

        return TryPlaceFrom(sections, context, member, new Position(sections.Count - 1, 0), previous)
            ?? throw new InvalidOperationException(
                $"Game {member.Item.BggId} does not fit an empty section of the '{context.Design.Name}' design.");
    }

    /// <summary>
    /// Tries the member lying flat across the existing sections in reading order from the start cubby on. Only a plain game
    /// that was chosen to face out or stand is eligible, and only when the setting is on and the few-games look is off: a
    /// game with expansions always stands and an expansion without an owned base game already lies flat.
    /// </summary>
    private static Position? TryPlaceLyingFlat(
        List<List<List<LayoutMember>>> sections,
        BuildContext context,
        LayoutMember member,
        bool fewGames,
        Position start,
        Position? previous) =>
        MayLieFlatInstead(context, member, fewGames)
            ? TryPlaceFrom(sections, context, member with { Pose = BoxPose.Flat }, start, previous)
            : null;

    /// <summary>
    /// Whether the game may lie flat when no cubby has room for it the way it was chosen: only a plain game chosen to face
    /// out or stand, and only when the setting is on and the few-games look is off.
    /// </summary>
    private static bool MayLieFlatInstead(BuildContext context, LayoutMember member, bool fewGames) =>
        context.Options.LieFlatBeforeNewSection
        && !fewGames
        && member.Pose != BoxPose.Flat
        && !member.IsOrphanExpansion
        && !member.HasFamily;

    private static Position? TryPlaceFrom(
        List<List<List<LayoutMember>>> sections,
        BuildContext context,
        LayoutMember member,
        Position start,
        Position? previous)
    {
        for (var sectionIndex = start.Section; sectionIndex < sections.Count; sectionIndex++)
        {
            var section = sections[sectionIndex];

            for (var cubbyIndex = sectionIndex == start.Section ? start.Cubby : 0; cubbyIndex < section.Count; cubbyIndex++)
            {
                var here = new Position(sectionIndex, cubbyIndex);
                var placed = previous is { } before && here.IsAfter(before) ? member with { FromPreviousCubby = true } : member;

                if (TryAdd(sections, context, sectionIndex, cubbyIndex, [placed]))
                {
                    return here;
                }
            }
        }

        return null;
    }

    private static bool CanArrange(BuildContext context, int sectionIndex, int cubbyIndex, IReadOnlyList<LayoutMember> candidate) =>
        CubbyArrangement.TryArrange(
            context.Design,
            context.Cubbies[cubbyIndex],
            candidate,
            context.Options,
            context.OrderSalt(sectionIndex, cubbyIndex)) is not null;

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

    private readonly record struct Position(int Section, int Cubby)
    {
        public bool IsAfter(Position other) => Section > other.Section || (Section == other.Section && Cubby > other.Cubby);
    }

    /// <summary>What a game of a run may do while it is placed.</summary>
    /// <param name="MayOpen">Whether the run may open a new section after the last one.</param>
    /// <param name="MayContinue">Whether a family may continue its stack in a second column in the next cubby.</param>
    private readonly record struct Permits(bool MayOpen, bool MayContinue);

    /// <summary>How a series of several games may spread over the cubbies, tried in this order.</summary>
    private enum RunMode
    {
        /// <summary>Every game in one cubby at once, the way it was chosen.</summary>
        Block,

        /// <summary>Each game in the cubby of the game before it or the very next one, the way it was chosen.</summary>
        Standing,

        /// <summary>As <see cref="Standing"/>, and a plain game that fits neither cubby the way it was chosen may lie flat.</summary>
        LyingFlat,

        /// <summary>
        /// As <see cref="LyingFlat"/>, and a family that is not the last game of the series keeps its whole stack in its own
        /// column, with the marker, so the next game of the series has room in the next cubby.
        /// </summary>
        KeepingStacks,
    }

    /// <summary>
    /// What the sections held before a series was tried: how many sections there were and how many members each cubby had.
    /// Placing only ever adds members and sections, so restoring the counts undoes a try exactly.
    /// </summary>
    private sealed class Checkpoint
    {
        private readonly int[][] _counts;

        private Checkpoint(int[][] counts) => _counts = counts;

        public int SectionCount => _counts.Length;

        public static Checkpoint Take(List<List<List<LayoutMember>>> sections) =>
            new([.. sections.Select(section => section.Select(cubby => cubby.Count).ToArray())]);

        public void Restore(List<List<List<LayoutMember>>> sections)
        {
            sections.RemoveRange(SectionCount, sections.Count - SectionCount);

            for (var sectionIndex = 0; sectionIndex < SectionCount; sectionIndex++)
            {
                for (var cubbyIndex = 0; cubbyIndex < _counts[sectionIndex].Length; cubbyIndex++)
                {
                    var cubby = sections[sectionIndex][cubbyIndex];
                    var count = _counts[sectionIndex][cubbyIndex];

                    cubby.RemoveRange(count, cubby.Count - count);
                }
            }
        }
    }

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

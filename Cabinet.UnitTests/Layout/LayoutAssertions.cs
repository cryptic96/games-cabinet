using System.Text.Json;
using Cabinet.Domain.Layout;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Invariant checks and comparisons shared by every layout test, so each test states its intent and the rules about
/// what a valid cabinet looks like live in one place.
/// </summary>
internal static class LayoutAssertions
{
    /// <summary>
    /// Asserts the layout is a valid cabinet for the items: each item stands exactly once, every placement is inside its
    /// cubby, placements in a cubby may touch but never overlap, every pile of flat boxes goes widest at the bottom with
    /// no box overhanging the one beneath it, and cubbies sit inside their section without overlapping.
    /// </summary>
    public static void AssertValid(CabinetLayout layout, IReadOnlyList<CabinetItem> items)
    {
        layout.Sections.Should().NotBeEmpty("a cabinet always has at least one section");

        foreach (var section in layout.Sections)
        {
            AssertSectionGeometry(section);
        }

        AssertEveryItemPlacedOnce(layout, items);
    }

    /// <summary>
    /// The section and cubby positions that differ between two layouts, comparing the serialised placement lists. A cubby
    /// that exists only in the second layout counts as changed when it holds placements.
    /// </summary>
    public static IReadOnlyList<(int Section, int Cubby)> ChangedCubbies(CabinetLayout before, CabinetLayout after)
    {
        var changed = new List<(int Section, int Cubby)>();

        foreach (var section in after.Sections)
        {
            foreach (var cubby in section.Cubbies)
            {
                var previous = FindPlacements(before, section.Index, cubby.Index);

                if (Serialize(previous) != Serialize(cubby.Placements))
                {
                    changed.Add((section.Index, cubby.Index));
                }
            }
        }

        return changed;
    }

    private static IReadOnlyList<Placement> FindPlacements(CabinetLayout layout, int sectionIndex, int cubbyIndex) =>
        layout.Sections
            .Where(section => section.Index == sectionIndex)
            .SelectMany(section => section.Cubbies)
            .Where(cubby => cubby.Index == cubbyIndex)
            .Select(cubby => cubby.Placements)
            .FirstOrDefault() ?? [];

    private static string Serialize(IReadOnlyList<Placement> placements) =>
        JsonSerializer.Serialize(placements, LayoutJson.Options);

    /// <summary>
    /// The owned base game an expansion stands beside: the one with the lowest game identifier among the base games it
    /// names, or null when none of them is owned. Worked out here from the items alone so the tests check the engine
    /// against an independent reading of the rule.
    /// </summary>
    public static int? OwnedParentOf(CabinetItem expansion, IReadOnlyList<CabinetItem> items)
    {
        var ownedBases = items.Where(item => item.Kind == ItemKind.Base).Select(item => item.BggId).ToHashSet();

        return expansion.ExpansionOf
            .Select(reference => reference.BggId)
            .Where(ownedBases.Contains)
            .Order()
            .Select(id => (int?)id)
            .FirstOrDefault();
    }

    /// <summary>Every placement of the layout with the section and cubby it stands in.</summary>
    public static IReadOnlyList<(int Section, int Cubby, Placement Placement)> PlacementsWithPosition(CabinetLayout layout) =>
        layout.Sections
            .SelectMany(section => section.Cubbies.SelectMany(cubby =>
                cubby.Placements.Select(placement => (section.Index, cubby.Index, placement))))
            .ToList();

    private static void AssertEveryItemPlacedOnce(CabinetLayout layout, IReadOnlyList<CabinetItem> items)
    {
        var placed = PlacementsWithPosition(layout);
        var standing = placed
            .Where(entry => entry.Placement.Kind is not (PlacementKind.ExpansionLayer or PlacementKind.MoreMarker or PlacementKind.ExpansionSpine))
            .ToList();
        var topLevelIds = items
            .Where(item => item.Kind == ItemKind.Base || OwnedParentOf(item, items) is null)
            .Select(item => item.BggId)
            .Order();

        standing.Select(entry => entry.Placement.GameId).Order()
            .Should().Equal(topLevelIds, "every base game and every expansion without an owned base game stands exactly once");

        foreach (var family in items.Where(item => item.Kind == ItemKind.Expansion && OwnedParentOf(item, items) is not null)
                     .GroupBy(item => OwnedParentOf(item, items)!.Value))
        {
            AssertFamilyAccountedFor(
                layout,
                family.Key,
                family.OrderBy(item => item.CollectionId).ThenBy(item => item.BggId).Select(item => item.BggId).ToList(),
                standing,
                placed);
        }

        placed.Where(entry => entry.Placement.Kind is PlacementKind.ExpansionLayer or PlacementKind.ExpansionSpine)
            .Select(entry => entry.Placement.GameId)
            .Should().OnlyHaveUniqueItems("no expansion is drawn twice, upright and as a layer");
        placed.Where(entry => entry.Placement.FamilyId is { } familyId
                && !items.Any(item => item.BggId == familyId && item.Kind == ItemKind.Base))
            .Should().BeEmpty("a family always belongs to an owned base game");
    }

    private static void AssertFamilyAccountedFor(
        CabinetLayout layout,
        int baseId,
        IReadOnlyList<int> expansionIds,
        List<(int Section, int Cubby, Placement Placement)> standing,
        IReadOnlyList<(int Section, int Cubby, Placement Placement)> placed)
    {
        var baseEntry = standing.Single(entry => entry.Placement.GameId == baseId);
        var members = placed.Where(entry => entry.Placement.FamilyId == baseId
            && entry.Placement.Kind is PlacementKind.ExpansionLayer or PlacementKind.MoreMarker or PlacementKind.ExpansionSpine).ToList();
        var layers = members.Where(entry => entry.Placement.Kind == PlacementKind.ExpansionLayer).ToList();
        var uprights = members.Where(entry => entry.Placement.Kind == PlacementKind.ExpansionSpine)
            .OrderBy(entry => entry.Placement.XMm).ToList();
        var markers = members.Where(entry => entry.Placement.Kind == PlacementKind.MoreMarker).ToList();

        members.Should().OnlyContain(
            entry => entry.Section == baseEntry.Section && entry.Cubby == baseEntry.Cubby,
            "uprights, layers and the marker of family {0} stand in the cubby of their base game", baseId);
        members.Select(entry => entry.Placement.GameId).Where(id => id != baseId).Should().OnlyContain(id => expansionIds.Contains(id));
        markers.Should().HaveCountLessThanOrEqualTo(1, "family {0} has at most one marker", baseId);
        (layers.Count + uprights.Count + markers.Sum(entry => entry.Placement.MoreCount ?? 0))
            .Should().Be(expansionIds.Count, "family {0} counts each of its expansions once", baseId);
        uprights.Should().HaveCountLessThanOrEqualTo(Orientation.MaxUprightExpansions, "family {0} has at most two uprights", baseId);

        var edge = baseEntry.Placement.XMm + baseEntry.Placement.WidthMm;

        foreach (var upright in uprights)
        {
            upright.Placement.XMm.Should().Be(edge, "uprights of family {0} touch the base game and each other", baseId);
            upright.Placement.YMm.Should().Be(0, "uprights of family {0} stand on the cubby floor", baseId);
            upright.Placement.BaseTitle.Should().NotBeNullOrEmpty("an upright names its base game");
            edge += upright.Placement.WidthMm;
        }

        members.Where(entry => entry.Placement.Kind is PlacementKind.ExpansionLayer or PlacementKind.MoreMarker)
            .Should().OnlyContain(entry => entry.Placement.XMm == edge, "the stack column of family {0} starts after the last upright", baseId);

        AssertStackOrder(baseId, expansionIds, uprights.Select(entry => entry.Placement.GameId).ToList(), layers, markers);

        if (SectionDesigns.TryGet(layout.Profile, out var design))
        {
            var columnWidth = members.Any(entry => entry.Placement.Kind != PlacementKind.ExpansionSpine) ? design.StackColumnWidthMm : 0;

            (edge + columnWidth - baseEntry.Placement.XMm).Should().BeLessThanOrEqualTo(
                design.Limits.MaxWidthMm, "family {0} stays within the widest box the design holds", baseId);
            uprights.Should().OnlyContain(entry => entry.Placement.WidthMm >= design.MinUprightExpansionWidthMm);
        }
    }

    private static void AssertStackOrder(
        int baseId,
        IReadOnlyList<int> expansionIdsInCollectionOrder,
        IReadOnlyList<int> uprightIds,
        IReadOnlyList<(int Section, int Cubby, Placement Placement)> layers,
        IReadOnlyList<(int Section, int Cubby, Placement Placement)> markers)
    {
        var stacked = expansionIdsInCollectionOrder.Where(id => !uprightIds.Contains(id)).ToList();
        var shown = stacked.Take(layers.Count).ToList();
        var fromFloor = layers.OrderBy(entry => entry.Placement.YMm).Select(entry => entry.Placement).ToList();
        var expectedOrder = shown
            .OrderByDescending(id => fromFloor.Single(placement => placement.GameId == id).HeightMm)
            .ThenBy(id => stacked.IndexOf(id))
            .ToList();

        fromFloor.Select(placement => placement.GameId).Should().Equal(
            expectedOrder, "the layers of family {0} are the earliest stacked expansions, thickest at the bottom, ties in collection order", baseId);

        var top = 0;

        foreach (var layer in fromFloor)
        {
            layer.YMm.Should().Be(top, "layers of family {0} touch from the floor up", baseId);
            top += layer.HeightMm;
        }

        markers.Should().OnlyContain(entry => entry.Placement.YMm == top, "the marker of family {0} sits on the top layer", baseId);
    }

    private static void AssertSectionGeometry(LayoutSection section)
    {
        foreach (var cubby in section.Cubbies)
        {
            cubby.XMm.Should().BeGreaterThanOrEqualTo(0);
            cubby.YMm.Should().BeGreaterThanOrEqualTo(0);
            (cubby.XMm + cubby.WidthMm).Should().BeLessThanOrEqualTo(section.WidthMm, "cubby {0} stays inside the section", cubby.Index);
            (cubby.YMm + cubby.HeightMm).Should().BeLessThanOrEqualTo(section.HeightMm, "cubby {0} stays inside the section", cubby.Index);
            AssertPlacementsInside(cubby);
            AssertPilesGoWidestAtTheBottom(cubby);
        }

        AssertNoOverlap(
            section.Cubbies.Select(cubby => (cubby.XMm, cubby.YMm, cubby.WidthMm, cubby.HeightMm)).ToList(),
            $"cubbies of section {section.Index}");
    }

    private static void AssertPlacementsInside(LayoutCubby cubby)
    {
        foreach (var placement in cubby.Placements)
        {
            placement.XMm.Should().BeGreaterThanOrEqualTo(0);
            placement.YMm.Should().BeGreaterThanOrEqualTo(0);
            (placement.XMm + placement.WidthMm).Should().BeLessThanOrEqualTo(cubby.WidthMm, "game {0} stays inside cubby {1}", placement.GameId, cubby.Index);
            (placement.YMm + placement.HeightMm).Should().BeLessThanOrEqualTo(cubby.HeightMm, "game {0} stays inside cubby {1}", placement.GameId, cubby.Index);
        }

        AssertNoOverlap(
            cubby.Placements.Select(placement => (placement.XMm, placement.YMm, placement.WidthMm, placement.HeightMm)).ToList(),
            $"placements of cubby {cubby.Index}");
    }

    private static void AssertPilesGoWidestAtTheBottom(LayoutCubby cubby)
    {
        var piles = cubby.Placements
            .Where(placement => placement.Kind is PlacementKind.FlatBox or PlacementKind.OrphanExpansion)
            .GroupBy(placement => placement.XMm);

        foreach (var pile in piles)
        {
            var fromFloor = pile.OrderBy(placement => placement.YMm).ToList();

            for (var index = 1; index < fromFloor.Count; index++)
            {
                var below = fromFloor[index - 1];
                var above = fromFloor[index];

                above.WidthMm.Should().BeLessThanOrEqualTo(
                    below.WidthMm,
                    "game {0} must not overhang game {1} in cubby {2}", above.GameId, below.GameId, cubby.Index);

                if (above.WidthMm == below.WidthMm)
                {
                    above.HeightMm.Should().BeLessThanOrEqualTo(
                        below.HeightMm,
                        "of two boxes as wide as each other the thicker lies lower, game {0} over game {1} in cubby {2}",
                        above.GameId, below.GameId, cubby.Index);
                }
            }
        }
    }

    private static void AssertNoOverlap(IReadOnlyList<(int X, int Y, int Width, int Height)> rectangles, string what)
    {
        for (var first = 0; first < rectangles.Count; first++)
        {
            for (var second = first + 1; second < rectangles.Count; second++)
            {
                Intersects(rectangles[first], rectangles[second]).Should().BeFalse("{0} {1} and {2} may touch but not overlap", what, first, second);
            }
        }
    }

    private static bool Intersects((int X, int Y, int Width, int Height) a, (int X, int Y, int Width, int Height) b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
}

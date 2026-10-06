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
    /// cubby, placements in a cubby may touch but never overlap, and cubbies sit inside their section without overlapping.
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
            .Where(entry => entry.Placement.Kind is not (PlacementKind.ExpansionLayer or PlacementKind.MoreMarker))
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
            AssertFamilyAccountedFor(family.Key, family.Select(item => item.BggId).ToList(), standing, placed);
        }

        placed.Where(entry => entry.Placement.Kind == PlacementKind.ExpansionLayer)
            .Select(entry => entry.Placement.GameId)
            .Should().OnlyHaveUniqueItems("no expansion layer appears twice");
        placed.Where(entry => entry.Placement.FamilyId is { } familyId
                && !items.Any(item => item.BggId == familyId && item.Kind == ItemKind.Base))
            .Should().BeEmpty("a family always belongs to an owned base game");
    }

    private static void AssertFamilyAccountedFor(
        int baseId,
        IReadOnlyList<int> expansionIds,
        List<(int Section, int Cubby, Placement Placement)> standing,
        IReadOnlyList<(int Section, int Cubby, Placement Placement)> placed)
    {
        var baseEntry = standing.Single(entry => entry.Placement.GameId == baseId);
        var members = placed.Where(entry => entry.Placement.FamilyId == baseId
            && entry.Placement.Kind is PlacementKind.ExpansionLayer or PlacementKind.MoreMarker).ToList();
        var layers = members.Where(entry => entry.Placement.Kind == PlacementKind.ExpansionLayer).ToList();
        var markers = members.Where(entry => entry.Placement.Kind == PlacementKind.MoreMarker).ToList();

        members.Should().OnlyContain(
            entry => entry.Section == baseEntry.Section && entry.Cubby == baseEntry.Cubby,
            "layers and the marker of family {0} stand in the cubby of their base game", baseId);
        layers.Select(entry => entry.Placement.GameId).Should().OnlyContain(id => expansionIds.Contains(id));
        markers.Should().HaveCountLessThanOrEqualTo(1, "family {0} has at most one marker", baseId);
        (layers.Count + markers.Sum(entry => entry.Placement.MoreCount ?? 0))
            .Should().Be(expansionIds.Count, "family {0} counts each of its expansions once", baseId);
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

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

    private static void AssertEveryItemPlacedOnce(CabinetLayout layout, IReadOnlyList<CabinetItem> items)
    {
        var placedIds = layout.Sections
            .SelectMany(section => section.Cubbies)
            .SelectMany(cubby => cubby.Placements)
            .Select(placement => placement.GameId)
            .Order()
            .ToList();

        placedIds.Should().Equal(items.Select(item => item.BggId).Order(), "every item stands exactly once");
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

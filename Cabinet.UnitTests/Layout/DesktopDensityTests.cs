using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Pins how full the desktop cabinet is, next to the phone checks: on realistic collections no section but the last keeps a
/// bare shelf row, a collection of about sixty-five games has no bare row in the middle of any section, and a large
/// collection does not end in a run of nearly empty sections because its biggest boxes had too few cubbies to go to.
/// A section before the last holds at least 25 boxes: placing a series together packs one seeded collection a little less
/// tightly than game by game, which is why the floor sits below the 30 it had before series stood together.
/// </summary>
public class DesktopDensityTests
{
    private const int LargeCount = 400;
    private const int MediumCount = 65;
    private const int NonLastSectionFloor = 25;
    private const string SamplePrefix = "sample-";
    private const string SpikePrefix = "spike-";

    private static readonly int[] MediumSeeds = [1, 2, 3, 4, 5, 6, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112];
    private static readonly int[] LargeSeeds = [4242, 11, 12, 201, 202, 203, 204, 205, 206];

    private static readonly string[] MediumNames =
        [SamplePrefix + "65", .. MediumSeeds.Select(seed => SpikePrefix + MediumCount + "-" + seed)];

    private static readonly string[] LargeNames =
        [SamplePrefix + "400", .. LargeSeeds.Select(seed => SpikePrefix + LargeCount + "-" + seed)];

    public static TheoryData<string> MediumCollections => new(MediumNames);

    public static TheoryData<string> LargeCollections => new(LargeNames);

    public static TheoryData<string> AllCollections => new(MediumNames.Concat(LargeNames).ToArray());

    [Theory]
    [MemberData(nameof(AllCollections))]
    [Trait("Category", "Layout")]
    public void No_desktop_section_but_the_last_keeps_an_empty_row(string name)
    {
        var items = ItemsFor(name);

        var layout = CabinetLayoutEngine.Build(items, SectionDesigns.Desktop);
        var report = LayoutDensity.Measure(layout);

        LayoutAssertions.AssertValid(layout, items);
        report.MaxEmptyRows.Should().Be(0, "{0}: {1}", name, report);
    }

    [Theory]
    [MemberData(nameof(MediumCollections))]
    [Trait("Category", "Layout")]
    public void A_medium_collection_has_no_empty_row_in_the_middle_of_any_desktop_section(string name)
    {
        var layout = CabinetLayoutEngine.Build(ItemsFor(name), SectionDesigns.Desktop);

        LayoutDensity.HollowRows(layout).Should().OnlyContain(rows => rows == 0, "{0}: {1}", name, string.Join(", ", LayoutDensity.PlacementsPerSection(layout)));
    }

    [Theory]
    [MemberData(nameof(LargeCollections))]
    [Trait("Category", "Layout")]
    public void A_large_collection_does_not_end_in_near_empty_desktop_sections(string name)
    {
        var layout = CabinetLayoutEngine.Build(ItemsFor(name), SectionDesigns.Desktop);
        var placements = LayoutDensity.PlacementsPerSection(layout);

        placements.SkipLast(1).Should().OnlyContain(count => count >= NonLastSectionFloor, "{0}: placements per section {1}", name, string.Join(", ", placements));
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Fewer_than_twelve_games_fit_in_one_desktop_section()
    {
        foreach (var name in new[] { "0", "1", "5" })
        {
            SyntheticCollections.TryGetSample(name, out var items);

            CabinetLayoutEngine.Build(items, SectionDesigns.Desktop).Sections.Should().ContainSingle(name);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_medium_sample_takes_two_desktop_sections_and_the_large_sample_fewer_than_nine()
    {
        SyntheticCollections.TryGetSample("65", out var medium);
        SyntheticCollections.TryGetSample("400", out var large);

        CabinetLayoutEngine.Build(medium, SectionDesigns.Desktop).Sections.Should().HaveCount(2);
        CabinetLayoutEngine.Build(large, SectionDesigns.Desktop).Sections.Count.Should().BeLessThan(9);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Hollow_rows_count_only_bare_rows_above_a_used_row()
    {
        var layout = CabinetLayoutEngine.Build([], SectionDesigns.Desktop);

        LayoutDensity.HollowRows(layout).Should().Equal(0);
        LayoutDensity.PlacementsPerSection(layout).Should().Equal(0);
    }

    private static IReadOnlyList<CabinetItem> ItemsFor(string name)
    {
        if (name.StartsWith(SamplePrefix, StringComparison.Ordinal))
        {
            SyntheticCollections.TryGetSample(name[SamplePrefix.Length..], out var sample);

            return sample;
        }

        var parts = name[SpikePrefix.Length..].Split('-');

        return LayoutDensity.SpikeShaped(int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture));
    }
}

using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Pins how the last section is drawn only down to its last used shelf row: never fewer than two rows, every earlier
/// section whole, and the section height always matching the rows that are drawn.
/// </summary>
public class TrimmedSectionTests
{
    private static readonly LayoutOptions SpinesOnly = new(0, CoverStrategy.SizeWeighted, 6, 0);

    public static TheoryData<string, string> Samples
    {
        get
        {
            var data = new TheoryData<string, string>();

            foreach (var design in SectionDesigns.All)
            {
                foreach (var sample in SyntheticCollections.SampleNames)
                {
                    data.Add(design.Name, sample);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Samples))]
    [Trait("Category", "Layout")]
    public void The_last_section_ends_at_its_last_used_row_and_every_earlier_section_is_whole(string profile, string sample)
    {
        SectionDesigns.TryGet(profile, out var design);
        SyntheticCollections.TryGetSample(sample, out var items);

        var layout = CabinetLayoutEngine.Build(items, design!);

        foreach (var section in layout.Sections.Take(layout.Sections.Count - 1))
        {
            section.Cubbies.Should().HaveCount(design!.Cubbies.Count);
            section.HeightMm.Should().Be(design.InteriorHeightMm);
        }

        var last = layout.Sections[^1];
        var usedRows = RowsUsed(last, design!);
        var expectedRows = Math.Min(design!.Rows.Count, Math.Max(CabinetLayoutEngine.MinTrimmedRows, usedRows));

        RowTops(last).Should().HaveCount(expectedRows);
        last.HeightMm.Should().Be(HeightOfRows(design, expectedRows));
        last.Cubbies.Select(cubby => cubby.Index).Should().Equal(Enumerable.Range(0, last.Cubbies.Count));
        last.Cubbies.Should().HaveCount(design.Cubbies.Count(cubby => cubby.YMm <= TopOfRow(design, expectedRows - 1)));
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void An_empty_collection_shows_two_bare_rows()
    {
        var layout = CabinetLayoutEngine.Build([], SectionDesigns.Desktop);

        var section = layout.Sections.Should().ContainSingle().Subject;
        RowTops(section).Should().HaveCount(2);
        section.HeightMm.Should().Be(HeightOfRows(SectionDesigns.Desktop, 2));
        section.Cubbies.Should().OnlyContain(cubby => cubby.Placements.Count == 0);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_design_with_a_single_row_keeps_that_row()
    {
        var design = new SectionDesign("test", 300, 10, [new ShelfRow(300, [300])]) { StackColumnWidthMm = 100 };

        var layout = CabinetLayoutEngine.Build([], design, SpinesOnly);

        layout.Sections.Should().ContainSingle().Subject.Cubbies.Should().ContainSingle();
        layout.Sections[0].HeightMm.Should().Be(300);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_game_landing_in_a_trimmed_row_adds_the_row_and_moves_nothing()
    {
        var design = FourRowDesign();
        var four = Spines(4);

        var before = CabinetLayoutEngine.Build(four, design, SpinesOnly);
        var after = CabinetLayoutEngine.Build([.. four, SpineItem(5)], design, SpinesOnly);

        RowTops(before.Sections[0]).Should().HaveCount(2);
        RowTops(after.Sections[0]).Should().HaveCount(3);
        after.Sections[0].HeightMm.Should().BeGreaterThan(before.Sections[0].HeightMm);

        foreach (var cubby in before.Sections[0].Cubbies)
        {
            var grown = after.Sections[0].Cubbies.Single(candidate => candidate.Index == cubby.Index);

            grown.Placements.Should().BeEquivalentTo(cubby.Placements, options => options.WithStrictOrdering());
        }

        LayoutAssertions.ChangedCubbies(before, after).Should().ContainSingle();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Opening_a_new_section_gives_the_old_last_section_its_full_height_and_moves_nothing()
    {
        var design = FourRowDesign();
        var full = Spines(8);

        var before = CabinetLayoutEngine.Build(full, design, SpinesOnly);
        var after = CabinetLayoutEngine.Build([.. full, SpineItem(9)], design, SpinesOnly);

        before.Sections.Should().ContainSingle();
        after.Sections.Should().HaveCount(2);
        after.Sections[0].HeightMm.Should().Be(design.InteriorHeightMm);
        after.Sections[1].HeightMm.Should().Be(HeightOfRows(design, 2));

        foreach (var cubby in before.Sections[0].Cubbies)
        {
            after.Sections[0].Cubbies.Single(candidate => candidate.Index == cubby.Index).Placements
                .Should().BeEquivalentTo(cubby.Placements, options => options.WithStrictOrdering());
        }
    }

    private static SectionDesign FourRowDesign() =>
        new(
            "test",
            590,
            10,
            [
                new ShelfRow(280, [290, 290]),
                new ShelfRow(280, [290, 290]),
                new ShelfRow(280, [290, 290]),
                new ShelfRow(280, [290, 290]),
            ])
        {
            StackColumnWidthMm = 100,
        };

    private static List<CabinetItem> Spines(int count) =>
        Enumerable.Range(1, count).Select(SpineItem).ToList();

    private static CabinetItem SpineItem(int number) =>
        new(number, number, $"Invented Title {number}", ItemKind.Base, new BoxDimensions(100, 250, 150), []);

    private static List<int> RowTops(LayoutSection section) =>
        section.Cubbies.Select(cubby => cubby.YMm).Distinct().Order().ToList();

    private static int RowsUsed(LayoutSection section, SectionDesign design)
    {
        var tops = design.Cubbies.Select(cubby => cubby.YMm).Distinct().Order().ToList();
        var lowest = section.Cubbies.Where(cubby => cubby.Placements.Count > 0).Select(cubby => cubby.YMm).DefaultIfEmpty(-1).Max();

        return lowest < 0 ? 0 : tops.IndexOf(lowest) + 1;
    }

    private static int TopOfRow(SectionDesign design, int row) =>
        design.Rows.Take(row).Sum(item => item.HeightMm + design.FrameMm);

    private static int HeightOfRows(SectionDesign design, int rows) =>
        design.Rows.Take(rows).Sum(row => row.HeightMm) + (design.FrameMm * (rows - 1));
}

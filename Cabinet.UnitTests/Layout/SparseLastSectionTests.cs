using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Pins how the last section of a cabinet that does not balance its sections is drawn when it is sparse: it keeps only the
/// shelf rows that hold a game, at least two, so its boxes stand on its bottom shelf with no empty shelf above or between
/// them, while a last section filled from the top and every other section are drawn exactly as the plain rule gives. The
/// expected shape is worked out here from the design alone.
/// </summary>
[Trait("Category", "Layout")]
public class SparseLastSectionTests
{
    private const int ShortBoxMm = 250;

    private const int TallBoxMm = 350;

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

    [Fact]
    public void One_tall_box_stands_on_the_bottom_of_a_two_shelf_last_section()
    {
        var design = SectionDesigns.Phone;
        var item = new CabinetItem(1, 1, "Invented Tall Game", ItemKind.Base, new BoxDimensions(250, 360, 60), []);

        var layout = CabinetLayoutEngine.Build([item], design);

        var section = layout.Sections.Should().ContainSingle().Subject;
        var home = section.Cubbies.Single(cubby => cubby.Placements.Count > 0);
        var homeRow = RowOf(design, home.Index);

        homeRow.Should().BeGreaterThan(1, "the tall box fits only a lower shelf row, so rows above it would be empty");
        Kept(section, design).Should().Equal(homeRow - 1, homeRow);
        section.HeightMm.Should().Be(HeightOfRows(design, homeRow - 1, homeRow));
        home.YMm.Should().Be(design.Rows[homeRow - 1].HeightMm + design.FrameMm, "the box stands on the bottom shelf");
        LayoutAssertions.AssertValid(layout, [item]);
    }

    [Fact]
    public void Two_boxes_in_two_lower_rows_keep_exactly_those_two_rows()
    {
        var design = NarrowDesign();
        var items = new[] { TallItem(1), TallItem(2), TallItem(3) };

        var layout = CabinetLayoutEngine.Build(items, design, SpinesOnly);

        var section = layout.Sections.Should().ContainSingle().Subject;
        FilledRows(section, design).Should().Equal(2, 3);
        Kept(section, design).Should().Equal(2, 3);
        section.Cubbies.Select(cubby => cubby.YMm).Distinct().Order().Should().Equal(0, design.Rows[2].HeightMm + design.FrameMm);
        section.HeightMm.Should().Be(HeightOfRows(design, 2, 3));
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    public void Boxes_in_rows_zero_two_and_three_drop_the_empty_row_between_them()
    {
        var design = NarrowDesign();
        var items = new[] { ShortItem(1), TallItem(2), TallItem(3), TallItem(4) };

        var layout = CabinetLayoutEngine.Build(items, design, SpinesOnly);

        var section = layout.Sections.Should().ContainSingle().Subject;
        FilledRows(section, design).Should().Equal(0, 2, 3);
        Kept(section, design).Should().Equal(0, 2, 3);
        section.Cubbies.Select(cubby => cubby.YMm).Distinct().Order().Should().Equal(
            0,
            design.Rows[0].HeightMm + design.FrameMm,
            design.Rows[0].HeightMm + design.Rows[2].HeightMm + (2 * design.FrameMm));
        section.HeightMm.Should().Be(HeightOfRows(design, 0, 2, 3));
        section.Cubbies.Select(cubby => cubby.Index).Should().Equal(design.Cubbies.Where(cubby => RowOf(design, cubby.Index) != 1).Select(cubby => cubby.Index));
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    public void A_last_section_filled_from_the_top_is_drawn_as_before()
    {
        var design = NarrowDesign();
        var items = Enumerable.Range(1, 7).Select(ShortItem).ToList();

        var layout = CabinetLayoutEngine.Build(items, design, SpinesOnly);

        var section = layout.Sections.Should().ContainSingle().Subject;
        FilledRows(section, design).Should().Equal(0, 1, 2, 3);
        AssertDrawnFromTheTop(section, design);
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    public void Only_the_first_row_filled_is_drawn_as_before()
    {
        var design = NarrowDesign();
        var items = new[] { ShortItem(1) };

        var layout = CabinetLayoutEngine.Build(items, design, SpinesOnly);

        var section = layout.Sections.Should().ContainSingle().Subject;
        FilledRows(section, design).Should().Equal(0);
        Kept(section, design).Should().Equal(0, 1);
        AssertDrawnFromTheTop(section, design);
        LayoutAssertions.AssertValid(layout, items);
    }

    [Fact]
    public void A_cabinet_with_no_games_is_drawn_as_before()
    {
        var design = NarrowDesign();

        var layout = CabinetLayoutEngine.Build([], design, SpinesOnly);

        AssertDrawnFromTheTop(layout.Sections.Should().ContainSingle().Subject, design);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Only_the_last_section_of_an_unbalanced_build_changes(string profile, string sample)
    {
        SectionDesigns.TryGet(profile, out var design).Should().BeTrue();
        ArgumentNullException.ThrowIfNull(design);
        SyntheticCollections.TryGetSample(sample, out var items);

        var layout = CabinetLayoutEngine.Build(items, design, LayoutAssertions.SectionBySection);

        foreach (var section in layout.Sections.Take(layout.Sections.Count - 1))
        {
            section.HeightMm.Should().Be(design.InteriorHeightMm);
            section.Cubbies.Select(cubby => (cubby.Index, cubby.XMm, cubby.YMm)).Should().Equal(
                design.Cubbies.Select(cubby => (cubby.Index, cubby.XMm, cubby.YMm)));
        }

        var last = layout.Sections[^1];
        var kept = Kept(last, design);
        var filled = FilledRows(last, design);

        kept.Should().Contain(filled.Count > 0 ? filled : [kept[0]], "every game stays drawn");
        kept.Should().HaveCountGreaterThanOrEqualTo(Math.Min(CabinetLayoutEngine.MinTrimmedRows, design.Rows.Count));
        last.Cubbies.Select(cubby => cubby.Index).Should().Equal(
            design.Cubbies.Where(cubby => kept.Contains(RowOf(design, cubby.Index))).Select(cubby => cubby.Index));
        last.HeightMm.Should().Be(HeightOfRows(design, [.. kept]));

        if (filled.Count > 0 && kept.SequenceEqual(Enumerable.Range(0, kept.Count)))
        {
            AssertDrawnFromTheTop(last, design);
        }

        LayoutAssertions.AssertValid(layout, items);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("65")]
    [InlineData("400")]
    public void Every_balanced_desktop_section_keeps_the_rows_from_the_top(string sample)
    {
        var design = SectionDesigns.Desktop;
        SyntheticCollections.TryGetSample(sample, out var items);

        var layout = CabinetLayoutEngine.Build(items, design);

        foreach (var section in layout.Sections)
        {
            AssertDrawnFromTheTop(section, design);
        }
    }

    private static void AssertDrawnFromTheTop(LayoutSection section, SectionDesign design)
    {
        var lowest = FilledRows(section, design).DefaultIfEmpty(-1).Max();
        var rows = Math.Min(design.Rows.Count, Math.Max(CabinetLayoutEngine.MinTrimmedRows, lowest + 1));

        section.HeightMm.Should().Be(HeightOfRows(design, [.. Enumerable.Range(0, rows)]));
        section.Cubbies.Select(cubby => (cubby.Index, cubby.XMm, cubby.YMm, cubby.WidthMm, cubby.HeightMm)).Should().Equal(
            design.Cubbies.Where(cubby => cubby.YMm <= design.Rows.Take(rows - 1).Sum(row => row.HeightMm + design.FrameMm))
                .Select(cubby => (cubby.Index, cubby.XMm, cubby.YMm, cubby.WidthMm, cubby.HeightMm)));
    }

    private static SectionDesign NarrowDesign() =>
        new(
            "test",
            400,
            10,
            [
                new ShelfRow(280, [400]),
                new ShelfRow(280, [400]),
                new ShelfRow(380, [400]),
                new ShelfRow(380, [400]),
            ])
        {
            StackColumnWidthMm = 100,
            MaxSpineHeightMm = 370,
        };

    private static CabinetItem ShortItem(int number) =>
        new(number, number, $"Invented Short Game {number}", ItemKind.Base, new BoxDimensions(100, ShortBoxMm, 150), []);

    private static CabinetItem TallItem(int number) =>
        new(number, number, $"Invented Tall Game {number}", ItemKind.Base, new BoxDimensions(100, TallBoxMm, 150), []);

    private static int RowOf(SectionDesign design, int cubbyIndex)
    {
        var tops = design.Cubbies.Select(cubby => cubby.YMm).Distinct().Order().ToList();

        return tops.IndexOf(design.Cubbies[cubbyIndex].YMm);
    }

    private static List<int> FilledRows(LayoutSection section, SectionDesign design) =>
        [.. section.Cubbies.Where(cubby => cubby.Placements.Count > 0).Select(cubby => RowOf(design, cubby.Index)).Distinct().Order()];

    private static List<int> Kept(LayoutSection section, SectionDesign design) =>
        [.. section.Cubbies.Select(cubby => RowOf(design, cubby.Index)).Distinct().Order()];

    private static int HeightOfRows(SectionDesign design, params int[] rows) =>
        rows.Sum(row => design.Rows[row].HeightMm) + (design.FrameMm * (rows.Length - 1));
}

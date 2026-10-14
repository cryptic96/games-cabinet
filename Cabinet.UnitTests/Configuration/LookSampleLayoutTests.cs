using System.Text.Json;
using Cabinet.Domain.Layout;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.UnitTests.Configuration;

/// <summary>
/// Pins the shape of the hand-written sample layout the owner's look preview draws: it must read as a cabinet layout, show two rows of
/// three cubbies with nine boxes, one of them facing out and one cubby left empty, use tones that include the colours the preview needs
/// to show, hold no stack marker, stay inside its cubbies, and carry only invented titles.
/// </summary>
[Trait("Category", "Configuration")]
public sealed class LookSampleLayoutTests
{
    private const string RustBackground = "#b8481a";
    private const string TealBackground = "#17766f";
    private const string NavyBackground = "#23407a";
    private const string CreamBackground = "#efe3c2";

    private static readonly Lazy<CabinetLayout> Sample = new(Read);

    [Fact]
    public void The_sample_reads_as_a_cabinet_layout_with_one_section()
    {
        Sample.Value.Sections.Should().ContainSingle();
        Sample.Value.Profile.Should().Be("desktop");
    }

    [Fact]
    public void The_section_has_two_rows_of_three_cubbies_that_end_where_the_section_ends()
    {
        var section = Sample.Value.Sections[0];
        var rows = section.Cubbies.GroupBy(cubby => cubby.YMm).OrderBy(group => group.Key).ToList();

        section.Cubbies.Should().HaveCount(6);
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(row => row.Count() == 3);
        rows.Should().OnlyContain(row => row.Max(cubby => cubby.XMm + cubby.WidthMm) == section.WidthMm);
        rows.Max(row => row.Max(cubby => cubby.YMm + cubby.HeightMm)).Should().Be(section.HeightMm);
    }

    [Fact]
    public void The_cubbies_hold_nine_boxes_of_which_one_faces_out_and_one_cubby_is_empty()
    {
        var placements = Sample.Value.Sections[0].Cubbies.SelectMany(cubby => cubby.Placements).ToList();

        placements.Should().HaveCount(9);
        placements.Count(placement => placement.Kind == PlacementKind.Cover).Should().Be(1);
        placements.Count(placement => placement.Kind == PlacementKind.Spine).Should().Be(8);
        Sample.Value.Sections[0].Cubbies.Count(cubby => cubby.Placements.Count == 0).Should().Be(1);
    }

    [Fact]
    public void The_sample_holds_no_stack_marker_and_no_expansion()
    {
        var kinds = Sample.Value.Sections[0].Cubbies.SelectMany(cubby => cubby.Placements).Select(placement => placement.Kind).ToList();

        kinds.Should().NotContain([PlacementKind.MoreMarker, PlacementKind.ExpansionLayer, PlacementKind.ExpansionSpine, PlacementKind.OrphanExpansion]);
    }

    [Fact]
    public void The_palette_is_the_cabinets_own_and_the_boxes_use_rust_teal_navy_and_cream()
    {
        var used = Sample.Value.Sections[0].Cubbies
            .SelectMany(cubby => cubby.Placements)
            .Select(placement => Sample.Value.Palette[placement.ToneIndex].Background)
            .ToList();

        Sample.Value.Palette.Should().Equal(SpinePalette.Tones);
        used.Should().Contain([RustBackground, TealBackground, NavyBackground, CreamBackground]);
    }

    [Fact]
    public void Every_box_stands_inside_its_cubby_and_no_two_boxes_of_a_cubby_overlap()
    {
        foreach (var cubby in Sample.Value.Sections[0].Cubbies)
        {
            foreach (var placement in cubby.Placements)
            {
                placement.XMm.Should().BeGreaterThanOrEqualTo(0);
                placement.YMm.Should().BeGreaterThanOrEqualTo(0);
                (placement.XMm + placement.WidthMm).Should().BeLessThanOrEqualTo(cubby.WidthMm);
                (placement.YMm + placement.HeightMm).Should().BeLessThanOrEqualTo(cubby.HeightMm);
            }

            var ordered = cubby.Placements.OrderBy(placement => placement.XMm).ToList();

            for (var index = 1; index < ordered.Count; index++)
            {
                (ordered[index - 1].XMm + ordered[index - 1].WidthMm).Should().BeLessThanOrEqualTo(ordered[index].XMm);
            }
        }
    }

    [Fact]
    public void Every_box_has_a_distinct_game_and_entry_and_a_title_the_label_starts_with()
    {
        var placements = Sample.Value.Sections[0].Cubbies.SelectMany(cubby => cubby.Placements).ToList();

        placements.Select(placement => placement.GameId).Should().OnlyHaveUniqueItems();
        placements.Select(placement => placement.EntryId).Should().OnlyHaveUniqueItems();
        placements.Should().OnlyContain(placement => !string.IsNullOrWhiteSpace(placement.Title) && placement.Label == placement.Title);
    }

    [Fact]
    public void Every_title_of_the_sample_is_one_of_the_invented_names_and_nothing_else()
    {
        var sampleTitles = Sample.Value.Sections[0].Cubbies.SelectMany(cubby => cubby.Placements).Select(placement => placement.Title).ToList();

        sampleTitles.Should().BeEquivalentTo(
        [
            "Lantern & Harbour",
            "Quiet Quarry",
            "Night Market",
            "Copper Canal",
            "Orchard Rules",
            "Paper Lanterns",
            "Tidewater",
            "Salt and Sparrow",
            "Brass Garden",
        ]);
    }

    private static CabinetLayout Read()
    {
        var path = Path.Combine(RepositoryPaths.ServiceDirectory(), "wwwroot", "js", "owner-look-sample.json");

        return JsonSerializer.Deserialize<CabinetLayout>(File.ReadAllText(path), LayoutJson.Options)
            ?? throw new InvalidOperationException("The sample layout is empty.");
    }
}

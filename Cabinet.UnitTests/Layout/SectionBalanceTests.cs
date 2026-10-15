using System.Text.Json;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Pins the even spread of games over the desktop sections: by default every desktop section of the samples is drawn
/// within one shelf row of every other and every game stands exactly once; the phone cabinet never balances; and with the setting off the
/// desktop cabinet is arranged section by section as the games come, with a short last section.
/// </summary>
public class SectionBalanceTests
{
    private static readonly LayoutOptions Balanced = LayoutOptions.Default;

    private static readonly LayoutOptions Plain = LayoutAssertions.SectionBySection;

    public static TheoryData<string> BalancedSamples => new() { "12", "65", "400" };

    public static TheoryData<string> AllSamples
    {
        get
        {
            var data = new TheoryData<string>();

            foreach (var sample in SyntheticCollections.SampleNames)
            {
                data.Add(sample);
            }

            return data;
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_balance_setting_is_on_by_default_and_the_desktop_design_is_the_only_one_that_asks_for_it()
    {
        LayoutOptions.Default.BalanceSections.Should().BeTrue();
        SectionDesigns.Desktop.BalancesSections.Should().BeTrue();
        SectionDesigns.Phone.BalancesSections.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Turning_the_balance_setting_off_changes_the_fingerprint_and_leaving_it_on_does_not()
    {
        Plain.Fingerprint.Should().NotBe(LayoutOptions.Default.Fingerprint);
        (LayoutOptions.Default with { BalanceSections = true }).Fingerprint.Should().Be(LayoutOptions.Default.Fingerprint);
    }

    [Theory]
    [MemberData(nameof(BalancedSamples))]
    [Trait("Category", "Layout")]
    public void Every_desktop_section_is_drawn_within_one_shelf_row_of_every_other(string sample)
    {
        SyntheticCollections.TryGetSample(sample, out var items);

        var layout = CabinetLayoutEngine.Build(items, SectionDesigns.Desktop, Balanced);

        var rows = LayoutAssertions.RowsDrawnPerSection(layout);

        (rows.Max() - rows.Min()).Should().BeLessThanOrEqualTo(1, "the drawn rows per section are {0}", string.Join(", ", rows));
        LayoutAssertions.AssertValid(layout, items);
    }

    [Theory]
    [MemberData(nameof(BalancedSamples))]
    [Trait("Category", "Layout")]
    public void Balancing_never_needs_more_sections_than_the_plain_arrangement(string sample)
    {
        SyntheticCollections.TryGetSample(sample, out var items);

        var plain = CabinetLayoutEngine.Build(items, SectionDesigns.Desktop, Plain);
        var balanced = CabinetLayoutEngine.Build(items, SectionDesigns.Desktop, Balanced);

        balanced.Sections.Count.Should().BeLessThanOrEqualTo(plain.Sections.Count);
    }

    [Theory]
    [MemberData(nameof(BalancedSamples))]
    [Trait("Category", "Layout")]
    public void Balancing_keeps_every_game_in_the_cabinet_exactly_once(string sample)
    {
        SyntheticCollections.TryGetSample(sample, out var items);

        var balanced = CabinetLayoutEngine.Build(items, SectionDesigns.Desktop, Balanced);

        LayoutAssertions.PlacementsWithPosition(balanced)
            .Where(entry => entry.Placement.Kind is PlacementKind.Spine or PlacementKind.Cover or PlacementKind.FlatBox or PlacementKind.OrphanExpansion)
            .Select(entry => entry.Placement.GameId)
            .Order()
            .Should().Equal(
                items.Where(item => item.Kind == ItemKind.Base || LayoutAssertions.OwnedParentOf(item, items) is null)
                    .Select(item => item.BggId)
                    .Order());
    }

    [Theory]
    [MemberData(nameof(BalancedSamples))]
    [Trait("Category", "Layout")]
    public void Balancing_keeps_every_series_running_on_without_leaving_a_cubby_out(string sample)
    {
        SyntheticCollections.TryGetSample(sample, out var items);

        var plain = CabinetLayoutEngine.Build(items, SectionDesigns.Desktop, Plain);
        var balanced = CabinetLayoutEngine.Build(items, SectionDesigns.Desktop, Balanced);

        LayoutAssertions.SeriesGaps(balanced, items)
            .Should().HaveCountLessThanOrEqualTo(LayoutAssertions.SeriesGaps(plain, items).Count);
    }

    [Theory]
    [MemberData(nameof(AllSamples))]
    [Trait("Category", "Layout")]
    public void The_phone_cabinet_is_the_same_with_the_balance_setting_on_or_off(string sample)
    {
        SyntheticCollections.TryGetSample(sample, out var items);

        var plain = CabinetLayoutEngine.Build(items, SectionDesigns.Phone, Plain);
        var balanced = CabinetLayoutEngine.Build(items, SectionDesigns.Phone, Balanced);

        Serialize(balanced.Sections).Should().Be(Serialize(plain.Sections));
    }

    [Theory]
    [MemberData(nameof(AllSamples))]
    [Trait("Category", "Layout")]
    public void Every_sample_stays_valid_on_the_desktop_with_the_balance_setting_on(string sample)
    {
        SyntheticCollections.TryGetSample(sample, out var items);

        LayoutAssertions.AssertValid(CabinetLayoutEngine.Build(items, SectionDesigns.Desktop, Balanced), items);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_collection_that_fits_one_section_is_unchanged_by_balancing()
    {
        SyntheticCollections.TryGetSample("5", out var items);

        var plain = CabinetLayoutEngine.Build(items, SectionDesigns.Desktop, Plain);
        var balanced = CabinetLayoutEngine.Build(items, SectionDesigns.Desktop, Balanced);

        Serialize(balanced.Sections).Should().Be(Serialize(plain.Sections));
    }

    private static string Serialize(IReadOnlyList<LayoutSection> sections) => JsonSerializer.Serialize(sections, LayoutJson.Options);
}

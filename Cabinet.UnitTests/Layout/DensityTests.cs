using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Pins how full the phone cabinet is: on realistic collections no section but the last keeps more than one shelf row
/// without a placement, and a large collection does not need a long tower of sections.
/// </summary>
public class DensityTests
{
    private const int MaxEmptyRows = 1;
    private const int LargeSampleSectionLimit = 12;
    private const int SpikeSeed = 4242;
    private const int SpikeCount = 400;
    private const string SpikeName = "spike-shaped";
    private const int EarlierMixPhoneSectionTotal = 55;
    private const int ShareDefault = 25;
    private const int ShareServer = 33;

    /// <summary>The phone sections of the realistic mix of sixty-five games, seeds one to twelve, at a cover share of 25 and of 33 percent.</summary>
    private static readonly (int At25, int At33)[] MixPhoneSections =
    [
        (2, 2), (2, 3), (3, 2), (2, 3), (2, 2), (2, 3), (2, 2), (3, 3), (2, 2), (2, 2), (2, 2), (2, 2),
    ];

    public static TheoryData<string> Collections => new("65", "400", SpikeName);

    [Theory]
    [MemberData(nameof(Collections))]
    [Trait("Category", "Layout")]
    public void No_phone_section_but_the_last_keeps_more_than_one_empty_row(string name)
    {
        var items = ItemsFor(name);

        var layout = CabinetLayoutEngine.Build(items, SectionDesigns.Phone);
        var report = LayoutDensity.Measure(layout);

        TestContext.Current.SendDiagnosticMessage($"{name}: {report}");
        LayoutAssertions.AssertValid(layout, items);
        report.MaxEmptyRows.Should().BeLessThanOrEqualTo(MaxEmptyRows, "{0}: {1}", name, report);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_large_sample_takes_fewer_than_twelve_phone_sections()
    {
        SyntheticCollections.TryGetSample("400", out var items);

        var report = LayoutDensity.Measure(CabinetLayoutEngine.Build(items, SectionDesigns.Phone));

        report.Sections.Should().BeLessThan(LargeSampleSectionLimit, "{0}", report);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_realistic_mix_does_not_add_phone_sections()
    {
        var measured = MixPhoneSections
            .Select((_, index) => (Seed: index + 1, Sections: new[] { ShareDefault, ShareServer }
                .Select(share => CabinetLayoutEngine.Build(
                    LayoutDensity.ItemsFor($"mix-65-{index + 1}"),
                    SectionDesigns.Phone,
                    LayoutOptions.Default with { CoverSharePercent = share }).Sections.Count)
                .ToArray()))
            .ToList();

        measured.Select(entry => (entry.Sections[0], entry.Sections[1])).Should().Equal(MixPhoneSections);
        measured.Sum(entry => entry.Sections.Sum()).Should().BeLessThanOrEqualTo(EarlierMixPhoneSectionTotal, "the phone takes no more sections in all than it did before families faced out and continued, series stood together and the desktop was retuned");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Fewer_than_twelve_games_fit_in_one_phone_section()
    {
        foreach (var name in new[] { "0", "1", "5" })
        {
            SyntheticCollections.TryGetSample(name, out var items);

            CabinetLayoutEngine.Build(items, SectionDesigns.Phone).Sections.Should().ContainSingle(name);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_measure_of_a_single_section_has_no_earlier_sections_to_count()
    {
        var layout = CabinetLayoutEngine.Build([], SectionDesigns.Phone);

        LayoutDensity.Measure(layout).Sections.Should().Be(1);
        LayoutDensity.Measure(layout).EmptyRows.Should().BeEmpty();
        LayoutDensity.Measure(layout).MaxEmptyRows.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_spike_shaped_collection_is_the_same_for_the_same_seed_and_uses_the_recorded_spread()
    {
        var first = LayoutDensity.SpikeShaped(SpikeSeed, SpikeCount);
        var second = LayoutDensity.SpikeShaped(SpikeSeed, SpikeCount);

        first.Should().BeEquivalentTo(second, options => options.WithStrictOrdering());
        first.Should().HaveCount(SpikeCount);
        first.Select(item => item.Box.HeightMm).Distinct().Count().Should().BeGreaterThan(50);
        first.Should().OnlyContain(item => item.Box.HeightMm >= item.Box.WidthMm);
    }

    private static IReadOnlyList<CabinetItem> ItemsFor(string name)
    {
        if (name == SpikeName)
        {
            return LayoutDensity.SpikeShaped(SpikeSeed, SpikeCount);
        }

        SyntheticCollections.TryGetSample(name, out var items);

        return items;
    }
}

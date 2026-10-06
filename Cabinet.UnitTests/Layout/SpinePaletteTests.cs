using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>Pins the placeholder colour table, its readability and the per-game tone and pattern picks.</summary>
public class SpinePaletteTests
{
    private const double MinimumContrast = 4.5;
    private const int ChannelCount = 3;

    [Fact]
    [Trait("Category", "Layout")]
    public void The_palette_holds_at_least_twelve_tones()
    {
        SpinePalette.Tones.Count.Should().BeGreaterThanOrEqualTo(12);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Every_text_colour_is_white_or_the_dark_ink()
    {
        SpinePalette.Tones.Select(tone => tone.Text).Should().OnlyContain(text => text == "#ffffff" || text == "#2a1a10");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Every_background_and_text_pair_reaches_the_minimum_contrast()
    {
        foreach (var tone in SpinePalette.Tones)
        {
            Contrast(Rgb(tone.Background), Rgb(tone.Text)).Should().BeGreaterThanOrEqualTo(MinimumContrast, tone.Background);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_pattern_count_is_six()
    {
        SpinePalette.PatternCount.Should().Be(6);
    }

    [Theory]
    [InlineData("65")]
    [InlineData("400")]
    [Trait("Category", "Layout")]
    public void Every_placement_carries_a_tone_and_pattern_inside_their_tables(string sample)
    {
        SyntheticCollections.TryGetSample(sample, out var items).Should().BeTrue();

        var layout = CabinetLayoutEngine.Build(items, SectionDesigns.Desktop);
        var placements = layout.Sections.SelectMany(section => section.Cubbies).SelectMany(cubby => cubby.Placements).ToList();

        placements.Should().NotBeEmpty();
        placements.Should().OnlyContain(placement => placement.ToneIndex >= 0 && placement.ToneIndex < layout.Palette.Count);
        placements.Should().OnlyContain(placement => placement.PatternIndex >= 0 && placement.PatternIndex < SpinePalette.PatternCount);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_layout_carries_the_palette_that_the_picks_index_into()
    {
        var layout = CabinetLayoutEngine.Build([], SectionDesigns.Desktop);

        layout.Palette.Should().Equal(SpinePalette.Tones);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_game_keeps_its_tone_and_pattern_in_two_different_collections()
    {
        var items = SyntheticCollections.Random(11, 60);
        var withoutSome = items.Where((_, index) => index % 3 != 0).ToList();
        var first = CabinetLayoutEngine.Build(items, SectionDesigns.Desktop);
        var second = CabinetLayoutEngine.Build(withoutSome, SectionDesigns.Desktop);

        var firstPicks = PicksByGame(first);
        var secondPicks = PicksByGame(second);

        secondPicks.Should().NotBeEmpty();

        foreach (var (gameId, picks) in secondPicks)
        {
            firstPicks[gameId].Should().Be(picks, "game {0} must not change colour when its neighbours change", gameId);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Tone_and_pattern_follow_only_the_game_identifier()
    {
        var layout = CabinetLayoutEngine.Build(SyntheticCollections.Random(5, 40), SectionDesigns.Desktop);

        foreach (var (gameId, picks) in PicksByGame(layout))
        {
            picks.Should().Be((SpinePalette.ToneFor(gameId), SpinePalette.PatternFor(gameId)));
        }
    }

    private static Dictionary<int, (int Tone, int Pattern)> PicksByGame(CabinetLayout layout) =>
        layout.Sections
            .SelectMany(section => section.Cubbies)
            .SelectMany(cubby => cubby.Placements)
            .ToDictionary(placement => placement.GameId, placement => (placement.ToneIndex, placement.PatternIndex));

    private static int[] Rgb(string hex) =>
        Enumerable.Range(0, ChannelCount).Select(channel => Convert.ToInt32(hex.Substring(1 + (channel * 2), 2), 16)).ToArray();

    private static double Contrast(int[] first, int[] second)
    {
        var a = Luminance(first);
        var b = Luminance(second);
        var (lighter, darker) = a >= b ? (a, b) : (b, a);

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(int[] rgb)
    {
        var linear = rgb.Select(channel =>
        {
            var value = channel / 255.0;

            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }).ToArray();

        return (0.2126 * linear[0]) + (0.7152 * linear[1]) + (0.0722 * linear[2]);
    }
}

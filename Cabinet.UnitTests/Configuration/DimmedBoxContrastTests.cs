using System.Globalization;
using System.Text.RegularExpressions;
using Cabinet.Domain.Layout;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.UnitTests.Configuration;

/// <summary>
/// Holds the readability of a box in shadow to documented floors, worked out from the shipped style sheets. A box that does not match
/// what the visitor is looking for stands under a dark veil with a lit rim: its label is deliberately less readable than a lit box's,
/// so the state is an exception with measured limits and not a skipped check. The floors are the lowest the label may fall to, how
/// readable it gets while the pointer rests on the box, and how well the box and its rim still stand out from every back.
/// </summary>
[Trait("Category", "Configuration")]
public partial class DimmedBoxContrastTests
{
    private const double VeilAlphaLowest = 0.5;
    private const double VeilAlphaHighest = 0.7;
    private const double DimmedLabelFloor = 2.0;
    private const double PeekLabelFloor = 4.5;
    private const double ShadedPeekLabelFloor = 3.5;
    private const double RimAgainstBackFloor = 1.5;
    private const double BodyAgainstBirchBackFloor = 1.5;

    private static readonly Lazy<StyleSheetTokens> Sheets = new(() =>
        StyleSheetTokens.FromFiles(RepositoryPaths.ServiceDirectory(), "cabinet-wood.css", "cabinet-modifiers.css"));

    private static readonly Lazy<string> ModifiersText = new(() =>
        File.ReadAllText(Path.Combine(RepositoryPaths.ServiceDirectory(), "wwwroot", "css", "cabinet-modifiers.css")));

    /// <summary>The woods whose back is dark, so the rim of a veiled box is what sets the box apart from it.</summary>
    public static TheoryData<string, string> DarkBacks { get; } = new()
    {
        { "brown", "wood" },
        { "oak", "wood" },
        { "walnut", "wood" },
        { "mahogany", "wood" },
        { "brown", "painted" },
        { "birch", "painted" },
    };

    [Fact]
    public void The_veil_strength_stays_in_its_range()
    {
        var root = Sheets.Value.ForWood("brown");

        StyleSheetTokens.Number(root, "--veil-alpha").Should().BeInRange(VeilAlphaLowest, VeilAlphaHighest, "a silent change of the veil is caught here");
        StyleSheetTokens.Number(root, "--veil-peek-alpha").Should().BeLessThan(StyleSheetTokens.Number(root, "--veil-alpha"), "the peek is a thinner veil");
    }

    [Fact]
    public void A_dimmed_label_stays_above_its_floor_and_the_range_is_recorded()
    {
        var readings = SpinePalette.Tones
            .Select(tone => (Tone: tone, Ratio: LabelContrast(tone, VeilAlpha(), 0)))
            .ToList();

        TestContext.Current.TestOutputHelper?.WriteLine(
            "Dimmed label contrast at veil " + VeilAlpha().ToString("0.00", CultureInfo.InvariantCulture)
            + ": " + readings.Min(reading => reading.Ratio).ToString("0.00", CultureInfo.InvariantCulture)
            + " to " + readings.Max(reading => reading.Ratio).ToString("0.00", CultureInfo.InvariantCulture)
            + " (" + string.Join(", ", readings.Select(reading => reading.Tone.Background + " " + reading.Ratio.ToString("0.00", CultureInfo.InvariantCulture))) + ")");

        foreach (var (tone, ratio) in readings)
        {
            ratio.Should().BeGreaterThanOrEqualTo(DimmedLabelFloor, $"the label of the {tone.Background} box under the veil");
        }
    }

    [Fact]
    public void A_label_under_the_peek_reads_with_no_shelf_shade_and_stays_readable_under_the_shade_of_the_shelf_above()
    {
        var peek = PeekAlpha();
        var shelfShade = ShelfShadeAlpha();

        foreach (var tone in SpinePalette.Tones)
        {
            LabelContrast(tone, peek, 0).Should().BeGreaterThanOrEqualTo(PeekLabelFloor, $"the {tone.Background} label at the peek with no shelf shade");
            LabelContrast(tone, peek, shelfShade).Should().BeGreaterThanOrEqualTo(ShadedPeekLabelFloor, $"the {tone.Background} label at the peek in the worst shaded band");
        }
    }

    [Theory]
    [MemberData(nameof(DarkBacks))]
    public void The_lit_rim_of_a_veiled_box_stands_out_from_the_back(string wood, string backs)
    {
        var tokens = WithBacks(Sheets.Value.ForWood(wood), backs);
        var back = StyleSheetTokens.Colour(tokens, "--back");
        var veil = StyleSheetTokens.Channels(tokens, "--box-shade-rgb");
        var (rimColour, rimAlpha) = Rim();

        foreach (var tone in SpinePalette.Tones)
        {
            var body = StyleSheetTokens.Over(veil, ParseHex(tone.Background), VeilAlpha());
            var rim = StyleSheetTokens.Over(rimColour, body, rimAlpha);

            SpineColour.ContrastRatio(rim, back).Should().BeGreaterThanOrEqualTo(RimAgainstBackFloor, $"the rim of the {tone.Background} box against {wood}'s {backs} back");
        }
    }

    [Fact]
    public void A_veiled_box_stays_apart_from_the_birch_back()
    {
        var tokens = Sheets.Value.ForWood("birch");
        var back = StyleSheetTokens.Colour(tokens, "--back");
        var veil = StyleSheetTokens.Channels(tokens, "--box-shade-rgb");

        foreach (var tone in SpinePalette.Tones)
        {
            var body = StyleSheetTokens.Over(veil, ParseHex(tone.Background), VeilAlpha());

            SpineColour.ContrastRatio(body, back).Should().BeGreaterThanOrEqualTo(BodyAgainstBirchBackFloor, $"the veiled {tone.Background} box against the birch back");
        }
    }

    [Fact]
    public void The_light_layer_stays_under_the_boxes_and_under_the_cap()
    {
        var root = Sheets.Value.ForWood("brown");
        var cap = SpinePalette.MaxShadePercent / 100.0;

        StyleSheetTokens.Number(root, "--lamp-front").Should().Be(0, "the light layer stays under the boxes and lights no veiled face");

        foreach (var depth in new[] { "flat", "shaded", "deep" })
        {
            ShadeAlphaOf(depth).Should().BeLessThanOrEqualTo(cap, $"{depth} shades a box no more than the cap every floor above is worked out under");
        }
    }

    [Fact]
    public void The_veil_rim_is_the_one_the_floors_are_worked_out_with()
    {
        var (colour, alpha) = Rim();

        colour.Should().Be(new RgbColour(255, 255, 255));
        alpha.Should().BeGreaterThan(0);
    }

    private static double VeilAlpha() => StyleSheetTokens.Number(Sheets.Value.ForWood("brown"), "--veil-alpha");

    private static double PeekAlpha() => StyleSheetTokens.Number(Sheets.Value.ForWood("brown"), "--veil-peek-alpha");

    private static double ShelfShadeAlpha() => ShadeAlphaOf("shaded");

    private static double ShadeAlphaOf(string depth)
    {
        var selector = depth == "shaded" ? ".cabinet" : $"[data-depth=\"{depth}\"] .cabinet";

        return StyleSheetTokens.Number(Sheets.Value.Block(selector), "--shade-alpha");
    }

    private static double LabelContrast(PaletteTone tone, double veilAlpha, double shelfShadeAlpha)
    {
        var veil = StyleSheetTokens.Channels(Sheets.Value.ForWood("brown"), "--box-shade-rgb");
        var background = Shaded(veil, ParseHex(tone.Background), veilAlpha, shelfShadeAlpha);
        var text = Shaded(veil, ParseHex(tone.Text), veilAlpha, shelfShadeAlpha);

        return SpineColour.ContrastRatio(text, background);
    }

    private static RgbColour Shaded(RgbColour shade, RgbColour colour, double veilAlpha, double shelfShadeAlpha)
    {
        var veiled = StyleSheetTokens.Over(shade, colour, veilAlpha);

        return shelfShadeAlpha <= 0 ? veiled : StyleSheetTokens.Over(shade, veiled, shelfShadeAlpha);
    }

    private static (RgbColour Colour, double Alpha) Rim()
    {
        var match = RimPattern().Match(ModifiersText.Value);

        match.Success.Should().BeTrue("the veil draws a lit rim on its own node");

        return (
            new RgbColour(
                byte.Parse(match.Groups["r"].Value, CultureInfo.InvariantCulture),
                byte.Parse(match.Groups["g"].Value, CultureInfo.InvariantCulture),
                byte.Parse(match.Groups["b"].Value, CultureInfo.InvariantCulture)),
            double.Parse(match.Groups["a"].Value, CultureInfo.InvariantCulture));
    }

    [GeneratedRegex(@"\.placement-veil\s*\{[^}]*box-shadow:\s*inset 1px 1px 0 rgb\((?<r>\d+) (?<g>\d+) (?<b>\d+) / (?<a>[0-9.]+)\)")]
    private static partial Regex RimPattern();

    private static IReadOnlyDictionary<string, string> WithBacks(IReadOnlyDictionary<string, string> wood, string backs)
    {
        if (backs == "wood")
        {
            return wood;
        }

        var merged = new Dictionary<string, string>(wood, StringComparer.Ordinal);

        foreach (var (name, value) in Sheets.Value.Block($"[data-backs=\"{backs}\"]"))
        {
            merged[name] = value;
        }

        return merged;
    }

    private static RgbColour ParseHex(string hex)
    {
        RgbColour.TryParseHex(hex, out var colour).Should().BeTrue($"{hex} is a palette colour");

        return colour;
    }
}

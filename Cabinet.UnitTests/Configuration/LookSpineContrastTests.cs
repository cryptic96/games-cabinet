using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.UnitTests.Configuration;

/// <summary>
/// Pins what the lights and the depth do to the boxes, from the shipped style sheets. Every palette tone keeps readable text
/// under every light and depth, because the lamp sits under the boxes and nothing but the shade of the shelf above falls on a box
/// face. And every back keeps the boxes visible against it, bare, under the brightest part of the lamp and in its faint tail, so
/// a change to a back or to the strength of the lamp that would make a box vanish into its cubby fails here.
/// </summary>
[Trait("Category", "Configuration")]
public class LookSpineContrastTests
{
    private const double TextContrast = 4.5;
    private const double VisibilityFloor = 1.3;
    private const double AcceptedSlack = 0.02;
    private const double LampPeakAlpha = 0.55;
    private const double LampTailAlpha = 0.05;
    private const string FlatDepth = "flat";
    private const string ShadedDepth = "shaded";
    private const string DeepDepth = "deep";

    private static readonly Lazy<StyleSheetTokens> Sheets = new(() =>
        StyleSheetTokens.FromFiles(RepositoryPaths.ServiceDirectory(), "cabinet-wood.css", "cabinet-modifiers.css"));

    private static readonly Lazy<string> ModifiersText = new(() =>
        File.ReadAllText(Path.Combine(RepositoryPaths.ServiceDirectory(), "wwwroot", "css", "cabinet-modifiers.css")));

    private static readonly string[] Lights = ["off", "on", "evenings (lit)"];

    private static readonly string[] Depths = [FlatDepth, ShadedDepth, DeepDepth];

    /// <summary>The 108 cases: every palette tone under every light and every depth.</summary>
    public static TheoryData<int, string, string> ToneLightDepthCases { get; } = BuildToneCases();

    /// <summary>The backs the boxes stand against, each with the accepted contrast of the weakest tone: bare, at the lamp's peak and in its tail.</summary>
    public static TheoryData<string, string, double, double, double> BackCases { get; } = new()
    {
        { "brown", "wood", 1.66, 1.43, 1.49 },
        { "walnut", "wood", 1.71, 1.41, 1.53 },
        { "oak", "wood", 1.70, 1.42, 1.53 },
        { "birch", "wood", 1.49, 1.39, 1.49 },
        { "mahogany", "wood", 1.85, 1.50, 1.67 },
        { "brown", "painted", 1.66, 1.42, 1.49 },
        { "birch", "painted", 1.66, 1.42, 1.49 },
    };

    [Theory]
    [MemberData(nameof(ToneLightDepthCases))]
    public void A_palette_tone_keeps_readable_text_under_the_shade_of_every_depth_and_the_lamp_of_every_light(int toneIndex, string lights, string depth)
    {
        var root = Sheets.Value.ForWood("brown");
        var tone = SpinePalette.Tones[toneIndex];
        var lampFront = lights == "off" ? 0 : StyleSheetTokens.Number(root, "--lamp-front");
        var shadeAlpha = ShadeAlphaOf(depth);
        var lamp = StyleSheetTokens.Channels(root, "--lamp-rgb");
        var shade = StyleSheetTokens.Channels(root, "--box-shade-rgb");

        var background = OnTheBox(ParseHex(tone.Background), lamp, lampFront, shade, shadeAlpha);
        var text = OnTheBox(ParseHex(tone.Text), lamp, lampFront, shade, shadeAlpha);

        SpineColour.ContrastRatio(text, background).Should().BeGreaterThanOrEqualTo(TextContrast, $"{tone.Background} with {tone.Text} under {depth} with the lights {lights}");
    }

    [Fact]
    public void The_lamp_does_not_reach_a_box_face_and_no_depth_shades_a_box_more_than_the_palette_allows()
    {
        var root = Sheets.Value.ForWood("brown");
        var cap = SpinePalette.MaxShadePercent / 100.0;

        StyleSheetTokens.Number(root, "--lamp-front").Should().Be(0);

        foreach (var depth in Depths)
        {
            ShadeAlphaOf(depth).Should().BeLessThanOrEqualTo(cap, $"{depth} shades the boxes no more than the palette's cap");
        }

        ShadeAlphaOf(FlatDepth).Should().Be(0);
        ShadeAlphaOf(ShadedDepth).Should().Be(cap);
    }

    [Fact]
    public void The_pool_of_the_lamp_is_painted_with_the_strengths_the_visibility_check_uses()
    {
        ModifiersText.Value.Should().Contain($"{LampPeakAlpha:0.00} * var(--pool-k)");
        ModifiersText.Value.Should().Contain($"{LampTailAlpha:0.00} * var(--pool-k)");
    }

    [Theory]
    [MemberData(nameof(BackCases))]
    public void The_weakest_box_stays_visible_against_the_back_bare_at_the_peak_of_the_lamp_and_in_its_tail(
        string wood,
        string backs,
        double acceptedBare,
        double acceptedPeak,
        double acceptedTail)
    {
        var tokens = WithBacks(Sheets.Value.ForWood(wood), backs);
        var back = StyleSheetTokens.Colour(tokens, "--back");
        var lamp = StyleSheetTokens.Channels(tokens, "--lamp-rgb");
        var poolStrength = StyleSheetTokens.Number(tokens, "--pool-k");
        var label = $"{wood} with {backs} backs";

        var bare = WeakestBoxAgainst(back);
        var peak = WeakestBoxAgainst(StyleSheetTokens.Over(lamp, back, LampPeakAlpha * poolStrength));
        var tail = WeakestBoxAgainst(StyleSheetTokens.Over(lamp, back, LampTailAlpha * poolStrength));

        bare.Should().BeGreaterThanOrEqualTo(acceptedBare - AcceptedSlack, $"{label}, bare");
        peak.Should().BeGreaterThanOrEqualTo(acceptedPeak - AcceptedSlack, $"{label}, at the lamp's peak");
        tail.Should().BeGreaterThanOrEqualTo(acceptedTail - AcceptedSlack, $"{label}, in the lamp's tail");
        new[] { bare, peak, tail }.Should().OnlyContain(ratio => ratio >= VisibilityFloor, $"{label}: no value under {VisibilityFloor}");
    }

    private static TheoryData<int, string, string> BuildToneCases()
    {
        var data = new TheoryData<int, string, string>();

        for (var tone = 0; tone < SpinePalette.Tones.Count; tone++)
        {
            foreach (var lights in Lights)
            {
                foreach (var depth in Depths)
                {
                    data.Add(tone, lights, depth);
                }
            }
        }

        return data;
    }

    private static double ShadeAlphaOf(string depth)
    {
        var selector = depth == ShadedDepth ? ".cabinet" : $"[data-depth=\"{depth}\"] .cabinet";

        return StyleSheetTokens.Number(Sheets.Value.Block(selector), "--shade-alpha");
    }

    private static RgbColour OnTheBox(RgbColour colour, RgbColour lamp, double lampFront, RgbColour shade, double shadeAlpha) =>
        StyleSheetTokens.Over(shade, StyleSheetTokens.Over(lamp, colour, lampFront), shadeAlpha);

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

    private static double WeakestBoxAgainst(RgbColour back) =>
        SpinePalette.Tones.Min(tone => SpineColour.ContrastRatio(ParseHex(tone.Background), back));

    private static RgbColour ParseHex(string hex)
    {
        RgbColour.TryParseHex(hex, out var colour).Should().BeTrue($"{hex} is a palette colour");

        return colour;
    }
}

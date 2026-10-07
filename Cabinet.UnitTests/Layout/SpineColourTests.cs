using Cabinet.Domain.Layout;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>Pins how a colour taken from box art becomes a legible spine background and text pair.</summary>
public class SpineColourTests
{
    private const string WhiteHex = "#ffffff";
    private const string BlackHex = "#000000";
    private const double MinimumContrast = 4.5;
    private const double MinimumUnchangedShare = 0.70;
    private const double MaximumLightnessChange = 0.07;
    private const double MaximumHueChangeDegrees = 5.0;
    private const double HueCheckMinimumChroma = 0.05;
    private const int GridStep = 16;
    private const int GridCount = 4913;
    private const double FullTurnDegrees = 360.0;
    private const double HalfTurnDegrees = 180.0;

    private static readonly Lazy<IReadOnlyList<(RgbColour Colour, PaletteTone Pair)>> Grid = new(BuildGrid);

    [Fact]
    [Trait("Category", "Layout")]
    public void A_red_that_already_reads_under_the_shade_keeps_its_own_background_with_white_text()
    {
        var pair = SpineColour.PairFor(new RgbColour(189, 30, 40));

        pair.Background.Should().Be("#bd1e28");
        pair.Text.Should().Be(WhiteHex);
        SpineColour.PassesUnderShade(Parse(pair.Background), Parse(pair.Text)).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_mid_green_that_fails_under_the_shade_becomes_a_lighter_green_that_passes()
    {
        var extracted = new RgbColour(51, 153, 51);

        var pair = SpineColour.PairFor(extracted);
        var background = Parse(pair.Background);

        pair.Background.Should().NotBe(extracted.ToHex());
        SpineColour.LightnessOf(background).Should().BeGreaterThan(SpineColour.LightnessOf(extracted));
        background.G.Should().BeGreaterThan(background.R).And.BeGreaterThan(background.B);
        SpineColour.PassesUnderShade(background, Parse(pair.Text)).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_grid_holds_every_combination_of_seventeen_channel_levels()
    {
        Grid.Value.Should().HaveCount(GridCount);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Every_grid_colour_gets_a_pair_that_passes_at_both_shade_ends_with_white_or_black_text()
    {
        foreach (var (colour, pair) in Grid.Value)
        {
            var background = Parse(pair.Background);
            var text = Parse(pair.Text);

            pair.Text.Should().BeOneOf(WhiteHex, BlackHex, colour.ToHex());
            SpineColour.PassesUnderShade(background, text).Should().BeTrue(colour.ToHex());
            SpineColour.ContrastRatio(background, text).Should().BeGreaterThanOrEqualTo(MinimumContrast, colour.ToHex());
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void At_least_seventy_percent_of_grid_colours_keep_their_own_background()
    {
        var unchanged = Grid.Value.Count(entry => entry.Pair.Background == entry.Colour.ToHex());

        ((double)unchanged / Grid.Value.Count).Should().BeGreaterThanOrEqualTo(MinimumUnchangedShare);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void No_grid_colour_moves_in_lightness_by_more_than_seven_hundredths()
    {
        var largest = Grid.Value.Max(entry =>
            Math.Abs(SpineColour.LightnessOf(Parse(entry.Pair.Background)) - SpineColour.LightnessOf(entry.Colour)));

        largest.Should().BeLessThanOrEqualTo(MaximumLightnessChange);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_nudged_colour_with_real_chroma_keeps_its_hue_within_five_degrees()
    {
        var checkedColours = 0;

        foreach (var (colour, pair) in Grid.Value.Where(entry => entry.Pair.Background != entry.Colour.ToHex()))
        {
            var (_, chroma, hue) = OklabOf(colour);

            if (chroma <= HueCheckMinimumChroma)
            {
                continue;
            }

            var (_, _, nudgedHue) = OklabOf(Parse(pair.Background));
            var difference = Math.Abs(nudgedHue - hue) % FullTurnDegrees;

            checkedColours++;
            Math.Min(difference, FullTurnDegrees - difference).Should().BeLessThanOrEqualTo(MaximumHueChangeDegrees, colour.ToHex());
        }

        checkedColours.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void White_text_is_chosen_whenever_white_passes()
    {
        foreach (var (colour, pair) in Grid.Value.Where(entry => SpineColour.PassesUnderShade(entry.Colour, SpineColour.White)))
        {
            pair.Background.Should().Be(colour.ToHex());
            pair.Text.Should().Be(WhiteHex);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Black_text_is_chosen_when_only_black_passes_on_the_extracted_colour()
    {
        var extracted = new RgbColour(240, 240, 240);

        var pair = SpineColour.PairFor(extracted);

        pair.Background.Should().Be(extracted.ToHex());
        pair.Text.Should().Be(BlackHex);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_pair_is_a_pure_function_of_the_colour()
    {
        foreach (var (colour, pair) in Grid.Value.Where((_, index) => index % GridStep == 0))
        {
            SpineColour.PairFor(colour).Should().Be(pair);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_contrast_of_exactly_the_minimum_passes_and_a_hair_below_does_not()
    {
        SpineColour.MeetsMinimum(MinimumContrast).Should().BeTrue();
        SpineColour.MeetsMinimum(MinimumContrast - 1e-9).Should().BeFalse();
        SpineColour.MinimumContrast.Should().Be(MinimumContrast);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Black_on_white_has_the_widest_contrast()
    {
        SpineColour.ContrastRatio(SpineColour.Black, SpineColour.White).Should().BeApproximately(21.0, 1e-9);
    }

    [Theory]
    [InlineData("#FFFFFF")]
    [InlineData("#fff")]
    [InlineData("red")]
    [InlineData("#12345g")]
    [InlineData("")]
    [InlineData(null)]
    [Trait("Category", "Layout")]
    public void A_background_that_is_not_lowercase_six_digit_hexadecimal_is_refused(string? background)
    {
        SpineColour.IsValidPair(new PaletteTone(background!, WhiteHex)).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_missing_pair_is_refused()
    {
        SpineColour.IsValidPair(null).Should().BeFalse();
    }

    [Theory]
    [InlineData("#2a1a10")]
    [InlineData("#FFFFFF")]
    [InlineData("#fff")]
    [InlineData("black")]
    [Trait("Category", "Layout")]
    public void A_text_colour_other_than_white_or_black_is_refused(string text)
    {
        SpineColour.IsValidPair(new PaletteTone("#bd1e28", text)).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_pair_that_fails_under_the_shade_is_refused()
    {
        SpineColour.IsValidPair(new PaletteTone("#777777", WhiteHex)).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Every_pair_returned_for_the_grid_is_valid()
    {
        Grid.Value.Should().OnlyContain(entry => SpineColour.IsValidPair(entry.Pair));
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Hexadecimal_text_round_trips_for_every_grid_colour()
    {
        foreach (var (colour, _) in Grid.Value)
        {
            RgbColour.TryParseHex(colour.ToHex(), out var parsed).Should().BeTrue();
            parsed.Should().Be(colour);
        }
    }

    private static IReadOnlyList<(RgbColour Colour, PaletteTone Pair)> BuildGrid()
    {
        var levels = Enumerable.Range(0, byte.MaxValue / GridStep + 1).Select(index => (byte)(index * GridStep)).Append(byte.MaxValue).ToArray();
        var grid = new List<(RgbColour, PaletteTone)>();

        foreach (var red in levels)
        {
            foreach (var green in levels)
            {
                foreach (var blue in levels)
                {
                    var colour = new RgbColour(red, green, blue);

                    grid.Add((colour, SpineColour.PairFor(colour)));
                }
            }
        }

        return grid;
    }

    private static RgbColour Parse(string hex)
    {
        RgbColour.TryParseHex(hex, out var colour).Should().BeTrue(hex);

        return colour;
    }

    private static (double Lightness, double Chroma, double HueDegrees) OklabOf(RgbColour colour)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255.0;

            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        var r = Linear(colour.R);
        var g = Linear(colour.G);
        var b = Linear(colour.B);
        var l = Math.Cbrt((0.4122214708 * r) + (0.5363325363 * g) + (0.0514459929 * b));
        var m = Math.Cbrt((0.2119034982 * r) + (0.6806995451 * g) + (0.1073969566 * b));
        var s = Math.Cbrt((0.0883024619 * r) + (0.2817188376 * g) + (0.6299787005 * b));
        var a = (1.9779984951 * l) - (2.4285922050 * m) + (0.4505937099 * s);
        var bAxis = (0.0259040371 * l) + (0.7827717662 * m) - (0.8086757660 * s);
        var hue = Math.Atan2(bAxis, a) * HalfTurnDegrees / Math.PI;

        return ((0.2104542553 * l) + (0.7936177850 * m) - (0.0040720468 * s), Math.Sqrt((a * a) + (bAxis * bAxis)), (hue + FullTurnDegrees) % FullTurnDegrees);
    }
}

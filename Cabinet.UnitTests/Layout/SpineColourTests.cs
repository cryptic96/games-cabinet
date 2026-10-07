using Cabinet.Domain.Layout;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>Pins how a colour taken from box art becomes a legible spine background and text pair.</summary>
public class SpineColourTests
{
    private const string WhiteHex = "#ffffff";

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

    private static RgbColour Parse(string hex)
    {
        RgbColour.TryParseHex(hex, out var colour).Should().BeTrue(hex);

        return colour;
    }
}

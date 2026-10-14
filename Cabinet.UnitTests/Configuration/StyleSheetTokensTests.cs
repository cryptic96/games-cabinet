using Cabinet.Domain.Layout;
using FluentAssertions;

namespace Cabinet.UnitTests.Configuration;

/// <summary>Checks the reader of custom properties on a small invented style sheet, so the contrast tests can trust what it reports.</summary>
[Trait("Category", "Configuration")]
public class StyleSheetTokensTests
{
    private const string RawSample = """
        @layer a, b;
        @layer b {
        <comment>A comment with a { brace and a --fake: #000000; declaration.</comment>
        :root,
        [data-wood] {
          --base: #102030;
          --other: var(--base);
          --soft: color-mix(in srgb, var(--base) 25%, #ffffff);
          --dark: color-mix(in oklch, #ffffff, black 100%);
          --missing: var(--nothing, #010203);
          --triple: 10 20 30;
          --alpha: 0.5;
        }

        [data-wood="one"] {
          --base: #ff0000;
        }

        @container (max-width: 400px) {
          .inner > * {
            --inside: #00ff00;
          }
        }
        }
        """;

    private static readonly string Sample = RawSample
        .Replace("<comment>", "/" + "** ", StringComparison.Ordinal)
        .Replace("</comment>", " *" + "/", StringComparison.Ordinal);

    [Fact]
    public void A_block_inside_a_layer_is_found_and_a_comment_never_adds_a_declaration()
    {
        var tokens = StyleSheetTokens.Parse(Sample);

        tokens.Block(":root").Keys.Should().BeEquivalentTo("--base", "--other", "--soft", "--dark", "--missing", "--triple", "--alpha");
        tokens.Block(":root").Should().NotContainKey("--fake");
    }

    [Fact]
    public void A_block_inside_a_container_query_is_found_too()
    {
        StyleSheetTokens.Parse(Sample).Block(".inner > *").Should().ContainKey("--inside");
    }

    [Fact]
    public void A_wood_block_overrides_the_root_block_and_references_follow_the_override()
    {
        var merged = StyleSheetTokens.Parse(Sample).ForWood("one");

        StyleSheetTokens.Colour(merged, "--base").Should().Be(new RgbColour(255, 0, 0));
        StyleSheetTokens.Colour(merged, "--other").Should().Be(new RgbColour(255, 0, 0));
    }

    [Fact]
    public void A_reference_to_a_missing_token_uses_its_fallback()
    {
        var merged = StyleSheetTokens.Parse(Sample).ForWood("two");

        StyleSheetTokens.Colour(merged, "--missing").Should().Be(new RgbColour(1, 2, 3));
    }

    [Fact]
    public void A_mix_in_srgb_blends_the_gamma_encoded_channels_by_the_given_share()
    {
        var merged = StyleSheetTokens.Parse(Sample).ForWood("two");

        StyleSheetTokens.Colour(merged, "--soft").Should().Be(new RgbColour(
            (byte)Math.Round((0x10 * 0.25) + (255 * 0.75)),
            (byte)Math.Round((0x20 * 0.25) + (255 * 0.75)),
            (byte)Math.Round((0x30 * 0.25) + (255 * 0.75))));
    }

    [Fact]
    public void A_mix_in_oklch_with_all_of_black_is_black()
    {
        var merged = StyleSheetTokens.Parse(Sample).ForWood("two");

        StyleSheetTokens.Colour(merged, "--dark").Should().Be(new RgbColour(0, 0, 0));
    }

    [Fact]
    public void Numbers_and_channel_triples_are_read_as_they_are_written()
    {
        var merged = StyleSheetTokens.Parse(Sample).ForWood("two");

        StyleSheetTokens.Number(merged, "--alpha").Should().Be(0.5);
        StyleSheetTokens.Channels(merged, "--triple").Should().Be(new RgbColour(10, 20, 30));
    }

    [Fact]
    public void A_colour_laid_over_another_is_blended_on_the_encoded_channels_and_luminance_matches_the_standard()
    {
        StyleSheetTokens.Over(new RgbColour(255, 255, 255), new RgbColour(0, 0, 0), 0.5).Should().Be(new RgbColour(128, 128, 128));
        StyleSheetTokens.Luminance(new RgbColour(255, 255, 255)).Should().BeApproximately(1.0, 0.0001);
        StyleSheetTokens.Luminance(new RgbColour(0, 0, 0)).Should().BeApproximately(0.0, 0.0001);
    }
}

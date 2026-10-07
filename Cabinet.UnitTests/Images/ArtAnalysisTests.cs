using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.FakeBgg;
using Cabinet.Repository.Images;
using FluentAssertions;
using SkiaSharp;

namespace Cabinet.UnitTests.Images;

/// <summary>Proves the art analysis tells photographed boxes from flat covers and takes colours from the art, never from its plain background.</summary>
[Trait("Category", "Images")]
public sealed class ArtAnalysisTests
{
    [Fact]
    public void A_box_on_white_is_a_3D_shot_with_a_red_main_colour_and_white_edges()
    {
        var facts = Analyse(SyntheticArtKind.BoxOnWhite);

        ArtVerdicts.Classify(facts.Features, ArtThresholds.Default).Should().Be(ArtVerdict.ThreeD);
        facts.Main.R.Should().BeGreaterThanOrEqualTo(150);
        facts.Main.G.Should().BeLessThanOrEqualTo(80);
        facts.Main.B.Should().BeLessThanOrEqualTo(80);
        foreach (var edge in new[] { facts.Top, facts.Right, facts.Bottom, facts.Left })
        {
            Lowest(edge).Should().BeGreaterThanOrEqualTo(230);
        }
    }

    [Fact]
    public void A_full_bleed_cover_is_flat_with_no_backdrop_and_a_reddish_main_colour()
    {
        var facts = Analyse(SyntheticArtKind.FlatCover);

        ArtVerdicts.Classify(facts.Features, ArtThresholds.Default).Should().Be(ArtVerdict.Flat);
        facts.Features.BackdropShare.Should().BeLessThan(0.10);
        facts.Main.R.Should().BeGreaterThan(facts.Main.G);
        facts.Main.R.Should().BeGreaterThan(facts.Main.B);
    }

    private static ArtFacts Analyse(SyntheticArtKind kind)
    {
        using var bitmap = SKBitmap.Decode(SyntheticArt.Encode(kind));

        return ArtAnalysis.Analyse(bitmap);
    }

    private static int Lowest(RgbColour colour) => Math.Min(colour.R, Math.Min(colour.G, colour.B));
}

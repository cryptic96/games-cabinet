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

    [Theory]
    [InlineData(SyntheticArtKind.FlatCover)]
    [InlineData(SyntheticArtKind.FlatWide)]
    [InlineData(SyntheticArtKind.FlatNarrow)]
    [InlineData(SyntheticArtKind.Banner)]
    [InlineData(SyntheticArtKind.WhiteFramed)]
    [InlineData(SyntheticArtKind.BlackFramed)]
    [InlineData(SyntheticArtKind.AllWhite)]
    [InlineData(SyntheticArtKind.NearBlack)]
    [InlineData(SyntheticArtKind.MidGreen)]
    [InlineData(SyntheticArtKind.GradientFullBleed)]
    public void Every_flat_fixture_classifies_as_flat(SyntheticArtKind kind)
    {
        ArtVerdicts.Classify(Analyse(kind).Features, ArtThresholds.Default).Should().Be(ArtVerdict.Flat);
    }

    [Fact]
    public void An_all_white_cover_is_flat_because_it_is_almost_all_backdrop()
    {
        Analyse(SyntheticArtKind.AllWhite).Features.BackdropShare.Should().BeGreaterThanOrEqualTo(0.97);
    }

    [Theory]
    [InlineData(SyntheticArtKind.BoxOnWhite)]
    [InlineData(SyntheticArtKind.BoxOnGreyGradient)]
    [InlineData(SyntheticArtKind.BoxOnBlack)]
    [InlineData(SyntheticArtKind.BoxTransparent)]
    public void Every_3D_fixture_classifies_as_a_3D_shot(SyntheticArtKind kind)
    {
        var features = Analyse(kind).Features;

        ArtVerdicts.Classify(features, ArtThresholds.Default).Should().Be(ArtVerdict.ThreeD);
        features.Fill.Should().BeLessThanOrEqualTo(0.93);
        features.Corner2.Should().BeGreaterThanOrEqualTo(0.40);
    }

    [Fact]
    public void A_white_framed_cover_takes_the_colour_inside_its_frame()
    {
        Analyse(SyntheticArtKind.WhiteFramed).Main.Should().Match<RgbColour>(colour => Near(colour, new RgbColour(20, 130, 140), 12));
    }

    [Fact]
    public void A_box_on_a_grey_gradient_takes_the_box_colour_and_not_the_grey()
    {
        var main = Analyse(SyntheticArtKind.BoxOnGreyGradient).Main;

        ((int)main.B).Should().BeGreaterThan(main.R + 60);
        Saturation(main).Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void A_transparent_box_takes_a_green_main_colour()
    {
        var main = Analyse(SyntheticArtKind.BoxTransparent).Main;

        ((int)main.G).Should().BeGreaterThan(main.R + 40);
        ((int)main.G).Should().BeGreaterThan(main.B + 40);
    }

    [Fact]
    public void A_full_bleed_gradient_is_not_treated_as_backdrop_and_gives_a_blue_main_colour()
    {
        var facts = Analyse(SyntheticArtKind.GradientFullBleed);

        facts.Features.BackdropShare.Should().BeLessThan(0.10);
        ((int)facts.Main.B).Should().BeGreaterThan(facts.Main.R + 40);
        facts.Main.B.Should().BeGreaterThan(facts.Main.G);
    }

    [Fact]
    public void A_mid_green_cover_keeps_its_field_colour_although_the_field_touches_every_edge()
    {
        Analyse(SyntheticArtKind.MidGreen).Main.Should().Match<RgbColour>(colour => Near(colour, new RgbColour(51, 153, 51), 12));
    }

    [Fact]
    public void A_near_black_cover_keeps_its_dark_field_colour()
    {
        Highest(Analyse(SyntheticArtKind.NearBlack).Main).Should().BeLessThanOrEqualTo(40);
    }

    [Fact]
    public void An_all_white_cover_uses_its_overall_mean_colour()
    {
        Lowest(Analyse(SyntheticArtKind.AllWhite).Main).Should().BeGreaterThanOrEqualTo(240);
    }

    [Fact]
    public void A_fully_transparent_picture_gives_the_fallback_colour_without_failing()
    {
        using var bitmap = Blank(10, 10, SKColors.Transparent);

        var facts = ArtAnalysis.Analyse(bitmap);

        facts.Main.ToHex().Should().Be("#808080");
        facts.Main.Should().Be(ArtAnalysis.FallbackColour);
    }

    [Fact]
    public void A_one_pixel_picture_does_not_fail()
    {
        using var bitmap = Blank(1, 1, new SKColor(10, 200, 30));

        var act = () => ArtAnalysis.Analyse(bitmap);

        act.Should().NotThrow();
    }

    [Fact]
    public void A_box_on_white_has_near_white_edges_and_a_box_on_black_has_near_black_edges()
    {
        var onWhite = Analyse(SyntheticArtKind.BoxOnWhite);
        var onBlack = Analyse(SyntheticArtKind.BoxOnBlack);

        new[] { onWhite.Top, onWhite.Right, onWhite.Bottom, onWhite.Left }.Should().OnlyContain(edge => Lowest(edge) >= 230);
        new[] { onBlack.Top, onBlack.Right, onBlack.Bottom, onBlack.Left }.Should().OnlyContain(edge => Highest(edge) <= 40);
    }

    [Fact]
    public void A_black_framed_cover_has_near_black_edges()
    {
        var facts = Analyse(SyntheticArtKind.BlackFramed);

        new[] { facts.Top, facts.Right, facts.Bottom, facts.Left }.Should().OnlyContain(edge => Highest(edge) <= 40);
    }

    [Fact]
    public void A_transparent_edge_strip_takes_the_main_colour()
    {
        var facts = Analyse(SyntheticArtKind.BoxTransparent);

        new[] { facts.Top, facts.Right, facts.Bottom, facts.Left }.Should().OnlyContain(edge => edge == facts.Main);
    }

    [Fact]
    public void A_banner_has_different_top_and_bottom_edge_colours()
    {
        var facts = Analyse(SyntheticArtKind.Banner);

        facts.Top.Should().NotBe(facts.Bottom);
    }

    [Fact]
    public void Equal_scores_are_broken_by_the_lowest_colour_bucket()
    {
        using var bitmap = TwoBlocksInsideNoisyBorder();

        ArtAnalysis.Analyse(bitmap).Main.Should().Be(new RgbColour(0, 0, 200));
    }

    [Theory]
    [InlineData(SyntheticArtKind.FlatCover)]
    [InlineData(SyntheticArtKind.BoxOnWhite)]
    [InlineData(SyntheticArtKind.BoxTransparent)]
    [InlineData(SyntheticArtKind.WhiteFramed)]
    public void Analysing_the_same_bytes_twice_gives_equal_facts(SyntheticArtKind kind)
    {
        Analyse(kind).Should().Be(Analyse(kind));
    }

    [Fact]
    public void A_very_large_picture_is_analysed_from_its_small_copy()
    {
        using var bitmap = Blank(4000, 3000, SKColors.White);
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { Color = new SKColor(200, 20, 20) })
        {
            canvas.DrawRect(new SKRect(1200, 800, 2800, 2200), paint);
        }

        var facts = ArtAnalysis.Analyse(bitmap);

        ArtVerdicts.Classify(facts.Features, ArtThresholds.Default).Should().Be(ArtVerdict.Flat);
        facts.Main.R.Should().BeGreaterThanOrEqualTo(150);
        facts.Top.Should().Be(new RgbColour(255, 255, 255));
    }

    [Fact]
    public void A_half_transparent_picture_keeps_its_straight_colour()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(200, 200, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        bitmap.Pixels = Enumerable.Repeat(new SKColor(200, 40, 40, 128), 200 * 200).ToArray();
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);

        var main = Stored(png.ToArray()).Main;

        Near(main, new RgbColour(200, 40, 40), 12).Should().BeTrue($"the main colour was {main.ToHex()}");
    }

    [Fact]
    public void A_cut_out_box_with_a_see_through_shadow_is_a_3D_shot_with_the_box_colour_as_main_colour()
    {
        var facts = Stored(SyntheticArtKind.BoxTransparentShadow);

        ArtVerdicts.Classify(facts.Features, ArtThresholds.Default).Should().Be(ArtVerdict.ThreeD, Describe(facts));
        facts.Main.B.Should().BeGreaterThanOrEqualTo(facts.Main.R, Describe(facts));
        Highest(facts.Main).Should().BeGreaterThan(120, Describe(facts));
    }

    [Fact]
    public void A_box_cropped_close_on_a_noisy_white_backdrop_is_a_3D_shot()
    {
        var facts = Stored(SyntheticArtKind.BoxOnNoisyWhite);

        ArtVerdicts.Classify(facts.Features, ArtThresholds.Default).Should().Be(ArtVerdict.ThreeD, Describe(facts));
        facts.Features.Fill.Should().BeLessThanOrEqualTo(0.93, Describe(facts));
        facts.Features.Corner2.Should().BeGreaterThanOrEqualTo(0.40, Describe(facts));
    }

    [Fact]
    public void A_flat_cover_inside_a_thick_dark_border_is_flat_and_warm_with_dark_edges()
    {
        var facts = Stored(SyntheticArtKind.DarkBorderCover);

        ArtVerdicts.Classify(facts.Features, ArtThresholds.Default).Should().Be(ArtVerdict.Flat, Describe(facts));
        ((int)facts.Main.R).Should().BeGreaterThanOrEqualTo(facts.Main.B + 40, Describe(facts));
        new[] { facts.Top, facts.Right, facts.Bottom, facts.Left }.Should().OnlyContain(edge => Highest(edge) <= 40);
    }

    [Fact]
    public void A_cut_out_box_seen_almost_face_on_is_a_3D_shot()
    {
        var facts = Stored(SyntheticArtKind.CutOutFrontOn);

        ArtVerdicts.Classify(facts.Features, ArtThresholds.Default).Should().Be(ArtVerdict.ThreeD, Describe(facts));
        facts.Features.CutOut.Should().BeTrue(Describe(facts));
    }

    [Theory]
    [InlineData(SyntheticArtKind.BoxTransparent)]
    [InlineData(SyntheticArtKind.BoxTransparentShadow)]
    public void Cut_out_boxes_report_a_cut_out(SyntheticArtKind kind)
    {
        var facts = Stored(kind);

        ArtVerdicts.Classify(facts.Features, ArtThresholds.Default).Should().Be(ArtVerdict.ThreeD, Describe(facts));
        facts.Features.CutOut.Should().BeTrue(Describe(facts));
    }

    [Fact]
    public void Every_opaque_fixture_reports_no_cut_out()
    {
        var opaque = SyntheticArt.All.Where(kind => kind is not (SyntheticArtKind.Undecodable or SyntheticArtKind.BoxTransparent
            or SyntheticArtKind.BoxTransparentShadow or SyntheticArtKind.CutOutFrontOn));

        foreach (var kind in opaque)
        {
            Stored(kind).Features.CutOut.Should().BeFalse(kind.ToString());
        }
    }

    private static string Describe(ArtFacts facts) =>
        $"features {facts.Features}, score {ArtVerdicts.Score(facts.Features):0.00}, main {facts.Main.ToHex()}";

    private static ArtFacts Stored(SyntheticArtKind kind) => Stored(SyntheticArt.Encode(kind));

    private static ArtFacts Stored(byte[] bytes) =>
        ArtProcessor.Process(bytes, new ArtLimits(12_000_000, 36_000_000)).Should().BeOfType<ArtProcessing.Done>().Subject.Facts;

    private static ArtFacts Analyse(SyntheticArtKind kind)
    {
        using var bitmap = SKBitmap.Decode(SyntheticArt.Encode(kind));

        return ArtAnalysis.Analyse(bitmap);
    }

    private static SKBitmap Blank(int width, int height, SKColor colour)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(colour);

        return bitmap;
    }

    private static SKBitmap TwoBlocksInsideNoisyBorder()
    {
        const int size = ArtAnalysis.AnalysisWidth;
        const int greyStep = 20;
        const int greyLevels = 12;
        var bitmap = Blank(size, size, SKColors.Black);
        using var canvas = new SKCanvas(bitmap);
        using var blue = new SKPaint { Color = new SKColor(0, 0, 200) };
        using var red = new SKPaint { Color = new SKColor(200, 0, 0) };
        canvas.DrawRect(new SKRect(1, 1, 1 + ((size - 2) / 2), size - 1), blue);
        canvas.DrawRect(new SKRect(1 + ((size - 2) / 2), 1, size - 1, size - 1), red);

        var ring = 0;
        for (var index = 0; index < size; index++)
        {
            foreach (var (x, y) in new[] { (index, 0), (index, size - 1), (0, index), (size - 1, index) })
            {
                var level = (byte)((greyStep * (ring++ % greyLevels)) + 10);
                bitmap.SetPixel(x, y, new SKColor(level, level, level));
            }
        }

        return bitmap;
    }

    private static bool Near(RgbColour colour, RgbColour expected, int tolerance) =>
        Math.Abs(colour.R - expected.R) <= tolerance && Math.Abs(colour.G - expected.G) <= tolerance && Math.Abs(colour.B - expected.B) <= tolerance;

    private static double Saturation(RgbColour colour)
    {
        var highest = Highest(colour);

        return highest == 0 ? 0 : (double)(highest - Lowest(colour)) / highest;
    }

    private static int Highest(RgbColour colour) => Math.Max(colour.R, Math.Max(colour.G, colour.B));

    private static int Lowest(RgbColour colour) => Math.Min(colour.R, Math.Min(colour.G, colour.B));
}

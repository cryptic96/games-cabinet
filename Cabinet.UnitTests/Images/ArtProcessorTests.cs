using System.Security.Cryptography;
using System.Text;
using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.Repository.Images;
using FluentAssertions;
using SkiaSharp;

namespace Cabinet.UnitTests.Images;

/// <summary>Proves the processor only scales down, never crops, keeps alpha, names files by content and refuses what it cannot or may not decode.</summary>
[Trait("Category", "Images")]
public sealed class ArtProcessorTests
{
    private static readonly ArtLimits Limits = new(12_000_000, 36_000_000);

    [Fact]
    public void Bytes_that_are_not_a_picture_are_undecodable()
    {
        ArtProcessor.Process(Encoding.ASCII.GetBytes("this is not a picture"), Limits).Should().BeSameAs(ArtProcessing.Undecodable);
        ArtProcessor.Process([], Limits).Should().BeSameAs(ArtProcessing.Undecodable);
    }

    [Fact]
    public void A_header_announcing_more_pixels_than_the_cap_is_refused_before_decoding()
    {
        var picture = Png(100, 100);

        ArtProcessor.Process(picture, new ArtLimits(12_000_000, 9_999)).Should().Be(ArtProcessing.Refused("pixels"));
        ArtProcessor.Process(picture, new ArtLimits(12_000_000, 10_000)).Should().BeOfType<ArtProcessing.Done>();
    }

    [Fact]
    public void A_large_source_gives_a_480_and_a_240_pixel_variant_with_the_aspect_ratio_kept()
    {
        var variants = Variants(Png(1000, 1500));

        variants.Select(variant => (variant.Width, variant.Height)).Should().Equal((480, 720), (240, 360));
    }

    [Fact]
    public void A_source_exactly_480_wide_gives_both_variants()
    {
        Variants(Png(480, 300)).Select(variant => variant.Width).Should().Equal(480, 240);
    }

    [Fact]
    public void A_source_between_240_and_480_wide_gives_one_240_pixel_variant()
    {
        Variants(Png(300, 300)).Select(variant => (variant.Width, variant.Height)).Should().Equal((240, 240));
        Variants(Png(479, 100)).Select(variant => variant.Width).Should().Equal(240);
    }

    [Fact]
    public void A_source_narrower_than_240_keeps_its_own_width_and_is_never_enlarged()
    {
        Variants(Png(200, 100)).Select(variant => (variant.Width, variant.Height)).Should().Equal((200, 100));
        Variants(Png(1, 1)).Select(variant => (variant.Width, variant.Height)).Should().Equal((1, 1));
    }

    [Fact]
    public void A_very_wide_source_is_scaled_to_its_own_ratio_and_never_to_zero_height()
    {
        Variants(Png(2000, 3)).Select(variant => (variant.Width, variant.Height)).Should().Equal((480, 1), (240, 1));
    }

    [Fact]
    public void Every_variant_is_a_webp_file_that_decodes_to_the_stated_size()
    {
        foreach (var variant in Variants(Png(800, 600)))
        {
            Encoding.ASCII.GetString(variant.Bytes, 0, 4).Should().Be("RIFF");
            Encoding.ASCII.GetString(variant.Bytes, 8, 4).Should().Be("WEBP");
            using var decoded = SKBitmap.Decode(variant.Bytes);
            (decoded.Width, decoded.Height).Should().Be((variant.Width, variant.Height));
        }
    }

    [Fact]
    public void A_transparent_corner_stays_transparent()
    {
        using var decoded = SKBitmap.Decode(Variants(Png(600, 800, transparentCorner: true))[0].Bytes);

        decoded.GetPixel(0, 0).Alpha.Should().Be(0);
        decoded.GetPixel(decoded.Width - 1, decoded.Height - 1).Alpha.Should().Be(255);
    }

    [Fact]
    public void Names_follow_the_hash_of_the_encoded_bytes_and_equal_input_gives_equal_names()
    {
        var first = Variants(Png(800, 600));
        var second = Variants(Png(800, 600));

        first.Select(variant => variant.Name).Should().Equal(second.Select(variant => variant.Name));

        foreach (var variant in first)
        {
            var hash = Convert.ToHexStringLower(SHA256.HashData(variant.Bytes))[..16];

            variant.Name.Should().Be($"{hash}-{variant.Width}.webp");
            variant.Name.Should().MatchRegex(@"^[0-9a-f]{16}-[0-9]{1,4}\.webp$");
        }
    }

    [Fact]
    public void Different_pictures_get_different_names()
    {
        Variants(Png(800, 600, SKColors.Red))[0].Name.Should().NotBe(Variants(Png(800, 600, SKColors.Blue))[0].Name);
    }

    [Fact]
    public void A_flat_cover_is_measured_as_flat_with_a_red_main_colour()
    {
        var done = Done(SyntheticArt.Encode(SyntheticArtKind.FlatCover));

        ArtVerdicts.Classify(done.Facts.Features, ArtThresholds.Default).Should().Be(ArtVerdict.Flat);
        done.Facts.Main.R.Should().BeGreaterThan(done.Facts.Main.G).And.BeGreaterThan(done.Facts.Main.B);
    }

    [Fact]
    public void A_photographed_box_on_white_is_measured_as_three_dimensional_with_near_white_edges()
    {
        var done = Done(SyntheticArt.Encode(SyntheticArtKind.BoxOnWhite));
        var edges = new[] { done.Facts.Top, done.Facts.Right, done.Facts.Bottom, done.Facts.Left };

        ArtVerdicts.Classify(done.Facts.Features, ArtThresholds.Default).Should().Be(ArtVerdict.ThreeD);
        edges.Should().OnlyContain(edge => edge.R >= ArtAnalysis.NearWhiteMin && edge.G >= ArtAnalysis.NearWhiteMin && edge.B >= ArtAnalysis.NearWhiteMin);
    }

    [Fact]
    public void The_analysis_version_is_a_positive_number_a_release_can_raise()
    {
        ArtProcessor.AnalysisVersion.Should().BePositive();
    }

    private static ArtProcessing.Done Done(byte[] picture) =>
        ArtProcessor.Process(picture, Limits).Should().BeOfType<ArtProcessing.Done>().Which;

    private static IReadOnlyList<EncodedArt> Variants(byte[] picture) =>
        ArtProcessor.Process(picture, Limits).Should().BeOfType<ArtProcessing.Done>().Which.Variants;

    private static byte[] Png(int width, int height, SKColor? fill = null, bool transparentCorner = false)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(fill ?? new SKColor(170, 60, 40));

            if (transparentCorner)
            {
                using var paint = new SKPaint { BlendMode = SKBlendMode.Clear };
                canvas.DrawRect(new SKRect(0, 0, width / 10f, height / 10f), paint);
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }
}

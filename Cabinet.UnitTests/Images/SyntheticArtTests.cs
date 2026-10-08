using Cabinet.FakeBgg;
using FluentAssertions;
using SkiaSharp;

namespace Cabinet.UnitTests.Images;

/// <summary>Proves every invented picture exists, has its stated size and is drawn identically every time.</summary>
[Trait("Category", "Images")]
public sealed class SyntheticArtTests
{
    private const int WhiteMarginPx = 24;

    public static TheoryData<SyntheticArtKind> DecodableKinds()
    {
        var kinds = new TheoryData<SyntheticArtKind>();
        foreach (var kind in SyntheticArt.All.Where(kind => kind != SyntheticArtKind.Undecodable))
        {
            kinds.Add(kind);
        }

        return kinds;
    }

    [Fact]
    public void Every_kind_is_listed_once()
    {
        SyntheticArt.All.Should().BeEquivalentTo(Enum.GetValues<SyntheticArtKind>());
        SyntheticArt.All.Should().HaveCount(25);
    }

    [Theory]
    [MemberData(nameof(DecodableKinds))]
    public void A_decodable_kind_decodes_to_its_stated_size(SyntheticArtKind kind)
    {
        using var bitmap = SKBitmap.Decode(SyntheticArt.Encode(kind));

        bitmap.Should().NotBeNull();
        (bitmap.Width, bitmap.Height).Should().Be(SyntheticArt.SizeOf(kind));
    }

    [Fact]
    public void The_undecodable_kind_is_not_a_picture()
    {
        using var codec = SKCodec.Create(new SKMemoryStream(SyntheticArt.Encode(SyntheticArtKind.Undecodable)));

        codec.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(DecodableKinds))]
    public void Encoding_a_kind_twice_gives_identical_bytes(SyntheticArtKind kind)
    {
        SyntheticArt.Encode(kind).Should().Equal(SyntheticArt.Encode(kind));
    }

    [Theory]
    [InlineData(SyntheticArtKind.FlatCover, 600, 800)]
    [InlineData(SyntheticArtKind.FlatWide, 900, 800)]
    [InlineData(SyntheticArtKind.FlatNarrow, 500, 800)]
    [InlineData(SyntheticArtKind.Banner, 1200, 300)]
    [InlineData(SyntheticArtKind.WhiteFramed, 600, 800)]
    [InlineData(SyntheticArtKind.BlackFramed, 600, 800)]
    [InlineData(SyntheticArtKind.AllWhite, 600, 800)]
    [InlineData(SyntheticArtKind.NearBlack, 600, 800)]
    [InlineData(SyntheticArtKind.MidGreen, 600, 800)]
    [InlineData(SyntheticArtKind.GradientFullBleed, 600, 800)]
    [InlineData(SyntheticArtKind.BoxOnWhite, 800, 800)]
    [InlineData(SyntheticArtKind.BoxOnGreyGradient, 800, 800)]
    [InlineData(SyntheticArtKind.BoxOnBlack, 800, 800)]
    [InlineData(SyntheticArtKind.BoxTransparent, 800, 800)]
    [InlineData(SyntheticArtKind.BoxTransparentShadow, 760, 640)]
    [InlineData(SyntheticArtKind.BoxOnNoisyWhite, 760, 640)]
    [InlineData(SyntheticArtKind.DarkBorderCover, 600, 800)]
    [InlineData(SyntheticArtKind.CutOutFrontOn, 640, 640)]
    [InlineData(SyntheticArtKind.BoxOnWhiteTightCrop, 760, 640)]
    [InlineData(SyntheticArtKind.CoverColouredField, 600, 800)]
    [InlineData(SyntheticArtKind.CoverOnBlackIrregular, 800, 460)]
    [InlineData(SyntheticArtKind.CoverOnBlackScattered, 540, 860)]
    [InlineData(SyntheticArtKind.CoverColourFramed, 700, 480)]
    [InlineData(SyntheticArtKind.CoverLightEdge, 600, 800)]
    public void Sizes_match_the_fixture_list(SyntheticArtKind kind, int width, int height)
    {
        SyntheticArt.SizeOf(kind).Should().Be((width, height));
    }

    [Fact]
    public void The_cropped_shots_reach_their_side_edges_inside_any_white_margin_and_the_shadow_is_see_through()
    {
        using var shadowed = SKBitmap.Decode(SyntheticArt.Encode(SyntheticArtKind.BoxTransparentShadow));
        using var noisy = SKBitmap.Decode(SyntheticArt.Encode(SyntheticArtKind.BoxOnNoisyWhite));

        shadowed.GetPixel(0, 0).Alpha.Should().Be(0);
        Enumerable.Range(0, shadowed.Width).Select(x => shadowed.GetPixel(x, shadowed.Height - 1).Alpha)
            .Should().Contain(alpha => alpha > 0 && alpha < 255);
        FirstColumnWhere(shadowed, colour => colour.Alpha == 255).Should().BeLessThanOrEqualTo(4);
        LastColumnWhere(shadowed, colour => colour.Alpha == 255).Should().BeGreaterThanOrEqualTo(shadowed.Width - 5);
        noisy.GetPixel(0, 0).Alpha.Should().Be(255);
        FirstColumnWhere(noisy, IsColourful).Should().BeLessThanOrEqualTo(WhiteMarginPx + 4);
        LastColumnWhere(noisy, IsColourful).Should().BeGreaterThanOrEqualTo(noisy.Width - 1 - WhiteMarginPx - 4);
    }

    private static int FirstColumnWhere(SKBitmap bitmap, Func<SKColor, bool> test) =>
        Enumerable.Range(0, bitmap.Width).First(x => Enumerable.Range(0, bitmap.Height).Any(y => test(bitmap.GetPixel(x, y))));

    private static int LastColumnWhere(SKBitmap bitmap, Func<SKColor, bool> test) =>
        Enumerable.Range(0, bitmap.Width).Last(x => Enumerable.Range(0, bitmap.Height).Any(y => test(bitmap.GetPixel(x, y))));

    private static bool IsColourful(SKColor colour) => Math.Max(colour.Red, Math.Max(colour.Green, colour.Blue)) - Math.Min(colour.Red, Math.Min(colour.Green, colour.Blue)) >= 30;

    [Fact]
    public void The_front_on_cut_out_has_only_fully_transparent_or_fully_opaque_pixels_and_fills_most_of_its_frame()
    {
        using var bitmap = SKBitmap.Decode(SyntheticArt.Encode(SyntheticArtKind.CutOutFrontOn));
        var alphas = bitmap.Pixels.Select(colour => colour.Alpha).ToList();

        alphas.Should().OnlyContain(alpha => alpha == 0 || alpha == 255);
        bitmap.GetPixel(0, 0).Alpha.Should().Be(0);
        bitmap.GetPixel(bitmap.Width - 1, bitmap.Height - 1).Alpha.Should().Be(0);
        ((double)alphas.Count(alpha => alpha == 255) / alphas.Count).Should().BeInRange(0.75, 0.95);
    }

    [Fact]
    public void The_transparent_box_is_see_through_outside_the_box()
    {
        using var bitmap = SKBitmap.Decode(SyntheticArt.Encode(SyntheticArtKind.BoxTransparent));

        bitmap.GetPixel(0, 0).Alpha.Should().Be(0);
        bitmap.GetPixel(bitmap.Width - 1, bitmap.Height - 1).Alpha.Should().Be(0);
        bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2).Alpha.Should().Be(255);
    }
}

using Cabinet.FakeBgg;
using FluentAssertions;
using SkiaSharp;

namespace Cabinet.UnitTests.Images;

/// <summary>Proves every invented picture exists, has its stated size and is drawn identically every time.</summary>
[Trait("Category", "Images")]
public sealed class SyntheticArtTests
{
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
        SyntheticArt.All.Should().HaveCount(15);
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
    public void Sizes_match_the_fixture_list(SyntheticArtKind kind, int width, int height)
    {
        SyntheticArt.SizeOf(kind).Should().Be((width, height));
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

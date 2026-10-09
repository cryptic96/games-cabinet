using Cabinet.Domain.Layout;
using Cabinet.FakeBgg;
using Cabinet.Repository.Images;
using FluentAssertions;
using SkiaSharp;

namespace Cabinet.UnitTests.Images;

/// <summary>
/// Proves the small copy every measurement runs on stays small whatever the shape of the picture: never wider than the
/// analysis width or than the picture itself, never taller than its height cap, and exactly the size it always was for a
/// picture of ordinary shape, so the measurements of ordinary pictures do not move.
/// </summary>
[Trait("Category", "Images")]
public sealed class ArtWorkingCopyTests
{
    private const long AllocationBudgetBytes = 8 * 1024 * 1024;

    public static TheoryData<SyntheticArtKind> DecodableKinds()
    {
        var kinds = new TheoryData<SyntheticArtKind>();
        foreach (var kind in SyntheticArt.All.Where(kind => kind != SyntheticArtKind.Undecodable))
        {
            kinds.Add(kind);
        }

        return kinds;
    }

    [Theory]
    [MemberData(nameof(DecodableKinds))]
    public void Every_invented_picture_keeps_the_working_copy_it_always_had(SyntheticArtKind kind)
    {
        var (width, height) = SyntheticArt.SizeOf(kind);

        ArtAnalysis.WorkingSize(width, height).Should().Be(FullWidthCopy(width, height));
    }

    [Theory]
    [InlineData(96, 96)]
    [InlineData(96, 960)]
    [InlineData(97, 300)]
    [InlineData(100, 1000)]
    [InlineData(1000, 100)]
    [InlineData(1200, 300)]
    [InlineData(4000, 3000)]
    [InlineData(960, 9600)]
    [InlineData(9600, 960)]
    public void A_picture_at_least_as_wide_as_the_copy_with_a_usable_shape_keeps_the_full_copy_width(int width, int height)
    {
        ArtAnalysis.HasUsableShape(width, height).Should().BeTrue();
        ArtAnalysis.WorkingSize(width, height).Should().Be(FullWidthCopy(width, height));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(10, 10)]
    [InlineData(50, 80)]
    [InlineData(95, 100)]
    [InlineData(95, 950)]
    [InlineData(40, 960)]
    public void A_picture_narrower_than_the_copy_is_never_scaled_up(int width, int height)
    {
        ArtAnalysis.WorkingSize(width, height).Should().Be((width, height));
    }

    [Theory]
    [InlineData(1, 16383, 1, 960)]
    [InlineData(4, 16383, 1, 960)]
    [InlineData(40, 961, 40, 960)]
    [InlineData(96, 16383, 6, 960)]
    [InlineData(16383, 1, 96, 1)]
    [InlineData(16383, 4, 96, 1)]
    public void The_working_copy_of_an_extreme_shape_stays_inside_its_bounds(int width, int height, int copyWidth, int copyHeight)
    {
        var size = ArtAnalysis.WorkingSize(width, height);

        size.Should().Be((copyWidth, copyHeight));
        size.Width.Should().BeInRange(1, ArtAnalysis.AnalysisWidth);
        size.Height.Should().BeInRange(1, ArtAnalysis.MaxAnalysisHeight);
        ((long)size.Width * size.Height).Should().BeLessThanOrEqualTo((long)ArtAnalysis.AnalysisWidth * ArtAnalysis.MaxAnalysisHeight);
    }

    [Theory]
    [InlineData(1000, 100, true)]
    [InlineData(1001, 100, false)]
    [InlineData(100, 1000, true)]
    [InlineData(100, 1001, false)]
    [InlineData(1, 10, true)]
    [InlineData(1, 11, false)]
    [InlineData(1, 1, true)]
    [InlineData(1, 16383, false)]
    [InlineData(4, 16383, false)]
    [InlineData(2000, 3, false)]
    public void A_shape_is_usable_up_to_one_side_ten_times_the_other(int width, int height, bool usable)
    {
        ArtAnalysis.HasUsableShape(width, height).Should().Be(usable);
    }

    [Theory]
    [InlineData(1, 16383)]
    [InlineData(4, 16383)]
    public void Measuring_a_hairline_picture_allocates_little_and_still_gives_its_colour(int width, int height)
    {
        using var bitmap = Filled(width, height, new SKColor(30, 90, 200));
        var before = GC.GetAllocatedBytesForCurrentThread();

        var facts = ArtAnalysis.Analyse(bitmap);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().BeLessThan(AllocationBudgetBytes, "a 96 pixel wide copy of the whole height would take hundreds of megabytes");
        facts.Main.Should().Be(new RgbColour(30, 90, 200));
    }

    private static (int Width, int Height) FullWidthCopy(int width, int height) =>
        (ArtAnalysis.AnalysisWidth, Math.Max(1, (int)Math.Round((double)height * ArtAnalysis.AnalysisWidth / width, MidpointRounding.AwayFromZero)));

    private static SKBitmap Filled(int width, int height, SKColor colour)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(colour);

        return bitmap;
    }
}

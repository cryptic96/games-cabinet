using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.Service.Review;
using FluentAssertions;
using SkiaSharp;

namespace Cabinet.UnitTests.Review;

/// <summary>Proves the sheet is drawn in pages of eight rows at the page width from a synthetic collection.</summary>
[Trait("Category", "Images")]
public sealed class ReviewSheetTests : IDisposable
{
    private readonly ReviewFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Ten_games_make_two_png_pages_at_the_page_width()
    {
        var (regular, bold) = InstalledFont();
        AddGames(10);
        var rows = ReviewSheetModel.Build(_fixture.Snapshot(), ArtRules.Default, _fixture.ArtPath);

        var pages = ReviewSheet.Draw(rows, ArtRules.Default, regular, bold);

        pages.Should().HaveCount(2);

        foreach (var page in pages)
        {
            using var decoded = SKBitmap.Decode(page);
            decoded.Should().NotBeNull();
            decoded.Width.Should().Be(ReviewSheet.PageWidthPx);
        }
    }

    [Fact]
    public void Seventeen_games_make_three_pages()
    {
        var (regular, bold) = InstalledFont();
        AddGames(17);
        var rows = ReviewSheetModel.Build(_fixture.Snapshot(), ArtRules.Default, _fixture.ArtPath);

        ReviewSheet.Draw(rows, ArtRules.Default, regular, bold).Should().HaveCount(3);
    }

    [Fact]
    public void A_page_with_text_is_not_a_blank_page()
    {
        var (regular, bold) = InstalledFont();
        AddGames(1);
        var rows = ReviewSheetModel.Build(_fixture.Snapshot(), ArtRules.Default, _fixture.ArtPath);

        var page = ReviewSheet.Draw(rows, ArtRules.Default, regular, bold).Single();

        using var decoded = SKBitmap.Decode(page);
        var distinctColours = Enumerable.Range(0, decoded.Width)
            .Select(x => decoded.GetPixel(x, 30))
            .Distinct()
            .Count();
        distinctColours.Should().BeGreaterThan(2);
    }

    [Fact]
    public void An_empty_collection_still_gives_one_header_page()
    {
        var (regular, bold) = InstalledFont();

        var pages = ReviewSheet.Draw([], ArtRules.Default, regular, bold);

        pages.Should().HaveCount(1);
    }

    [Fact]
    public void The_header_states_the_thresholds_and_the_shape_margin_in_use()
    {
        var text = ReviewSheet.RulesText(ArtRules.Default);

        text.Should().Contain("fill at least 97%").And.Contain("shape margin 12%").And.Contain("second corner at least 40%")
            .And.Contain("wider by more than 20%");
    }

    private void AddGames(int count)
    {
        var kinds = new[] { SyntheticArtKind.FlatCover, SyntheticArtKind.BoxOnWhite, SyntheticArtKind.FlatWide, SyntheticArtKind.Banner };

        for (var number = 1; number <= count; number++)
        {
            var version = _fixture.Picture($"version-{number}", kinds[number % kinds.Length]);
            var main = _fixture.Picture($"main-{number}", SyntheticArtKind.FlatNarrow);
            _fixture.Game(number, $"Invented Lighthouse {number}", version, main, new VersionDimensions(7.5, 10, 2.5));
        }
    }

    private static (SKTypeface Regular, SKTypeface Bold) InstalledFont()
    {
        foreach (var (regular, bold) in ReviewSheetCommand.FontSearchPaths)
        {
            if (File.Exists(regular) && File.Exists(bold))
            {
                return (SKTypeface.FromFile(regular), SKTypeface.FromFile(bold));
            }
        }

        Assert.Skip("no DejaVu font is installed on this machine, so the drawing cannot be checked here");

        throw new InvalidOperationException("unreachable");
    }
}

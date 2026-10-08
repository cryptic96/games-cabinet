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
    private const float SmallTextSize = 14f;
    private const int ReviewSheetMargin = 22;

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

    [Fact]
    public void The_rules_line_is_wrapped_into_lines_that_fit_the_page_and_give_back_the_whole_text()
    {
        var (regular, _) = InstalledFont();
        using var font = new SKFont(regular, SmallTextSize);
        var widest = new ArtRules(ArtThresholds.Default with { FlatMinFill = 0.98 }, 100, true, 100);

        foreach (var rules in new[] { ArtRules.Default, widest })
        {
            var lines = ReviewSheet.RulesLines(rules, font, ReviewSheet.RulesWidthPx);

            lines.Should().HaveCountGreaterThan(1);
            lines.Should().OnlyContain(line => font.MeasureText(line) <= ReviewSheet.RulesWidthPx);
            Collapsed(string.Join(" ", lines)).Should().Be(Collapsed(ReviewSheet.RulesText(rules)));
        }
    }

    [Fact]
    public void A_rule_that_is_wider_than_the_page_is_broken_at_its_spaces()
    {
        var (regular, _) = InstalledFont();
        using var font = new SKFont(regular, SmallTextSize);

        var lines = ReviewSheet.RulesLines(ArtRules.Default, font, 300);

        lines.Should().HaveCountGreaterThan(3);
        lines.Should().OnlyContain(line => font.MeasureText(line) <= 300);
        Collapsed(string.Join(" ", lines)).Should().Be(Collapsed(ReviewSheet.RulesText(ArtRules.Default)));
    }

    [Fact]
    public void The_page_header_grows_by_one_line_height_per_extra_rules_line_and_the_rows_start_below_it()
    {
        var (regular, bold) = InstalledFont();
        using var font = new SKFont(regular, SmallTextSize);
        var lineCount = ReviewSheet.RulesLines(ArtRules.Default, font, ReviewSheet.RulesWidthPx).Count;
        AddGames(2);
        var rows = ReviewSheetModel.Build(_fixture.Snapshot(), ArtRules.Default, _fixture.ArtPath);

        var page = ReviewSheet.Draw(rows, ArtRules.Default, regular, bold).Single();

        using var decoded = SKBitmap.Decode(page);
        decoded.Height.Should().Be(ReviewSheet.PageHeightPx(2, lineCount));
        ReviewSheet.HeaderHeightPx(lineCount).Should().BeGreaterThan(ReviewSheet.HeaderHeightPx(1));
        ReviewSheet.RulesBaselinePx(lineCount - 1).Should().BeLessThan(ReviewSheet.HeaderHeightPx(lineCount));
    }

    [Fact]
    public void Nothing_of_the_rules_line_is_drawn_past_the_right_margin()
    {
        var (regular, bold) = InstalledFont();
        using var font = new SKFont(regular, SmallTextSize);
        var lineCount = ReviewSheet.RulesLines(ArtRules.Default, font, ReviewSheet.RulesWidthPx).Count;
        AddGames(1);
        var rows = ReviewSheetModel.Build(_fixture.Snapshot(), ArtRules.Default, _fixture.ArtPath);

        var page = ReviewSheet.Draw(rows, ArtRules.Default, regular, bold).Single();

        using var decoded = SKBitmap.Decode(page);
        var pageColour = decoded.GetPixel(decoded.Width - 1, 0);
        for (var y = 40; y < ReviewSheet.HeaderHeightPx(lineCount); y++)
        {
            for (var x = decoded.Width - ReviewSheetMargin; x < decoded.Width; x++)
            {
                decoded.GetPixel(x, y).Should().Be(pageColour, $"pixel {x},{y} lies in the right margin");
            }
        }
    }

    private static string Collapsed(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

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

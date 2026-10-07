using System.Text.RegularExpressions;
using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.Service.Review;
using FluentAssertions;

namespace Cabinet.UnitTests.Review;

/// <summary>Proves each row of the sheet says in plain words what the cabinet did for that game.</summary>
[Trait("Category", "Images")]
public sealed partial class ReviewSheetModelTests : IDisposable
{
    private static readonly VersionDimensions UprightBox = new(7.5, 10, 2.5);

    private readonly ReviewFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void A_flat_version_picture_reads_flat_and_is_chosen_with_a_two_decimal_score()
    {
        var version = _fixture.Picture("version", SyntheticArtKind.FlatCover);
        var main = _fixture.Picture("main", SyntheticArtKind.FlatNarrow);
        _fixture.Game(1, "Invented Lighthouse", version, main, UprightBox);

        var row = Rows().Single();

        row.Verdict.Should().Be("flat");
        row.ScoreText.Should().MatchRegex(TwoDecimals());
        row.ChosenText.Should().Be("chosen: version image");
        row.Pick.Should().Be(ArtPick.VersionImage);
        row.CandidateAPath.Should().NotBeNull();
        row.CandidateBPath.Should().NotBeNull();
        row.ResultArtPath.Should().Be(row.CandidateAPath);
    }

    [Fact]
    public void A_photographed_version_with_a_flat_main_picture_reads_3d_shot_and_chooses_the_main_picture()
    {
        var version = _fixture.Picture("version", SyntheticArtKind.BoxOnWhite);
        var main = _fixture.Picture("main", SyntheticArtKind.FlatCover);
        _fixture.Game(1, "Invented Lighthouse", version, main, UprightBox);

        var row = Rows().Single();

        row.Verdict.Should().Be("3D shot");
        row.ChosenText.Should().Be("chosen: main image");
        row.ResultArtPath.Should().Be(row.CandidateBPath);
    }

    [Fact]
    public void A_picture_that_is_neither_clearly_flat_nor_clearly_photographed_reads_unsure()
    {
        var unsure = new ArtFeatures(0.2, 0.95, 0.3, 0.2, 4);
        var version = _fixture.MeasuredPicture("version", SyntheticArtKind.FlatCover, unsure);
        _fixture.Game(1, "Invented Lighthouse", version, null, UprightBox);

        var row = Rows().Single();

        row.Verdict.Should().Be("unsure");
        row.ScoreText.Should().Be("0.24");
        row.ChosenText.Should().Be("chosen: version image");
    }

    [Fact]
    public void A_game_without_a_version_picture_reads_no_verdict_and_shows_none_in_the_first_column()
    {
        var main = _fixture.Picture("main", SyntheticArtKind.FlatCover);
        _fixture.Game(1, "Invented Lighthouse", null, main, UprightBox);

        var row = Rows().Single();

        row.Verdict.Should().Be("no verdict");
        row.ScoreText.Should().BeNull();
        row.CandidateAPath.Should().BeNull();
        row.CandidateBPath.Should().NotBeNull();
        row.ChosenText.Should().Be("chosen: main image");
    }

    [Fact]
    public void A_main_picture_that_failed_shows_none_in_the_second_column()
    {
        var version = _fixture.Picture("version", SyntheticArtKind.FlatCover);
        var main = _fixture.FailedPicture("main");
        _fixture.Game(1, "Invented Lighthouse", version, main, UprightBox);

        var row = Rows().Single();

        row.CandidateAPath.Should().NotBeNull();
        row.CandidateBPath.Should().BeNull();
        row.ChosenText.Should().Be("chosen: version image");
    }

    [Fact]
    public void A_game_with_no_usable_picture_shows_none_twice_and_the_generated_cover()
    {
        var version = _fixture.Picture("version", SyntheticArtKind.Undecodable);
        var main = _fixture.FailedPicture("main");
        _fixture.Game(1, "Invented Lighthouse", version, main, UprightBox);

        var row = Rows().Single();

        row.CandidateAPath.Should().BeNull();
        row.CandidateBPath.Should().BeNull();
        row.ChosenText.Should().Be("generated cover");
        row.Pick.Should().Be(ArtPick.GeneratedCover);
        row.ResultArtPath.Should().BeNull();
        row.Verdict.Should().Be("no verdict");
    }

    [Fact]
    public void A_picture_whose_file_is_gone_from_the_art_directory_reads_as_none()
    {
        var version = _fixture.Picture("version", SyntheticArtKind.FlatCover);
        _fixture.Game(1, "Invented Lighthouse", version, null, UprightBox);
        foreach (var file in Directory.EnumerateFiles(_fixture.ArtPath))
        {
            File.Delete(file);
        }

        Rows().Single().CandidateAPath.Should().BeNull();
    }

    [Fact]
    public void The_size_source_reads_real_size_cover_shape_estimate_or_default()
    {
        var cover = _fixture.Picture("cover", SyntheticArtKind.FlatCover);
        var banner = _fixture.Picture("banner", SyntheticArtKind.Banner);
        _fixture.Game(1, "Invented Lighthouse One", cover, null, UprightBox);
        _fixture.Game(2, "Invented Lighthouse Two", cover, null, null);
        _fixture.Game(3, "Invented Lighthouse Three", null, null, null);
        _fixture.Game(4, "Invented Lighthouse Four", null, null, null, withDetails: false);
        _fixture.Game(5, "Invented Lighthouse Five", banner, null, UprightBox);

        var sources = Rows().Select(row => row.SizeSource).ToList();

        sources.Should().Equal("real size", "cover shape", "estimate", "default", "cover shape");
    }

    [Fact]
    public void Seventeen_games_are_numbered_from_one_in_collection_order()
    {
        for (var number = 17; number >= 1; number--)
        {
            _fixture.Game(number, $"Invented Lighthouse {number}", null, null, UprightBox);
        }

        var rows = Rows();

        rows.Select(row => row.Position).Should().Equal(Enumerable.Range(1, 17));
        rows.Select(row => row.Title).Should().Equal(Enumerable.Range(1, 17).Select(number => $"Invented Lighthouse {number}"));
    }

    [Fact]
    public void A_blank_title_reads_untitled_game()
    {
        _fixture.Game(1, " ", null, null, UprightBox);

        Rows().Single().Title.Should().Be(ReviewSheetModel.UntitledTitle);
    }

    [Fact]
    public void The_spine_pair_is_the_stored_pair_or_the_palette_tone_of_the_game()
    {
        var cover = _fixture.Picture("cover", SyntheticArtKind.MidGreen);
        _fixture.Game(1, "Invented Lighthouse One", cover, null, UprightBox);
        _fixture.Game(2, "Invented Lighthouse Two", null, null, UprightBox);

        var rows = Rows();

        rows[0].SpineBackground.Should().MatchRegex("^#[0-9a-f]{6}$");
        rows[1].SpineBackground.Should().MatchRegex("^#[0-9a-f]{6}$");
        rows[1].SpineText.Should().MatchRegex("^#[0-9a-f]{6}$");
    }

    private IReadOnlyList<ReviewRow> Rows() => ReviewSheetModel.Build(_fixture.Snapshot(), ArtRules.Default, _fixture.ArtPath);

    [GeneratedRegex(@"^\d\.\d\d$")]
    private static partial Regex TwoDecimals();
}

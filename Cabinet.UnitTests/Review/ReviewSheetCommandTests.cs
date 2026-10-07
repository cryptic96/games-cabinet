using Cabinet.FakeBgg;
using Cabinet.Service.Review;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.UnitTests.Review;

/// <summary>Proves the operator command reports each failure with its own exit code and never prints collection data.</summary>
[Trait("Category", "Images")]
public sealed class ReviewSheetCommandTests : IDisposable
{
    private readonly ReviewFixture _fixture = new();
    private readonly TemporaryDirectory _out = new();

    public void Dispose()
    {
        _fixture.Dispose();
        _out.Dispose();
    }

    [Fact]
    public void No_arguments_print_the_usage_and_exit_with_2()
    {
        var result = Run([]);

        result.ExitCode.Should().Be(2);
        result.Error.Should().Contain("usage: review-sheet");
        result.Output.Should().BeEmpty();
    }

    [Theory]
    [InlineData("--unknown", "value")]
    [InlineData("--state")]
    [InlineData("stray")]
    public void An_unknown_option_or_a_missing_value_exits_with_2(params string[] args)
    {
        Run(args).ExitCode.Should().Be(2);
    }

    [Fact]
    public void A_missing_required_option_exits_with_2()
    {
        Run(["--state", _fixture.StatePath]).ExitCode.Should().Be(2);
        Run(["--out", _out.FullPath]).ExitCode.Should().Be(2);
    }

    [Fact]
    public void A_state_directory_without_a_stored_collection_exits_with_3()
    {
        var result = Run(["--state", _fixture.StatePath, "--out", _out.FullPath]);

        result.ExitCode.Should().Be(3);
        Directory.EnumerateFiles(_out.FullPath).Should().BeEmpty();
    }

    [Fact]
    public void A_font_that_cannot_be_loaded_exits_with_4_and_names_the_package()
    {
        AddStoredGames(2);

        var result = Run(["--state", _fixture.StatePath, "--out", _out.FullPath, "--font", Path.Combine(_out.FullPath, "nonexistent.ttf")]);

        result.ExitCode.Should().Be(4);
        result.Error.Should().Contain("fonts-dejavu-core");
        Directory.EnumerateFiles(_out.FullPath).Should().BeEmpty();
    }

    [Fact]
    public void An_output_path_that_cannot_be_created_exits_with_5()
    {
        RequireInstalledFont();
        AddStoredGames(2);
        var blocker = Path.Combine(_out.FullPath, "blocker");
        File.WriteAllText(blocker, "not a directory");

        var result = Run(["--state", _fixture.StatePath, "--out", Path.Combine(blocker, "pages")]);

        result.ExitCode.Should().Be(5);
    }

    [Fact]
    public void A_successful_run_writes_the_pages_and_prints_counts_only()
    {
        RequireInstalledFont();
        AddStoredGames(10);
        var pagesDirectory = Path.Combine(_out.FullPath, "pages");

        var result = Run(["--state", _fixture.StatePath, "--out", pagesDirectory]);

        result.ExitCode.Should().Be(0);
        Directory.EnumerateFiles(pagesDirectory).Select(Path.GetFileName).Order().Should().Equal("review-01.png", "review-02.png");
        result.Output.Should().Contain("2 pages").And.Contain("10 games");
        result.Output.Should().NotContain("Invented").And.NotContain(_fixture.StatePath).And.NotContain(".webp").And.NotContain("example.org");
        result.Error.Should().BeEmpty();
    }

    private static (int ExitCode, string Output, string Error) Run(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = ReviewSheetCommand.Run(args, output, error);

        return (code, output.ToString(), error.ToString());
    }

    private static void RequireInstalledFont()
    {
        if (!ReviewSheetCommand.FontSearchPaths.Any(paths => File.Exists(paths.Regular)))
        {
            Assert.Skip("no DejaVu font is installed on this machine, so the command cannot draw here");
        }
    }

    private void AddStoredGames(int count)
    {
        for (var number = 1; number <= count; number++)
        {
            var version = _fixture.Picture($"version-{number}", SyntheticArtKind.FlatCover);
            _fixture.Game(number, $"Invented Lighthouse {number}", version, null);
        }

        _fixture.Save();
    }
}

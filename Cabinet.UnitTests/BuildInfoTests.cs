using Cabinet.Domain;
using FluentAssertions;

namespace Cabinet.UnitTests;

/// <summary>Verifies the informational version is split into a version and a commit.</summary>
public class BuildInfoTests
{
    private const string FullCommit = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void Parse_splits_version_and_commit_at_the_plus_sign()
    {
        var build = BuildInfo.Parse($"0.1.0+{FullCommit}");

        build.Version.Should().Be("0.1.0");
        build.Commit.Should().Be(FullCommit);
        build.ShortCommit.Should().Be("0123456");
    }

    [Fact]
    public void Parse_reports_an_unknown_commit_when_the_version_has_no_plus_sign()
    {
        var build = BuildInfo.Parse("1.2.3");

        build.Version.Should().Be("1.2.3");
        build.Commit.Should().Be("unknown");
        build.ShortCommit.Should().Be("unknown");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_falls_back_to_defaults_for_a_blank_value(string? value)
    {
        var build = BuildInfo.Parse(value);

        build.Version.Should().Be("0.0.0");
        build.Commit.Should().Be("unknown");
    }

    [Fact]
    public void Parse_keeps_a_prerelease_version_intact()
    {
        var build = BuildInfo.Parse($"2.0.0-rc.1+{FullCommit}");

        build.Version.Should().Be("2.0.0-rc.1");
        build.Commit.Should().Be(FullCommit);
    }
}

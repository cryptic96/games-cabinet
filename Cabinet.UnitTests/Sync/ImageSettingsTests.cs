using Cabinet.Service.Sync;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Cabinet.UnitTests.Sync;

/// <summary>Verifies the picture settings fall back to their defaults, bind from configuration and reject bad values with the key named.</summary>
[Trait("Category", "Images")]
public sealed class ImageSettingsTests
{
    private static readonly TestEnvironment Environment = new("Production");

    [Fact]
    public void The_committed_appsettings_bind_to_the_documented_defaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(RepositoryPaths.ServiceDirectory(), "appsettings.json"), optional: false)
            .Build();

        var committed = ImageSettings.FromConfiguration(configuration, Environment);
        var defaults = ImageSettings.FromConfiguration(Configure(), Environment);

        defaults.AllowedHosts.Should().BeEquivalentTo("cf.geekdo-images.com");
        defaults.MaxBytes.Should().Be(12 * 1_048_576L);
        defaults.MaxPixels.Should().Be(36_000_000);
        defaults.DownloadGap.Should().Be(TimeSpan.FromMilliseconds(1000));
        defaults.MaxDownloadsPerRun.Should().Be(80);
        defaults.RetryFailedAfter.Should().Be(TimeSpan.FromHours(24));
        defaults.PruneGrace.Should().Be(TimeSpan.FromDays(7));
        defaults.DevelopmentOrigin.Should().BeNull();
        committed.AllowedHosts.Should().BeEquivalentTo(defaults.AllowedHosts);
        (committed with { AllowedHosts = defaults.AllowedHosts }).Should().Be(defaults);
    }

    [Fact]
    public void Values_that_are_set_replace_the_defaults_key_by_key()
    {
        var options = ImageSettings.FromConfiguration(
            Configure(
                ("Images:AllowedHosts", " Cdn.Example.org , images.example.org "),
                ("Images:MaxMegabytes", "5"),
                ("Images:MaxMegapixels", "10"),
                ("Images:DownloadGapMilliseconds", "2500"),
                ("Images:MaxDownloadsPerRun", "3"),
                ("Images:RetryFailedAfterHours", "48"),
                ("Images:PruneGraceDays", "14")),
            Environment);

        options.AllowedHosts.Should().BeEquivalentTo("cdn.example.org", "images.example.org");
        options.MaxBytes.Should().Be(5 * 1_048_576L);
        options.MaxPixels.Should().Be(10_000_000);
        options.DownloadGap.Should().Be(TimeSpan.FromMilliseconds(2500));
        options.MaxDownloadsPerRun.Should().Be(3);
        options.RetryFailedAfter.Should().Be(TimeSpan.FromHours(48));
        options.PruneGrace.Should().Be(TimeSpan.FromDays(14));
    }

    [Theory]
    [InlineData("Images:MaxMegabytes", "0")]
    [InlineData("Images:MaxMegabytes", "51")]
    [InlineData("Images:MaxMegapixels", "0")]
    [InlineData("Images:MaxMegapixels", "101")]
    [InlineData("Images:DownloadGapMilliseconds", "499")]
    [InlineData("Images:DownloadGapMilliseconds", "60001")]
    [InlineData("Images:MaxDownloadsPerRun", "0")]
    [InlineData("Images:MaxDownloadsPerRun", "1001")]
    [InlineData("Images:RetryFailedAfterHours", "0")]
    [InlineData("Images:RetryFailedAfterHours", "721")]
    [InlineData("Images:PruneGraceDays", "0")]
    [InlineData("Images:PruneGraceDays", "366")]
    public void A_number_outside_its_range_stops_startup_with_the_key_named(string key, string value)
    {
        var act = () => ImageSettings.FromConfiguration(Configure((key, value)), Environment);

        act.Should().Throw<InvalidOperationException>().WithMessage($"{key} must be a whole number between*");
    }

    [Theory]
    [InlineData("Images:MaxMegabytes", "twelve")]
    [InlineData("Images:MaxMegabytes", "1.5")]
    [InlineData("Images:MaxMegapixels", "")]
    [InlineData("Images:DownloadGapMilliseconds", "1e3")]
    [InlineData("Images:MaxDownloadsPerRun", "80 ")]
    [InlineData("Images:RetryFailedAfterHours", "-")]
    [InlineData("Images:PruneGraceDays", "7d")]
    public void A_value_that_is_not_a_whole_number_stops_startup_with_the_key_named(string key, string value)
    {
        var act = () => ImageSettings.FromConfiguration(Configure((key, value)), Environment);

        act.Should().Throw<InvalidOperationException>().WithMessage($"{key} must be a whole number between*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",,")]
    [InlineData("https://cf.example.org/")]
    [InlineData("cf.example.org/path")]
    [InlineData("cf.example.org:8443")]
    [InlineData("a b")]
    public void An_empty_or_unusable_host_list_is_refused_with_the_key_named(string value)
    {
        var act = () => ImageSettings.FromConfiguration(Configure(("Images:AllowedHosts", value)), Environment);

        act.Should().Throw<InvalidOperationException>().WithMessage("Images:AllowedHosts must list at least one host name*");
    }

    private static IConfiguration Configure(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value))
            .Build();
}

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

    [Theory]
    [InlineData("Production")]
    [InlineData("Testing")]
    [InlineData("Staging")]
    public void Outside_development_a_configured_origin_is_ignored_and_reported(string environment)
    {
        var options = ImageSettings.FromConfiguration(Configure(("Images:DevelopmentOrigin", "http://127.0.0.1:6190")), new TestEnvironment(environment));

        options.DevelopmentOrigin.Should().BeNull();
        options.DevelopmentOriginIgnored.Should().BeTrue();
        options.Policy.Allows(new Uri("http://127.0.0.1:6190/fake-art/1-main.png")).Should().BeFalse();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void Without_an_origin_nothing_is_reported_as_ignored(string environment)
    {
        var options = ImageSettings.FromConfiguration(Configure(), new TestEnvironment(environment));

        options.DevelopmentOrigin.Should().BeNull();
        options.DevelopmentOriginIgnored.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://127.0.0.1:6190")]
    [InlineData("http://127.0.0.1:6190/")]
    [InlineData(" https://localhost:6191 ")]
    public void In_development_an_http_or_https_origin_is_accepted_and_opens_only_that_origin(string value)
    {
        var options = ImageSettings.FromConfiguration(Configure(("Images:DevelopmentOrigin", value)), new TestEnvironment("Development"));

        options.DevelopmentOrigin.Should().NotBeNull();
        options.DevelopmentOriginIgnored.Should().BeFalse();
        options.Policy.Allows(new Uri($"{options.DevelopmentOrigin!.GetLeftPart(UriPartial.Authority)}/fake-art/1-main.png")).Should().BeTrue();
        options.Policy.Allows(new Uri("http://127.0.0.1:6999/fake-art/1-main.png")).Should().BeFalse();
        options.Policy.Allows(new Uri("https://example.org/picture.png")).Should().BeFalse();
    }

    [Theory]
    [InlineData("http://127.0.0.1:6190/path")]
    [InlineData("http://127.0.0.1:6190/?query=1")]
    [InlineData("http://127.0.0.1:6190/#fragment")]
    [InlineData("ftp://127.0.0.1")]
    [InlineData("http://user@127.0.0.1:6190")]
    [InlineData("127.0.0.1:6190")]
    [InlineData("/relative")]
    public void In_development_a_value_that_is_not_a_plain_origin_is_refused_with_the_key_named(string value)
    {
        var act = () => ImageSettings.FromConfiguration(Configure(("Images:DevelopmentOrigin", value)), new TestEnvironment("Development"));

        act.Should().Throw<InvalidOperationException>().WithMessage("Images:DevelopmentOrigin must be*");
    }

    private static IConfiguration Configure(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value))
            .Build();
}

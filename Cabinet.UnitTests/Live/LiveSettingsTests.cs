using Cabinet.Service.Live;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.UnitTests.Live;

/// <summary>Verifies the live settings bind from configuration, fall back to the default and reject bad values with the key named.</summary>
[Trait("Category", "Configuration")]
public class LiveSettingsTests
{
    [Fact]
    public void The_committed_appsettings_bind_to_one_hundred_connections()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(RepositoryPaths.ServiceDirectory(), "appsettings.json"), optional: false)
            .Build();

        LiveSettings.FromConfiguration(configuration).Should().Be(new LiveOptions(100));
    }

    [Fact]
    public void A_missing_key_falls_back_to_the_default()
    {
        LiveSettings.FromConfiguration(Configure()).Should().Be(new LiveOptions(LiveOptions.DefaultMaxConnections));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("2", 2)]
    [InlineData("10000", 10000)]
    public void A_value_in_range_is_used(string text, int expected)
    {
        LiveSettings.FromConfiguration(Configure(("Live:MaxConnections", text))).MaxConnections.Should().Be(expected);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("10001")]
    [InlineData("-5")]
    [InlineData("many")]
    [InlineData("")]
    public void A_value_out_of_range_or_not_a_number_is_rejected_naming_the_key(string text)
    {
        var act = () => LiveSettings.FromConfiguration(Configure(("Live:MaxConnections", text)));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Live:MaxConnections*");
    }

    [Fact]
    public void Registering_the_live_channel_with_a_bad_value_stops_at_once()
    {
        var act = () => new ServiceCollection().AddCabinetLive(Configure(("Live:MaxConnections", "0")));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Live:MaxConnections*");
    }

    [Fact]
    public void The_limiter_admits_up_to_the_cap_and_frees_a_place_only_for_an_admitted_connection()
    {
        var limiter = new LiveConnectionLimiter(new LiveOptions(2));

        limiter.TryAdmit("a").Should().BeTrue();
        limiter.TryAdmit("b").Should().BeTrue();
        limiter.TryAdmit("c").Should().BeFalse();
        limiter.Release("c");
        limiter.Count.Should().Be(2);
        limiter.Release("a");
        limiter.Count.Should().Be(1);
        limiter.TryAdmit("c").Should().BeTrue();
    }

    private static IConfiguration Configure(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();
}

using Cabinet.Service.Hosting;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.UnitTests.Hosting;

/// <summary>Verifies the host limits bind from configuration, fall back to their defaults and reject bad values with the key named.</summary>
[Trait("Category", "Configuration")]
public class HostLimitSettingsTests
{
    private const string ConnectionsKey = "HostLimits:MaxConcurrentConnections";
    private const string BodyKey = "HostLimits:MaxRequestBodyBytes";
    private const string TimeoutKey = "HostLimits:RequestHeadersTimeoutSeconds";

    [Fact]
    public void Missing_keys_fall_back_to_the_defaults()
    {
        HostLimitSettings.FromConfiguration(Configure()).Should().Be(
            new HostLimitOptions(512, 65536, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void The_committed_appsettings_take_the_defaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(RepositoryPaths.ServiceDirectory(), "appsettings.json"), optional: false)
            .Build();

        HostLimitSettings.FromConfiguration(configuration).Should().Be(
            new HostLimitOptions(
                HostLimitOptions.DefaultMaxConcurrentConnections,
                HostLimitOptions.DefaultMaxRequestBodyBytes,
                TimeSpan.FromSeconds(HostLimitOptions.DefaultRequestHeadersTimeoutSeconds)));
    }

    [Fact]
    public void Values_in_range_are_used()
    {
        var options = HostLimitSettings.FromConfiguration(Configure((ConnectionsKey, "16"), (BodyKey, "10485760"), (TimeoutKey, "120")));

        options.Should().Be(new HostLimitOptions(16, 10_485_760, TimeSpan.FromSeconds(120)));
    }

    [Theory]
    [InlineData(ConnectionsKey, "15")]
    [InlineData(ConnectionsKey, "100001")]
    [InlineData(ConnectionsKey, "many")]
    [InlineData(ConnectionsKey, "")]
    [InlineData(BodyKey, "1023")]
    [InlineData(BodyKey, "10485761")]
    [InlineData(BodyKey, "-1")]
    [InlineData(BodyKey, "big")]
    [InlineData(BodyKey, "")]
    [InlineData(TimeoutKey, "1")]
    [InlineData(TimeoutKey, "121")]
    [InlineData(TimeoutKey, "soon")]
    [InlineData(TimeoutKey, "")]
    public void A_value_out_of_range_or_not_a_number_is_rejected_naming_the_key(string key, string text)
    {
        var act = () => HostLimitSettings.FromConfiguration(Configure((key, text)));

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{key}*");
    }

    [Fact]
    public void Registering_the_limits_with_a_bad_value_stops_at_once()
    {
        var act = () => new ServiceCollection().AddCabinetHostLimits(Configure((TimeoutKey, "0")));

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{TimeoutKey}*");
    }

    private static IConfiguration Configure(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();
}

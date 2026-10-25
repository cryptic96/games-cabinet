using Cabinet.Service.Limits;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.UnitTests.Limits;

/// <summary>Verifies the client limit settings bind from configuration, fall back to the defaults and reject bad values with the full key named.</summary>
[Trait("Category", "Configuration")]
public class ClientLimitSettingsTests
{
    private const string PressesKey = "ClientLimits:SyncPressesPerWindow";
    private const string WindowKey = "ClientLimits:SyncWindowSeconds";

    [Fact]
    public void The_committed_appsettings_bind_to_five_presses_per_minute()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(RepositoryPaths.ServiceDirectory(), "appsettings.json"), optional: false)
            .Build();

        ClientLimitSettings.FromConfiguration(configuration)
            .Should().Be(new ClientLimitOptions(5, TimeSpan.FromSeconds(60), 20, 60, TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void Missing_keys_fall_back_to_the_defaults()
    {
        ClientLimitSettings.FromConfiguration(Configure()).Should().Be(new ClientLimitOptions(
            ClientLimitOptions.DefaultSyncPressesPerWindow,
            TimeSpan.FromSeconds(ClientLimitOptions.DefaultSyncWindowSeconds),
            ClientLimitOptions.DefaultLiveConnectionsPerClient,
            ClientLimitOptions.DefaultLiveConnectsPerWindow,
            TimeSpan.FromSeconds(ClientLimitOptions.DefaultLiveWindowSeconds)));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("7", 7)]
    [InlineData("1000", 1000)]
    public void A_press_count_in_range_is_used(string text, int expected)
    {
        ClientLimitSettings.FromConfiguration(Configure((PressesKey, text))).SyncPressesPerWindow.Should().Be(expected);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("90", 90)]
    [InlineData("3600", 3600)]
    public void A_window_in_range_is_used(string text, int expectedSeconds)
    {
        ClientLimitSettings.FromConfiguration(Configure((WindowKey, text))).SyncWindow
            .Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1001")]
    [InlineData("-5")]
    [InlineData("many")]
    [InlineData("")]
    public void A_press_count_out_of_range_or_not_a_number_is_rejected_naming_the_key(string text)
    {
        var act = () => ClientLimitSettings.FromConfiguration(Configure((PressesKey, text)));

        act.Should().Throw<InvalidOperationException>().WithMessage("*ClientLimits:SyncPressesPerWindow*");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("3601")]
    [InlineData("-5")]
    [InlineData("soon")]
    [InlineData("")]
    public void A_window_out_of_range_or_not_a_number_is_rejected_naming_the_key(string text)
    {
        var act = () => ClientLimitSettings.FromConfiguration(Configure((WindowKey, text)));

        act.Should().Throw<InvalidOperationException>().WithMessage("*ClientLimits:SyncWindowSeconds*");
    }

    [Fact]
    public void Registering_the_limits_with_a_bad_value_stops_at_once()
    {
        var act = () => new ServiceCollection().AddCabinetClientLimits(Configure((PressesKey, "0")));

        act.Should().Throw<InvalidOperationException>().WithMessage("*ClientLimits:SyncPressesPerWindow*");
    }

    private static IConfiguration Configure(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();
}

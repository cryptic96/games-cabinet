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
    private const string LiveConnectionsKey = "ClientLimits:LiveConnectionsPerClient";
    private const string LiveConnectsKey = "ClientLimits:LiveConnectsPerWindow";
    private const string LiveWindowKey = "ClientLimits:LiveWindowSeconds";
    private const string SiteWideKey = "ClientLimits:LiveConnectsSiteWidePerWindow";

    [Fact]
    public void The_committed_appsettings_bind_to_the_decided_limits()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(RepositoryPaths.ServiceDirectory(), "appsettings.json"), optional: false)
            .Build();

        ClientLimitSettings.FromConfiguration(configuration)
            .Should().Be(new ClientLimitOptions(5, TimeSpan.FromSeconds(60), 20, 60, TimeSpan.FromSeconds(60), 600));
    }

    [Fact]
    public void Missing_keys_fall_back_to_the_defaults()
    {
        ClientLimitSettings.FromConfiguration(Configure()).Should().Be(new ClientLimitOptions(
            ClientLimitOptions.DefaultSyncPressesPerWindow,
            TimeSpan.FromSeconds(ClientLimitOptions.DefaultSyncWindowSeconds),
            ClientLimitOptions.DefaultLiveConnectionsPerClient,
            ClientLimitOptions.DefaultLiveConnectsPerWindow,
            TimeSpan.FromSeconds(ClientLimitOptions.DefaultLiveWindowSeconds),
            ClientLimitOptions.DefaultLiveConnectsSiteWide));
    }

    [Fact]
    public void The_code_defaults_are_the_decided_limits()
    {
        ClientLimitSettings.FromConfiguration(Configure()).Should().Be(
            new ClientLimitOptions(5, TimeSpan.FromSeconds(60), 20, 60, TimeSpan.FromSeconds(60), 600));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("250", 250)]
    [InlineData("100000", 100000)]
    public void A_site_wide_live_connect_count_in_range_is_used(string text, int expected)
    {
        ClientLimitSettings.FromConfiguration(Configure((SiteWideKey, text))).LiveConnectsSiteWide.Should().Be(expected);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("100001")]
    [InlineData("-5")]
    [InlineData("many")]
    [InlineData("")]
    public void A_site_wide_live_connect_count_out_of_range_or_not_a_number_is_rejected_naming_the_key(string text)
    {
        var act = () => ClientLimitSettings.FromConfiguration(Configure((SiteWideKey, text)));

        act.Should().Throw<InvalidOperationException>().WithMessage("*ClientLimits:LiveConnectsSiteWidePerWindow*");
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
    [InlineData("600", 600)]
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
    [InlineData("601")]
    [InlineData("-5")]
    [InlineData("soon")]
    [InlineData("")]
    public void A_window_out_of_range_or_not_a_number_is_rejected_naming_the_key(string text)
    {
        var act = () => ClientLimitSettings.FromConfiguration(Configure((WindowKey, text)));

        act.Should().Throw<InvalidOperationException>().WithMessage("*ClientLimits:SyncWindowSeconds*");
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("35", 35)]
    [InlineData("1000", 1000)]
    public void A_live_connection_cap_in_range_is_used(string text, int expected)
    {
        ClientLimitSettings.FromConfiguration(Configure((LiveConnectionsKey, text))).LiveConnectionsPerClient.Should().Be(expected);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("120", 120)]
    [InlineData("10000", 10000)]
    public void A_live_connect_count_in_range_is_used(string text, int expected)
    {
        ClientLimitSettings.FromConfiguration(Configure((LiveConnectsKey, text))).LiveConnectsPerWindow.Should().Be(expected);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("30", 30)]
    [InlineData("600", 600)]
    public void A_live_window_in_range_is_used(string text, int expectedSeconds)
    {
        ClientLimitSettings.FromConfiguration(Configure((LiveWindowKey, text))).LiveWindow
            .Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Theory]
    [InlineData(LiveConnectionsKey, "0")]
    [InlineData(LiveConnectionsKey, "1001")]
    [InlineData(LiveConnectionsKey, "-5")]
    [InlineData(LiveConnectionsKey, "many")]
    [InlineData(LiveConnectionsKey, "")]
    [InlineData(LiveConnectsKey, "0")]
    [InlineData(LiveConnectsKey, "10001")]
    [InlineData(LiveConnectsKey, "-5")]
    [InlineData(LiveConnectsKey, "many")]
    [InlineData(LiveConnectsKey, "")]
    [InlineData(LiveWindowKey, "0")]
    [InlineData(LiveWindowKey, "601")]
    [InlineData(LiveWindowKey, "-5")]
    [InlineData(LiveWindowKey, "soon")]
    [InlineData(LiveWindowKey, "")]
    public void A_live_setting_out_of_range_or_not_a_number_is_rejected_naming_the_key(string key, string text)
    {
        var act = () => ClientLimitSettings.FromConfiguration(Configure((key, text)));

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{key}*");
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

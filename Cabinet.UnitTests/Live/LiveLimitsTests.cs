using Cabinet.Service.Live;
using FluentAssertions;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Cabinet.UnitTests.Live;

/// <summary>
/// Proves the live channel keeps the small limits a low-power host facing the internet needs: tiny messages, short timeouts,
/// no detailed errors, and a web server cap on upgraded connections equal to the live connection cap.
/// </summary>
[Trait("Category", "Configuration")]
public class LiveLimitsTests
{
    [Fact]
    public void The_hub_accepts_only_tiny_messages_keeps_short_timeouts_and_hides_error_details()
    {
        using var provider = Register(new Dictionary<string, string?>());

        var hub = provider.GetRequiredService<IOptions<HubOptions>>().Value;

        hub.EnableDetailedErrors.Should().BeFalse();
        hub.MaximumReceiveMessageSize.Should().Be(1024);
        hub.KeepAliveInterval.Should().Be(TimeSpan.FromSeconds(15));
        hub.ClientTimeoutInterval.Should().Be(TimeSpan.FromSeconds(30));
        hub.HandshakeTimeout.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(null, 100)]
    [InlineData("37", 37)]
    public void The_web_server_caps_upgraded_connections_at_the_live_connection_cap(string? configured, int expected)
    {
        using var provider = Register(new Dictionary<string, string?> { ["Live:MaxConnections"] = configured });

        var kestrel = provider.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        kestrel.Limits.MaxConcurrentUpgradedConnections.Should().Be(expected);
        provider.GetRequiredService<LiveOptions>().MaxConnections.Should().Be(expected);
    }

    private static ServiceProvider Register(IReadOnlyDictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        return new ServiceCollection().AddLogging().AddCabinetLive(configuration).BuildServiceProvider();
    }
}

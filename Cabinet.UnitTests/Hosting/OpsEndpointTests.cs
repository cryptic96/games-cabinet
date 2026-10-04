using Cabinet.Service.Hosting;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Cabinet.UnitTests.Hosting;

/// <summary>Verifies the ops endpoint only accepts loopback hosts.</summary>
public class OpsEndpointTests
{
    [Theory]
    [InlineData("http://127.0.0.1:6080", 6080)]
    [InlineData("http://[::1]:6081", 6081)]
    [InlineData("http://localhost:6080", 6080)]
    public void FromConfiguration_accepts_loopback_hosts(string url, int expectedPort)
    {
        var configuration = BuildConfiguration(url);

        var port = OpsEndpoint.FromConfiguration(configuration);

        port.Should().Be(expectedPort);
    }

    [Theory]
    [InlineData("http://0.0.0.0:6080")]
    [InlineData("http://*:6080")]
    [InlineData("http://+:6080")]
    [InlineData("http://192.0.2.10:6080")]
    public void FromConfiguration_rejects_non_loopback_hosts(string url)
    {
        var configuration = BuildConfiguration(url);

        var act = () => OpsEndpoint.FromConfiguration(configuration);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FromConfiguration_rejects_a_missing_url()
    {
        var configuration = BuildConfiguration(null);

        var act = () => OpsEndpoint.FromConfiguration(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Kestrel:Endpoints:Ops:Url*");
    }

    private static IConfiguration BuildConfiguration(string? opsUrl)
    {
        var values = new Dictionary<string, string?>();
        if (opsUrl is not null)
        {
            values["Kestrel:Endpoints:Ops:Url"] = opsUrl;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}

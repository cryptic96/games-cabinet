using System.Net;
using System.Text.Json;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>Verifies health is reachable on the ops listener only and reports the running version.</summary>
public class HealthEndpointTests
{
    [Fact]
    public async Task Health_is_served_on_the_ops_port_only()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var ops = factory.CreateOpsClient();
        using var publicClient = factory.CreatePublicClient();

        using var opsResponse = await ops.GetAsync("/health", TestContext.Current.CancellationToken);
        using var publicResponse = await publicClient.GetAsync("/health", TestContext.Current.CancellationToken);

        opsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        opsResponse.ShouldHaveMediaType("application/json");
        publicResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Health_is_healthy_on_a_fresh_host_with_no_state()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var ops = factory.CreateOpsClient();

        var body = await ops.GetStringAsync("/health", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);

        document.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        document.RootElement.GetProperty("version").GetString().Should().NotBeNullOrWhiteSpace();
        document.RootElement.GetProperty("commit").GetString().Should().NotBeNullOrWhiteSpace();
    }
}

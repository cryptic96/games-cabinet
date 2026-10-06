using System.Net;
using System.Net.Sockets;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>Verifies the test host survives a chosen port being taken before Kestrel binds it.</summary>
public class TestHostPortTests
{
    [Fact]
    public async Task A_port_taken_before_binding_is_replaced_with_a_free_one()
    {
        using var squatter = new TcpListener(IPAddress.Loopback, 0);
        squatter.Start();
        var takenPort = ((IPEndPoint)squatter.LocalEndpoint).Port;
        var picks = 0;

        await using var factory = new CabinetWebApplicationFactory(
            new Dictionary<string, string?>(),
            () =>
            {
                var ports = CabinetWebApplicationFactory.PickFreeLoopbackPorts();
                return ++picks == 1 ? (takenPort, ports.Ops) : ports;
            });
        using var publicClient = factory.CreatePublicClient();
        using var ops = factory.CreateOpsClient();

        using var pageResponse = await publicClient.GetAsync("/", TestContext.Current.CancellationToken);
        using var healthResponse = await ops.GetAsync("/health", TestContext.Current.CancellationToken);

        picks.Should().Be(2);
        factory.PublicPort.Should().NotBe(takenPort);
        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void Picked_ports_are_two_different_ports()
    {
        var (publicPort, opsPort) = CabinetWebApplicationFactory.PickFreeLoopbackPorts();

        publicPort.Should().NotBe(opsPort);
    }
}

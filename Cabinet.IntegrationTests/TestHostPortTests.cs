using System.Net;
using System.Net.Sockets;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

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
                return ++picks == 1 ? (takenPort, ports.Ops, ports.Owner) : ports;
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
    public async Task An_owner_port_taken_before_binding_is_replaced_with_a_free_one()
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
                return ++picks == 1 ? (ports.Public, ports.Ops, takenPort) : ports;
            });
        using var owner = factory.CreateOwnerClient();

        using var response = await owner.GetAsync("/owner/settings", TestContext.Current.CancellationToken);

        picks.Should().Be(2);
        factory.OwnerPort.Should().NotBe(takenPort);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_owner_listener_is_bound_to_loopback_on_its_own_port()
    {
        await using var factory = new CabinetWebApplicationFactory();
        var addresses = factory.ServingServices.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses;

        factory.OwnerPort.Should().NotBe(factory.PublicPort);
        factory.OwnerPort.Should().NotBe(factory.OpsPort);
        addresses.Should().Contain($"http://127.0.0.1:{factory.OwnerPort}");
    }

    [Fact]
    public void A_port_clash_found_when_listening_is_retried_like_one_found_when_binding()
    {
        var foundWhenBinding = new IOException("Failed to bind.", new AddressInUseException("Address already in use"));
        var foundWhenListening = new SocketException((int)SocketError.AddressAlreadyInUse);

        CabinetWebApplicationFactory.IsPortClash(foundWhenBinding).Should().BeTrue();
        CabinetWebApplicationFactory.IsPortClash(foundWhenListening).Should().BeTrue();
    }

    [Fact]
    public void Other_start_failures_are_not_retried_as_port_clashes()
    {
        CabinetWebApplicationFactory.IsPortClash(new IOException("Disk full.")).Should().BeFalse();
        CabinetWebApplicationFactory.IsPortClash(new SocketException((int)SocketError.AccessDenied)).Should().BeFalse();
        CabinetWebApplicationFactory.IsPortClash(new InvalidOperationException("Bad setting.")).Should().BeFalse();
    }

    [Fact]
    public void Picked_ports_are_three_different_ports()
    {
        var (publicPort, opsPort, ownerPort) = CabinetWebApplicationFactory.PickFreeLoopbackPorts();

        publicPort.Should().NotBe(opsPort);
        publicPort.Should().NotBe(ownerPort);
        opsPort.Should().NotBe(ownerPort);
    }
}

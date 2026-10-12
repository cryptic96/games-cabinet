using System.Net;
using System.Net.Sockets;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>Verifies the owner tools exist only on their own port and answer only to clients inside the configured ranges.</summary>
public class OwnerWallTests
{
    private const string NotAvailableText = "Not available from this address.";

    [Fact]
    public async Task The_settings_page_answers_on_the_owner_port_from_a_home_address()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var owner = factory.CreateOwnerClient();

        using var response = await owner.GetAsync("/owner/settings", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.ShouldHaveMediaType("text/html");
        html.Should().Contain("Owner tools");
        html.Should().Contain("<html lang=\"en\">");
    }

    [Fact]
    public async Task A_client_address_outside_every_range_gets_a_bare_refusal()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var owner = factory.CreateOwnerClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/owner/settings");
        request.Headers.Add("X-Forwarded-For", "203.0.113.9");

        using var response = await owner.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.ShouldHaveMediaType("text/plain");
        body.Should().Be(NotAvailableText);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task A_forwarded_client_address_inside_a_range_gets_through()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var owner = factory.CreateOwnerClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/owner/settings");
        request.Headers.Add("X-Forwarded-For", "192.0.2.10");

        using var response = await owner.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_settings_page_does_not_exist_on_the_public_port()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var publicClient = factory.CreatePublicClient();

        using var response = await publicClient.GetAsync("/owner/settings", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task A_path_the_site_does_not_know_answers_the_same_empty_not_found_as_an_owner_path()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var publicClient = factory.CreatePublicClient();

        using var unknown = await publicClient.GetAsync("/nothing-here", TestContext.Current.CancellationToken);
        using var owner = await publicClient.GetAsync("/owner/settings", TestContext.Current.CancellationToken);

        unknown.StatusCode.Should().Be(owner.StatusCode);
        (await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should().Be(await owner.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_cabinet_answers_on_both_ports()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var publicClient = factory.CreatePublicClient();
        using var owner = factory.CreateOwnerClient();

        using var onPublic = await publicClient.GetAsync("/", TestContext.Current.CancellationToken);
        using var onOwner = await owner.GetAsync("/", TestContext.Current.CancellationToken);

        onPublic.StatusCode.Should().Be(HttpStatusCode.OK);
        onOwner.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Without_an_owner_endpoint_the_owner_page_answers_not_found_on_the_public_port()
    {
        await using var factory = new CabinetWebApplicationFactory(
            new Dictionary<string, string?> { ["Kestrel:Endpoints:Owner:Url"] = null });
        using var publicClient = factory.CreatePublicClient();

        using var response = await publicClient.GetAsync("/owner/settings", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_owner_page_sets_no_cookie_other_than_the_antiforgery_cookie()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var owner = factory.CreateOwnerClient();

        using var response = await owner.GetAsync("/owner/settings", TestContext.Current.CancellationToken);
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];

        cookies.Should().OnlyContain(cookie => cookie.StartsWith("cabinet-owner-af", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_forwarded_address_from_the_known_proxy_inside_a_range_gets_through()
    {
        await using var host = await UntrustedPeerHost.StartAsync("198.51.100.1", "192.0.2.0/24");

        var (status, _) = await host.GetProbeAsync("198.51.100.1", UntrustedPeerHost.OwnerPort, "192.0.2.10");

        status.Should().Be(200);
    }

    [Fact]
    public async Task A_forwarded_address_from_the_known_proxy_outside_every_range_is_refused()
    {
        await using var host = await UntrustedPeerHost.StartAsync("198.51.100.1", "192.0.2.0/24");

        var (status, _) = await host.GetProbeAsync("198.51.100.1", UntrustedPeerHost.OwnerPort, "203.0.113.9");

        status.Should().Be(403);
    }

    [Fact]
    public async Task A_forged_forwarded_address_from_an_untrusted_peer_is_ignored_and_the_peer_is_refused()
    {
        await using var host = await UntrustedPeerHost.StartAsync("198.51.100.1", "192.0.2.0/24");

        var (ownerStatus, _) = await host.GetProbeAsync("198.51.100.7", UntrustedPeerHost.OwnerPort, "192.0.2.10");
        var (publicStatus, publicBody) = await host.GetProbeAsync("198.51.100.7", UntrustedPeerHost.PublicPort, "192.0.2.10");

        ownerStatus.Should().Be(403);
        publicStatus.Should().Be(200);
        publicBody.Should().Be("198.51.100.7");
    }

    [Fact]
    public async Task Every_request_on_one_kept_alive_owner_connection_is_range_checked_on_its_own()
    {
        await using var factory = new CabinetWebApplicationFactory();
        var connections = 0;
        using var handler = new SocketsHttpHandler
        {
            MaxConnectionsPerServer = 1,
            ConnectCallback = async (context, cancellationToken) =>
            {
                Interlocked.Increment(ref connections);
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(context.DnsEndPoint, cancellationToken);

                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };
        using var owner = new HttpClient(handler) { BaseAddress = new Uri($"http://127.0.0.1:{factory.OwnerPort}") };

        var statuses = new List<HttpStatusCode>();
        foreach (var forwardedFor in new string?[] { "192.0.2.10", "203.0.113.9", null })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/owner/settings");
            if (forwardedFor is not null)
            {
                request.Headers.Add("X-Forwarded-For", forwardedFor);
            }

            using var response = await owner.SendAsync(request, TestContext.Current.CancellationToken);
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            statuses.Add(response.StatusCode);
        }

        statuses.Should().Equal(HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.OK);
        connections.Should().Be(1);
    }
}

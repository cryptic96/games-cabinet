using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Cabinet.Service.Owner;

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

    [Theory]
    [InlineData(null)]
    [InlineData("not-an-address")]
    public async Task A_request_from_the_known_proxy_without_a_usable_forwarded_address_is_refused_even_when_a_range_holds_the_proxy(string? forwardedFor)
    {
        await using var host = await UntrustedPeerHost.StartAsync("198.51.100.1", "198.51.100.0/24");

        var (status, _) = await host.GetProbeAsync("198.51.100.1", UntrustedPeerHost.OwnerPort, forwardedFor);

        status.Should().Be(403);
    }

    [Fact]
    public async Task An_in_range_peer_that_is_not_the_configured_proxy_is_refused_with_or_without_a_forwarded_address()
    {
        await using var host = await UntrustedPeerHost.StartAsync("198.51.100.1", "192.0.2.0/24");

        var (withoutHeader, _) = await host.GetProbeAsync("192.0.2.50", UntrustedPeerHost.OwnerPort, null);
        var (withHeader, _) = await host.GetProbeAsync("192.0.2.50", UntrustedPeerHost.OwnerPort, "192.0.2.10");

        withoutHeader.Should().Be(403);
        withHeader.Should().Be(403);
    }

    [Fact]
    public async Task Loopback_is_not_believed_as_a_forwarding_proxy_unless_it_is_configured()
    {
        await using var host = await UntrustedPeerHost.StartAsync("198.51.100.1", "192.0.2.0/24");

        var (status, _) = await host.GetProbeAsync("127.0.0.1", UntrustedPeerHost.OwnerPort, "192.0.2.10");

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

    [Theory]
    [InlineData("/Owner/Settings")]
    [InlineData("/OWNER/settings")]
    [InlineData("/" + "/owner/settings")]
    [InlineData("/owner/../owner/settings")]
    [InlineData("/%6Fwner/settings")]
    [InlineData("/owner%2Fsettings")]
    [InlineData("/owner/settings/")]
    [InlineData("/owner")]
    public async Task A_path_trick_toward_an_owner_page_answers_not_found_on_the_public_port(string requestTarget)
    {
        await using var factory = new CabinetWebApplicationFactory();

        var (status, body) = await RawHttp.SendAsync(
            factory.PublicPort, requestTarget, headers: null, TestContext.Current.CancellationToken);

        status.Should().Be(404);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task A_host_header_naming_the_owner_port_does_not_open_the_owner_page_on_the_public_port()
    {
        await using var factory = new CabinetWebApplicationFactory();
        var spoofed = new Dictionary<string, string> { ["Host"] = $"127.0.0.1:{factory.OwnerPort}" };

        var (status, body) = await RawHttp.SendAsync(
            factory.PublicPort, "/owner/settings", spoofed, TestContext.Current.CancellationToken);

        status.Should().Be(404);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task Every_endpoint_whose_route_starts_with_owner_carries_the_owner_only_marker()
    {
        await using var factory = new CabinetWebApplicationFactory();
        var endpoints = factory.ServingServices.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => StartsWithOwner(endpoint.RoutePattern.RawText))
            .ToList();

        endpoints.Should().NotBeEmpty("the owner pages are endpoints and the check must have something to look at");
        endpoints.Should().OnlyContain(
            endpoint => endpoint.Metadata.GetMetadata<OwnerOnlyAttribute>() != null,
            "an owner route without the marker would answer on the public port");
    }

    [Fact]
    public async Task Every_static_asset_that_only_the_owner_pages_use_carries_the_owner_only_marker()
    {
        await using var factory = new CabinetWebApplicationFactory();
        var endpoints = factory.ServingServices.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => OwnerAccess.IsOwnerAsset(endpoint.RoutePattern.RawText))
            .ToList();

        endpoints.Should().Contain(endpoint => endpoint.RoutePattern.RawText!.EndsWith("owner-locations.js", StringComparison.Ordinal));
        endpoints.Should().Contain(endpoint => endpoint.RoutePattern.RawText!.EndsWith("owner.css", StringComparison.Ordinal));
        endpoints.Should().OnlyContain(
            endpoint => endpoint.Metadata.GetMetadata<OwnerOnlyAttribute>() != null,
            "an owner script or stylesheet without the marker would be served on the public port");
    }

    [Theory]
    [InlineData("/js/owner-locations.js")]
    [InlineData("/js/owner-link.js")]
    [InlineData("/js/owner-settings.js")]
    [InlineData("/js/owner-cabinet.js")]
    [InlineData("/css/owner.css")]
    public async Task The_owner_scripts_and_stylesheet_answer_not_found_on_the_public_port_and_are_served_on_the_owner_port(string path)
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var publicClient = factory.CreatePublicClient();
        using var owner = factory.CreateOwnerClient();

        using var onPublic = await publicClient.GetAsync(path, TestContext.Current.CancellationToken);
        using var onOwner = await owner.GetAsync(path, TestContext.Current.CancellationToken);

        onPublic.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await onPublic.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        onOwner.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/cabinet/layout?sample=65&profile=desktop")]
    [InlineData("/cabinet/layout?sample=12&profile=phone")]
    [InlineData("/cabinet/cards?sample=65&profile=desktop")]
    [InlineData("/cabinet/cards?sample=12&profile=phone")]
    public async Task The_public_page_and_its_data_never_mention_an_owner_path(string path)
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var publicClient = factory.CreatePublicClient();

        using var response = await publicClient.GetAsync(path, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotContainEquivalentOf("/owner");
    }

    [Fact]
    public async Task Every_shared_page_script_never_mentions_an_owner_path()
    {
        await using var factory = new CabinetWebApplicationFactory();
        var scripts = Directory
            .EnumerateFiles(Path.Combine(factory.ServingServices.GetRequiredService<IHostEnvironment>().ContentRootPath, "wwwroot", "js"), "*.js")
            .Where(file => !Path.GetFileName(file).StartsWith("owner-", StringComparison.Ordinal))
            .ToList();

        scripts.Should().NotBeEmpty();
        foreach (var script in scripts)
        {
            (await File.ReadAllTextAsync(script, TestContext.Current.CancellationToken))
                .Should().NotContainEquivalentOf("/owner", because: Path.GetFileName(script));
        }
    }

    [Fact]
    public async Task A_form_post_of_more_than_the_body_limit_answers_payload_too_large()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var owner = factory.CreateOwnerClient();
        var (cookie, token) = IssueAntiforgery(factory.ServingServices);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/owner/locations")
        {
            Content = new StringContent("filler=" + new string('a', 70 * 1024), Encoding.UTF8, new MediaTypeHeaderValue("application/x-www-form-urlencoded")),
        };
        request.Headers.Add("Cookie", cookie);
        request.Headers.Add("RequestVerificationToken", token);

        using var response = await owner.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    private static bool StartsWithOwner(string? rawText) =>
        (rawText ?? string.Empty).TrimStart('/').StartsWith("owner", StringComparison.OrdinalIgnoreCase);

    private static (string Cookie, string Token) IssueAntiforgery(IServiceProvider services)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        var tokens = services.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
        var setCookie = context.Response.Headers.SetCookie.ToString();

        return (setCookie.Split(';')[0], tokens.RequestToken!);
    }
}

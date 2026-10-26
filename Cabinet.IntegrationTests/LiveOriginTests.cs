using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Bgg;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves the live channel admits only the site's own pages: a request whose Origin names another host or port, or is the
/// opaque origin, is refused with 403 and no body on the negotiate request, the WebSocket upgrade and the Server-Sent Events
/// stream, on the public port and the owner port, while a request with the site's own origin or none at all is admitted, and a
/// refused foreign page spends none of a visitor's connection budget.
/// </summary>
[Trait("Category", "Live")]
public class LiveOriginTests
{
    private const string NegotiatePath = "/cabinet/live/negotiate?negotiateVersion=1";
    private const string ConnectPath = "/cabinet/live?id=unknown";
    private const string UpgradePath = "/cabinet/live";

    public enum Listener
    {
        Public,
        Owner,
    }

    [Theory]
    [InlineData(Listener.Public)]
    [InlineData(Listener.Owner)]
    public async Task A_negotiate_with_the_sites_own_origin_or_no_origin_is_admitted(Listener listener)
    {
        await using var factory = CreateFactory();
        using var client = ClientOn(factory, listener);
        var port = PortOf(factory, listener);

        var same = await SendNegotiate(client, $"http://127.0.0.1:{port}");
        var none = await SendNegotiate(client, origin: null);

        same.StatusCode.Should().Be(HttpStatusCode.OK);
        none.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(Listener.Public, "https://evil.example.org")]
    [InlineData(Listener.Public, "http://127.0.0.1:1")]
    [InlineData(Listener.Public, "null")]
    [InlineData(Listener.Owner, "https://evil.example.org")]
    [InlineData(Listener.Owner, "http://127.0.0.1:1")]
    [InlineData(Listener.Owner, "null")]
    public async Task A_negotiate_from_a_foreign_origin_is_refused_with_no_body(Listener listener, string origin)
    {
        await using var factory = CreateFactory();
        using var client = ClientOn(factory, listener);

        var response = await SendNegotiate(client, origin);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Body.Should().BeEmpty();
    }

    [Theory]
    [InlineData(Listener.Public)]
    [InlineData(Listener.Owner)]
    public async Task The_other_listeners_port_is_not_the_sites_origin_for_this_listener(Listener listener)
    {
        await using var factory = CreateFactory();
        using var client = ClientOn(factory, listener);
        var other = PortOf(factory, listener == Listener.Public ? Listener.Owner : Listener.Public);

        var response = await SendNegotiate(client, $"http://127.0.0.1:{other}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(Listener.Public)]
    [InlineData(Listener.Owner)]
    public async Task A_web_socket_upgrade_from_a_foreign_origin_is_refused(Listener listener)
    {
        await using var factory = CreateFactory();
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Origin", "https://evil.example.org");
        socket.Options.CollectHttpResponseDetails = true;

        var act = () => socket.ConnectAsync(new Uri($"ws://127.0.0.1:{PortOf(factory, listener)}{ConnectPath}"), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WebSocketException>();
        socket.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(Listener.Public)]
    [InlineData(Listener.Owner)]
    public async Task A_server_sent_events_stream_from_a_foreign_origin_is_refused(Listener listener)
    {
        await using var factory = CreateFactory();
        using var client = ClientOn(factory, listener);
        using var request = new HttpRequestMessage(HttpMethod.Get, ConnectPath);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Add("Origin", "https://evil.example.org");

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Foreign_origin_negotiations_spend_none_of_the_connection_budget()
    {
        var settings = new Dictionary<string, string?> { ["ClientLimits:LiveConnectsPerWindow"] = "1" };
        await using var factory = CreateFactory(settings);
        using var client = factory.CreatePublicClient();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            (await SendNegotiate(client, "https://evil.example.org")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        var own = await SendNegotiate(client, $"http://127.0.0.1:{factory.PublicPort}");

        own.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_https_origin_without_a_port_matches_a_forwarded_https_request_without_a_port()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreatePublicClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, NegotiatePath);
        request.Headers.Host = "cabinet.example.com";
        request.Headers.Add("Origin", "https://cabinet.example.com");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("wss", "https://cabinet.example.com", true)]
    [InlineData("wss", "https://elsewhere.example.org", false)]
    [InlineData("wss", "http://cabinet.example.com", false)]
    [InlineData("ws", "http://cabinet.example.com", true)]
    [InlineData("ws", "https://cabinet.example.com", false)]
    [InlineData("https", "https://cabinet.example.com", true)]
    public async Task A_negotiate_behind_the_proxy_compares_the_origin_with_the_forwarded_scheme(
        string forwardedProto, string origin, bool admitted)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreatePublicClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, NegotiatePath);
        request.Headers.Host = "cabinet.example.com";
        request.Headers.Add("Origin", origin);
        request.Headers.Add("X-Forwarded-Proto", forwardedProto);
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(admitted ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("wss", "https://cabinet.example.com", true)]
    [InlineData("wss", "https://elsewhere.example.org", false)]
    [InlineData("wss", "http://cabinet.example.com", false)]
    [InlineData("ws", "http://cabinet.example.com", true)]
    [InlineData("https", "https://cabinet.example.com", true)]
    public async Task A_web_socket_upgrade_behind_the_proxy_compares_the_origin_with_the_forwarded_scheme(
        string forwardedProto, string origin, bool admitted)
    {
        await using var factory = CreateFactory();
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Host", "cabinet.example.com");
        socket.Options.SetRequestHeader("Origin", origin);
        socket.Options.SetRequestHeader("X-Forwarded-Proto", forwardedProto);
        socket.Options.SetRequestHeader("X-Forwarded-For", "203.0.113.7");
        socket.Options.CollectHttpResponseDetails = true;

        var address = new Uri($"ws://127.0.0.1:{factory.PublicPort}{UpgradePath}");

        if (admitted)
        {
            await socket.ConnectAsync(address, TestContext.Current.CancellationToken);

            socket.State.Should().Be(WebSocketState.Open);
            socket.HttpStatusCode.Should().Be(HttpStatusCode.SwitchingProtocols);

            return;
        }

        var refused = () => socket.ConnectAsync(address, TestContext.Current.CancellationToken);

        await refused.Should().ThrowAsync<WebSocketException>();
        socket.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static CabinetWebApplicationFactory CreateFactory(IReadOnlyDictionary<string, string?>? settings = null)
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));

        return new CabinetWebApplicationFactory(
            settings ?? new Dictionary<string, string?>(),
            services =>
            {
                services.AddSingleton(SyncHarness.Options());
                services.AddSingleton<IRequestPacer>(new NoWaitPacer());
                services.AddSingleton<TimeProvider>(SyncHarness.NewClock());
                services.AddHttpClient<ICollectionSource, BggClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
                services.AddHttpClient<IEnrichmentSource, BggThingClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
            });
    }

    private static HttpClient ClientOn(CabinetWebApplicationFactory factory, Listener listener) =>
        listener == Listener.Public ? factory.CreatePublicClient() : factory.CreateOwnerClient();

    private static int PortOf(CabinetWebApplicationFactory factory, Listener listener) =>
        listener == Listener.Public ? factory.PublicPort : factory.OwnerPort;

    private static async Task<(HttpStatusCode StatusCode, string Body)> SendNegotiate(HttpClient client, string? origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, NegotiatePath);
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}

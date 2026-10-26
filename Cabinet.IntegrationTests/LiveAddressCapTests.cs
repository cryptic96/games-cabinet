using System.Collections.Concurrent;
using System.Net;
using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Bgg;
using Cabinet.Service.Live;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves one client address can hold a game night's worth of live pages and no more, on both transports: the page over the
/// cap is refused quietly before any handshake, the places free up as pages close, other addresses are unaffected, new
/// connections are counted per window on the negotiate request only, and a Server-Sent Events page is never refused for
/// the messages it posts to stay connected. A site-wide window of new connections backs the per-address ones, so a crowd
/// of different addresses cannot open connections faster than the site accepts.
/// </summary>
[Trait("Category", "Live")]
public class LiveAddressCapTests
{
    private const string OtherClient = "203.0.113.8";
    private const string SharedClient = "203.0.113.7";
    private const string ThirdClient = "203.0.113.9";
    private const int ShippedCap = 20;
    private const int SeveralPings = 4;

    [Theory]
    [InlineData(HttpTransportType.WebSockets)]
    [InlineData(HttpTransportType.ServerSentEvents)]
    public async Task A_second_page_from_one_address_is_refused_at_a_cap_of_one_and_another_address_is_not(HttpTransportType transport)
    {
        await using var host = Host(new Dictionary<string, string?> { ["ClientLimits:LiveConnectionsPerClient"] = "1" });
        await using var pages = new PageSet(host.Factory, transport);
        await pages.StartHeldAsync(forwardedFor: null, expectedCount: 1);
        var refusalsBefore = host.Refusals.Refusals;

        var refused = await pages.TryStartAsync(forwardedFor: null);

        refused.Should().BeFalse("the address already holds its one place");
        host.Refusals.Refusals.Should().BeGreaterThan(refusalsBefore, "the refusal is the server's 429 on the connect request");
        host.Limiter.Count.Should().Be(1, "a refused page takes no place");

        await pages.StartHeldAsync(OtherClient, expectedCount: 2);
    }

    [Theory]
    [InlineData(HttpTransportType.WebSockets)]
    [InlineData(HttpTransportType.ServerSentEvents)]
    public async Task A_place_freed_by_a_closing_page_can_be_taken_by_the_next_page_from_the_same_address(HttpTransportType transport)
    {
        await using var host = Host(new Dictionary<string, string?> { ["ClientLimits:LiveConnectionsPerClient"] = "1" });
        await using var pages = new PageSet(host.Factory, transport);
        var first = await pages.StartHeldAsync(forwardedFor: null, expectedCount: 1);
        (await pages.TryStartAsync(forwardedFor: null)).Should().BeFalse();

        await first.Connection.StopAsync(TestContext.Current.CancellationToken);
        await SyncHarness.WaitUntil(() => Task.FromResult(host.Limiter.Count == 0));

        await SyncHarness.WaitUntil(() => pages.TryStartAsync(forwardedFor: null));
        await SyncHarness.WaitUntil(() => Task.FromResult(host.Limiter.Count == 1));
    }

    [Fact]
    public async Task Negotiate_requests_are_counted_per_address_in_a_window_and_refused_with_a_wait()
    {
        await using var host = Host(new Dictionary<string, string?> { ["ClientLimits:LiveConnectsPerWindow"] = "2" });
        using var client = host.Factory.CreatePublicClient();

        var first = await Negotiate(client, forwardedFor: null);
        var second = await Negotiate(client, forwardedFor: null);
        using var third = await NegotiateResponse(client, forwardedFor: null);
        var other = await Negotiate(client, OtherClient);

        first.Should().Be(HttpStatusCode.OK);
        second.Should().Be(HttpStatusCode.OK);
        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        third.Headers.RetryAfter!.Delta!.Value.TotalSeconds.Should().BeInRange(1, 60);
        third.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await third.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        other.Should().Be(HttpStatusCode.OK, "another address has a window of its own");
    }

    [Fact]
    public async Task Connections_that_skip_negotiation_are_counted_in_the_window_and_the_one_over_it_is_refused_quietly()
    {
        await using var host = Host(new Dictionary<string, string?> { ["ClientLimits:LiveConnectsPerWindow"] = "2" });
        await using var pages = new PageSet(host.Factory, HttpTransportType.WebSockets, skipNegotiation: true);
        using var client = host.Factory.CreatePublicClient();
        await pages.StartHeldAsync(forwardedFor: null, expectedCount: 1);
        await pages.StartHeldAsync(forwardedFor: null, expectedCount: 2);
        var refusalsBefore = host.Refusals.Refusals;
        var problemsBefore = host.Logs.Problems.Count;

        var refused = await pages.TryStartAsync(forwardedFor: null);
        using var plain = await client.GetAsync(LiveEndpoints.Route, TestContext.Current.CancellationToken);

        refused.Should().BeFalse("the window of two new connections is spent, and skipping the negotiation does not escape it");
        host.Refusals.Refusals.Should().BeGreaterThan(refusalsBefore);
        plain.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        plain.Headers.RetryAfter!.Delta!.Value.TotalSeconds.Should().BeInRange(1, 60);
        plain.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await plain.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        host.Limiter.Count.Should().Be(2, "a refused connection takes no place");
        host.Logs.Problems.Skip(problemsBefore).Should().BeEmpty("a quiet refusal writes nothing at Warning or above");

        var other = await pages.TryStartAsync(OtherClient);

        other.Should().BeTrue("another address has a window of its own");
        await SyncHarness.WaitUntil(() => Task.FromResult(host.Limiter.Count == 3));
    }

    [Fact]
    public async Task A_negotiated_connection_is_counted_once_so_a_window_of_two_admits_two_pages_and_refuses_the_third()
    {
        await using var host = Host(new Dictionary<string, string?> { ["ClientLimits:LiveConnectsPerWindow"] = "2" });
        await using var pages = new PageSet(host.Factory, HttpTransportType.WebSockets);
        await pages.StartHeldAsync(forwardedFor: null, expectedCount: 1);
        await pages.StartHeldAsync(forwardedFor: null, expectedCount: 2);

        var refused = await pages.TryStartAsync(forwardedFor: null);

        refused.Should().BeFalse("two negotiated pages spent the window of two, one negotiate request each");
        host.Limiter.Count.Should().Be(2);
    }

    [Fact]
    public async Task A_server_sent_events_page_outlives_several_pings_under_a_window_of_one_connect()
    {
        await using var host = Host(new Dictionary<string, string?> { ["ClientLimits:LiveConnectsPerWindow"] = "1" });
        using var client = host.Factory.CreatePublicClient();
        await using var pages = new PageSet(host.Factory, HttpTransportType.ServerSentEvents);
        var page = await pages.StartHeldAsync(forwardedFor: null, expectedCount: 1, keepAlive: TimeSpan.FromMilliseconds(500));

        var refusalsBefore = host.Refusals.Refusals;

        await SyncHarness.WaitUntil(() => Task.FromResult(host.Refusals.Messages >= SeveralPings));
        using var press = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);
        await SyncHarness.WaitUntil(() => Task.FromResult(page.ReceivedCount >= 2));

        host.Refusals.Refusals.Should().Be(refusalsBefore, "the posted pings are messages, which no live limit counts");
        page.Connection.State.Should().Be(HubConnectionState.Connected);
        host.Limiter.Count.Should().Be(1);
    }

    [Fact]
    public async Task Negotiate_requests_are_counted_site_wide_and_a_new_address_is_refused_once_the_site_budget_is_spent()
    {
        await using var host = Host(new Dictionary<string, string?> { ["ClientLimits:LiveConnectsSiteWidePerWindow"] = "2" });
        using var client = host.Factory.CreatePublicClient();

        var first = await Negotiate(client, SharedClient);
        var second = await Negotiate(client, OtherClient);
        var problemsBefore = host.Logs.Problems.Count;
        using var third = await NegotiateResponse(client, ThirdClient);

        first.Should().Be(HttpStatusCode.OK);
        second.Should().Be(HttpStatusCode.OK);
        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "a third address has its own window but the site's window is spent");
        third.Headers.RetryAfter!.Delta!.Value.TotalSeconds.Should().BeInRange(1, 60);
        third.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await third.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        host.Logs.Problems.Skip(problemsBefore).Should().BeEmpty("a quiet refusal writes nothing at Warning or above");
    }

    [Fact]
    public async Task A_refused_address_spends_nothing_of_the_site_budget()
    {
        await using var host = Host(new Dictionary<string, string?>
        {
            ["ClientLimits:LiveConnectsPerWindow"] = "1",
            ["ClientLimits:LiveConnectsSiteWidePerWindow"] = "2",
        });
        using var client = host.Factory.CreatePublicClient();

        var first = await Negotiate(client, SharedClient);
        var again = await Negotiate(client, SharedClient);
        var againAndAgain = await Negotiate(client, SharedClient);
        var other = await Negotiate(client, OtherClient);

        first.Should().Be(HttpStatusCode.OK);
        again.Should().Be(HttpStatusCode.TooManyRequests, "the address spent its own window");
        againAndAgain.Should().Be(HttpStatusCode.TooManyRequests);
        other.Should().Be(HttpStatusCode.OK, "the refused requests of one address took nothing from the site's budget");
    }

    [Theory]
    [InlineData(HttpTransportType.WebSockets)]
    [InlineData(HttpTransportType.ServerSentEvents)]
    public async Task A_spent_site_budget_refuses_the_next_page_from_a_different_address_and_leaves_held_pages_connected(HttpTransportType transport)
    {
        await using var host = Host(new Dictionary<string, string?> { ["ClientLimits:LiveConnectsSiteWidePerWindow"] = "2" });
        await using var pages = new PageSet(host.Factory, transport);
        var held = await pages.StartHeldAsync(SharedClient, expectedCount: 1);
        await pages.StartHeldAsync(OtherClient, expectedCount: 2);
        var refusalsBefore = host.Refusals.Refusals;

        var refused = await pages.TryStartAsync(ThirdClient);

        refused.Should().BeFalse("the site accepted its two new connections for this window");
        host.Refusals.Refusals.Should().BeGreaterThan(refusalsBefore);
        held.Connection.State.Should().Be(HubConnectionState.Connected);
        host.Limiter.Count.Should().Be(2, "a refused page takes no place and the held pages stay");
    }

    [Fact]
    public async Task A_server_sent_events_page_keeps_posting_messages_after_the_site_budget_is_spent()
    {
        await using var host = Host(new Dictionary<string, string?> { ["ClientLimits:LiveConnectsSiteWidePerWindow"] = "1" });
        await using var pages = new PageSet(host.Factory, HttpTransportType.ServerSentEvents);
        var page = await pages.StartHeldAsync(SharedClient, expectedCount: 1, keepAlive: TimeSpan.FromMilliseconds(500));
        (await pages.TryStartAsync(OtherClient)).Should().BeFalse("the site's one new connection is spent");
        var refusalsAfterTheRefusal = host.Refusals.Refusals;
        var messagesBefore = host.Refusals.Messages;

        await SyncHarness.WaitUntil(() => Task.FromResult(host.Refusals.Messages >= messagesBefore + SeveralPings));

        host.Refusals.Refusals.Should().Be(refusalsAfterTheRefusal, "the posted pings are messages, which no live limit counts");
        page.Connection.State.Should().Be(HubConnectionState.Connected);
        host.Limiter.Count.Should().Be(1);
    }

    [Fact]
    public async Task The_host_caps_upgraded_connections_at_the_site_wide_live_limit()
    {
        await using var host = Host(new Dictionary<string, string?> { ["Live:MaxConnections"] = "7" });

        var kestrel = host.Factory.ServingServices.GetRequiredService<IOptions<KestrelServerOptions>>().Value;
        var live = host.Factory.ServingServices.GetRequiredService<LiveOptions>();

        live.MaxConnections.Should().Be(7);
        kestrel.Limits.MaxConcurrentUpgradedConnections.Should().Be(live.MaxConnections);
    }

    [Theory]
    [InlineData(HttpTransportType.WebSockets)]
    [InlineData(HttpTransportType.ServerSentEvents)]
    public async Task The_shipped_per_address_cap_admits_twenty_pages_refuses_the_twenty_first_quietly_and_admits_another_address(HttpTransportType transport)
    {
        await using var host = Host(new Dictionary<string, string?>());
        await using var pages = new PageSet(host.Factory, transport);

        for (var count = 1; count <= ShippedCap; count++)
        {
            await pages.StartHeldAsync(SharedClient, expectedCount: count);
        }

        var refusalsBefore = host.Refusals.Refusals;
        var problemsBefore = host.Logs.Problems.Count;
        var refused = await pages.TryStartAsync(SharedClient);

        refused.Should().BeFalse("the shipped cap is twenty live pages for one address");
        host.Refusals.Refusals.Should().BeGreaterThan(refusalsBefore, "the connect request was answered 429 before any handshake");
        host.Limiter.Count.Should().Be(ShippedCap);
        host.Logs.Problems.Skip(problemsBefore).Should().BeEmpty("a quiet refusal writes nothing at Warning or above");

        await pages.StartHeldAsync(OtherClient, expectedCount: ShippedCap + 1);
    }

    private static TestHost Host(IReadOnlyDictionary<string, string?> settings)
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        var refusals = new LiveRefusalWatch();
        var logs = new CapturingLoggerProvider();
        var factory = new CabinetWebApplicationFactory(
            settings,
            services =>
            {
                services.AddSingleton(SyncHarness.Options());
                services.AddSingleton<IRequestPacer>(new NoWaitPacer());
                services.AddSingleton<TimeProvider>(SyncHarness.NewClock());
                services.AddHttpClient<ICollectionSource, BggClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
                services.AddHttpClient<IEnrichmentSource, BggThingClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
                services.AddSingleton<ILoggerProvider>(logs);
                refusals.Register(services);
            });

        return new TestHost(factory, refusals, logs);
    }

    private static async Task<HttpStatusCode> Negotiate(HttpClient client, string? forwardedFor)
    {
        using var response = await NegotiateResponse(client, forwardedFor);

        return response.StatusCode;
    }

    private static Task<HttpResponseMessage> NegotiateResponse(HttpClient client, string? forwardedFor)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/cabinet/live/negotiate?negotiateVersion=1");
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private sealed class TestHost(CabinetWebApplicationFactory factory, LiveRefusalWatch refusals, CapturingLoggerProvider logs) : IAsyncDisposable
    {
        public CabinetWebApplicationFactory Factory { get; } = factory;

        public LiveRefusalWatch Refusals { get; } = refusals;

        public CapturingLoggerProvider Logs { get; } = logs;

        public LiveConnectionLimiter Limiter => Factory.ServingServices.GetRequiredService<LiveConnectionLimiter>();

        public ValueTask DisposeAsync() => Factory.DisposeAsync();
    }

    private sealed class PageSet(CabinetWebApplicationFactory factory, HttpTransportType transport, bool skipNegotiation = false) : IAsyncDisposable
    {
        private readonly List<Page> _pages = [];

        public async Task<Page> StartHeldAsync(string? forwardedFor, int expectedCount, TimeSpan? keepAlive = null)
        {
            var page = new Page(factory, transport, forwardedFor, keepAlive, skipNegotiation);
            _pages.Add(page);
            await page.Connection.StartAsync(TestContext.Current.CancellationToken);
            var limiter = factory.ServingServices.GetRequiredService<LiveConnectionLimiter>();
            await SyncHarness.WaitUntil(() => Task.FromResult(limiter.Count == expectedCount));

            return page;
        }

        public async Task<bool> TryStartAsync(string? forwardedFor)
        {
            var page = new Page(factory, transport, forwardedFor, null, skipNegotiation);

            try
            {
                await page.Connection.StartAsync(TestContext.Current.CancellationToken);
                _pages.Add(page);

                return true;
            }
            catch (Exception exception) when (exception is AggregateException or HttpRequestException or InvalidOperationException or System.Net.WebSockets.WebSocketException)
            {
                await page.DisposeAsync();

                return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var page in _pages)
            {
                await page.DisposeAsync();
            }
        }
    }

    private sealed class Page : IAsyncDisposable
    {
        private readonly ConcurrentQueue<byte> _received = new();

        public Page(CabinetWebApplicationFactory factory, HttpTransportType transport, string? forwardedFor, TimeSpan? keepAlive, bool skipNegotiation)
        {
            Connection = new HubConnectionBuilder()
                .WithUrl(
                    new Uri($"http://127.0.0.1:{factory.PublicPort}/cabinet/live"),
                    options =>
                    {
                        options.Transports = transport;
                        options.SkipNegotiation = skipNegotiation;
                        if (forwardedFor is not null)
                        {
                            options.Headers["X-Forwarded-For"] = forwardedFor;
                        }
                    })
                .Build();
            Connection.On<object>("StatusChanged", _ => _received.Enqueue(0));

            if (keepAlive is { } interval)
            {
                Connection.KeepAliveInterval = interval;
            }
        }

        public HubConnection Connection { get; }

        public int ReceivedCount => _received.Count;

        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }
}

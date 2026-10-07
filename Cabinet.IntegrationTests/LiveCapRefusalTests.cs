using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Bgg;
using Cabinet.Service.Live;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves a page refused at the connection cap is told not to come back by itself, even when it is built to reconnect, and
/// that the refusal neither disturbs the pages already connected nor takes or loses a place.
/// </summary>
[Trait("Category", "Live")]
public class LiveCapRefusalTests
{
    [Theory]
    [InlineData(HttpTransportType.WebSockets, HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents)]
    [InlineData(HttpTransportType.ServerSentEvents, HttpTransportType.ServerSentEvents)]
    public async Task A_refused_reconnecting_page_stops_after_one_attempt_and_the_first_page_keeps_working(
        HttpTransportType firstTransport,
        HttpTransportType refusedTransport)
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        var settings = new Dictionary<string, string?> { ["Live:MaxConnections"] = "1" };
        var attempts = new AttemptCounter();
        await using var factory = CreateCountingFactory(handler, settings, attempts);
        using var client = factory.CreatePublicClient();
        var limiter = factory.ServingServices.GetRequiredService<LiveConnectionLimiter>();
        await using var first = new ReconnectingPage(factory, firstTransport);
        await first.StartAsync();
        await using var refused = new ReconnectingPage(factory, refusedTransport);

        await refused.StartAsync();
        await refused.Closed.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        refused.Connection.State.Should().Be(HubConnectionState.Disconnected);
        attempts.Count.Should().Be(2, "one connection for each page, and a refused page must not ask again by itself");
        refused.ReconnectingCount.Should().Be(0);
        refused.ReconnectedCount.Should().Be(0);
        limiter.Count.Should().Be(1, "the refused connection took no place and the first one kept its own");

        using var press = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);
        await SyncHarness.WaitUntil(() => Task.FromResult(first.ReceivedCount >= 2));

        first.Connection.State.Should().Be(HubConnectionState.Connected);
        first.ReconnectingCount.Should().Be(0);
        limiter.Count.Should().Be(1);
    }

    [Fact]
    public async Task The_place_of_a_page_that_leaves_can_be_taken_by_the_next_page_after_a_refusal()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        var settings = new Dictionary<string, string?> { ["Live:MaxConnections"] = "1" };
        await using var factory = CreateCountingFactory(handler, settings, new AttemptCounter());
        var limiter = factory.ServingServices.GetRequiredService<LiveConnectionLimiter>();
        await using var first = new ReconnectingPage(factory, HttpTransportType.WebSockets);
        await first.StartAsync();
        await using var refused = new ReconnectingPage(factory, HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents);
        await refused.StartAsync();
        await refused.Closed.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        await first.Connection.StopAsync(TestContext.Current.CancellationToken);
        await SyncHarness.WaitUntil(() => Task.FromResult(limiter.Count == 0));
        await using var admitted = new ReconnectingPage(factory, HttpTransportType.WebSockets);
        await admitted.StartAsync();
        await SyncHarness.WaitUntil(() => Task.FromResult(limiter.Count == 1));

        admitted.Closed.IsCompleted.Should().BeFalse("a place was free, so the connection is held open");
        admitted.Connection.State.Should().Be(HubConnectionState.Connected);
    }

    [Fact]
    public async Task The_close_message_sent_to_a_refused_page_does_not_allow_it_to_reconnect()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        var settings = new Dictionary<string, string?> { ["Live:MaxConnections"] = "1" };
        await using var factory = CreateCountingFactory(handler, settings, new AttemptCounter());
        await using var first = new ReconnectingPage(factory, HttpTransportType.ServerSentEvents);
        await first.StartAsync();

        var frames = await ReadFramesOfRefusedWebSocketAsync(factory);

        var close = frames.Single(frame => frame.Contains("\"type\":7", StringComparison.Ordinal));
        using var message = JsonDocument.Parse(close);
        message.RootElement.TryGetProperty("allowReconnect", out var allowReconnect).Should().BeFalse(
            $"a close message that allows reconnecting lets the client retry at once; it said {allowReconnect}");
        frames.Should().NotContain(frame => frame.Contains("\"type\":1", StringComparison.Ordinal), "nothing but the handshake and the close is sent");
    }

    private static async Task<List<string>> ReadFramesOfRefusedWebSocketAsync(CabinetWebApplicationFactory factory)
    {
        var cancellation = TestContext.Current.CancellationToken;
        using var client = factory.CreatePublicClient();
        using var negotiation = await client.PostAsync("/cabinet/live/negotiate?negotiateVersion=1", content: null, cancellation);
        using var offer = JsonDocument.Parse(await negotiation.Content.ReadAsStringAsync(cancellation));
        var token = offer.RootElement.GetProperty("connectionToken").GetString();
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{factory.PublicPort}/cabinet/live?id={token}"), cancellation);
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, cancellation);

        var frames = new List<string>();
        var buffer = new byte[4096];
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(TimeSpan.FromSeconds(10));

        for (;;)
        {
            var received = await socket.ReceiveAsync(buffer, limit.Token);

            if (received.MessageType == WebSocketMessageType.Close)
            {
                return frames;
            }

            frames.AddRange(Encoding.UTF8.GetString(buffer, 0, received.Count).Split('\u001e', StringSplitOptions.RemoveEmptyEntries));
        }
    }

    /// <summary>A hub connection that reconnects the way a browser page does, and counts what it does.</summary>
    private sealed class ReconnectingPage : IAsyncDisposable
    {
        private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<byte> _received = new();
        private int _reconnecting;
        private int _reconnected;

        public ReconnectingPage(CabinetWebApplicationFactory factory, HttpTransportType transport)
        {
            Connection = new HubConnectionBuilder()
                .WithUrl(
                    new Uri($"http://127.0.0.1:{factory.PublicPort}/cabinet/live"),
                    options => options.Transports = transport)
                .WithAutomaticReconnect()
                .Build();
            Connection.On<object>("StatusChanged", _ => _received.Enqueue(0));
            Connection.Reconnecting += _ =>
            {
                Interlocked.Increment(ref _reconnecting);

                return Task.CompletedTask;
            };
            Connection.Reconnected += _ =>
            {
                Interlocked.Increment(ref _reconnected);

                return Task.CompletedTask;
            };
            Connection.Closed += _ =>
            {
                _closed.TrySetResult();

                return Task.CompletedTask;
            };
        }

        public HubConnection Connection { get; }

        public Task Closed => _closed.Task;

        public int ReconnectingCount => Volatile.Read(ref _reconnecting);

        public int ReconnectedCount => Volatile.Read(ref _reconnected);

        public int ReceivedCount => _received.Count;

        public Task StartAsync() => Connection.StartAsync(TestContext.Current.CancellationToken);

        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }

    private static CabinetWebApplicationFactory CreateCountingFactory(
        ScriptedBggHandler handler,
        IReadOnlyDictionary<string, string?> settings,
        AttemptCounter attempts) =>
        new(
            settings,
            services =>
            {
                services.AddSingleton(SyncHarness.Options());
                services.AddSingleton<IRequestPacer>(new NoWaitPacer());
                services.AddSingleton<TimeProvider>(SyncHarness.NewClock());
                services.AddHttpClient<ICollectionSource, BggClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
                services.AddHttpClient<IEnrichmentSource, BggThingClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
                services.Configure<HubOptions>(hub => hub.AddFilter(attempts));
            });

    /// <summary>Counts every connection the hub is asked to accept, which is one for every attempt a page makes to connect.</summary>
    private sealed class AttemptCounter : IHubFilter
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
        {
            Interlocked.Increment(ref _count);

            return next(context);
        }
    }
}

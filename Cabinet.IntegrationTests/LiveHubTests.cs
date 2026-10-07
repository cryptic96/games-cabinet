using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Live;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves open pages hear about a sync through the live channel, that the channel only ever sends, and that it is bounded in
/// the transports it offers and the number of connections it holds.
/// </summary>
[Trait("Category", "Live")]
public class LiveHubTests
{
    private const string LiveRoute = "/cabinet/live";

    [Fact]
    public async Task A_connected_page_hears_that_a_sync_started_and_that_it_finished()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        await using var factory = SyncHarness.CreateFactory(handler, SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();
        await using var page = await LivePage.ConnectAsync(factory, HttpTransportType.WebSockets);

        using var press = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);
        await SyncHarness.WaitUntil(() => Task.FromResult(page.Received.Count >= 2));
        var status = await SyncHarness.ReadStatus(client);

        var received = page.Received.ToArray();
        received[0].GetProperty("running").GetBoolean().Should().BeTrue();
        received[1].GetProperty("running").GetBoolean().Should().BeFalse();
        received[1].GetProperty("lastResult").GetString().Should().Be("changed");
        received[1].GetProperty("snapshotVersion").GetString().Should().NotBeNullOrEmpty()
            .And.Be(status.Json.GetProperty("snapshotVersion").GetString());
    }

    [Theory]
    [InlineData("OnConnectedAsync")]
    [InlineData("Dispose")]
    [InlineData("StatusChanged")]
    [InlineData("SyncNow")]
    public async Task No_method_can_be_invoked_on_the_hub_and_nothing_reaches_BGG(string method)
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        await using var factory = SyncHarness.CreateFactory(handler, SyncHarness.NewClock());
        await using var page = await LivePage.ConnectAsync(factory, HttpTransportType.WebSockets);

        var invocation = () => page.Connection.InvokeAsync(method, cancellationToken: TestContext.Current.CancellationToken);

        (await invocation.Should().ThrowAsync<HubException>()).Which.Message.Should().Contain("Method does not exist");
        await AssertNoSyncWasRequested(factory, handler);
    }

    [Fact]
    public async Task A_status_sent_to_the_hub_with_arguments_is_refused_as_well()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        await using var factory = SyncHarness.CreateFactory(handler, SyncHarness.NewClock());
        await using var page = await LivePage.ConnectAsync(factory, HttpTransportType.WebSockets);

        var invocation = () => page.Connection.InvokeAsync("StatusChanged", new { running = true }, TestContext.Current.CancellationToken);

        (await invocation.Should().ThrowAsync<HubException>()).Which.Message.Should().Contain("Method does not exist");
        await AssertNoSyncWasRequested(factory, handler);
    }

    [Fact]
    public async Task The_negotiation_offers_websockets_and_server_sent_events_but_not_long_polling()
    {
        await using var factory = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();

        using var response = await client.PostAsync($"{LiveRoute}/negotiate?negotiateVersion=1", content: null, TestContext.Current.CancellationToken);
        using var negotiation = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var offered = negotiation.RootElement.GetProperty("availableTransports").EnumerateArray()
            .Select(transport => transport.GetProperty("transport").GetString())
            .ToList();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        offered.Should().BeEquivalentTo(["WebSockets", "ServerSentEvents"]);
    }

    [Fact]
    public async Task The_live_connection_options_keep_small_buffers_and_allow_only_the_two_streaming_transports()
    {
        await using var factory = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock());

        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(LiveRoute, StringComparison.Ordinal) == true)
            .ToList();
        var options = routes
            .Single(endpoint => endpoint.RoutePattern.RawText == $"{LiveRoute}/negotiate")
            .Metadata.GetMetadata<HttpConnectionDispatcherOptions>();

        routes.Select(endpoint => endpoint.RoutePattern.RawText).Should().BeEquivalentTo([LiveRoute, $"{LiveRoute}/negotiate"]);
        options.Should().NotBeNull("the negotiation route carries the connection options the live route is mapped with");
        options!.Transports.Should().Be(HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents);
        options.ApplicationMaxBufferSize.Should().Be(4096);
        options.TransportMaxBufferSize.Should().Be(8192);
    }

    [Fact]
    public async Task A_client_restricted_to_long_polling_cannot_connect()
    {
        await using var factory = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock());
        await using var page = LivePage.Create(factory, HttpTransportType.LongPolling);

        var start = () => page.Connection.StartAsync(TestContext.Current.CancellationToken);

        var failure = await start.Should().ThrowAsync<AggregateException>();

        failure.Which.InnerExceptions.Should().NotBeEmpty().And.OnlyContain(
            inner => inner.Message.Contains("disabled by the client", StringComparison.Ordinal),
            "the only transports the server offers are the two the client switched off");
    }

    [Fact]
    public async Task A_client_restricted_to_server_sent_events_connects_and_receives_broadcasts()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        await using var factory = SyncHarness.CreateFactory(handler, SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();
        await using var page = await LivePage.ConnectAsync(factory, HttpTransportType.ServerSentEvents);

        using var press = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);
        await SyncHarness.WaitUntil(() => Task.FromResult(page.Received.Count >= 2));

        page.Received.Last().GetProperty("lastResult").GetString().Should().Be("changed");
    }

    [Theory]
    [InlineData(HttpTransportType.WebSockets)]
    [InlineData(HttpTransportType.ServerSentEvents)]
    public async Task A_connection_beyond_the_cap_is_closed_and_a_place_is_freed_when_one_leaves(HttpTransportType transport)
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        var settings = new Dictionary<string, string?> { ["Live:MaxConnections"] = "2" };
        await using var factory = SyncHarness.CreateFactory(handler, SyncHarness.NewClock(), settings);
        using var client = factory.CreatePublicClient();
        var limiter = factory.ServingServices.GetRequiredService<LiveConnectionLimiter>();
        await using var first = await LivePage.ConnectAsync(factory, transport);
        await using var second = await LivePage.ConnectAsync(factory, transport);

        await ExpectRefusedAsync(factory, transport);

        limiter.Count.Should().Be(2, "the refused connection took no place");
        using var press = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);
        await SyncHarness.WaitUntil(() => Task.FromResult(first.Received.Count >= 2 && second.Received.Count >= 2));

        await first.Connection.StopAsync(TestContext.Current.CancellationToken);
        await SyncHarness.WaitUntil(() => Task.FromResult(limiter.Count == 1));
        await using var admitted = await LivePage.ConnectAsync(factory, transport);
        await SyncHarness.WaitUntil(() => Task.FromResult(limiter.Count == 2));

        admitted.Closed.IsCompleted.Should().BeFalse("a place was freed, so the connection is held open");
    }

    [Fact]
    public async Task Received_payloads_carry_no_credential_and_no_failure_category()
    {
        await using var factory = SyncHarness.CreateFactory(SyncHarness.Refusing(), SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();
        await using var page = await LivePage.ConnectAsync(factory, HttpTransportType.WebSockets);

        using var press = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);
        await SyncHarness.WaitUntil(() => Task.FromResult(page.Received.Count >= 2));
        var status = await SyncHarness.ReadStatus(client);

        var failureNames = Enum.GetNames<SyncFailure>().Where(name => name != nameof(SyncFailure.None));
        foreach (var payload in page.Received.Select(received => received.GetRawText()))
        {
            payload.Should().NotContain(SyncHarness.Username).And.NotContain(SyncHarness.Token);
            foreach (var name in failureNames)
            {
                payload.Should().NotContainEquivalentOf(name);
            }
        }

        page.Received.Last().GetProperty("lastResult").GetString().Should().Be("failed");
        page.Received.Last().EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(status.Json.EnumerateObject().Select(property => property.Name));
    }

    private static async Task AssertNoSyncWasRequested(CabinetWebApplicationFactory factory, ScriptedBggHandler handler)
    {
        using var client = factory.CreatePublicClient();
        var status = await SyncHarness.ReadStatus(client);

        status.Running.Should().BeFalse("a sync that was accepted would be running");
        status.CooldownEndsUtc.Should().BeNull("an accepted request opens the shared window at once");
        status.LastResult.Should().BeNull();
        handler.Requests.Should().BeEmpty();
    }

    private static async Task ExpectRefusedAsync(CabinetWebApplicationFactory factory, HttpTransportType transport)
    {
        await using var page = LivePage.Create(factory, transport);

        try
        {
            await page.Connection.StartAsync(TestContext.Current.CancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return;
        }

        await page.Closed.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    /// <summary>A hub connection that keeps every status it is sent.</summary>
    internal sealed class LivePage : IAsyncDisposable
    {
        private readonly HubConnection _connection;
        private readonly ConcurrentQueue<JsonElement> _received = new();
        private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private LivePage(HubConnection connection)
        {
            _connection = connection;
            _connection.On<JsonElement>("StatusChanged", status => _received.Enqueue(status.Clone()));
            _connection.Closed += _ =>
            {
                _closed.TrySetResult();

                return Task.CompletedTask;
            };
        }

        /// <summary>The statuses received so far, oldest first.</summary>
        public IReadOnlyCollection<JsonElement> Received => _received;

        /// <summary>Completes when the server or the network closes the connection.</summary>
        public Task Closed => _closed.Task;

        /// <summary>The connection itself, for tests that send something.</summary>
        public HubConnection Connection => _connection;

        /// <summary>Connects to the live route of a booted host over the given transport.</summary>
        public static async Task<LivePage> ConnectAsync(CabinetWebApplicationFactory factory, HttpTransportType transport)
        {
            var page = Create(factory, transport);
            await page._connection.StartAsync(TestContext.Current.CancellationToken);

            return page;
        }

        /// <summary>Builds the connection without starting it, so a test can expect the start to fail.</summary>
        public static LivePage Create(CabinetWebApplicationFactory factory, HttpTransportType transport)
        {
            var connection = new HubConnectionBuilder()
                .WithUrl(
                    new Uri($"http://127.0.0.1:{factory.PublicPort}{LiveRoute}"),
                    options => options.Transports = transport)
                .Build();

            return new LivePage(connection);
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }
}

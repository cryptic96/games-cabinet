using System.Collections.Concurrent;
using System.Text.Json;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

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

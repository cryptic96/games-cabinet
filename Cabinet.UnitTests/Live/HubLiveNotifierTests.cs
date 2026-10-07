using System.Collections.Concurrent;
using Cabinet.Service.Live;
using Cabinet.Service.Sync;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Live;

/// <summary>Proves a broadcast is bounded in time and that a slow or failing one never reaches the sync that announced itself.</summary>
[Trait("Category", "Live")]
public class HubLiveNotifierTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private static readonly CabinetStatus Status = new(
        new DateTimeOffset(2030, 1, 15, 12, 0, 0, TimeSpan.Zero),
        null,
        null,
        true,
        null,
        false,
        10800,
        null,
        null);

    [Fact]
    public async Task A_broadcast_that_never_completes_is_given_up_on_after_the_time_limit()
    {
        var clock = new FakeTimeProvider();
        var logger = new ListLogger();
        var notifier = Notifier(new NeverCompletes(), logger, clock);

        var publish = notifier.PublishAsync(Status, TestContext.Current.CancellationToken);
        clock.Advance(HubLiveNotifier.TimeLimit - TimeSpan.FromSeconds(1));
        publish.IsCompleted.Should().BeFalse("the limit has not passed yet");
        clock.Advance(TimeSpan.FromSeconds(1));
        await publish.WaitAsync(Bound, TestContext.Current.CancellationToken);

        logger.Lines.Should().ContainSingle().Which.Level.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public void The_time_limit_is_short()
    {
        HubLiveNotifier.TimeLimit.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task A_broadcast_that_completes_in_time_logs_nothing()
    {
        var logger = new ListLogger();

        await Notifier(new Completes(), logger, new FakeTimeProvider()).PublishAsync(Status, TestContext.Current.CancellationToken);

        logger.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task A_broadcast_that_fails_is_swallowed_and_only_the_exception_type_is_logged()
    {
        var logger = new ListLogger();

        await Notifier(new Fails(), logger, new FakeTimeProvider()).PublishAsync(Status, TestContext.Current.CancellationToken);

        var line = logger.Lines.Should().ContainSingle().Which;
        line.Message.Should().Contain(nameof(InvalidOperationException)).And.NotContain("secret detail");
    }

    [Fact]
    public async Task A_host_stop_ends_the_wait_without_an_exception()
    {
        using var stop = new CancellationTokenSource();
        var notifier = Notifier(new NeverCompletes(), new ListLogger(), new FakeTimeProvider());

        var publish = notifier.PublishAsync(Status, stop.Token);
        await stop.CancelAsync();

        await publish.WaitAsync(Bound, TestContext.Current.CancellationToken);
    }

    private static HubLiveNotifier Notifier(ICabinetClient client, ILogger<HubLiveNotifier> logger, TimeProvider clock) =>
        new(new FakeHubContext(client), logger, clock);

    private sealed class NeverCompletes : ICabinetClient
    {
        public Task StatusChanged(CabinetStatus status) => new TaskCompletionSource().Task;
    }

    private sealed class Completes : ICabinetClient
    {
        public Task StatusChanged(CabinetStatus status) => Task.CompletedTask;
    }

    private sealed class Fails : ICabinetClient
    {
        public Task StatusChanged(CabinetStatus status) => Task.FromException(new InvalidOperationException("secret detail"));
    }

    private sealed class FakeHubContext(ICabinetClient client) : IHubContext<CabinetHub, ICabinetClient>
    {
        public IHubClients<ICabinetClient> Clients { get; } = new FakeClients(client);

        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class FakeClients(ICabinetClient all) : IHubClients<ICabinetClient>
    {
        public ICabinetClient All => all;

        public ICabinetClient AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

        public ICabinetClient Client(string connectionId) => throw new NotSupportedException();

        public ICabinetClient Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();

        public ICabinetClient Group(string groupName) => throw new NotSupportedException();

        public ICabinetClient GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

        public ICabinetClient Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();

        public ICabinetClient User(string userId) => throw new NotSupportedException();

        public ICabinetClient Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class ListLogger : ILogger<HubLiveNotifier>
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message)> _lines = new();

        public IReadOnlyList<(LogLevel Level, string Message)> Lines => [.. _lines];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _lines.Enqueue((logLevel, formatter(state, exception)));
    }
}

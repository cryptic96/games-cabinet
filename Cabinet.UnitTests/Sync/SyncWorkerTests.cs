using Cabinet.Domain.Collection;
using Cabinet.Service.Collection;
using Cabinet.Service.Live;
using Cabinet.Service.Sync;
using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Sync;

/// <summary>
/// Proves a run that a service stop interrupts is not recorded as a failure, while a failed run, a run that is cancelled
/// without a stop and a run that throws unexpectedly are recorded, and the worker goes on to the next request.
/// </summary>
[Trait("Category", "Sync")]
public class SyncWorkerTests
{
    private static readonly DateTimeOffset Start = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly SyncOptions Options = new(
        true,
        TimeSpan.FromMinutes(60),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromSeconds(120),
        TimeSpan.FromHours(3));

    [Fact]
    public async Task A_stop_during_a_run_records_no_failure_and_no_result()
    {
        var store = new InMemorySyncStateStore();
        var source = new WaitingSource();
        using var harness = Harness(store, source);
        await harness.Worker.StartAsync(TestContext.Current.CancellationToken);
        harness.Coordinator.TryRequest(SyncTrigger.Manual).Should().BeOfType<SyncRequestResult.Started>();
        await source.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        await harness.Worker.StopAsync(TestContext.Current.CancellationToken);

        store.Stored.LastResult.Should().BeNull();
        store.Stored.LastFailure.Should().Be(SyncFailure.None);
        store.Stored.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public async Task A_stalled_run_is_recorded_as_a_timeout_once_the_clock_passes_the_whole_run_limit()
    {
        var store = new InMemorySyncStateStore();
        var source = new WaitingSource();
        using var harness = Harness(store, source);
        await harness.Worker.StartAsync(TestContext.Current.CancellationToken);
        harness.Coordinator.TryRequest(SyncTrigger.Manual).Should().BeOfType<SyncRequestResult.Started>();
        await source.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        harness.Clock.Advance(SyncWorker.RunLimit - TimeSpan.FromSeconds(1));
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        harness.Coordinator.IsRunning.Should().BeTrue();
        store.Stored.LastResult.Should().BeNull();

        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        await WaitForFinishAsync(harness.Coordinator);

        SyncWorker.RunLimit.Should().Be(TimeSpan.FromMinutes(10));
        store.Stored.LastResult.Should().Be(SyncResult.Failed);
        store.Stored.LastFailure.Should().Be(SyncFailure.Timeout);
        store.Stored.ConsecutiveFailures.Should().Be(1);
        await harness.Worker.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_stop_is_still_not_recorded_when_the_clock_passes_the_whole_run_limit_afterwards()
    {
        var store = new InMemorySyncStateStore();
        var source = new WaitingSource();
        using var harness = Harness(store, source);
        await harness.Worker.StartAsync(TestContext.Current.CancellationToken);
        harness.Coordinator.TryRequest(SyncTrigger.Manual).Should().BeOfType<SyncRequestResult.Started>();
        await source.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        await harness.Worker.StopAsync(TestContext.Current.CancellationToken);
        harness.Clock.Advance(SyncWorker.RunLimit * 2);

        store.Stored.LastResult.Should().BeNull();
        store.Stored.LastFailure.Should().Be(SyncFailure.None);
        store.Stored.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public async Task A_run_that_fails_on_its_own_is_still_recorded_as_a_failure()
    {
        var store = new InMemorySyncStateStore();
        var source = new FailingSource();
        using var harness = Harness(store, source);
        await harness.Worker.StartAsync(TestContext.Current.CancellationToken);

        harness.Coordinator.TryRequest(SyncTrigger.Manual);
        await WaitForFinishAsync(harness.Coordinator);

        store.Stored.LastResult.Should().Be(SyncResult.Failed);
        store.Stored.LastFailure.Should().Be(SyncFailure.Throttled);
        store.Stored.ConsecutiveFailures.Should().Be(1);
        await harness.Worker.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_run_cancelled_without_a_service_stop_is_recorded_as_a_timeout_and_the_next_request_still_runs()
    {
        var store = new InMemorySyncStateStore();
        var source = new ThrowingSource(() => new OperationCanceledException(new CancellationToken(canceled: true)));
        using var harness = Harness(store, source);
        await harness.Worker.StartAsync(TestContext.Current.CancellationToken);

        harness.Coordinator.TryRequest(SyncTrigger.Manual).Should().BeOfType<SyncRequestResult.Started>();
        await WaitForFinishAsync(harness.Coordinator);

        store.Stored.LastResult.Should().Be(SyncResult.Failed);
        store.Stored.LastFailure.Should().Be(SyncFailure.Timeout);
        store.Stored.ConsecutiveFailures.Should().Be(1);

        harness.Coordinator.TryRequest(SyncTrigger.Scheduled).Should().BeOfType<SyncRequestResult.Started>();
        await WaitForFinishAsync(harness.Coordinator);

        source.Calls.Should().Be(2);
        store.Stored.ConsecutiveFailures.Should().Be(2);
        await harness.Worker.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_run_that_throws_unexpectedly_is_recorded_as_unavailable_logged_by_type_only_and_the_next_request_still_runs()
    {
        const string Secret = "sentinel-secret-message";
        var store = new InMemorySyncStateStore();
        var source = new ThrowingSource(() => new InvalidOperationException(Secret));
        var logger = new RecordingLogger();
        using var harness = Harness(store, source, logger);
        await harness.Worker.StartAsync(TestContext.Current.CancellationToken);

        harness.Coordinator.TryRequest(SyncTrigger.Manual).Should().BeOfType<SyncRequestResult.Started>();
        await WaitForFinishAsync(harness.Coordinator);

        store.Stored.LastResult.Should().Be(SyncResult.Failed);
        store.Stored.LastFailure.Should().Be(SyncFailure.Unavailable);
        var line = logger.Lines.Should().ContainSingle().Subject;
        line.Level.Should().Be(LogLevel.Error);
        line.Message.Should().Contain(nameof(InvalidOperationException)).And.NotContain(Secret);

        harness.Coordinator.TryRequest(SyncTrigger.Scheduled).Should().BeOfType<SyncRequestResult.Started>();
        await WaitForFinishAsync(harness.Coordinator);

        source.Calls.Should().Be(2);
        logger.Lines.Should().HaveCount(2).And.OnlyContain(line => !line.Message.Contains(Secret, StringComparison.Ordinal));
        await harness.Worker.StopAsync(TestContext.Current.CancellationToken);
    }

    private static WorkerHarness Harness(InMemorySyncStateStore store, ICollectionSource source, ILogger<SyncWorker>? logger = null)
    {
        var clock = new FakeTimeProvider(Start);
        var coordinator = new SyncCoordinator(store, Options, clock);
        var collection = new CollectionStore();
        var runner = new SyncRunner(() => source, new NullSnapshotStore(), collection, clock, NullLogger<SyncRunner>.Instance);
        var status = new SyncStatusService(coordinator, collection, Options, clock);
        var worker = new SyncWorker(coordinator, runner, status, new SilentNotifier(), clock, logger ?? NullLogger<SyncWorker>.Instance);

        return new WorkerHarness(coordinator, worker, clock);
    }

    private static async Task WaitForFinishAsync(SyncCoordinator coordinator)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (coordinator.IsRunning)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The run did not finish within ten seconds.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        }
    }

    private sealed record WorkerHarness(SyncCoordinator Coordinator, SyncWorker Worker, FakeTimeProvider Clock) : IDisposable
    {
        public void Dispose() => Worker.Dispose();
    }

    private sealed class WaitingSource : ICollectionSource
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<CollectionFetchResult> FetchOwnedAsync(CancellationToken cancellationToken)
        {
            Started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            return new CollectionFetchResult.Failed(SyncFailure.Unavailable);
        }
    }

    private sealed class FailingSource : ICollectionSource
    {
        public Task<CollectionFetchResult> FetchOwnedAsync(CancellationToken cancellationToken) =>
            Task.FromResult<CollectionFetchResult>(new CollectionFetchResult.Failed(SyncFailure.Throttled));
    }

    private sealed class NullSnapshotStore : ISnapshotStore
    {
        public CollectionSnapshot? Load() => null;

        public void Save(CollectionSnapshot snapshot)
        {
        }
    }

    private sealed class SilentNotifier : ILiveNotifier
    {
        public Task PublishAsync(CabinetStatus status, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ThrowingSource(Func<Exception> exception) : ICollectionSource
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Task<CollectionFetchResult> FetchOwnedAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);

            throw exception();
        }
    }

    private sealed record LogLine(LogLevel Level, string Message);

    private sealed class RecordingLogger : ILogger<SyncWorker>
    {
        private readonly ConcurrentQueue<LogLine> _lines = new();

        public IReadOnlyList<LogLine> Lines => [.. _lines];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _lines.Enqueue(new LogLine(logLevel, exception is null ? formatter(state, exception) : $"{formatter(state, exception)} {exception}"));
    }
}

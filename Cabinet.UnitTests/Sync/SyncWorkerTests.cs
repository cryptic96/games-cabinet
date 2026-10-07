using Cabinet.Domain.Collection;
using Cabinet.Service.Collection;
using Cabinet.Service.Live;
using Cabinet.Service.Sync;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Sync;

/// <summary>Proves a run that a service stop interrupts is not recorded as a failure, and a failed run still is.</summary>
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

    private static WorkerHarness Harness(InMemorySyncStateStore store, ICollectionSource source)
    {
        var clock = new FakeTimeProvider(Start);
        var coordinator = new SyncCoordinator(store, Options, clock);
        var collection = new CollectionStore();
        var runner = new SyncRunner(() => source, new NullSnapshotStore(), collection, clock, NullLogger<SyncRunner>.Instance);
        var status = new SyncStatusService(coordinator, collection, Options, clock);
        var worker = new SyncWorker(coordinator, runner, status, new SilentNotifier(), NullLogger<SyncWorker>.Instance);

        return new WorkerHarness(coordinator, worker);
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

    private sealed record WorkerHarness(SyncCoordinator Coordinator, SyncWorker Worker) : IDisposable
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
}

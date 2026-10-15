using Cabinet.Domain.Owner;
using Cabinet.Service.Live;
using Cabinet.Service.Owner;
using Cabinet.Service.Sync;
using Cabinet.UnitTests.Sync;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cabinet.UnitTests.Owner;

/// <summary>Proves a burst of owner saves tells open pages once, shortly after the burst, never later than a ceiling, and only when what visitors see changed.</summary>
[Trait("Category", "Owner")]
public sealed class OwnerChangeNotifierTests
{
    private const string CabinetA = "Cabinet A";
    private const string ShelfB = "Shelf B";
    private const int FirstGame = 100;

    private static readonly SyncOptions Options = new(
        true,
        TimeSpan.FromMinutes(60),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromSeconds(120),
        TimeSpan.FromHours(3));

    [Fact]
    public async Task Five_saves_one_second_apart_give_one_notification_three_seconds_after_the_last()
    {
        var rig = await StartAsync();

        for (var save = 0; save < 5; save++)
        {
            await rig.PlaceNextGameAsync();
            rig.Harness.Clock.Advance(TimeSpan.FromSeconds(1));
        }

        rig.Live.Calls.Should().BeEmpty();
        rig.Harness.Clock.Advance(TimeSpan.FromSeconds(1.9));
        rig.Live.Calls.Should().BeEmpty("the quiet period of three seconds has not passed");
        rig.Harness.Clock.Advance(TimeSpan.FromSeconds(0.1));

        var call = rig.Live.Calls.Should().ContainSingle().Which;
        call.At.Should().Be(OwnerTestKit.Start + TimeSpan.FromSeconds(7));
        call.Status.SnapshotVersion.Should().Be(rig.Harness.Version);
        rig.Harness.Clock.Advance(TimeSpan.FromMinutes(1));
        rig.Live.Calls.Should().HaveCount(1);
    }

    [Fact]
    public async Task Saves_every_two_seconds_for_half_a_minute_notify_at_the_ceiling_and_again_after_the_next_one()
    {
        var rig = await StartAsync();

        for (var save = 0; save < 16; save++)
        {
            await rig.PlaceNextGameAsync();
            rig.Harness.Clock.Advance(TimeSpan.FromSeconds(1));
            rig.Harness.Clock.Advance(TimeSpan.FromSeconds(1));
        }

        rig.Harness.Clock.Advance(TimeSpan.FromMinutes(1));

        rig.Live.Calls.Select(call => call.At - OwnerTestKit.Start).Should().Equal(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(31));
    }

    [Fact]
    public async Task A_burst_that_ends_where_it_began_notifies_nobody_because_the_collection_version_did_not_change()
    {
        var rig = await StartAsync();

        await rig.PlaceNextGameAsync();
        await rig.Harness.Service.MutateAsync(
            rig.Harness.Service.Revision,
            data => Cabinet.Domain.Owner.LocationEdits.Assign(data, FirstGame, null, "Game 100"),
            CancellationToken.None);
        rig.Harness.Clock.Advance(TimeSpan.FromMinutes(1));

        rig.Live.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task A_look_change_alone_notifies_once_after_the_quiet_period_and_carries_the_new_look()
    {
        var rig = await StartAsync();

        await rig.SetLookAsync("modern", "oak");
        rig.Harness.Clock.Advance(TimeSpan.FromSeconds(2.9));
        rig.Live.Calls.Should().BeEmpty("the quiet period of three seconds has not passed");
        rig.Harness.Clock.Advance(TimeSpan.FromSeconds(0.1));

        var call = rig.Live.Calls.Should().ContainSingle().Which;
        call.Status.Look.Should().Be(new LookStatus("modern", "oak", "wood", "off", "shaded", false));
        call.Status.SnapshotVersion.Should().Be(rig.Harness.Version);
        rig.Harness.Clock.Advance(TimeSpan.FromMinutes(1));
        rig.Live.Calls.Should().HaveCount(1);
    }

    [Fact]
    public async Task Changing_the_look_and_back_within_the_quiet_period_notifies_nobody()
    {
        var rig = await StartAsync();

        await rig.SetLookAsync("modern", "oak");
        rig.Harness.Clock.Advance(TimeSpan.FromSeconds(1));
        await rig.SetLookAsync(CabinetLook.Default.Design, CabinetLook.Default.Wood);
        rig.Harness.Clock.Advance(TimeSpan.FromMinutes(1));

        rig.Live.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task A_look_change_and_a_collection_change_in_one_burst_notify_once_with_both()
    {
        var rig = await StartAsync();

        await rig.SetLookAsync("library", "birch");
        rig.Harness.Clock.Advance(TimeSpan.FromSeconds(1));
        await rig.PlaceNextGameAsync();
        rig.Harness.Clock.Advance(TimeSpan.FromMinutes(1));

        var call = rig.Live.Calls.Should().ContainSingle().Which;
        call.Status.Look.Design.Should().Be("library");
        call.Status.Look.LightWall.Should().BeTrue("birch has a light wall");
        call.Status.SnapshotVersion.Should().Be(rig.Harness.Version);
    }

    [Fact]
    public async Task A_failed_look_notification_is_tried_again_on_the_next_change()
    {
        var rig = await StartAsync();
        rig.Live.FailNext = true;

        await rig.SetLookAsync("modern", "oak");
        rig.Harness.Clock.Advance(TimeSpan.FromSeconds(3));
        rig.Live.Attempts.Should().Be(1);
        rig.Live.Calls.Should().BeEmpty();
        await rig.SetLookAsync("modern", "oak", lights: "on");
        rig.Harness.Clock.Advance(TimeSpan.FromSeconds(3));

        rig.Live.Calls.Should().ContainSingle().Which.Status.Look.Should().Be(new LookStatus("modern", "oak", "wood", "on", "shaded", false));
    }

    [Fact]
    public async Task A_failed_notification_is_not_fatal_and_the_next_burst_is_announced()
    {
        var rig = await StartAsync();
        rig.Live.FailNext = true;

        await rig.PlaceNextGameAsync();
        rig.Harness.Clock.Advance(TimeSpan.FromSeconds(3));
        var attemptsAfterFailure = rig.Live.Attempts;
        await rig.PlaceNextGameAsync();
        rig.Harness.Clock.Advance(TimeSpan.FromSeconds(3));

        attemptsAfterFailure.Should().Be(1);
        rig.Live.Calls.Should().ContainSingle().Which.Status.SnapshotVersion.Should().Be(rig.Harness.Version);
    }

    [Fact]
    public async Task After_the_notifier_is_stopped_saves_notify_nobody()
    {
        var rig = await StartAsync();
        await rig.PlaceNextGameAsync();

        await rig.Notifier.StopAsync(CancellationToken.None);
        await rig.PlaceNextGameAsync();
        rig.Harness.Clock.Advance(TimeSpan.FromMinutes(1));

        rig.Live.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Stopping_after_the_host_disposed_the_notifier_is_harmless()
    {
        var rig = await StartAsync();

        rig.Notifier.Dispose();
        var stop = () => rig.Notifier.StopAsync(CancellationToken.None);

        await stop.Should().NotThrowAsync();
        await rig.PlaceNextGameAsync();
        rig.Harness.Clock.Advance(TimeSpan.FromMinutes(1));
        rig.Live.Calls.Should().BeEmpty();
    }

    [Fact]
    public void The_quiet_period_is_three_seconds_and_the_ceiling_fifteen()
    {
        OwnerChangeNotifier.QuietPeriod.Should().Be(TimeSpan.FromSeconds(3));
        OwnerChangeNotifier.Ceiling.Should().Be(TimeSpan.FromSeconds(15));
    }

    private static async Task<Rig> StartAsync()
    {
        var gameIds = Enumerable.Range(FirstGame, 40).ToArray();
        var harness = OwnerTestKit.Harness(snapshot: OwnerTestKit.Listing(gameIds));
        await harness.AddAsync(CabinetA);
        await harness.AddAsync(ShelfB);
        var coordinator = new SyncCoordinator(new InMemorySyncStateStore(), Options, harness.Clock);
        var status = new SyncStatusService(coordinator, harness.Collection, Options, harness.Clock, new SiteLook(harness.Service));
        var live = new RecordingLiveNotifier(harness.Clock);
        var notifier = new OwnerChangeNotifier(harness.Service, status, live, harness.Clock, NullLogger<OwnerChangeNotifier>.Instance);
        await notifier.StartAsync(CancellationToken.None);

        return new Rig(harness, live, notifier);
    }

    private sealed class Rig(OwnerHarness harness, RecordingLiveNotifier live, OwnerChangeNotifier notifier)
    {
        private int _next = FirstGame;

        public OwnerHarness Harness { get; } = harness;

        public RecordingLiveNotifier Live { get; } = live;

        public OwnerChangeNotifier Notifier { get; } = notifier;

        public Task<OwnerResult> PlaceNextGameAsync() => Harness.PlaceAsync(_next++, CabinetA);

        public Task<OwnerResult> SetLookAsync(string design, string wood, string lights = "off") =>
            Harness.Service.MutateAsync(
                Harness.Service.Revision,
                data => CabinetLooks.SetLook(data, design, wood, CabinetLook.Default.Backs, lights, CabinetLook.Default.Depth),
                CancellationToken.None);
    }

    private sealed class RecordingLiveNotifier(TimeProvider clock) : ILiveNotifier
    {
        private readonly List<(DateTimeOffset At, CabinetStatus Status)> _calls = [];

        public IReadOnlyList<(DateTimeOffset At, CabinetStatus Status)> Calls => _calls;

        public int Attempts { get; private set; }

        public bool FailNext { get; set; }

        public Task PublishAsync(CabinetStatus status, CancellationToken cancellationToken)
        {
            Attempts++;

            if (FailNext)
            {
                FailNext = false;

                throw new InvalidOperationException("The hub is gone.");
            }

            _calls.Add((clock.GetUtcNow(), status));

            return Task.CompletedTask;
        }
    }
}

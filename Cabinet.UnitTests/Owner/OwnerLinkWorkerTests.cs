using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.Repository.Images;
using Cabinet.Service.Owner;
using Cabinet.Service.Sync;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Owner;

/// <summary>
/// Proves that the background work keeps a linked picture alive: a stored copy is checked again a week after the start and
/// not before, a failing check keeps the record and its files, two failed checks a day apart make the link dead, a success
/// clears that, none of it touches the revision or the change log, and a copy measured by an older analysis is fetched
/// again and replaced.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerLinkWorkerTests : IDisposable
{
    private const int GameId = 7;
    private const string Address = $"https://{ImageSettings.DefaultHost}/owner/worker.png";

    private static readonly byte[] Cover = SyntheticArt.Encode(SyntheticArtKind.FlatCover);

    private readonly TemporaryDirectory _storage = new();
    private readonly FakeTimeProvider _clock = new(OwnerTestKit.Start);
    private readonly InMemoryOwnerDataStore _store = new();
    private readonly CountingSource _source = new();
    private readonly List<OwnerLinkWorker> _workers = [];

    public void Dispose()
    {
        foreach (var worker in _workers)
        {
            worker.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            worker.Dispose();
        }

        _storage.Dispose();
    }

    [Fact]
    public async Task A_stored_copy_is_checked_again_seven_days_after_the_start_and_not_before()
    {
        var (owner, cache) = Seed(link: new PictureLink(Address, null, 0, null));
        await StartAsync(owner, cache);
        await WaitUntilAsync(() => Link(owner).Record is not null);
        _source.Requests.Should().Be(1);

        _clock.Advance(RecheckEvery - TimeSpan.FromHours(1));
        owner.RequestLinkWork();
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        _source.Requests.Should().Be(1, "seven days have not passed yet");

        _clock.Advance(TimeSpan.FromHours(1));
        owner.RequestLinkWork();
        await WaitUntilAsync(() => _source.Requests == 2);
    }

    [Fact]
    public async Task A_failing_check_keeps_the_record_and_files_and_a_second_one_a_day_later_makes_the_link_dead()
    {
        var (owner, cache) = Seed(link: new PictureLink(Address, null, 0, null));
        await StartAsync(owner, cache);
        await WaitUntilAsync(() => Link(owner).Record is not null);
        var stored = Link(owner).Record!;
        var dataBefore = Data(owner);
        _source.Answer = _ => ArtDownload.Failed("status", 404);

        _clock.Advance(RecheckEvery);
        owner.RequestLinkWork();
        await WaitUntilAsync(() => Link(owner).FailedChecks == 1);

        Link(owner).Record.Should().BeSameAs(stored);
        stored.Files!.Should().OnlyContain(file => cache.Has(file.Name));
        LinkHealth.IsDead(Link(owner), _clock.GetUtcNow()).Should().BeFalse("one failed check never makes a link dead");

        _clock.Advance(TimeSpan.FromDays(1));
        owner.RequestLinkWork();
        await WaitUntilAsync(() => Link(owner).FailedChecks == 2);

        Link(owner).Record.Should().BeSameAs(stored);
        stored.Files!.Should().OnlyContain(file => cache.Has(file.Name));
        LinkHealth.IsDead(Link(owner), _clock.GetUtcNow()).Should().BeTrue();
        AssertNothingLogged(dataBefore, Data(owner));
    }

    [Fact]
    public async Task A_second_failure_less_than_a_day_after_the_first_is_not_made_early()
    {
        var (owner, cache) = Seed(link: new PictureLink(Address, null, 0, null));
        await StartAsync(owner, cache);
        await WaitUntilAsync(() => Link(owner).Record is not null);
        _source.Answer = _ => ArtDownload.Failed("unavailable");
        _clock.Advance(RecheckEvery);
        owner.RequestLinkWork();
        await WaitUntilAsync(() => Link(owner).FailedChecks == 1);
        var requests = _source.Requests;

        _clock.Advance(TimeSpan.FromHours(23));
        owner.RequestLinkWork();
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);

        _source.Requests.Should().Be(requests);
        Link(owner).FailedChecks.Should().Be(1);
    }

    [Fact]
    public async Task A_success_after_the_link_died_clears_the_failures_and_replaces_the_record()
    {
        var (owner, cache) = Seed(link: new PictureLink(Address, null, 0, null));
        await StartAsync(owner, cache);
        await WaitUntilAsync(() => Link(owner).Record is not null);
        var stored = Link(owner).Record!;
        var dataBefore = Data(owner);
        _source.Answer = _ => ArtDownload.Failed("status", 500);
        _clock.Advance(RecheckEvery);
        owner.RequestLinkWork();
        await WaitUntilAsync(() => Link(owner).FailedChecks == 1);
        _clock.Advance(TimeSpan.FromDays(1));
        owner.RequestLinkWork();
        await WaitUntilAsync(() => Link(owner).FailedChecks == 2);
        _source.Answer = _ => ArtDownload.Downloaded(Cover);

        _clock.Advance(RecheckEvery);
        owner.RequestLinkWork();
        await WaitUntilAsync(() => Link(owner).FailedChecks == 0);

        Link(owner).FailedSinceUtc.Should().BeNull();
        Link(owner).Record!.AttemptedAtUtc.Should().BeAfter(stored.AttemptedAtUtc);
        LinkHealth.IsDead(Link(owner), _clock.GetUtcNow()).Should().BeFalse();
        Link(owner).Record!.Files!.Should().OnlyContain(file => cache.Has(file.Name));
        AssertNothingLogged(dataBefore, Data(owner));
    }

    [Fact]
    public async Task A_dead_link_is_checked_once_a_week_and_not_every_day()
    {
        var (owner, cache) = Seed(link: new PictureLink(Address, null, 0, null));
        await StartAsync(owner, cache);
        await WaitUntilAsync(() => Link(owner).Record is not null);
        _source.Answer = _ => ArtDownload.Failed("status", 404);
        _clock.Advance(RecheckEvery);
        owner.RequestLinkWork();
        await WaitUntilAsync(() => Link(owner).FailedChecks == 1);
        _clock.Advance(TimeSpan.FromDays(1));
        owner.RequestLinkWork();
        await WaitUntilAsync(() => Link(owner).FailedChecks == 2);
        var requests = _source.Requests;

        _clock.Advance(TimeSpan.FromDays(6));
        owner.RequestLinkWork();
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        _source.Requests.Should().Be(requests, "a dead link waits a week");

        _clock.Advance(TimeSpan.FromDays(1));
        owner.RequestLinkWork();
        await WaitUntilAsync(() => Link(owner).FailedChecks == 3);
    }

    [Fact]
    public async Task A_copy_measured_by_an_older_analysis_is_fetched_again_and_replaced()
    {
        var current = Stored(analysisVersion: ArtProcessor.AnalysisVersion);
        var older = current with { AnalysisVersion = ArtProcessor.AnalysisVersion - 1 };
        var (owner, cache) = Seed(link: new PictureLink(Address, older, 0, null), cacheFiles: older);
        await StartAsync(owner, cache);

        await WaitUntilAsync(() => Link(owner).Record?.AnalysisVersion == ArtProcessor.AnalysisVersion);

        _source.Requests.Should().Be(1);
        Link(owner).Record!.Files!.Should().OnlyContain(file => cache.Has(file.Name));
    }

    [Fact]
    public async Task A_copy_measured_by_the_current_analysis_with_its_files_is_not_fetched_at_the_start()
    {
        var current = Stored(analysisVersion: ArtProcessor.AnalysisVersion);
        var (owner, cache) = Seed(link: new PictureLink(Address, current, 0, null), cacheFiles: current);

        await StartAsync(owner, cache);
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);

        _source.Requests.Should().Be(0);
    }

    [Fact]
    public async Task An_older_copy_whose_link_now_fails_is_kept_and_the_failure_is_counted()
    {
        var current = Stored(analysisVersion: ArtProcessor.AnalysisVersion);
        var older = current with { AnalysisVersion = ArtProcessor.AnalysisVersion - 1 };
        var (owner, cache) = Seed(link: new PictureLink(Address, older, 0, null), cacheFiles: older);
        _source.Answer = _ => ArtDownload.Failed("status", 404);

        await StartAsync(owner, cache);
        await WaitUntilAsync(() => Link(owner).FailedChecks == 1);

        Link(owner).Record.Should().BeSameAs(older);
        older.Files!.Should().OnlyContain(file => cache.Has(file.Name));
    }

    private static TimeSpan RecheckEvery => OwnerLinkWorker.RecheckEvery;

    private static void AssertNothingLogged(OwnerData before, OwnerData after)
    {
        after.Revision.Should().Be(before.Revision);
        after.ChangesSinceDownload.Should().Be(before.ChangesSinceDownload);
        after.Changes.Should().Equal(before.Changes);
    }

    private static PictureLink Link(OwnerDataService owner) => Data(owner).Games.Single().Picture!.Link!;

    private static OwnerData Data(OwnerDataService owner) => ((OwnerDataState.Ready)owner.State).Data;

    private static ImageOptions Options() =>
        ImageSettings.FromConfiguration(new ConfigurationBuilder().Build(), new TestEnvironment("Production"));

    private static ImageRecord Stored(int analysisVersion)
    {
        var done = (ArtProcessing.Done)ArtProcessor.Process(Cover, Options().Limits);

        return ArtRecords.FromProcessed(Address, OwnerTestKit.Start, done) with { AnalysisVersion = analysisVersion };
    }

    private (OwnerDataService Owner, ArtCache Cache) Seed(PictureLink link, ImageRecord? cacheFiles = null)
    {
        var cache = new ArtCache(_storage.FullPath);

        if (cacheFiles is not null)
        {
            var done = (ArtProcessing.Done)ArtProcessor.Process(Cover, Options().Limits);

            foreach (var variant in done.Variants)
            {
                cache.Write(variant);
            }
        }

        var game = new OwnerGame(GameId, null, null, new PictureSetting(PictureChoice.Link, true, null, null, link));
        _store.State = new OwnerDataState.Ready(OwnerData.Empty with { Revision = 1, Games = [game] });
        _store.FileExists = true;
        _source.Answer = _ => ArtDownload.Downloaded(Cover);

        return (new OwnerDataService(_store, _clock, NullLogger<OwnerDataService>.Instance), cache);
    }

    private async Task StartAsync(OwnerDataService owner, ArtCache cache)
    {
        var fetcher = new OwnerLinkFetcher(() => _source, Options(), _clock);
        var sync = new SyncOptions(true, TimeSpan.FromHours(1), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(2), TimeSpan.FromHours(3));
        var worker = new OwnerLinkWorker(owner, fetcher, cache, sync, _clock, NullLogger<OwnerLinkWorker>.Instance);
        _workers.Add(worker);

        await worker.StartAsync(TestContext.Current.CancellationToken);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (!condition())
        {
            if (DateTime.UtcNow > until)
            {
                throw new TimeoutException("The condition did not become true in time.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        }
    }

    private sealed class CountingSource : IArtSource
    {
        private int _requests;

        public Func<Uri, ArtDownload> Answer { get; set; } = _ => ArtDownload.Downloaded([]);

        public int Requests => Volatile.Read(ref _requests);

        public async Task<ArtDownload> DownloadAsync(Uri uri, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            await Task.Yield();

            return Answer(uri);
        }
    }
}

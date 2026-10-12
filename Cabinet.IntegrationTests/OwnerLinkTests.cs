using System.Text.Json;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Images;
using Cabinet.Service.Owner;
using Cabinet.Service.Sync;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves that a picture the owner linked stays: the server fetches it in the background through the picture client that
/// carries no credentials, every visitor's card shows it once it is stored, a sync round long past the clean-up grace leaves
/// its files in place while removing a file nothing refers to, a sync round while the owner data cannot be read removes
/// nothing, and none of this runs on a host whose background work is switched off.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerLinkTests
{
    private const string CardsPath = "/cabinet/cards?profile=desktop";
    private const string LinkUrl = "https://example.org/owner/linked-picture.png";
    private const string StrayName = "0123456789abcdef-240.webp";

    private static readonly byte[] LinkedCover = SyntheticArt.Encode(SyntheticArtKind.CoverColouredField);

    [Fact]
    public async Task A_stored_link_with_no_copy_is_fetched_without_credentials_and_every_card_shows_it()
    {
        await using var host = await LinkHost.StartAsync(serveLink: true, background: true);

        var record = await host.WaitForRecordAsync();
        await SyncRounds.PressAndWait(host.Client, host.Clock, advance: false);
        var cover = await host.CoverAsync();

        var requests = host.Images.Requests.Where(request => request.Uri.AbsoluteUri == LinkUrl).ToList();
        requests.Should().ContainSingle();
        requests[0].AuthorizationScheme.Should().BeNull();
        requests[0].AuthorizationParameter.Should().BeNull();
        requests[0].HasCookie.Should().BeFalse();
        record.Status.Should().Be(ImageStatus.Ok);
        record.Files!.Should().NotBeEmpty().And.OnlyContain(file => File.Exists(host.ArtFile(file.Name)));
        record.Files.Select(file => file.Name).Should().Contain(Path.GetFileName(cover.Url));
    }

    [Fact]
    public async Task A_stored_copy_keeps_being_served_and_shown_while_its_link_answers_not_found()
    {
        await using var host = await LinkHost.StartAsync(serveLink: true, background: true);
        var record = await host.WaitForRecordAsync();
        await SyncRounds.PressAndWait(host.Client, host.Clock, advance: false);
        var before = await host.CoverAsync();
        host.Images.Withdraw(LinkUrl);

        host.Clock.Advance(OwnerLinkWorker.RecheckEvery + TimeSpan.FromHours(1));
        host.Service.RequestLinkWork();
        await SyncHarness.WaitUntil(() => Task.FromResult(host.LinkFailedChecks == 1));
        var after = await host.CoverAsync();

        host.Images.Requests.Count(request => request.Uri.AbsoluteUri == LinkUrl).Should().BeGreaterThan(1);
        host.LinkRecord.Should().BeSameAs(record);
        record.Files!.Should().OnlyContain(file => File.Exists(host.ArtFile(file.Name)));
        after.Url.Should().Be(before.Url);
        record.Files.Select(file => file.Name).Should().Contain(Path.GetFileName(after.Url));
        using var served = await host.Client.GetAsync(after.Url, TestContext.Current.CancellationToken);
        served.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        (await served.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_link_that_could_not_be_fetched_is_fetched_when_asked_for_again_and_the_card_changes_to_show_it()
    {
        await using var host = await LinkHost.StartAsync(serveLink: false, background: true);
        await SyncRounds.PressAndWait(host.Client, host.Clock, advance: false);
        var before = await host.CoverAsync();
        host.Images.Serve(LinkUrl, LinkedCover);

        host.Service.RequestLinkWork();
        var record = await host.WaitForRecordAsync();
        var after = await host.WaitForCoverFromAsync(record);

        host.LinkRecord.Should().NotBeNull();
        before.Url.Should().NotBe(after.Url);
        before.ETag.Should().NotBe(after.ETag);
        host.Images.Requests.Where(request => request.Uri.AbsoluteUri == LinkUrl).Should().OnlyContain(
            request => request.AuthorizationScheme == null && !request.HasCookie);
    }

    [Fact]
    public async Task A_sync_round_long_past_the_cleanup_grace_keeps_the_files_of_a_link_and_removes_a_file_nothing_refers_to()
    {
        await using var host = await LinkHost.StartAsync(serveLink: true, background: true);
        var record = await host.WaitForRecordAsync();
        host.PlantStray();
        host.AgeEveryArtFile();

        await SyncRounds.PressAndWait(host.Client, host.Clock, advance: false);

        record.Files!.Should().OnlyContain(file => File.Exists(host.ArtFile(file.Name)));
        File.Exists(host.ArtFile(StrayName)).Should().BeFalse();
    }

    [Fact]
    public async Task A_sync_round_while_the_owner_data_cannot_be_read_removes_nothing_from_the_art_directory()
    {
        await using var host = await LinkHost.StartAsync(serveLink: true, background: true, unreadable: true);
        host.PlantStray();
        host.AgeEveryArtFile();

        await SyncRounds.PressAndWait(host.Client, host.Clock, advance: false);

        File.Exists(host.ArtFile(StrayName)).Should().BeTrue();
        host.Images.Requests.Should().NotContain(request => request.Uri.AbsoluteUri == LinkUrl);
    }

    [Fact]
    public async Task A_host_whose_background_work_is_off_fetches_no_link()
    {
        await using var host = await LinkHost.StartAsync(serveLink: true, background: false);

        await SyncRounds.PressAndWait(host.Client, host.Clock, advance: false);

        host.Images.Requests.Should().NotContain(request => request.Uri.AbsoluteUri == LinkUrl);
        host.LinkRecord.Should().BeNull();
    }

    private sealed record Cover(string Url, string ETag);

    private sealed class LinkHost : IAsyncDisposable
    {
        private readonly TemporaryDirectory _storage;
        private readonly CabinetWebApplicationFactory _factory;
        private readonly int _gameId;

        private LinkHost(
            TemporaryDirectory storage,
            CabinetWebApplicationFactory factory,
            ScriptedImageHandler images,
            FakeTimeProvider clock,
            HttpClient client,
            int gameId)
        {
            _storage = storage;
            _factory = factory;
            Images = images;
            Clock = clock;
            Client = client;
            _gameId = gameId;
        }

        public ScriptedImageHandler Images { get; }

        public FakeTimeProvider Clock { get; }

        public HttpClient Client { get; }

        public OwnerDataService Service => _factory.ServingServices.GetRequiredService<OwnerDataService>();

        public ImageRecord? LinkRecord => Service.State is OwnerDataState.Ready ready
            ? ready.Data.Games.FirstOrDefault(game => game.GameId == _gameId)?.Picture?.Link?.Record
            : null;

        public int? LinkFailedChecks => Service.State is OwnerDataState.Ready ready
            ? ready.Data.Games.FirstOrDefault(game => game.GameId == _gameId)?.Picture?.Link?.FailedChecks
            : null;

        public static async Task<LinkHost> StartAsync(bool serveLink, bool background, bool unreadable = false)
        {
            var storage = new TemporaryDirectory();
            var items = SyntheticBggCollection.Create(5);
            var item = items.First(candidate => candidate.Owned && !candidate.IsExpansion);
            var images = new ScriptedImageHandler();

            if (serveLink)
            {
                images.Serve(LinkUrl, LinkedCover);
            }

            Seed(storage, item.ObjectId, unreadable);
            var clock = SyncHarness.NewClock();
            var settings = new Dictionary<string, string?>
            {
                ["Images:AllowedHosts"] = "example.org",
                ["Images:MaxDownloadsPerRun"] = "1000",
                ["Storage:Directory"] = storage.FullPath,
            };

            if (background)
            {
                settings["Sync:BackgroundEnabled"] = "true";
            }

            var factory = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(items), clock, settings, images);

            return new LinkHost(storage, factory, images, clock, factory.CreatePublicClient(), item.ObjectId);
        }

        public async Task<ImageRecord> WaitForRecordAsync()
        {
            await SyncHarness.WaitUntil(() => Task.FromResult(LinkRecord is not null));

            return LinkRecord!;
        }

        public async Task<Cover> WaitForCoverFromAsync(ImageRecord record)
        {
            var cover = await CoverAsync();

            await SyncHarness.WaitUntil(async () =>
            {
                cover = await CoverAsync();

                return record.Files!.Any(file => file.Name == Path.GetFileName(cover.Url));
            });

            return cover;
        }

        public async Task<Cover> CoverAsync()
        {
            using var response = await Client.GetAsync(CardsPath, TestContext.Current.CancellationToken);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var card = document.RootElement.GetProperty("cards").EnumerateArray()
                .First(candidate => candidate.GetProperty("gameId").GetInt32() == _gameId);

            var url = card.TryGetProperty("cover", out var cover) && cover.ValueKind == JsonValueKind.Object && cover.TryGetProperty("url", out var address)
                ? address.GetString()!
                : string.Empty;

            return new Cover(url, response.Headers.ETag!.Tag);
        }

        public string ArtFile(string name) => Path.Combine(_storage.FullPath, ArtCache.DirectoryName, name);

        public void PlantStray()
        {
            Directory.CreateDirectory(Path.Combine(_storage.FullPath, ArtCache.DirectoryName));
            File.WriteAllBytes(ArtFile(StrayName), [1, 2, 3]);
        }

        public void AgeEveryArtFile()
        {
            var old = Clock.GetUtcNow().UtcDateTime - _factory.ServingServices.GetRequiredService<ImageOptions>().PruneGrace - TimeSpan.FromDays(10);

            foreach (var file in Directory.EnumerateFiles(Path.Combine(_storage.FullPath, ArtCache.DirectoryName)))
            {
                File.SetLastWriteTimeUtc(file, old);
            }
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _factory.DisposeAsync();
            _storage.Dispose();
        }

        private static void Seed(TemporaryDirectory storage, int gameId, bool unreadable)
        {
            if (unreadable)
            {
                File.WriteAllText(Path.Combine(storage.FullPath, "owner-data.json"), "this is not an owner-data file");

                return;
            }

            var link = new PictureLink(LinkUrl, null, 0, null);
            var game = new OwnerGame(gameId, null, null, new PictureSetting(PictureChoice.Link, true, null, null, link));

            OwnerDataSeed.Write(storage.FullPath, OwnerData.Empty with { Revision = 1, Games = [game] });
        }
    }
}

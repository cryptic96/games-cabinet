using System.Globalization;
using System.Net;
using System.Text.Json;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Images;
using Cabinet.Service.Collection;
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

    [Fact]
    public async Task A_link_used_for_one_copy_draws_only_that_copy()
    {
        await using var host = await CopiesHost.StartAsync();
        var before = await host.CoversAsync();
        var (first, second) = (host.Copies[0], host.Copies[1]);

        var checkedLink = await host.CheckAsync(second);
        var used = await host.UseAsync(second, checkedLink.JsonText("token"));
        var linked = await host.CoversAsync();

        used.JsonText("outcome").Should().Be("saved");
        host.EntryOf(second)!.Picture!.Link!.Url.Should().Be(LinkUrl);
        host.EntryOf(second)!.Picture!.Choice.Should().Be(PictureChoice.Link);
        host.EntryOf(first)?.Picture?.Link.Should().BeNull();
        linked[first].Should().Be(before[first]);
        linked[second].Should().NotBe(before[second]);
        host.EntryOf(second)!.Picture!.Link!.Record!.Files!.Select(file => file.Name).Should().Contain(Path.GetFileName(linked[second]!));

        var removed = await host.RemoveAsync(second);
        var unlinked = await host.CoversAsync();

        removed.JsonText("outcome").Should().Be("saved");
        host.EntryOf(second)?.Picture?.Link.Should().BeNull();
        unlinked[second].Should().Be(before[second]);
        unlinked[first].Should().Be(before[first]);

        var undone = await host.UndoAsync(second);
        var restored = await host.CoversAsync();

        undone.JsonText("outcome").Should().Be("saved");
        host.EntryOf(second)!.Picture!.Link!.Url.Should().Be(LinkUrl);
        restored[first].Should().Be(before[first]);
        restored[second].Should().NotBe(before[second]);
    }

    [Fact]
    public async Task Every_link_handler_refuses_a_foreign_copy()
    {
        await using var host = await CopiesHost.StartAsync();
        var token = (await host.CheckAsync(host.Copies[1])).JsonText("token");
        var requests = host.Images.Requests.Count;
        var revision = host.Service.Revision;

        var check = await host.CheckAsync(host.Foreign);
        var use = await host.UseAsync(host.Foreign, token);
        var remove = await host.RemoveAsync(host.Foreign);
        var discard = await host.DiscardAsync(host.Foreign, token);
        var missing = await host.Session.PostJsonAsync("/owner/pictures?handler=CheckLink", [Field("gameId", host.GameId), Field("link", LinkUrl)]);
        using var owner = host.Factory.CreateOwnerClient();
        using var served = await owner.GetAsync($"/owner/preview/{token}", TestContext.Current.CancellationToken);

        foreach (var reply in new[] { check, use, remove, discard, missing })
        {
            reply.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
            reply.JsonText("status").Should().Be("unknown-game");
        }

        host.Service.Revision.Should().Be(revision);
        host.Images.Requests.Count.Should().Be(requests, "a refused check fetches nothing");
        served.StatusCode.Should().Be(HttpStatusCode.OK, "a refused post does not throw away a preview that belongs to another copy");
    }

    [Fact]
    public async Task A_preview_checked_for_one_copy_cannot_be_used_for_another_copy_of_the_same_game()
    {
        await using var host = await CopiesHost.StartAsync();
        var token = (await host.CheckAsync(host.Copies[1])).JsonText("token");
        var revision = host.Service.Revision;

        var use = await host.UseAsync(host.Copies[0], token);

        use.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        use.JsonText("status").Should().Be("link-expired");
        host.Service.Revision.Should().Be(revision);
    }

    private sealed record Cover(string Url, string ETag);

    private static KeyValuePair<string, string> Field(string name, long value) => new(name, value.ToString(CultureInfo.InvariantCulture));

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);

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

    private sealed class CopiesHost : IAsyncDisposable
    {
        private const string PagePath = "/owner/pictures?handler=";

        private readonly TemporaryDirectory _storage;
        private readonly HttpClient _client;

        private CopiesHost(TemporaryDirectory storage, CabinetWebApplicationFactory factory, ScriptedImageHandler images, HttpClient client, int gameId, IReadOnlyList<long> copies, long foreign)
        {
            _storage = storage;
            Factory = factory;
            Images = images;
            _client = client;
            GameId = gameId;
            Copies = copies;
            Foreign = foreign;
            Session = new OwnerSession(factory);
        }

        public CabinetWebApplicationFactory Factory { get; }

        public ScriptedImageHandler Images { get; }

        public OwnerSession Session { get; }

        public int GameId { get; }

        public IReadOnlyList<long> Copies { get; }

        public long Foreign { get; }

        public OwnerDataService Service => Factory.ServingServices.GetRequiredService<OwnerDataService>();

        public static async Task<CopiesHost> StartAsync()
        {
            var storage = new TemporaryDirectory();
            var items = SyntheticBggCollection.Create(5);
            var images = new ScriptedImageHandler().Serve(LinkUrl, LinkedCover);
            var clock = SyncHarness.NewClock();
            var factory = SyncHarness.CreateFactory(
                ScriptedBggHandler.ForCollection(items),
                clock,
                new Dictionary<string, string?>
                {
                    ["Images:AllowedHosts"] = "example.org",
                    ["Images:MaxDownloadsPerRun"] = "1000",
                    ["Storage:Directory"] = storage.FullPath,
                },
                images);
            var client = factory.CreatePublicClient();
            await SyncRounds.PressAndWait(client, clock, advance: false);

            var snapshotItems = factory.ServingServices.GetRequiredService<CollectionStore>().Current.Snapshot!.Items;
            var twice = snapshotItems.GroupBy(item => item.GameId).First(group => group.Select(item => item.CollectionId).Distinct().Count() > 1);
            var foreign = snapshotItems.First(item => item.GameId != twice.Key).CollectionId;

            return new CopiesHost(storage, factory, images, client, twice.Key, [.. twice.Select(item => item.CollectionId).Distinct().Order()], foreign);
        }

        public OwnerGame? EntryOf(long collectionId) =>
            Service.State is OwnerDataState.Ready ready ? ready.Data.Games.FirstOrDefault(game => game.CollectionId == collectionId) : null;

        public async Task<Dictionary<long, string?>> CoversAsync()
        {
            using var response = await _client.GetAsync(CardsPath, TestContext.Current.CancellationToken);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

            return document.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(
                card => card.GetProperty("entryId").GetInt64(),
                card => card.TryGetProperty("cover", out var cover) && cover.ValueKind == JsonValueKind.Object ? cover.GetProperty("url").GetString() : null);
        }

        public Task<OwnerReply> CheckAsync(long collectionId) =>
            Session.PostJsonAsync($"{PagePath}CheckLink", [Field("gameId", GameId), Field("collectionId", collectionId), Field("link", LinkUrl)]);

        public Task<OwnerReply> UseAsync(long collectionId, string token) =>
            Session.PostJsonAsync($"{PagePath}UseLink", [Field("gameId", GameId), Field("collectionId", collectionId), Field("token", token), Field("revision", Service.Revision)]);

        public Task<OwnerReply> RemoveAsync(long collectionId) =>
            Session.PostJsonAsync($"{PagePath}RemoveLink", [Field("gameId", GameId), Field("collectionId", collectionId), Field("revision", Service.Revision)]);

        public Task<OwnerReply> DiscardAsync(long collectionId, string token) =>
            Session.PostJsonAsync($"{PagePath}DiscardPreview", [Field("gameId", GameId), Field("collectionId", collectionId), Field("token", token)]);

        public Task<OwnerReply> UndoAsync(long collectionId) =>
            Session.PostJsonAsync($"{PagePath}Undo", [Field("gameId", GameId), Field("collectionId", collectionId), Field("revision", Service.Revision)]);

        public async ValueTask DisposeAsync()
        {
            Session.Dispose();
            _client.Dispose();
            await Factory.DisposeAsync();
            _storage.Dispose();
        }
    }
}

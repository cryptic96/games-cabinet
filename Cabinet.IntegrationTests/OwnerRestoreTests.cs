using System.Net;
using System.Text;
using System.Text.Json;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Storage;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves the restore from a file through booted hosts: a file downloaded from one server brings everything back into a
/// fresh one with an empty state directory, the data that was there is kept as an earlier version first, and every file the
/// check cannot fully accept changes nothing and says why.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerRestoreTests
{
    private const string SettingsPath = "/owner/settings";
    private const string DownloadPath = SettingsPath + "?handler=Download";
    private const string CheckPath = SettingsPath + "?handler=CheckFile";
    private const string RestorePath = SettingsPath + "?handler=Restore";
    private const string FileField = "ownerDataFile";
    private const string CardsPath = "/cabinet/cards?profile=desktop";
    private const string LinkAddress = "https://example.org/owner/linked-picture.png";
    private const string CabinetA = "Cabinet A";
    private const string ShelfB = "Shelf B";
    private const string CabinetAId = "a1b2c3d4";
    private const string ShelfBId = "e5f6a7b8";
    private const string PreviewHeading = "This file contains";
    private const string Summary = "2 locations, 2 placed games, 2 picture choices";

    private static readonly byte[] LinkedCover = SyntheticArt.Encode(SyntheticArtKind.CoverColouredField);

    [Fact]
    public async Task A_download_from_one_server_restores_into_a_fresh_server_and_its_cards_show_the_locations_and_the_link_picture_again()
    {
        await using var first = await RestoreHost.StartAsync(seed: Seeded());
        var download = await first.Session.PostFormAsync(DownloadPath, []);
        await using var second = await RestoreHost.StartAsync(seed: null, background: true);
        second.StartedEmpty().Should().BeTrue();
        await SyncRounds.PressAndWait(second.Client, second.Clock, advance: false);

        var check = await second.Session.PostFileAsync(CheckPath, FileField, Encoding.UTF8.GetBytes(download.Body));
        var preview = await second.Session.GetAsync(check.Location!);
        var token = OwnerSession.FieldValue(preview.Body, "token");
        var restored = await second.Session.PostFormAsync(RestorePath, [new("token", token)]);
        var record = await second.WaitForLinkRecordAsync(2);
        var cards = await second.CardsAsync();

        check.Status.Should().Be(HttpStatusCode.Found);
        check.Location.Should().StartWith(SettingsPath + "?restore=").And.EndWith("#restore-preview-heading");
        preview.Body.Should().Contain(PreviewHeading).And.Contain(Summary).And.Contain("In the file").And.Contain("On this server now");
        restored.Status.Should().Be(HttpStatusCode.OK);
        restored.Body.Should().Contain($"Restored. {Summary}. Your previous data is kept as an earlier version.").And.Contain("Fetching 1 picture in the background.");
        second.Data.Locations.Select(location => location.Name).Should().Equal(CabinetA, ShelfB);
        second.Data.Games.Select(game => game.LocationId).Should().Equal(CabinetAId, ShelfBId);
        second.Data.Games.Select(game => game.Picture!.Choice).Should().Equal(PictureChoice.MainPicture, PictureChoice.Link);
        second.Data.Games[1].Picture!.Link!.Url.Should().Be(LinkAddress);
        second.Data.Changes.Select(change => change.Text).Should().Contain("Restored from file");
        second.Service.Backup.ChangesSinceDownload.Should().Be(0);
        var placedFirst = cards.Where(card => card.GameId == second.GameIds[0] && !card.IsExpansion).ToList();
        var placedSecond = cards.Where(card => card.GameId == second.GameIds[1] && !card.IsExpansion).ToList();
        placedFirst.Should().NotBeEmpty().And.OnlyContain(card => card.Location == CabinetA);
        placedSecond.Should().NotBeEmpty().And.OnlyContain(card => card.Location == ShelfB);
        record.Files!.Select(file => file.Name).Should().Contain(Path.GetFileName(placedSecond[0].CoverUrl));
    }

    [Fact]
    public async Task The_check_of_a_script_answers_with_a_token_and_a_script_without_the_token_header_is_refused()
    {
        await using var first = await RestoreHost.StartAsync(seed: Seeded());
        var download = await first.Session.PostFormAsync(DownloadPath, []);
        await using var second = await RestoreHost.StartAsync(seed: null);
        var bytes = Encoding.UTF8.GetBytes(download.Body);

        var checkedFile = await second.Session.PostFileAsync(CheckPath, FileField, bytes, asScript: true);
        var forged = await second.Session.PostFileAsync(CheckPath, FileField, bytes, asScript: true, withToken: false);

        checkedFile.Status.Should().Be(HttpStatusCode.OK);
        checkedFile.JsonText("outcome").Should().Be("checked");
        checkedFile.JsonText("token").Should().MatchRegex("^[0-9a-f]{32}$");
        forged.Status.Should().Be(HttpStatusCode.BadRequest);
        second.Data.Locations.Should().BeEmpty();
    }

    [Fact]
    public async Task A_restore_keeps_the_data_that_was_there_as_an_earlier_version_that_holds_it()
    {
        await using var first = await RestoreHost.StartAsync(seed: Seeded());
        var download = await first.Session.PostFormAsync(DownloadPath, []);
        var existing = OwnerData.Empty with { Revision = 7, Locations = [new OwnerLocation("0f0f0f0f", "Crate C")] };
        await using var second = await RestoreHost.StartAsync(seed: _ => existing);

        await RestoreAsync(second, download.Body);
        var versions = new OwnerDataStore(second.StoragePath, NullLogger<OwnerDataStore>.Instance);

        versions.EarlierVersions().Should().ContainSingle();
        Encoding.UTF8.GetString(versions.ReadEarlierVersion(0)!).Should().Contain("Crate C");
        second.Data.Locations.Select(location => location.Name).Should().Equal(CabinetA, ShelfB);
        second.Data.Revision.Should().BeGreaterThan(7);
    }

    [Fact]
    public async Task A_restore_over_an_unreadable_file_keeps_the_file_byte_for_byte_as_an_earlier_version_and_makes_the_data_readable()
    {
        await using var first = await RestoreHost.StartAsync(seed: Seeded());
        var download = await first.Session.PostFormAsync(DownloadPath, []);
        var damaged = "this is not an owner-data file, but it was the file on this server"u8.ToArray();
        await using var second = await RestoreHost.StartAsync(seed: null, damaged: damaged);
        second.Service.State.Should().BeOfType<OwnerDataState.Unreadable>();

        await RestoreAsync(second, download.Body);
        var versions = new OwnerDataStore(second.StoragePath, NullLogger<OwnerDataStore>.Instance);

        second.Service.State.Should().BeOfType<OwnerDataState.Ready>();
        second.Data.Locations.Should().HaveCount(2);
        versions.EarlierVersions().Should().ContainSingle();
        versions.ReadEarlierVersion(0).Should().Equal(damaged);
    }

    [Fact]
    public async Task Keep_current_data_throws_the_check_away_and_leaves_the_data_and_its_earlier_versions_as_they_are()
    {
        await using var first = await RestoreHost.StartAsync(seed: Seeded());
        var download = await first.Session.PostFormAsync(DownloadPath, []);
        await using var second = await RestoreHost.StartAsync(seed: null);
        var check = await second.Session.PostFileAsync(CheckPath, FileField, Encoding.UTF8.GetBytes(download.Body));
        var token = OwnerSession.FieldValue((await second.Session.GetAsync(check.Location!)).Body, "token");

        var kept = await second.Session.PostFormAsync(SettingsPath + "?handler=Discard", [new("token", token)]);
        var late = await second.Session.PostFormAsync(RestorePath, [new("token", token)]);

        kept.Status.Should().Be(HttpStatusCode.Found);
        late.Body.Should().Contain("That check has run out. Choose the file again.");
        second.Data.Locations.Should().BeEmpty();
        second.StartedEmpty().Should().BeTrue();
    }

    [Fact]
    public async Task A_restore_without_the_antiforgery_token_is_refused_and_changes_nothing()
    {
        await using var first = await RestoreHost.StartAsync(seed: Seeded());
        var download = await first.Session.PostFormAsync(DownloadPath, []);
        await using var second = await RestoreHost.StartAsync(seed: null);
        var check = await second.Session.PostFileAsync(CheckPath, FileField, Encoding.UTF8.GetBytes(download.Body));
        var token = OwnerSession.FieldValue((await second.Session.GetAsync(check.Location!)).Body, "token");

        var forged = await second.Session.PostFormAsync(RestorePath, [new("token", token)], withToken: false);

        forged.Status.Should().Be(HttpStatusCode.BadRequest);
        second.Data.Locations.Should().BeEmpty();
    }

    private static async Task RestoreAsync(RestoreHost host, string downloadedFile)
    {
        var check = await host.Session.PostFileAsync(CheckPath, FileField, Encoding.UTF8.GetBytes(downloadedFile));
        var preview = await host.Session.GetAsync(check.Location!);
        var restored = await host.Session.PostFormAsync(RestorePath, [new("token", OwnerSession.FieldValue(preview.Body, "token"))]);

        restored.Status.Should().Be(HttpStatusCode.OK);
    }

    private static Func<IReadOnlyList<int>, OwnerData> Seeded() => gameIds =>
        OwnerData.Empty with
        {
            Revision = 5,
            SavedAtUtc = SyncHarness.StartTime.AddDays(-3),
            Locations = [new OwnerLocation(CabinetAId, CabinetA), new OwnerLocation(ShelfBId, ShelfB)],
            Games =
            [
                new OwnerGame(gameIds[0], CabinetAId, null, new PictureSetting(PictureChoice.MainPicture, true, null, null, null)),
                new OwnerGame(gameIds[1], ShelfBId, null, new PictureSetting(PictureChoice.Link, true, null, null, new PictureLink(LinkAddress, null, 0, null))),
            ],
            ChangesSinceDownload = 4,
            Changes = [new ChangeEntry(SyncHarness.StartTime.AddDays(-3), "Added location \"Cabinet A\"")],
        };

    private sealed record Card(int GameId, bool IsExpansion, string? Location, string CoverUrl);

    private sealed class RestoreHost : IAsyncDisposable
    {
        private readonly TemporaryDirectory _storage;
        private readonly CabinetWebApplicationFactory _factory;

        private RestoreHost(TemporaryDirectory storage, CabinetWebApplicationFactory factory, FakeTimeProvider clock, IReadOnlyList<int> gameIds)
        {
            _storage = storage;
            _factory = factory;
            Clock = clock;
            GameIds = gameIds;
            Session = new OwnerSession(factory);
            Client = factory.CreatePublicClient();
        }

        public FakeTimeProvider Clock { get; }

        public IReadOnlyList<int> GameIds { get; }

        public OwnerSession Session { get; }

        public HttpClient Client { get; }

        public string StoragePath => _storage.FullPath;

        public OwnerDataService Service => _factory.ServingServices.GetRequiredService<OwnerDataService>();

        public OwnerData Data => ((OwnerDataState.Ready)Service.State).Data;

        public static Task<RestoreHost> StartAsync(Func<IReadOnlyList<int>, OwnerData>? seed, bool background = false, byte[]? damaged = null)
        {
            var storage = new TemporaryDirectory();
            var items = SyntheticBggCollection.Create(5);
            var gameIds = items.Where(item => item.Owned && !item.IsExpansion).Select(item => item.ObjectId).Take(2).ToList();
            var images = new ScriptedImageHandler();
            images.Serve(LinkAddress, LinkedCover);

            if (seed is not null)
            {
                OwnerDataSeed.Write(storage.FullPath, seed(gameIds));
            }

            if (damaged is not null)
            {
                File.WriteAllBytes(Path.Combine(storage.FullPath, OwnerDataStore.FileName), damaged);
            }

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

            var clock = SyncHarness.NewClock();
            var factory = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(items), clock, settings, images);

            return Task.FromResult(new RestoreHost(storage, factory, clock, gameIds));
        }

        public bool StartedEmpty() => !File.Exists(Path.Combine(StoragePath, OwnerDataStore.FileName));

        public async Task<IReadOnlyList<Card>> CardsAsync()
        {
            using var response = await Client.GetAsync(CardsPath, TestContext.Current.CancellationToken);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

            return
            [
                .. document.RootElement.GetProperty("cards").EnumerateArray().Select(card => new Card(
                    card.GetProperty("gameId").GetInt32(),
                    card.TryGetProperty("isExpansion", out var expansion) && expansion.ValueKind == JsonValueKind.True,
                    card.TryGetProperty("location", out var location) && location.ValueKind == JsonValueKind.String ? location.GetString() : null,
                    card.TryGetProperty("cover", out var cover) && cover.ValueKind == JsonValueKind.Object && cover.TryGetProperty("url", out var address)
                        ? address.GetString()!
                        : string.Empty)),
            ];
        }

        public async Task<ImageRecord> WaitForLinkRecordAsync(int gameNumber)
        {
            var gameId = GameIds[gameNumber - 1];

            await SyncHarness.WaitUntil(() => Task.FromResult(
                Service.State is OwnerDataState.Ready ready
                && ready.Data.Games.FirstOrDefault(game => game.GameId == gameId)?.Picture?.Link?.Record is not null));

            return Data.Games.Single(game => game.GameId == gameId).Picture!.Link!.Record!;
        }

        public async ValueTask DisposeAsync()
        {
            Session.Dispose();
            Client.Dispose();
            await _factory.DisposeAsync();
            _storage.Dispose();
        }
    }
}

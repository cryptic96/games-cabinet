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

    [Fact]
    public async Task Every_file_the_check_cannot_fully_accept_changes_nothing_and_the_page_says_why()
    {
        await using var host = await RestoreHost.StartAsync(seed: _ => Existing());
        var path = Path.Combine(host.StoragePath, OwnerDataStore.FileName);
        var before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        var revision = host.Service.Revision;
        var cases = new (string Name, byte[]? File, string Message)[]
        {
            ("no file", null, "Choose a file first."),
            ("not an owner-data file", "just a note"u8.ToArray(), "That isn't an owner-data file. Choose a file you downloaded here."),
            ("truncated", Truncated(), "That file is damaged or incomplete, so nothing was changed."),
            ("newer", Encoding.UTF8.GetBytes($"{{\"kind\":\"{OwnerData.KindMarker}\",\"schemaVersion\":2}}"), "That file was made by a newer version of the site (file format 2; this site reads up to 1). Update the site first, or use an older download."),
            ("too large", Oversized(), "That file is too large (2.1 MB; the limit is 2 MB). Owner-data files are small, so this is probably the wrong file."),
            ("over the limits", TooManyLocations(), "That file holds more than this site accepts, so nothing was changed."),
        };

        foreach (var (name, file, message) in cases)
        {
            var reply = await host.Session.PostFileAsync(CheckPath, FileField, file);
            var page = WebUtility.HtmlDecode(reply.Body);

            reply.Status.Should().Be(HttpStatusCode.UnprocessableEntity, name);
            page.Should().Contain(message, name);
            page.Should().NotContain(PreviewHeading, name);
            (await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken)).Should().Equal(before, name);
            host.Service.Revision.Should().Be(revision, name);
        }

        new OwnerDataStore(host.StoragePath, NullLogger<OwnerDataStore>.Instance).EarlierVersions().Should().BeEmpty();
    }

    [Fact]
    public async Task A_script_is_told_a_refusal_as_json_with_the_same_words_and_the_data_stays()
    {
        await using var host = await RestoreHost.StartAsync(seed: _ => Existing());

        var refused = await host.Session.PostFileAsync(CheckPath, FileField, "just a note"u8.ToArray(), asScript: true);

        refused.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        refused.JsonText("outcome").Should().Be("refused");
        refused.JsonText("message").Should().Be("That isn't an owner-data file. Choose a file you downloaded here.");
        host.Data.Locations.Should().ContainSingle();
    }

    [Fact]
    public async Task A_link_on_another_host_in_an_uploaded_file_is_refused_and_never_fetched()
    {
        await using var host = await RestoreHost.StartAsync(seed: _ => Existing(), background: true);
        var foreign = OwnerData.Empty with
        {
            Locations = [new OwnerLocation(CabinetAId, CabinetA)],
            Games = [new OwnerGame(host.GameIds[0], CabinetAId, null, new PictureSetting(PictureChoice.Link, true, null, null, new PictureLink("https://evil.example.com/steal.png", null, 0, null)))],
        };

        var reply = await host.Session.PostFileAsync(CheckPath, FileField, OwnerDataExport.Write(foreign, SyncHarness.StartTime));

        reply.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        host.Data.Locations.Select(location => location.Name).Should().Equal("Crate C");
        host.Images.Requests.Should().NotContain(request => request.Uri.Host == "evil.example.com");
    }

    [Fact]
    public async Task A_fifty_megabyte_upload_is_answered_413_and_changes_nothing()
    {
        await using var host = await RestoreHost.StartAsync(seed: _ => Existing());
        var path = Path.Combine(host.StoragePath, OwnerDataStore.FileName);
        var before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

        var reply = await host.Session.PostFileAsync(CheckPath, FileField, new byte[50 * 1024 * 1024], expectContinue: true);

        reply.Status.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken)).Should().Equal(before);
    }

    [Fact]
    public async Task A_check_without_the_antiforgery_token_is_refused_with_400_and_stages_nothing()
    {
        await using var host = await RestoreHost.StartAsync(seed: _ => Existing());

        var forged = await host.Session.PostFileAsync(CheckPath, FileField, Encoding.UTF8.GetBytes("{}"), withToken: false);

        forged.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Nothing_of_the_restore_exists_on_the_public_port()
    {
        await using var host = await RestoreHost.StartAsync(seed: _ => Existing());

        using var check = await host.Client.PostAsync(CheckPath, new MultipartFormDataContent(), TestContext.Current.CancellationToken);
        using var restore = await host.Client.PostAsync(RestorePath, new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);

        check.StatusCode.Should().Be(HttpStatusCode.NotFound);
        restore.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static OwnerData Existing() => OwnerData.Empty with
    {
        Revision = 2,
        Locations = [new OwnerLocation("0f0f0f0f", "Crate C")],
    };

    private static byte[] Truncated()
    {
        var whole = OwnerDataExport.Write(Existing(), SyncHarness.StartTime);

        return whole[..(whole.Length / 2)];
    }

    private static byte[] Oversized()
    {
        var file = new byte[OwnerDataExport.MaxRestoreBytes + 1];
        Array.Fill(file, (byte)' ');
        OwnerDataExport.Write(Existing(), SyncHarness.StartTime).CopyTo(file, 0);

        return file;
    }

    private static byte[] TooManyLocations() => OwnerDataExport.Write(
        OwnerData.Empty with
        {
            Locations = [.. Enumerable.Range(0, OwnerData.MaxLocations + 1).Select(index => new OwnerLocation($"{index:x8}", $"Place {index}"))],
        },
        SyncHarness.StartTime);

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

        private RestoreHost(TemporaryDirectory storage, CabinetWebApplicationFactory factory, FakeTimeProvider clock, IReadOnlyList<int> gameIds, ScriptedImageHandler images)
        {
            _storage = storage;
            _factory = factory;
            Images = images;
            Clock = clock;
            GameIds = gameIds;
            Session = new OwnerSession(factory);
            Client = factory.CreatePublicClient();
        }

        public ScriptedImageHandler Images { get; }

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

            return Task.FromResult(new RestoreHost(storage, factory, clock, gameIds, images));
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

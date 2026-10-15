using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Storage;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves the owner's backup: the download is a readable file that holds what the owner entered and nothing that is fetched
/// again, the reminder on the owner pages counts the changes since the last download and goes away with it, a download changes
/// nothing else in the owner data, and none of it exists on the public port.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerBackupTests
{
    private const string SettingsPath = "/owner/settings";
    private const string LocationsPath = "/owner/locations";
    private const string PicturesPath = "/owner/pictures";
    private const string DownloadPath = SettingsPath + "?handler=Download";
    private const string BackupStatePath = SettingsPath + "?handler=BackupState";
    private const string NeverDownloadedPrefix = "You haven&#x27;t downloaded your owner data yet. ";
    private const string LinkAddress = "https://cf.geekdo-images.com/example/pic.png";

    [Fact]
    public async Task After_two_location_changes_on_a_host_never_downloaded_every_owner_page_counts_them_and_offers_the_download()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        await AddLocationAsync(session, "Cabinet A", 0);
        await AddLocationAsync(session, "Shelf B", 1);

        var pictures = await session.GetAsync(PicturesPath);
        var locations = await session.GetAsync(LocationsPath);
        var settings = await session.GetAsync(SettingsPath);

        foreach (var page in new[] { pictures, locations, settings })
        {
            page.Body.Should().Contain(NeverDownloadedPrefix + "2 changes are only on this server.").And.Contain("Download owner data");
            page.Body.Should().Contain("handler=Download");
        }

        pictures.Body.Should().Contain("2 changes not downloaded");
    }

    [Fact]
    public async Task After_a_download_two_more_changes_are_counted_since_it_and_one_reads_in_the_singular()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        await AddLocationAsync(session, "Cabinet A", 0);
        await session.PostFormAsync(DownloadPath, []);
        await AddLocationAsync(session, "Shelf B", 1);
        var one = await session.GetAsync(PicturesPath);
        await AddLocationAsync(session, "Crate C", 2);

        var two = await session.GetAsync(LocationsPath);

        one.Body.Should().Contain("1 change since your last download").And.Contain("1 change not downloaded").And.Contain(">1 not downloaded<");
        two.Body.Should().Contain("2 changes since your last download").And.Contain("2 changes not downloaded").And.Contain(">2 not downloaded<");
    }

    [Fact]
    public async Task A_host_that_never_saw_a_change_has_no_banner_and_says_there_is_nothing_to_back_up_and_one_change_reads_in_the_singular()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);

        var empty = await session.GetAsync(SettingsPath);
        var emptyPictures = await session.GetAsync(PicturesPath);
        await AddLocationAsync(session, "Cabinet A", 0);
        var one = await session.GetAsync(PicturesPath);

        empty.Body.Should().Contain("Nothing to back up yet.");
        empty.Body.Should().NotContain("handler=Download").And.NotContain("class=\"owner-banner\"");
        emptyPictures.Body.Should().NotContain("class=\"owner-banner\"");
        one.Body.Should().Contain(NeverDownloadedPrefix + "1 change is only on this server.").And.Contain("1 change not downloaded");
    }

    [Fact]
    public async Task Owner_data_that_was_never_downloaded_says_so_with_the_count_in_the_singular_and_the_plural()
    {
        using var storage = new TemporaryDirectory();
        OwnerDataSeed.Write(storage.FullPath, OwnerDataSeed.With(["Cabinet A"], new Dictionary<int, string>()) with { ChangesSinceDownload = 3 });
        await using var factory = NewFactory(storage);
        using var session = new OwnerSession(factory);

        var plural = await session.GetAsync(LocationsPath);
        await factory.ServingServices.GetRequiredService<OwnerDataService>().MutateSystemAsync(
            data => data with { ChangesSinceDownload = 1 },
            TestContext.Current.CancellationToken);
        var singular = await session.GetAsync(LocationsPath);

        plural.Body.Should().Contain("You haven&#x27;t downloaded your owner data yet. 3 changes are only on this server.");
        singular.Body.Should().Contain("You haven&#x27;t downloaded your owner data yet. 1 change is only on this server.");
    }

    [Fact]
    public async Task The_download_is_an_indented_json_attachment_named_for_the_day_and_holds_what_the_owner_entered()
    {
        using var storage = new TemporaryDirectory();
        var clock = SyncHarness.NewClock();
        await using var factory = NewFactory(storage, clock);
        using var session = new OwnerSession(factory);
        var first = (await AddLocationAsync(session, "Cabinet A", 0)).JsonText("id");
        await AddLocationAsync(session, "Shelf B", 1);
        clock.Advance(TimeSpan.FromMinutes(5));

        var download = await session.PostFormAsync(DownloadPath, []);
        var data = Parse(download.Body, storage);

        download.Status.Should().Be(HttpStatusCode.OK);
        download.Header("Content-Type").Should().StartWith("application/json");
        download.Header("Content-Disposition").Should().StartWith("attachment").And.Contain("filename=cabinet-owner-data-2030-01-15.json");
        download.Header("Cache-Control").Should().Be("no-store");
        download.Body.Should().Contain("\n  \"kind\": \"games-cabinet-owner-data\"");
        data.Kind.Should().Be("games-cabinet-owner-data");
        data.SchemaVersion.Should().Be(2);
        data.Look.Should().Be(CabinetLook.Default);
        data.Locations.Select(location => location.Name).Should().Equal("Cabinet A", "Shelf B");
        data.Locations[0].Id.Should().Be(first);
        data.Changes.Select(change => change.Text).Should().HaveCount(2).And.OnlyContain(text => text.Contains("location"));
        data.LastDownloadAtUtc.Should().Be(clock.GetUtcNow());
        data.ChangesSinceDownload.Should().Be(0);
    }

    [Fact]
    public async Task The_download_keeps_each_games_decisions_and_link_address_and_leaves_out_what_was_fetched_from_the_link()
    {
        using var storage = new TemporaryDirectory();
        var seeded = Seeded();
        OwnerDataSeed.Write(storage.FullPath, seeded);
        await using var factory = NewFactory(storage);
        using var session = new OwnerSession(factory);

        var download = await session.PostFormAsync(DownloadPath, []);
        var data = Parse(download.Body, storage);

        var game = data.Games.Should().ContainSingle().Subject;
        game.GameId.Should().Be(101);
        game.LocationId.Should().Be("seed0000");
        game.Picture!.Choice.Should().Be(PictureChoice.Link);
        game.Picture.Checked.Should().BeTrue();
        game.Picture.SeenOwnedUrl.Should().Be("https://cf.geekdo-images.com/example/owned.png");
        game.Picture.SeenMainUrl.Should().Be("https://cf.geekdo-images.com/example/main.png");
        game.Picture.Link!.Url.Should().Be(LinkAddress);
        game.Picture.Link.Record.Should().BeNull();
        data.Changes.Should().Equal(seeded.Changes);
        ((OwnerDataState.Ready)factory.ServingServices.GetRequiredService<OwnerDataService>().State).Data.Games[0].Picture!.Link!.Record.Should().NotBeNull();
    }

    [Fact]
    public async Task After_a_download_no_owner_page_has_a_banner_and_settings_says_when_it_was_made_and_the_data_is_otherwise_untouched()
    {
        var clock = SyncHarness.NewClock();
        await using var factory = NewFactory(clock: clock);
        using var session = new OwnerSession(factory);
        await AddLocationAsync(session, "Cabinet A", 0);
        await AddLocationAsync(session, "Shelf B", 1);
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();
        var before = ((OwnerDataState.Ready)service.State).Data;
        clock.Advance(TimeSpan.FromHours(2));

        await session.PostFormAsync(DownloadPath, []);
        var after = ((OwnerDataState.Ready)service.State).Data;
        var pictures = await session.GetAsync(PicturesPath);
        var settings = await session.GetAsync(SettingsPath);

        after.Revision.Should().Be(before.Revision);
        after.Changes.Should().Equal(before.Changes);
        after.Locations.Should().Equal(before.Locations);
        after.ChangesSinceDownload.Should().Be(0);
        after.LastDownloadAtUtc.Should().Be(clock.GetUtcNow());
        BannerIsHidden(pictures.Body).Should().BeTrue();
        BannerIsHidden(settings.Body).Should().BeTrue();
        settings.Body.Should().Contain("Downloaded").And.Contain("datetime=\"2030-01-15T14:00:00Z\"").And.Contain("15 January 2030 at 14:00 UTC");
        settings.Body.Should().Contain("The site keeps the last 10 versions of your owner data on the server.");
    }

    [Fact]
    public async Task A_download_ends_the_chance_to_undo_the_latest_change_and_a_later_change_counts_from_zero_again()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        await AddLocationAsync(session, "Cabinet A", 0);
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();
        var revision = service.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
        service.UndoableRevision.Should().NotBeNull();

        await session.PostFormAsync(DownloadPath, []);
        var undo = await session.PostJsonAsync($"{LocationsPath}?handler=Undo", [Field("revision", revision)]);
        await AddLocationAsync(session, "Shelf B", service.Revision);
        var page = await session.GetAsync(PicturesPath);

        service.UndoableRevision.Should().NotBeNull();
        undo.Status.Should().Be(HttpStatusCode.Conflict);
        undo.JsonText("status").Should().Be("cant-undo");
        page.Body.Should().Contain("1 change since your last download");
    }

    [Fact]
    public async Task Undo_takes_the_entry_and_its_place_in_the_count_away()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        await AddLocationAsync(session, "Cabinet A", 0);
        await AddLocationAsync(session, "Shelf B", 1);
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();

        var undo = await session.PostJsonAsync($"{LocationsPath}?handler=Undo", [Field("revision", service.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture))]);
        var page = await session.GetAsync(SettingsPath);

        undo.JsonText("outcome").Should().Be("saved");
        page.Body.Should().Contain("1 change is only on this server.");
        ((OwnerDataState.Ready)service.State).Data.Changes.Should().ContainSingle();
    }

    [Fact]
    public async Task A_download_without_the_antiforgery_token_is_refused_and_records_nothing()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        await AddLocationAsync(session, "Cabinet A", 0);

        var refused = await session.PostFormAsync(DownloadPath, [], withToken: false);

        refused.Status.Should().Be(HttpStatusCode.BadRequest);
        factory.ServingServices.GetRequiredService<OwnerDataService>().Backup.ChangesSinceDownload.Should().Be(1);
    }

    [Fact]
    public async Task A_download_that_cannot_be_recorded_sends_the_owner_back_with_the_words_and_the_reminder_stays()
    {
        using var storage = new TemporaryDirectory();
        var store = new SometimesFailingStore(new OwnerDataStore(storage.FullPath, NullLogger<OwnerDataStore>.Instance));
        await using var factory = new CabinetWebApplicationFactory(
            new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath },
            services => services.AddSingleton<IOwnerDataStore>(store));
        using var session = new OwnerSession(factory);
        await AddLocationAsync(session, "Cabinet A", 0);
        store.FailSaves = true;

        var failed = await session.PostFormAsync(DownloadPath, []);
        var page = await session.GetAsync(failed.Location!);

        failed.Status.Should().Be(HttpStatusCode.Found);
        failed.Location.Should().StartWith($"{SettingsPath}?done=download-failed");
        page.Body.Should().Contain("Couldn&#x27;t prepare the download. Try again.").And.Contain("1 change is only on this server.");
        factory.ServingServices.GetRequiredService<OwnerDataService>().Backup.ChangesSinceDownload.Should().Be(1);
    }

    [Fact]
    public async Task The_backup_state_answers_with_the_count_and_the_last_download_and_is_not_kept()
    {
        var clock = SyncHarness.NewClock();
        await using var factory = NewFactory(clock: clock);
        using var session = new OwnerSession(factory);
        await AddLocationAsync(session, "Cabinet A", 0);

        var before = await session.GetAsync(BackupStatePath);
        await session.PostFormAsync(DownloadPath, []);
        await AddLocationAsync(session, "Shelf B", 1);
        var after = await session.GetAsync(BackupStatePath);

        before.Status.Should().Be(HttpStatusCode.OK);
        before.Header("Cache-Control").Should().Be("no-store");
        before.Json.GetProperty("changes").GetInt32().Should().Be(1);
        before.Json.GetProperty("lastDownloadAtUtc").ValueKind.Should().Be(JsonValueKind.Null);
        after.Json.GetProperty("changes").GetInt32().Should().Be(1);
        after.JsonText("lastDownloadAtUtc").Should().Be("2030-01-15T12:00:00Z");
    }

    [Fact]
    public async Task Nothing_of_the_backup_exists_on_the_public_port()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        await AddLocationAsync(session, "Cabinet A", 0);
        using var publicClient = factory.CreatePublicClient();

        var page = await publicClient.GetStringAsync("/", TestContext.Current.CancellationToken);
        var download = await publicClient.PostAsync(DownloadPath, new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);
        var state = await publicClient.GetAsync(BackupStatePath, TestContext.Current.CancellationToken);

        page.Should().NotContain("since your last download").And.NotContain("Download owner data").And.NotContain("owner-banner");
        download.StatusCode.Should().Be(HttpStatusCode.NotFound);
        state.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_downloaded_file_of_the_first_format_shows_the_older_format_line_and_restores_as_the_default_look()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        using var publicClient = factory.CreatePublicClient();
        await session.GetAsync(SettingsPath);
        await session.PostJsonAsync(
            SettingsPath + "?handler=Look",
            [Field("design", "legs"), Field("wood", "oak"), Field("backs", "painted"), Field("lights", "on"), Field("depth", "deep"), Field("revision", "0")]);
        var file = Encoding.UTF8.GetBytes(
            "{\"kind\":\"games-cabinet-owner-data\",\"schemaVersion\":1,\"revision\":3,\"finish\":\"classic\","
            + "\"locations\":[{\"id\":\"0a1b2c3d\",\"name\":\"Cabinet A\"}],\"games\":[],\"changesSinceDownload\":0,\"changes\":[]}");

        var check = await session.PostFileAsync(SettingsPath + "?handler=CheckFile", "ownerDataFile", file);
        var preview = await session.GetAsync(check.Location!);
        var restored = await session.PostFormAsync(SettingsPath + "?handler=Restore", [Field("token", OwnerSession.FieldValue(preview.Body, "token"))]);
        var page = await publicClient.GetStringAsync("/", TestContext.Current.CancellationToken);

        preview.Body.Should().Contain("This file uses an older format (1). It is upgraded when restored.");
        restored.Status.Should().Be(HttpStatusCode.OK);
        page.Should().Contain("data-design=\"classic\" data-wood=\"brown\" data-backs=\"wood\" data-lights=\"off\" data-depth=\"shaded\">");
        ((OwnerDataState.Ready)factory.ServingServices.GetRequiredService<OwnerDataService>().State).Data.Locations.Should().ContainSingle();
    }

    private static CabinetWebApplicationFactory NewFactory(TemporaryDirectory? storage = null, TimeProvider? clock = null) =>
        SyncHarness.CreateFactory(
            new ScriptedBggHandler(),
            clock ?? SyncHarness.NewClock(),
            storage is null ? null : new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);

    private static Task<OwnerReply> AddLocationAsync(OwnerSession session, string name, long revision) =>
        session.PostJsonAsync(
            $"{LocationsPath}?handler=AddLocation",
            [Field("name", name), Field("revision", revision.ToString(System.Globalization.CultureInfo.InvariantCulture))]);

    private static bool BannerIsHidden(string html)
    {
        var tag = Regex.Match(html, "<div class=\"owner-banner\"[^>]*>");

        return !tag.Success || tag.Value.Contains(" hidden", StringComparison.Ordinal);
    }

    private static OwnerData Parse(string body, TemporaryDirectory scratch)
    {
        var directory = Path.Combine(scratch.FullPath, $"parsed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, OwnerDataStore.FileName), body);

        return new OwnerDataStore(directory, NullLogger<OwnerDataStore>.Instance).Load()
            .Should().BeOfType<OwnerDataState.Ready>().Subject.Data;
    }

    private static OwnerData Seeded()
    {
        var link = new PictureLink(
            LinkAddress,
            new ImageRecord(LinkAddress, ImageStatus.Ok, SyncHarness.StartTime, [new ArtFile(240, 300, "pic-abc-240.webp")]),
            0,
            null);
        var picture = new PictureSetting(
            PictureChoice.Link,
            Checked: true,
            "https://cf.geekdo-images.com/example/owned.png",
            "https://cf.geekdo-images.com/example/main.png",
            link);

        return OwnerDataSeed.With(["Cabinet A", "Shelf B"], new Dictionary<int, string>()) with
        {
            Games = [new OwnerGame(101, "seed0000", null, picture)],
            ChangesSinceDownload = 2,
            Changes =
            [
                new ChangeEntry(SyncHarness.StartTime.AddMinutes(-5), "Picture of Example Game: your link"),
                new ChangeEntry(SyncHarness.StartTime.AddMinutes(-9), "Added location \"Cabinet A\""),
            ],
        };
    }
}

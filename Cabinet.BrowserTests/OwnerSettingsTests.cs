using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Storage;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser the owner's backup: the reminder banner on every owner page in each of its states and sizes, the
/// download with its busy state and the banner that goes away without a reload, the list of recent changes with the line at the
/// last download, and every time written in the viewer's own clock. The browser runs in a fixed time zone and a fixed moment, so
/// the words for today, yesterday and an older day are the same on every machine.
/// </summary>
[Trait("Category", "Browser")]
public sealed class OwnerSettingsTests : CabinetPageTest
{
    private const string SettingsPath = "/owner/settings";
    private const string LocationsPath = "/owner/locations";
    private const string PicturesPath = "/owner/pictures";
    private const string DownloadName = "Download owner data";
    private const string ViewerZone = "Asia/Tokyo";
    private const string FullSentenceThree = "3 changes since your last download";
    private const string ShortSentenceThree = "3 changes not downloaded";

    private static readonly DateTimeOffset PageNow = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly TemporaryDirectory _storage = new();

    /// <summary>Makes the browser read the clock in a zone far from the server's, so a time written in UTC would show.</summary>
    public override BrowserNewContextOptions ContextOptions()
    {
        var options = base.ContextOptions();
        options.TimezoneId = ViewerZone;

        return options;
    }

    [Theory]
    [InlineData(PicturesPath)]
    [InlineData(LocationsPath)]
    public async Task On_a_wide_screen_the_banner_is_the_full_sentence_with_the_full_button_on_every_owner_page(string path)
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithDataAsync(Data(3, PageNow.AddDays(-1)));
        await OpenAsync(path);

        var banner = Page.Locator(".owner-banner");
        await Expect(banner.Locator(".owner-banner-text .banner-full")).ToBeVisibleAsync();
        await Expect(banner.Locator(".owner-banner-text .banner-full")).ToHaveTextAsync(FullSentenceThree);
        await Expect(banner.Locator(".owner-banner-text .banner-short")).ToBeHiddenAsync();
        await Expect(banner.Locator(".owner-banner-text .banner-tiny")).ToBeHiddenAsync();
        await Expect(banner.Locator("button .banner-full")).ToHaveTextAsync(DownloadName);
        await Expect(banner.Locator("button .banner-short")).ToBeHiddenAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = DownloadName })).ToBeVisibleAsync();
        var beneathTheBar = await Page.EvaluateAsync<bool>(
            "() => document.querySelector('nav.owner-bar').nextElementSibling === document.querySelector('.owner-banner')");

        beneathTheBar.Should().BeTrue();
        await SaveScreenshotAsync($"owner-banner-wide-{path.Trim('/').Replace('/', '-')}");
        ExpectCleanPage();
    }

    [Theory]
    [InlineData(390, 844, "short", "23 changes not downloaded", "tiny")]
    [InlineData(320, 640, "tiny", "23 not downloaded", "short")]
    public async Task On_a_phone_the_banner_is_one_line_of_at_most_44_pixels_with_a_square_compact_button_and_nothing_wraps_or_overflows(
        int width, int height, string shown, string expectedText, string hidden)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartWithDataAsync(Data(23, PageNow.AddDays(-1)));
        await OpenAsync(LocationsPath);

        var banner = Page.Locator(".owner-banner");
        await Expect(banner.Locator($".owner-banner-text .banner-{shown}")).ToBeVisibleAsync();
        await Expect(banner.Locator($".owner-banner-text .banner-{shown}")).ToHaveTextAsync(expectedText);
        await Expect(banner.Locator($".owner-banner-text .banner-{hidden}")).ToBeHiddenAsync();
        await Expect(banner.Locator(".owner-banner-text .banner-full")).ToBeHiddenAsync();
        (await banner.GetByRole(AriaRole.Button).EvaluateAsync<string>("button => getComputedStyle(button).borderTopLeftRadius")).Should().Be("0px");
        await Expect(banner.Locator("button .banner-short")).ToHaveTextAsync("Download data");
        await Expect(banner.Locator("button .banner-short")).ToBeVisibleAsync();
        await Expect(banner.Locator("button .banner-full")).ToBeHiddenAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = DownloadName })).ToBeVisibleAsync();

        var box = (await banner.BoundingBoxAsync())!;
        var button = (await banner.GetByRole(AriaRole.Button).BoundingBoxAsync())!;
        var text = (await banner.Locator(".owner-banner-text").BoundingBoxAsync())!;
        var truncated = await banner.Locator(".owner-banner-text").EvaluateAsync<bool>("element => element.scrollWidth > element.clientWidth");
        var overflow = await Page.EvaluateAsync<double>("() => document.documentElement.scrollWidth - window.innerWidth");

        box.Height.Should().BeLessThanOrEqualTo(44);
        button.Height.Should().BeGreaterThanOrEqualTo(44).And.BeLessThanOrEqualTo(box.Height);
        text.Height.Should().BeLessThan(28, "the sentence stays on one line");
        (button.X + button.Width).Should().BeLessThanOrEqualTo(width, "the button is never cut off");
        overflow.Should().BeLessThanOrEqualTo(0);

        truncated.Should().BeFalse("two digits of changes still read in full beside the button, at the narrowest width too");

        box.X.Should().BeGreaterThanOrEqualTo(0);
        box.X.Should().BeLessThanOrEqualTo(0, "the strip runs to the screen edge on a phone");
        await SaveScreenshotAsync($"owner-banner-phone-{width}");
        ExpectCleanPage();
    }

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(390, 844)]
    public async Task On_the_settings_page_the_banner_in_the_backup_panel_always_shows_the_full_sentence(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartWithDataAsync(Data(3, PageNow.AddDays(-1)));
        await OpenAsync(SettingsPath);

        var banner = Page.Locator("#backup .owner-banner");
        await Expect(Page.Locator(".owner-banner")).ToHaveCountAsync(1);
        await Expect(banner.Locator(".owner-banner-text .banner-full")).ToHaveTextAsync(FullSentenceThree);
        await Expect(banner.Locator(".owner-banner-text .banner-short")).ToBeHiddenAsync();
        await Expect(banner.Locator("button .banner-full")).ToHaveTextAsync(DownloadName);
        await Expect(Page.Locator("#backup-quiet")).ToBeHiddenAsync();
        await Expect(Page.Locator("#backup .backup-footnote")).ToHaveTextAsync("The site keeps the last 10 versions of your owner data on the server.");
        var overflow = await Page.EvaluateAsync<double>("() => document.documentElement.scrollWidth - window.innerWidth");

        overflow.Should().BeLessThanOrEqualTo(0);
        await SaveScreenshotAsync($"owner-settings-banner-{width}");
        ExpectCleanPage();
    }

    [Fact]
    public async Task Data_that_was_never_downloaded_says_so_and_a_single_change_reads_in_the_singular()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithDataAsync(Data(1, null));
        await OpenAsync(LocationsPath);

        await Expect(Page.Locator(".owner-banner-text .banner-full"))
            .ToHaveTextAsync("You haven't downloaded your owner data yet. 1 change is only on this server.");
        await Expect(Page.Locator(".owner-banner-text .banner-short")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task With_nothing_entered_there_is_no_banner_and_the_backup_panel_says_there_is_nothing_to_back_up()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithDataAsync(null);

        await OpenAsync(PicturesPath);
        await Expect(Page.Locator(".owner-banner")).ToHaveCountAsync(0);
        await OpenAsync(SettingsPath);

        await Expect(Page.Locator("#backup-empty")).ToHaveTextAsync("Nothing to back up yet.");
        await Expect(Page.Locator(".owner-banner")).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = DownloadName })).ToHaveCountAsync(0);
        await Expect(Page.Locator("#change-log-empty")).ToHaveTextAsync("No changes yet. Edits you make show up here.");
        ExpectCleanPage();
    }

    [Fact]
    public async Task Pressing_the_button_shows_Preparing_on_a_disabled_button_and_then_the_banner_goes_away_without_a_reload()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithDataAsync(Data(3, PageNow.AddDays(-1)));
        await OpenAsync(LocationsPath);
        await Page.EvaluateAsync("() => { window.sameDocument = true; }");
        var button = Page.GetByRole(AriaRole.Button, new() { Name = DownloadName });

        var download = await Page.RunAndWaitForDownloadAsync(() => button.ClickAsync());

        download.SuggestedFilename.Should().Be("cabinet-owner-data-2030-01-15.json");
        var busy = Page.Locator(".owner-banner button[aria-disabled=\"true\"]");
        await Expect(busy).ToHaveTextAsync("Preparing...");
        await Expect(Page.Locator(".owner-banner")).ToBeHiddenAsync();
        (await Page.EvaluateAsync<bool>("() => window.sameDocument === true")).Should().BeTrue("the page was not reloaded");
        var file = JsonDocument.Parse(await File.ReadAllTextAsync((await download.PathAsync())!, TestContext.Current.CancellationToken));
        file.RootElement.GetProperty("kind").GetString().Should().Be("games-cabinet-owner-data");
        file.RootElement.GetProperty("changesSinceDownload").GetInt32().Should().Be(0);
        await Page.ReloadAsync();
        await Expect(Page.Locator(".owner-banner")).ToBeHiddenAsync();
        ExpectCleanPage();
    }

    [Fact]
    public async Task A_second_press_while_the_file_is_prepared_does_nothing_and_the_button_has_its_name_back_afterwards()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithDataAsync(Data(3, PageNow.AddDays(-1)));
        await Page.RouteAsync("**/owner/settings?handler=BackupState", route => route.AbortAsync());
        await OpenAsync(PicturesPath);
        var button = Page.GetByRole(AriaRole.Button, new() { Name = DownloadName });

        await Page.RunAndWaitForDownloadAsync(() => button.ClickAsync());
        var downloads = 0;
        Page.Download += (_, _) => downloads++;
        await Page.Locator(".owner-banner button[aria-disabled=\"true\"]").ClickAsync(new LocatorClickOptions { Force = true });
        await Task.Delay(2000, TestContext.Current.CancellationToken);

        downloads.Should().Be(0);
        await Expect(Page.Locator(".owner-banner")).ToBeVisibleAsync();
        await Expect(Page.Locator(".owner-banner .owner-banner-text .banner-full")).ToHaveTextAsync(FullSentenceThree);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = DownloadName })).ToBeVisibleAsync();
        await Expect(Page.Locator(".owner-banner button[aria-disabled]")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task A_download_that_cannot_be_prepared_shows_the_words_and_the_banner_stays()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        var store = new SometimesFailingStore(new OwnerDataStore(_storage.FullPath, NullLogger<OwnerDataStore>.Instance));
        OwnerDataSeed.Write(_storage.FullPath, Seeded(3, PageNow.AddDays(-1)));
        await StartAsync(
            new CabinetWebApplicationFactory(
                new Dictionary<string, string?> { ["Storage:Directory"] = _storage.FullPath },
                services => services.AddSingleton<IOwnerDataStore>(store)));
        store.FailSaves = true;
        await OpenAsync(LocationsPath);

        await Page.GetByRole(AriaRole.Button, new() { Name = DownloadName }).ClickAsync();

        await Expect(Page.Locator(".backup-notice")).ToHaveTextAsync("Couldn't prepare the download. Try again.");
        await Expect(Page.Locator(".backup-notice")).ToHaveAttributeAsync("role", "alert");
        await Expect(Page.Locator("#backup .owner-banner .banner-full").First).ToHaveTextAsync(FullSentenceThree);
        await Expect(Page.Locator("#backup .owner-banner")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task On_the_settings_page_a_download_turns_the_banner_into_the_quiet_line_and_puts_the_divider_at_the_top_of_the_log()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithDataAsync(Data(2, PageNow.AddDays(-1), Entry(1, 11), Entry(2, 10), Entry(3, 9)));
        await OpenAsync(SettingsPath);
        await Expect(Page.Locator("#change-log > li")).ToHaveClassAsync(["change-entry", "change-entry", "change-divider", "change-entry"]);

        await Page.RunAndWaitForDownloadAsync(() => Page.GetByRole(AriaRole.Button, new() { Name = DownloadName }).ClickAsync());

        await Expect(Page.Locator("#backup .owner-banner")).ToBeHiddenAsync();
        await Expect(Page.Locator("#backup-line")).ToBeVisibleAsync();
        await Expect(Page.Locator("#backup-line")).ToHaveTextAsync("Downloaded today at 21:00");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = DownloadName })).ToBeVisibleAsync();
        await Expect(Page.Locator("#change-log > li")).ToHaveClassAsync(["change-divider", "change-entry", "change-entry", "change-entry"]);
        await Expect(Page.Locator("#owner-status")).ToHaveTextAsync("Downloaded today at 21:00");
        await Page.ReloadAsync();
        await Expect(Page.Locator("#backup-line")).ToHaveTextAsync("Downloaded today at 21:00");
        await SaveScreenshotAsync("owner-settings-downloaded");
        ExpectCleanPage();
    }

    [Fact]
    public async Task A_change_saved_with_script_brings_the_banner_up_and_Undo_takes_it_down_again()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithDataAsync(Data(0, PageNow.AddDays(-1)));
        await OpenAsync(LocationsPath);
        await Expect(Page.Locator(".owner-banner")).ToBeHiddenAsync();

        await Page.Locator("#your-locations > summary").ClickAsync();
        await Page.Locator("#new-location").FillAsync("Shelf B");
        await Page.Locator("#new-location").PressAsync("Enter");

        await Expect(Page.Locator(".owner-banner .owner-banner-text .banner-full")).ToHaveTextAsync("1 change since your last download");
        await Page.Locator("#new-location-status button").ClickAsync();
        await Expect(Page.Locator("#new-location-status")).ToContainTextAsync("Undone.");
        await Expect(Page.Locator(".owner-banner")).ToBeHiddenAsync();
        await Page.ReloadAsync();
        await Expect(Page.Locator(".owner-banner")).ToBeHiddenAsync();
        ExpectCleanPage();
    }

    [Fact]
    public async Task The_log_shows_the_last_20_changes_newest_first_while_the_banner_counts_all_23()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        var log = Enumerable.Range(0, 23).Select(index => new ChangeEntry(PageNow.AddMinutes(-10 - index), $"Change {23 - index}")).ToArray();
        await StartWithDataAsync(Data(23, PageNow.AddDays(-3), log));
        await OpenAsync(SettingsPath);

        var entries = Page.Locator(".change-log .change-entry");
        await Expect(entries).ToHaveCountAsync(20);
        await Expect(entries.First.Locator(".change-text")).ToHaveTextAsync("Change 23");
        await Expect(entries.Last.Locator(".change-text")).ToHaveTextAsync("Change 4");
        await Expect(Page.Locator("#backup .owner-banner .banner-full").First).ToHaveTextAsync("23 changes since your last download");
        await Expect(Page.Locator(".change-divider")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#recent-changes .owner-hint")).ToHaveTextAsync("The last 20 changes.");
        ExpectCleanPage();
    }

    [Fact]
    public async Task After_a_download_and_two_more_changes_the_divider_sits_after_the_first_two_entries()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithDataAsync(Data(2, PageNow.AddMinutes(-30), Entry(1, 5), Entry(2, 10), Entry(3, 60), Entry(4, 90)));
        await OpenAsync(SettingsPath);

        var items = Page.Locator("#change-log > li");
        await Expect(items).ToHaveClassAsync(["change-entry", "change-entry", "change-divider", "change-entry", "change-entry"]);
        await Expect(items.Nth(2)).ToHaveTextAsync("Downloaded today at 20:30");
        await Expect(items.Nth(0).Locator(".change-text")).ToHaveTextAsync("Change 1");
    }

    [Fact]
    public async Task One_entry_reads_as_a_single_ledger_line_and_the_divider_appears_only_after_a_download()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithDataAsync(Data(1, null, Entry(1, 5)));
        await OpenAsync(SettingsPath);

        await Expect(Page.Locator("#change-log > li")).ToHaveCountAsync(1);
        await Expect(Page.Locator(".change-divider")).ToHaveCountAsync(0);
    }

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(390, 844)]
    public async Task A_full_log_reads_as_a_ledger_with_the_time_beside_the_text_on_a_desktop_and_above_it_on_a_phone(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        var log = Enumerable.Range(0, 20)
            .Select(index => new ChangeEntry(PageNow.AddHours(-index * 7), index == 3
                ? "Renamed \"A cabinet with a very long and descriptive name that goes on and on\" to \"Another cabinet with an equally long and wordy name\""
                : $"Moved {index + 1} games to Shelf B"))
            .ToArray();
        await StartWithDataAsync(Data(4, PageNow.AddHours(-3 * 7 - 1), log));
        await OpenAsync(SettingsPath);

        var entry = Page.Locator(".change-entry").Nth(3);
        var time = (await entry.Locator("time").BoundingBoxAsync())!;
        var text = (await entry.Locator(".change-text").BoundingBoxAsync())!;
        var overflow = await Page.EvaluateAsync<double>("() => document.documentElement.scrollWidth - window.innerWidth");

        if (width >= 640)
        {
            time.Y.Should().BeLessThan(text.Y + 8);
            (time.X + time.Width).Should().BeLessThanOrEqualTo(text.X, "the time has its own column");
        }
        else
        {
            (time.Y + time.Height).Should().BeLessThanOrEqualTo(text.Y + 1, "the time sits above the text");
        }

        overflow.Should().BeLessThanOrEqualTo(0);
        await SaveScreenshotAsync($"owner-change-log-{width}");
        ExpectCleanPage();
    }

    [Fact]
    public async Task Every_time_is_written_in_the_viewer_zone_and_keeps_its_datetime()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithDataAsync(Data(
            3,
            PageNow.AddDays(-1),
            new ChangeEntry(PageNow.AddMinutes(14), "Change today"),
            new ChangeEntry(PageNow.AddDays(-1).AddMinutes(14), "Change yesterday"),
            new ChangeEntry(PageNow.AddDays(-6).AddMinutes(14), "Change older")));
        await OpenAsync(SettingsPath);

        var times = Page.Locator(".change-entry time");
        await Expect(times).ToHaveTextAsync(["Today at 21:14", "Yesterday at 21:14", "9 January at 21:14"]);
        await Expect(times.First).ToHaveAttributeAsync("datetime", "2030-01-15T12:14:00Z");
        await Expect(Page.Locator(".change-divider time")).ToHaveTextAsync("yesterday at 21:00");
        ExpectCleanPage();
    }

    [Fact]
    public async Task Without_script_the_download_still_works_and_every_time_reads_in_UTC()
    {
        OwnerDataSeed.Write(_storage.FullPath, Seeded(1, PageNow.AddDays(-1), new ChangeEntry(PageNow.AddMinutes(14), "Change today")));
        await StartAsync(SyncHarness.CreateFactory(
            new ScriptedBggHandler(),
            SyncHarness.NewClock(),
            new Dictionary<string, string?> { ["Storage:Directory"] = _storage.FullPath }));
        var plain = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            JavaScriptEnabled = false,
            TimezoneId = ViewerZone,
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
            AcceptDownloads = true,
        });
        var page = await plain.NewPageAsync();
        await page.GotoAsync($"http://127.0.0.1:{OwnerPort}{SettingsPath}");

        await Expect(page.Locator(".change-entry time")).ToHaveTextAsync("15 January 2030 at 12:14 UTC");
        await Expect(page.Locator(".change-entry time")).ToHaveAttributeAsync("datetime", "2030-01-15T12:14:00Z");
        var download = await page.RunAndWaitForDownloadAsync(() => page.GetByRole(AriaRole.Button, new() { Name = DownloadName }).ClickAsync());
        await page.ReloadAsync();

        download.SuggestedFilename.Should().Be("cabinet-owner-data-2030-01-15.json");
        await Expect(page.Locator("#backup .owner-banner")).ToBeHiddenAsync();
        await Expect(page.Locator("#backup-line")).ToContainTextAsync("Downloaded");
        await plain.CloseAsync();
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private static ChangeEntry Entry(int number, int minutesAgo) =>
        new(PageNow.AddMinutes(-minutesAgo), $"Change {number}");

    private static OwnerData Seeded(int changesSinceDownload, DateTimeOffset? lastDownload, params ChangeEntry[] log) =>
        OwnerDataSeed.With(["Cabinet A"], new Dictionary<int, string>()) with
        {
            ChangesSinceDownload = changesSinceDownload,
            LastDownloadAtUtc = lastDownload,
            Changes = log,
        };

    private static OwnerData Data(int changesSinceDownload, DateTimeOffset? lastDownload, params ChangeEntry[] log) =>
        Seeded(changesSinceDownload, lastDownload, log);

    private async Task StartWithDataAsync(OwnerData? data)
    {
        if (data is not null)
        {
            OwnerDataSeed.Write(_storage.FullPath, data);
        }

        await StartAsync(SyncHarness.CreateFactory(
            new ScriptedBggHandler(),
            SyncHarness.NewClock(),
            new Dictionary<string, string?> { ["Storage:Directory"] = _storage.FullPath }));
    }

    private async Task OpenAsync(string path)
    {
        await Page.Clock.SetFixedTimeAsync(PageNow.UtcDateTime);
        await GotoOwnerAsync(path);
    }

    private void ExpectCleanPage()
    {
        ConsoleErrors.Should().BeEmpty();
        RequestHosts.Should().OnlyContain(host => host == $"127.0.0.1:{OwnerPort}");
    }
}

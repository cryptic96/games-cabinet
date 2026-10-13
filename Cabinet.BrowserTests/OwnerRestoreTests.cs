using System.Text;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Storage;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser the restore panel of the settings page: a chosen file is checked without leaving the page and its
/// preview sets what the file holds next to what the server holds, a wrong file shows its own words and changes nothing, the
/// restore replaces the data and says so, Keep current data throws the check away, and at the width of a small phone the
/// comparison table fits without the page scrolling sideways.
/// </summary>
[Trait("Category", "Browser")]
public sealed class OwnerRestoreTests : CabinetPageTest
{
    private const string SettingsPath = "/owner/settings";
    private const string Summary = "2 locations, 1 placed game, 1 picture choice";

    private readonly TemporaryDirectory _storage = new();

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(390, 844)]
    [InlineData(320, 640)]
    public async Task A_chosen_file_is_checked_in_place_and_its_preview_sits_next_to_what_the_server_holds_without_overflow(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync();
        await GotoOwnerAsync(SettingsPath);

        await ChooseAsync(FileBytes());
        await Page.GetByRole(AriaRole.Button, new() { Name = "Check file" }).ClickAsync();

        var heading = Page.GetByRole(AriaRole.Heading, new() { Name = "This file contains" });
        await Expect(heading).ToBeVisibleAsync();
        await Expect(heading).ToBeFocusedAsync();
        (await heading.EvaluateAsync<string>("element => getComputedStyle(element).outlineStyle")).Should().Be("none", "a heading that only takes focus shows no ring");
        await Expect(Page.Locator("#restore-preview p").First).ToHaveTextAsync(Summary + ".");
        await Expect(Page.Locator(".restore-table th[scope=col]")).ToHaveTextAsync(["In the file", "On this server now"]);
        await Expect(Page.Locator(".restore-table th[scope=row]")).ToHaveCountAsync(4);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Restore file" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Keep current data" })).ToBeVisibleAsync();
        var overflow = await Page.EvaluateAsync<double>("() => document.documentElement.scrollWidth - window.innerWidth");
        var table = (await Page.Locator(".restore-table").BoundingBoxAsync())!;

        overflow.Should().BeLessThanOrEqualTo(0);
        (table.X + table.Width).Should().BeLessThanOrEqualTo(width);
        await SaveScreenshotAsync($"owner-restore-preview-{width}");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Restoring_replaces_the_data_and_says_what_came_back_and_that_the_previous_data_is_kept()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync();
        await GotoOwnerAsync(SettingsPath);
        await ChooseAsync(FileBytes());
        await Page.GetByRole(AriaRole.Button, new() { Name = "Check file" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "This file contains" })).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Restore file" }).ClickAsync();

        var done = Page.Locator("#restore-done-heading");
        await Expect(done).ToHaveTextAsync($"Restored. {Summary}. Your previous data is kept as an earlier version.");
        await Expect(done).ToBeFocusedAsync();
        await Expect(Page.Locator("#restore-preview")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".change-text").First).ToHaveTextAsync("Restored from file");
        await SaveScreenshotAsync("owner-restore-done");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_file_that_is_not_an_owner_data_file_shows_its_words_in_the_alert_line_and_the_page_stays_as_it_is()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await StartAsync();
        await GotoOwnerAsync(SettingsPath);

        await ChooseAsync(Encoding.UTF8.GetBytes("this is a plain note"));
        await Page.GetByRole(AriaRole.Button, new() { Name = "Check file" }).ClickAsync();

        var alert = Page.Locator("#restore-problem");
        await Expect(alert).ToHaveTextAsync("That isn't an owner-data file. Choose a file you downloaded here.");
        await Expect(alert).ToBeVisibleAsync();
        await Expect(Page.Locator("#restore-preview")).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Check file" })).ToBeEnabledAsync();
        await SaveScreenshotAsync("owner-restore-refused-390");
    }

    [Fact]
    public async Task Keep_current_data_discards_the_check_and_leaves_the_data_as_it_is()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync();
        await GotoOwnerAsync(SettingsPath);
        await ChooseAsync(FileBytes());
        await Page.GetByRole(AriaRole.Button, new() { Name = "Check file" }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "This file contains" })).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Keep current data" }).ClickAsync();

        await Expect(Page.Locator("#restore-preview")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#restore-done-heading")).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Check file" })).ToBeVisibleAsync();
    }

    private static byte[] FileBytes()
    {
        var data = OwnerData.Empty with
        {
            Revision = 6,
            SavedAtUtc = new DateTimeOffset(2030, 1, 10, 9, 30, 0, TimeSpan.Zero),
            Locations = [new OwnerLocation("a1b2c3d4", "Cabinet A"), new OwnerLocation("e5f6a7b8", "Shelf B")],
            Games = [new OwnerGame(101, "a1b2c3d4", null, new PictureSetting(PictureChoice.MainPicture, true, null, null, null))],
            Changes = [new ChangeEntry(new DateTimeOffset(2030, 1, 10, 9, 30, 0, TimeSpan.Zero), "Added location \"Cabinet A\"")],
        };

        return OwnerDataExport.Write(data, new DateTimeOffset(2030, 1, 10, 9, 31, 0, TimeSpan.Zero));
    }

    private async Task StartAsync()
    {
        var existing = OwnerData.Empty with
        {
            Revision = 2,
            SavedAtUtc = new DateTimeOffset(2030, 1, 14, 18, 0, 0, TimeSpan.Zero),
            Locations = [new OwnerLocation("0f0f0f0f", "Crate C")],
        };
        OwnerDataSeed.Write(_storage.FullPath, existing);

        await StartAsync(SyncHarness.CreateFactory(
            new ScriptedBggHandler(),
            SyncHarness.NewClock(),
            new Dictionary<string, string?> { ["Storage:Directory"] = _storage.FullPath }));
    }

    private Task ChooseAsync(byte[] content) =>
        Page.Locator("#restore-file").SetInputFilesAsync(new FilePayload { Name = "owner-data.json", MimeType = "application/json", Buffer = content });
}

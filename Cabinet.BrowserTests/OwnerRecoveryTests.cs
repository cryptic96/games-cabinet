using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Storage;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser what the owner sees when the stored owner data cannot be read, whether the file is damaged or was written
/// by a newer format: every owner page shows the recovery panel with the earlier versions in place of its content, an earlier version
/// goes through the same check and confirm as a file, nothing on the server is overwritten before that, focus lands where a keyboard
/// or screen reader user needs it after each step, and the comparison table fits the smallest phone.
/// </summary>
[Trait("Category", "Browser")]
public sealed class OwnerRecoveryTests : CabinetPageTest
{
    private const string SettingsPath = "/owner/settings";
    private const string Heading = "Your owner data couldn't be read";
    private const string PreviewHeading = "This file contains";

    private readonly TemporaryDirectory _storage = new();

    [Theory]
    [InlineData("/owner/pictures", false, 1440, 900)]
    [InlineData("/owner/locations", false, 390, 844)]
    [InlineData("/owner/settings", false, 1440, 900)]
    [InlineData("/owner/pictures", true, 390, 844)]
    [InlineData("/owner/locations", true, 1440, 900)]
    [InlineData("/owner/settings", true, 390, 844)]
    public async Task Every_owner_page_shows_the_recovery_panel_with_the_earlier_versions_in_place_of_its_content(string path, bool newerFormat, int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(newerFormat);
        var file = Path.Combine(_storage.FullPath, OwnerDataStore.FileName);
        var before = await File.ReadAllBytesAsync(file, TestContext.Current.CancellationToken);

        await GotoOwnerAsync(path);

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = Heading })).ToBeVisibleAsync();
        await Expect(Page.Locator("#owner-unreadable")).ToContainTextAsync("Nothing was changed. Restore it from a file you downloaded, or from an earlier version kept on this server.");
        await Expect(Page.Locator("#owner-unreadable").GetByRole(AriaRole.Link, new() { Name = "Restore from a file" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Use this version" })).ToHaveCountAsync(1);
        await Expect(Page.Locator(".version-item time")).ToHaveCountAsync(1);
        await Expect(Page.Locator("#owner-banner")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#change-log, #recent-changes")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".owner-plate")).ToBeVisibleAsync();
        await Expect(Page.Locator(".owner-bar")).ToBeVisibleAsync();
        var overflow = await Page.EvaluateAsync<double>("() => document.documentElement.scrollWidth - window.innerWidth");

        overflow.Should().BeLessThanOrEqualTo(0);
        (await File.ReadAllBytesAsync(file, TestContext.Current.CancellationToken)).Should().Equal(before);
        await SaveScreenshotAsync($"owner-unreadable-{(newerFormat ? "newer" : "damaged")}-{path.Split('/')[^1]}-{width}");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Settings_offers_the_restore_form_in_the_panel_while_the_other_pages_only_link_to_it()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(newerFormat: false);

        await GotoOwnerAsync("/owner/locations");
        await Expect(Page.Locator("#restore-file")).ToHaveCountAsync(0);
        await Page.Locator("#owner-unreadable").GetByRole(AriaRole.Link, new() { Name = "Restore from a file" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/owner/settings#restore$"));
        await Expect(Page.Locator("#restore-file")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Check file" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Use_this_version_shows_the_preview_with_focus_and_restoring_it_brings_the_pages_back_with_focus_on_the_restored_line()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(newerFormat: false);
        await GotoOwnerAsync(SettingsPath);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Use this version" }).ClickAsync();

        var preview = Page.GetByRole(AriaRole.Heading, new() { Name = PreviewHeading });
        await Expect(preview).ToBeVisibleAsync();
        await Expect(preview).ToBeFocusedAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = Heading })).ToBeVisibleAsync();
        await Expect(Page.Locator("#restore-preview p").First).ToHaveTextAsync("2 locations, 1 placed game, 0 picture choices.");
        await SaveScreenshotAsync("owner-unreadable-preview-1440");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Restore file" }).ClickAsync();

        var done = Page.Locator("#restore-done-heading");
        await Expect(done).ToHaveTextAsync("Restored. 2 locations, 1 placed game, 0 picture choices. Your previous data is kept as an earlier version.");
        await Expect(done).ToBeFocusedAsync();
        await Expect(Page.Locator("#owner-unreadable")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".change-text").First).ToHaveTextAsync("Restored an earlier version");
        await GotoOwnerAsync("/owner/locations");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = Heading })).ToHaveCountAsync(0);
        await Expect(Page.Locator("#loc-list .loc-name").First).ToHaveValueAsync("Cabinet A");
        ConsoleErrors.Should().BeEmpty();
    }

    private async Task StartAsync(bool newerFormat)
    {
        var data = OwnerData.Empty with
        {
            Revision = 4,
            SavedAtUtc = new DateTimeOffset(2030, 1, 14, 18, 0, 0, TimeSpan.Zero),
            Locations = [new OwnerLocation("a1b2c3d4", "Cabinet A"), new OwnerLocation("e5f6a7b8", "Shelf B")],
            Games = [new OwnerGame(101, "a1b2c3d4", null, null)],
            Changes = [new ChangeEntry(new DateTimeOffset(2030, 1, 14, 18, 0, 0, TimeSpan.Zero), "Added location \"Cabinet A\"")],
        };
        OwnerDataSeed.Write(_storage.FullPath, data);
        OwnerDataSeed.KeepEarlierVersion(_storage.FullPath, new DateTimeOffset(2030, 1, 14, 18, 5, 0, TimeSpan.Zero));
        OwnerDataSeed.WriteRaw(_storage.FullPath, newerFormat ? OwnerDataSeed.NewerFormat() : "{ this is not json"u8.ToArray());

        await StartAsync(SyncHarness.CreateFactory(
            new ScriptedBggHandler(),
            SyncHarness.NewClock(),
            new Dictionary<string, string?> { ["Storage:Directory"] = _storage.FullPath }));
    }
}

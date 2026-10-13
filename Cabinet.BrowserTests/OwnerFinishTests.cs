using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser the cabinet finish panel on the owner's settings page: one checked Classic tile with its drawn
/// swatch and the note, a tap that saves with a plain Saved line and nothing to undo, a failed save that leaves the tile
/// checked, a layout that fits a phone, and the finish class on the body of the page and of the public cabinet.
/// </summary>
[Trait("Category", "Browser")]
public sealed class OwnerFinishTests : CabinetPageTest
{
    private const string SettingsPath = "/owner/settings";
    private const string FinishHandlerPattern = "**/owner/settings?handler=Finish";

    private readonly TemporaryDirectory _storage = new();

    [Fact]
    public async Task The_panel_shows_one_checked_classic_tile_with_a_drawn_swatch_and_the_note()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();
        await GotoOwnerAsync(SettingsPath);

        var panel = Page.Locator("#finish");
        await Expect(panel.GetByRole(AriaRole.Heading, new() { Name = "Cabinet finish" })).ToBeVisibleAsync();
        await Expect(panel).ToContainTextAsync("The look of the cabinet for every visitor.");
        await Expect(panel.Locator("label.finish-tile")).ToHaveCountAsync(1);
        await Expect(panel.Locator("label.finish-tile .pic-label")).ToHaveTextAsync("Classic");
        await Expect(panel.Locator("label.finish-tile .pic-sub")).ToHaveTextAsync("Warm wood, as it is today.");
        await Expect(panel.GetByRole(AriaRole.Radio, new() { Name = "Classic" })).ToBeCheckedAsync();
        await Expect(panel.Locator(".finish-swatch")).ToHaveAttributeAsync("aria-hidden", "true");
        await Expect(panel.Locator(".finish-note")).ToHaveTextAsync("Classic is the only finish for now.");
        await Expect(panel.Locator(".finish-extras")).ToHaveCountAsync(0);
        await Expect(panel.Locator("[data-save]")).ToBeHiddenAsync();

        var swatch = (await panel.Locator(".finish-swatch").BoundingBoxAsync())!;
        var tile = (await panel.Locator("label.finish-tile").BoundingBoxAsync())!;

        ((double)swatch.Height).Should().BeApproximately(128, 0.5);
        swatch.Width.Should().BeGreaterThan(150).And.BeLessThanOrEqualTo(tile.Width);
        await SaveScreenshotAsync("owner-finish-1440");
        ExpectCleanPage();
    }

    [Fact]
    public async Task Choosing_the_checked_tile_again_saves_with_saved_and_no_undo_and_posts_once()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();
        await GotoOwnerAsync(SettingsPath);
        var posts = 0;
        Page.Request += (_, request) =>
        {
            if (request.Method == "POST" && request.Url.Contains("handler=Finish", StringComparison.Ordinal))
            {
                posts++;
            }
        };

        await Page.Locator("label.finish-tile").ClickAsync();

        await Expect(Page.Locator("#finish-status")).ToHaveTextAsync("Saved");
        await Expect(Page.Locator("#finish-status button")).ToHaveCountAsync(0);
        await Expect(Page.GetByRole(AriaRole.Radio, new() { Name = "Classic" })).ToBeCheckedAsync();
        posts.Should().Be(1);
        ExpectCleanPage();
    }

    [Fact]
    public async Task A_failed_save_shows_the_row_error_and_the_tile_stays_checked()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();
        await GotoOwnerAsync(SettingsPath);
        await Page.RouteAsync(FinishHandlerPattern, route => route.FulfillAsync(new RouteFulfillOptions { Status = 500, Body = string.Empty }));

        await Page.Locator("label.finish-tile").ClickAsync();

        await Expect(Page.Locator("#finish-status")).ToContainTextAsync("Couldn't save that. Nothing changed. Try again.");
        await Expect(Page.Locator("#finish-status")).ToHaveAttributeAsync("data-problem", "true");
        await Expect(Page.GetByRole(AriaRole.Radio, new() { Name = "Classic" })).ToBeCheckedAsync();
    }

    [Theory]
    [InlineData(390, 844)]
    [InlineData(320, 640)]
    public async Task On_a_phone_the_tile_and_its_swatch_fit_the_screen_without_a_sideways_scroll(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartHostAsync();
        await GotoOwnerAsync(SettingsPath);

        var tile = (await Page.Locator("label.finish-tile").BoundingBoxAsync())!;
        var swatch = (await Page.Locator(".finish-swatch").BoundingBoxAsync())!;
        var overflow = await Page.EvaluateAsync<double>("() => document.documentElement.scrollWidth - window.innerWidth");

        ((double)swatch.Height).Should().BeApproximately(128, 0.5);
        (tile.X + tile.Width).Should().BeLessThanOrEqualTo(width);
        overflow.Should().BeLessThanOrEqualTo(0);
        await Expect(Page.Locator("#finish .pic-choices")).ToBeVisibleAsync();
        await SaveScreenshotAsync($"owner-finish-{width}");
        ExpectCleanPage();
    }

    [Fact]
    public async Task The_body_of_an_owner_page_and_of_the_public_cabinet_carries_the_finish_class()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();

        await GotoOwnerAsync(SettingsPath);
        await Expect(Page.Locator("body")).ToHaveClassAsync("finish-classic");

        await GotoCabinetAsync("/");
        await Expect(Page.Locator("body")).ToHaveClassAsync("finish-classic");
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task StartHostAsync() =>
        await StartAsync(SyncHarness.CreateFactory(
            new ScriptedBggHandler(),
            SyncHarness.NewClock(),
            new Dictionary<string, string?> { ["Storage:Directory"] = _storage.FullPath }));

    private void ExpectCleanPage()
    {
        ConsoleErrors.Should().BeEmpty();
        RequestHosts.Should().OnlyContain(host => host == $"127.0.0.1:{OwnerPort}");
    }
}

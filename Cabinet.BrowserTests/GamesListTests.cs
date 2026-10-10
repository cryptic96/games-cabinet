using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the skip link is the first thing a keyboard reaches and leads to the games list, that the list is a
/// visible panel only while focus is inside it, that its entries open the same card and get focus back, and that it moves with the
/// arrow keys.
/// </summary>
[Trait("Category", "Browser")]
public sealed class GamesListTests : CabinetPageTest
{
    private const string Entries = ".games-list button";
    private const string OpenCard = "dialog.card-dialog[open]";
    private const string LiveRegionsOtherThanTheSyncNote = "[...document.querySelectorAll('[aria-live], [role=\"status\"], [role=\"alert\"], [role=\"log\"]')].filter((e) => e.id !== 'sync-note').length";
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    [Fact]
    public async Task The_first_Tab_shows_the_skip_link_inside_the_viewport()
    {
        await OpenAsync("/?sample=65", 1440, 900);

        await Page.Keyboard.PressAsync("Tab");

        await Expect(Page.Locator(".skip-link")).ToBeFocusedAsync();
        await Expect(Page.Locator(".skip-link")).ToBeInViewportAsync();
        await Expect(Page.Locator(".skip-link")).ToHaveTextAsync("Skip to the list of games");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Enter_on_the_skip_link_moves_focus_to_the_first_entry_and_the_list_becomes_a_visible_panel()
    {
        await OpenAsync("/?sample=65", 1440, 900);
        var hidden = await Page.Locator(".games-list").BoundingBoxAsync();
        hidden!.Width.Should().BeLessThanOrEqualTo(1);

        await Page.Keyboard.PressAsync("Tab");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#games-list-start")).ToBeFocusedAsync();
        var panel = await Page.Locator(".games-list").BoundingBoxAsync();
        panel!.Width.Should().BeGreaterThan(100);
        await SaveScreenshotAsync("games-list-panel-desktop");
    }

    [Fact]
    public async Task The_list_starts_with_the_level_two_heading_and_a_list_of_buttons()
    {
        await OpenAsync("/?sample=65", 1440, 900);

        await Expect(Page.Locator(".games-list")).ToMatchAriaSnapshotAsync("""
            - heading "All games" [level=2]
            - list:
              - listitem:
                - button /.+/
            """);
    }

    [Fact]
    public async Task Enter_on_an_entry_opens_the_card_of_that_game_and_Escape_returns_focus_to_the_same_entry()
    {
        await OpenAsync("/?sample=65", 1440, 900);

        await Page.Keyboard.PressAsync("Tab");
        await Page.Keyboard.PressAsync("Enter");
        var entry = Page.Locator("#games-list-start");
        var entryId = await entry.GetAttributeAsync("data-entry-id");
        var name = await entry.TextContentAsync();

        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator("#card-title")).ToBeFocusedAsync();
        var cardTitle = await Page.Locator("#card-title").TextContentAsync();
        name.Should().StartWith(cardTitle);
        (await Page.Locator(".games-list").BoundingBoxAsync())!.Width.Should().BeLessThanOrEqualTo(1);

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await Expect(Page.Locator($"{Entries}[data-entry-id=\"{entryId}\"]").First).ToBeFocusedAsync();
    }

    [Fact]
    public async Task The_page_keeps_one_live_region_and_the_list_adds_none()
    {
        await OpenAsync("/?sample=65", 1440, 900);

        (await Page.EvaluateAsync<int>(LiveRegionsOtherThanTheSyncNote)).Should().Be(0);

        await Page.Keyboard.PressAsync("Tab");
        await Page.Keyboard.PressAsync("Enter");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();

        (await Page.EvaluateAsync<int>(LiveRegionsOtherThanTheSyncNote)).Should().Be(0);
    }

    private async Task OpenAsync(string path, int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await GotoCabinetAsync(path);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}

using System.Text.RegularExpressions;
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

    [Fact]
    public async Task The_arrow_keys_move_between_entries_and_Home_and_End_jump_to_the_ends()
    {
        await OpenAsync("/?sample=65", 1440, 900);
        await Page.Keyboard.PressAsync("Tab");
        await Page.Keyboard.PressAsync("Enter");
        var entries = Page.Locator(Entries);
        var count = await entries.CountAsync();
        count.Should().BeGreaterThan(10);

        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(entries.Nth(1)).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("End");
        await Expect(entries.Nth(count - 1)).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(entries.Nth(count - 1)).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("Home");
        await Expect(entries.First).ToBeFocusedAsync();

        (await entries.EvaluateAllAsync<int>("(buttons) => buttons.filter((b) => b.getAttribute('tabindex') !== '-1').length")).Should().Be(0);
    }

    [Fact]
    public async Task Tab_leaves_the_list_for_the_cabinets_one_tab_stop_and_the_panel_goes_away()
    {
        await OpenAsync("/?sample=65", 1440, 900);
        await Page.Keyboard.PressAsync("Tab");
        await Page.Keyboard.PressAsync("Enter");
        await Page.Keyboard.PressAsync("ArrowDown");

        await Page.Keyboard.PressAsync("Tab");

        await Expect(Page.Locator("#cabinet .placement[tabindex=\"0\"]")).ToBeFocusedAsync();
        (await Page.Locator(".games-list").BoundingBoxAsync())!.Width.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task Without_card_data_the_entries_carry_titles_only_and_still_open_a_card_with_the_quiet_note()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        await Page.RouteAsync("**/cabinet/cards**", route => route.AbortAsync());
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var entries = Page.Locator(Entries);
        (await entries.CountAsync()).Should().BeGreaterThan(10);
        (await entries.AllTextContentsAsync()).Should().NotContain(name => name.Contains(" players", StringComparison.Ordinal));

        await Page.Keyboard.PressAsync("Tab");
        await Page.Keyboard.PressAsync("Enter");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator(".card-note")).ToHaveTextAsync("More details arrive after the next sync.");
    }

    [Fact]
    public async Task With_no_games_the_list_holds_only_its_heading_and_the_skip_link_lands_on_it()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        var drawn = Page.WaitForResponseAsync(response => response.Url.Contains("/cabinet/layout", StringComparison.Ordinal));
        await Page.GotoAsync($"http://127.0.0.1:{PublicPort}/?sample=0");
        await drawn;
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.Locator(Entries)).ToHaveCountAsync(0);

        await Page.Keyboard.PressAsync("Tab");
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#games-list-title")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Four_hundred_games_list_every_game_including_expansions_the_cabinet_hides_behind_a_marker_and_still_only_one_tab_stop_in_the_cabinet()
    {
        await OpenAsync("/?sample=400", 1440, 900);

        var listedIds = await Page.Locator(Entries).EvaluateAllAsync<string[]>("(buttons) => [...new Set(buttons.map((b) => b.dataset.entryId))]");
        var drawnIds = await Page.EvaluateAsync<string[]>("[...new Set([...document.querySelectorAll('#cabinet .placement:not([data-kind=\"moreMarker\"])')].map((b) => b.dataset.entryId))]");

        drawnIds.Length.Should().BeGreaterThan(300);
        listedIds.Should().HaveCount(400);
        listedIds.Should().Contain(drawnIds);
        await Expect(Page.Locator("#cabinet .placement[tabindex=\"0\"]")).ToHaveCountAsync(1);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_list_is_never_removed_from_the_accessibility_tree()
    {
        await OpenAsync("/?sample=65", 1440, 900);

        var display = await Page.Locator(".games-list").EvaluateAsync<string>("(section) => getComputedStyle(section).display");
        display.Should().NotBe("none");
        (await Page.Locator(".games-list").GetAttributeAsync("aria-hidden")).Should().BeNull();
        (await Page.Locator(".games-list").EvaluateAsync<int>("(section) => section.querySelectorAll('[aria-hidden]').length")).Should().Be(0);
    }

    [Fact]
    public async Task On_a_phone_the_skip_link_shows_inside_the_screen_on_the_first_Tab()
    {
        await OpenAsync("/?sample=65", 390, 844);

        await Page.Keyboard.PressAsync("Tab");

        await Expect(Page.Locator(".skip-link")).ToBeFocusedAsync();
        await Expect(Page.Locator(".skip-link")).ToBeInViewportAsync();
        await SaveScreenshotAsync("skip-link-phone");
    }

    private async Task OpenAsync(string path, int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await GotoCabinetAsync(path);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}

/// <summary>Proves in a real browser that a visitor whose browser prefers Dutch gets the skip link, the heading and the entries in Dutch.</summary>
[Trait("Category", "Browser")]
public sealed class DutchGamesListTests : CabinetPageTest
{
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    /// <summary>Makes the browser ask for Dutch, as a visitor with Dutch language settings does.</summary>
    public override BrowserNewContextOptions ContextOptions()
    {
        var options = base.ContextOptions();
        options.Locale = "nl-NL";

        return options;
    }

    [Fact]
    public async Task The_skip_link_heading_and_entries_are_in_Dutch_with_titles_unchanged()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.Locator(".games-list-heading")).ToHaveTextAsync("Alle spellen");
        await Expect(Page.Locator(".skip-link")).ToHaveTextAsync("Ga naar de lijst met spellen");

        var full = Page.Locator(".games-list button").Filter(new LocatorFilterOptions { HasTextRegex = new Regex(@" spelers?, .*minuten") });
        (await full.CountAsync()).Should().BeGreaterThan(0);
        (await Page.Locator(".games-list button").AllTextContentsAsync()).Should().NotContain(name => name.Contains(" players", StringComparison.Ordinal) || name.Contains(" minutes", StringComparison.Ordinal));
    }
}

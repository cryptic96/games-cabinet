using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the window may change width at any moment, as a resized window, a rotated tablet or a docked
/// laptop does, and the filters always land in the right place: an open fly-over closes at once and leaves no history step behind,
/// a pushed panel folds when the window narrows and comes back when it widens again, the one control set moves between the panel and
/// the phone's slim bar and sheet, focus never stays behind in a part that went away, and the remembered open or folded choice is
/// never changed by a width change.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterDockResizeTests : CabinetPageTest
{
    private const string RailMenu = ".filter-rail .filter-menu";
    private const string HeadMenu = "#filter-panel .filter-menu";
    private const string OpenSheet = "dialog.filter-dialog[open]";
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private const string ReadPlaces = """
        () => {
          const where = (selector) => {
            const element = document.querySelector(selector);
            if (element === null) { return null; }
            if (element.closest('.filter-sheet-groups') !== null) { return 'sheet'; }
            if (element.closest('#filter-panel') !== null) { return 'panel'; }
            if (element.closest('.filter-slim') !== null) { return 'slim'; }
            return 'elsewhere';
          };
          const radios = (name) => new Set([...document.querySelectorAll(`input[type="radio"][name="${name}"]`)].map((radio) => radio.closest('fieldset'))).size;
          return {
            view: where('.filter-view'),
            players: where('.filter-group[data-filter="players"]'),
            time: where('.filter-group[data-filter="time"]'),
            location: where('.filter-location'),
            search: where('#filter-q'),
            count: where('.filter-count'),
            playerGroups: radios('filter-players'),
            timeGroups: radios('filter-time'),
            viewGroups: radios('filter-view'),
            inert: document.querySelectorAll('[inert]').length,
            role: document.getElementById('filter-panel').getAttribute('role'),
            railRendered: document.querySelector('.filter-rail').getClientRects().length > 0,
          };
        }
        """;

    [Fact]
    public async Task Crossing_the_widths_lands_the_dock_and_the_controls_in_the_right_place()
    {
        await OpenAsync(DockWidths.Over, 800);
        var lengthBefore = await Page.EvaluateAsync<int>("() => window.history.length");

        await OpenFlyOverAsync();
        await ResizeAsync(DockWidths.Push, 800);

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        await WaitUntilTheCardStepIsGoneAsync();
        (await Page.EvaluateAsync<int>("() => window.history.length")).Should().BeLessThanOrEqualTo(lengthBefore + 1, "the fly-over's step was removed");
        var wide = await ReadPlacesAsync();
        wide.GetProperty("inert").GetInt32().Should().Be(0, "nothing stays inert once the fly-over closed");
        wide.GetProperty("role").ValueKind.Should().Be(JsonValueKind.Null);
        ExpectOneSetIn(wide, "panel", "panel", "widened while the fly-over was open");

        await ResizeAsync(DockWidths.Over, 800);
        await OpenFlyOverAsync();
        await Expect(Page.Locator("#filter-q")).ToBeFocusedAsync();
        await ResizeAsync(DockWidths.Phone, 844);

        await Expect(Page.Locator(".filter-open")).ToBeFocusedAsync();
        await WaitUntilTheCardStepIsGoneAsync();
        var phone = await ReadPlacesAsync();
        phone.GetProperty("railRendered").GetBoolean().Should().BeFalse("the dock is gone below the desktop width");
        phone.GetProperty("inert").GetInt32().Should().Be(0);
        phone.GetProperty("role").ValueKind.Should().Be(JsonValueKind.Null);
        ExpectOneSetIn(phone, "sheet", "slim", "narrowed to a phone");

        await Page.Locator(".filter-open").ClickAsync();
        await Expect(Page.Locator(OpenSheet)).ToBeVisibleAsync();
        await ResizeAsync(DockWidths.Push, 800);

        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        await WaitUntilTheCardStepIsGoneAsync();
        ExpectOneSetIn(await ReadPlacesAsync(), "panel", "panel", "widened again from a phone");
        (await Page.Locator("#filter-panel .filter-panel-groups").EvaluateAsync<string[]>("(slot) => [...slot.children].map((child) => child.dataset.filter ?? child.classList[0])"))
            .Should().Equal("filter-view", "players", "time", "filter-location");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_pushed_panel_folds_when_the_window_narrows_and_comes_back_when_it_widens()
    {
        await OpenAsync(DockWidths.Push, 800);

        await Page.Locator(RailMenu).ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "push");
        await Expect(Page.Locator(HeadMenu)).ToBeFocusedAsync();

        await ResizeAsync(DockWidths.Over, 800);

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        await Expect(Page.Locator(RailMenu)).ToBeFocusedAsync();

        await ResizeAsync(DockWidths.Push, 800);

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "push", new LocatorAssertionsToHaveAttributeOptions { Timeout = 5000 });

        await ResizeAsync(DockWidths.Over, 800);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        await OpenFlyOverAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        await WaitUntilTheCardStepIsGoneAsync();
        await ResizeAsync(DockWidths.Wide, 800);

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "push", new LocatorAssertionsToHaveAttributeOptions { Timeout = 5000 });

        await Page.Locator(HeadMenu).ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        await ResizeAsync(DockWidths.Over, 800);
        await ResizeAsync(DockWidths.Push, 800);

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        ConsoleErrors.Should().BeEmpty();
    }

    private static void ExpectOneSetIn(JsonElement places, string groups, string searchAndCount, string moment)
    {
        foreach (var group in new[] { "view", "players", "time", "location" })
        {
            places.GetProperty(group).GetString().Should().Be(groups, $"the {group} group {moment}");
        }

        places.GetProperty("search").GetString().Should().Be(searchAndCount, $"the search field {moment}");
        places.GetProperty("count").GetString().Should().Be(searchAndCount, $"the count {moment}");
        places.GetProperty("playerGroups").GetInt32().Should().Be(1, $"one players group {moment}");
        places.GetProperty("timeGroups").GetInt32().Should().Be(1, $"one time group {moment}");
        places.GetProperty("viewGroups").GetInt32().Should().Be(1, $"one view group {moment}");
    }

    private async Task<JsonElement> ReadPlacesAsync() => await Page.EvaluateAsync<JsonElement>(ReadPlaces);

    private async Task OpenFlyOverAsync()
    {
        await Page.Locator(RailMenu).ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "over");
        await Page.WaitForFunctionAsync("() => window.history.state !== null && window.history.state.dock === true");
    }

    private async Task ResizeAsync(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Page.WaitForFunctionAsync($"() => window.innerWidth === {width}");
        await Page.EvaluateAsync("() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
    }

    private async Task OpenAsync(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await Page.RouteAsync("**/cabinet/search-names**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "application/json",
            Body = "{\"names\":{}}",
        }));
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
    }
}

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the folded filter rail of a desktop tells what is filtered without opening anything: a group whose
/// filter is in use is filled and carries its value in its name and tooltip, the two-line count and the rail's Clear show only while
/// filtering, and both three-line buttons say how many filters are in use, in English and Dutch.
/// </summary>
[Trait("Category", "Browser")]
public sealed partial class FilterDockRailTests : CabinetPageTest
{
    private const string Bar = ".filter-bar";
    private const string RailCount = ".filter-rail .filter-rail-count";
    private const string RailClear = ".filter-rail .filter-rail-clear";
    private const string PanelCount = "#filter-panel .filter-count";
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private const string ReadFill = """
        (button) => {
          const probe = document.createElement('span');
          probe.style.backgroundColor = 'var(--room-text)';
          button.parentElement.append(probe);
          const wanted = getComputedStyle(probe).backgroundColor;
          probe.remove();
          return { background: getComputedStyle(button).backgroundColor, wanted };
        }
        """;

    private const string ReadCountLines = """
        () => {
          const number = document.querySelector('.filter-rail .filter-rail-count-number').getBoundingClientRect();
          const of = document.querySelector('.filter-rail .filter-rail-count-of').getBoundingClientRect();
          return { numberBottom: number.bottom, ofTop: of.top };
        }
        """;

    private bool _started;

    [Fact]
    public async Task A_chosen_player_count_fills_its_rail_button_and_names_it()
    {
        await OpenAsync("/?sample=65", DockWidths.Push);

        await OpenFilterPanelAsync();
        await Page.Locator("#filter-panel fieldset[data-filter=\"players\"] input[value=\"4\"]").ClickAsync();
        await Expect(Page.Locator(Bar)).ToHaveAttributeAsync("data-active", string.Empty);
        await FoldFilterPanelAsync();

        foreach (var language in new[] { "en", "nl" })
        {
            await ChooseLanguageAsync(language);
            var text = Words.For(language);
            var players = RailButton("players");

            await Expect(players).ToHaveAttributeAsync("data-filled", string.Empty);
            await Expect(players).ToHaveAccessibleNameAsync(text.Players + ": 4");
            await Expect(players).ToHaveAttributeAsync("title", text.Players + ": 4");
            await ExpectFilledAsync(players, $"{language}: the chosen group wears the chosen-chip pair");

            foreach (var (group, label) in new[] { ("search", text.Search), ("time", text.Time), ("location", text.Location) })
            {
                await Expect(RailButton(group)).Not.ToHaveAttributeAsync("data-filled", string.Empty);
                await Expect(RailButton(group)).ToHaveAttributeAsync("aria-label", label);
                await Expect(RailButton(group)).ToHaveAttributeAsync("title", label);
            }

            var (lit, total) = await PanelTallyAsync(text);
            await Expect(Page.Locator(RailCount)).ToBeVisibleAsync();
            await Expect(Page.Locator(RailCount + " .filter-rail-count-number")).ToHaveTextAsync(lit.ToString(CultureInfo.InvariantCulture));
            await Expect(Page.Locator(RailCount + " .filter-rail-count-of")).ToHaveTextAsync($"{text.Of} {total.ToString(CultureInfo.InvariantCulture)}");
            var lines = await Page.EvaluateAsync<JsonElement>(ReadCountLines);
            lines.GetProperty("ofTop").GetDouble().Should().BeGreaterThanOrEqualTo(lines.GetProperty("numberBottom").GetDouble() - 0.5, $"{language}: the number stands above its second line");
            await Expect(Page.Locator(RailClear)).ToBeVisibleAsync();
            await Expect(Page.Locator(RailClear)).Not.ToHaveAttributeAsync("data-highlighted", string.Empty);

            await Expect(Page.Locator(".filter-rail .filter-menu")).ToHaveAccessibleNameAsync(text.ActiveName(1));

            foreach (var menu in await Page.Locator(".filter-menu").AllAsync())
            {
                await Expect(menu).ToHaveAttributeAsync("title", text.ActiveName(1));
                await Expect(menu.Locator(".filter-menu-suffix")).ToHaveTextAsync(text.ActiveName(1)["Filters".Length..]);
            }

            await SaveScreenshotAsync($"filter-rail-players-{language}");
        }

        ConsoleErrors.Should().BeEmpty();
    }

    private ILocator RailButton(string group) => Page.Locator($".filter-rail .filter-rail-button[data-group=\"{group}\"]");

    private async Task ExpectFilledAsync(ILocator button, string because)
    {
        await Expect(button).ToBeVisibleAsync();
        var fill = await button.EvaluateAsync<JsonElement>(ReadFill);
        fill.GetProperty("background").GetString().Should().Be(fill.GetProperty("wanted").GetString(), because);
    }

    private async Task<(int Lit, int Total)> PanelTallyAsync(Words text)
    {
        await Expect(Page.Locator(PanelCount)).ToHaveTextAsync(text.FullCount());
        var match = text.FullCount().Match((await Page.Locator(PanelCount).TextContentAsync())!);

        return (int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    private async Task ChooseLanguageAsync(string language)
    {
        if (await Page.Locator("html").GetAttributeAsync("lang") == language)
        {
            return;
        }

        await Page.Locator($".lang-toggle a[lang={language}]").ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", language);
        await Expect(Page.Locator(Bar)).ToBeVisibleAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task OpenAsync(string pathAndQuery, int width, int height = 900)
    {
        await Page.SetViewportSizeAsync(width, height);

        if (!_started)
        {
            await StartAsync(PrototypeOn);
            await RouteNamesAsync();
            _started = true;
        }

        await GotoCabinetAsync(pathAndQuery);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(Bar)).ToBeVisibleAsync();
    }

    private Task RouteNamesAsync() => Page.RouteAsync("**/cabinet/search-names**", route => route.FulfillAsync(new RouteFulfillOptions
    {
        Status = 200,
        ContentType = "application/json",
        Body = "{\"names\":{}}",
    }));

    private sealed record Words(string Language)
    {
        private bool Dutch => Language == "nl";

        public string Search => Dutch ? "Zoek een spel" : "Search games";

        public string Players => Dutch ? "Spelers" : "Players";

        public string Time => Dutch ? "Max. minuten" : "Max. minutes";

        public string Location => Dutch ? "Locatie" : "Location";

        public string Of => Dutch ? "van" : "of";

        public static Words For(string language) => new(language);

        public string ActiveName(int active) => Dutch ? $"Filters · {active} actief" : $"Filters · {active} active";

        public Regex FullCount() => Dutch ? DutchFullCount() : EnglishFullCount();
    }

    [GeneratedRegex(@"^(\d+) of (\d+) games?$")]
    private static partial Regex EnglishFullCount();

    [GeneratedRegex(@"^(\d+) van (\d+) spel(?:len)?$")]
    private static partial Regex DutchFullCount();
}

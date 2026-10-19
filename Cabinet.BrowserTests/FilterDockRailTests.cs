using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the folded filter rail of a desktop tells what is filtered without opening anything: a group whose
/// filter is in use is filled and carries its value in its name and tooltip, the two-line count and the rail's Clear show only while
/// filtering, and both three-line buttons say how many filters are in use, in English and Dutch. The open panel's count line names the
/// collection at rest; when the card details fail the card-based rail buttons are dashed, and without owner locations nothing about
/// locations is offered.
/// </summary>
[Trait("Category", "Browser")]
public sealed partial class FilterDockRailTests : CabinetPageTest
{
    private const string Bar = ".filter-bar";
    private const string RailCount = ".filter-rail .filter-rail-count";
    private const string RailClear = ".filter-rail .filter-rail-clear";
    private const string PanelCount = "#filter-panel .filter-count";
    private const string PanelClear = "#filter-panel .filter-clear";
    private const string Field = "#filter-q";
    private const string Live = "#filter-live";
    private const string StorageKey = "cabinet-filters";
    private const string LongPlace = "Shelf B with the extra deep boxes at the very back";
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

    private const string ReadColour = """
        (element, token) => {
          const probe = document.createElement('span');
          probe.style.color = `var(${token})`;
          element.parentElement.append(probe);
          const wanted = getComputedStyle(probe).color;
          probe.remove();
          const style = getComputedStyle(element);
          return { colour: style.color, wanted, weight: style.fontWeight };
        }
        """;

    private const string ReadCountLines = """
        () => {
          const number = document.querySelector('.filter-rail .filter-rail-count-number').getBoundingClientRect();
          const of = document.querySelector('.filter-rail .filter-rail-count-of').getBoundingClientRect();
          return { numberBottom: number.bottom, ofTop: of.top };
        }
        """;

    private const string ReadUnavailable = """
        (button) => {
          const style = getComputedStyle(button);
          return { border: style.borderTopStyle, background: style.backgroundColor };
        }
        """;

    private const string ReadNoteGap = """
        () => {
          const count = document.querySelector('#filter-panel .filter-count').getBoundingClientRect();
          const note = document.querySelector('#filter-panel .filter-note').getBoundingClientRect();
          return { gap: note.top - count.bottom };
        }
        """;

    private const string ReadSecondLine = """
        () => {
          const rail = document.querySelector('.filter-rail').getBoundingClientRect();
          const line = document.querySelector('.filter-rail .filter-rail-count-of');
          const box = line.getBoundingClientRect();
          const text = line.firstChild;
          let from = 0;
          const words = line.textContent.split(' ').map((word) => {
            const start = line.textContent.indexOf(word, from);
            from = start + word.length;
            const range = document.createRange();
            range.setStart(text, start);
            range.setEnd(text, start + word.length);
            return range.getClientRects().length;
          });
          return {
            railLeft: rail.left,
            railRight: rail.right,
            left: box.left,
            right: box.right,
            clipped: line.scrollWidth > line.clientWidth + 0.5 || line.scrollHeight > line.clientHeight + 0.5,
            lines: Math.round(box.height / parseFloat(getComputedStyle(line).lineHeight)),
            words,
          };
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

            await ExpectRailNameAsync("players", text.Players + ": 4");

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

    [Fact]
    public async Task Every_group_shows_its_value_on_the_rail()
    {
        await Page.SetViewportSizeAsync(DockWidths.Push, 900);
        var host = await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(5));
        var ids = await host.SeedAsync("Cabinet A", LongPlace);
        var games = host.Cards.DistinctBy(card => card.GameId).ToList();
        games.Should().HaveCountGreaterThanOrEqualTo(3, "two games are placed and one stays without a location");
        await host.PlaceAsync(games[1], ids[0]);
        await host.PlaceAsync(games[2], ids[1]);
        await RouteNamesAsync();
        _started = true;
        await OpenAsync("/", DockWidths.Push);

        foreach (var language in new[] { "en", "nl" })
        {
            await ChooseLanguageAsync(language);
            var text = Words.For(language);

            await OpenFilterPanelAsync();
            await Page.Locator(Field).FillAsync("cata");
            await Page.Locator("#filter-panel fieldset[data-filter=\"time\"] input[value=\"60\"]").ClickAsync();
            await Page.Locator("#filter-loc").SelectOptionAsync(ids[0]);
            await FoldFilterPanelAsync();

            await ExpectRailNameAsync("search", $"{text.Search}: cata");
            await ExpectRailNameAsync("time", $"{text.Time}: 60");
            await ExpectRailNameAsync("location", $"{text.Location}: Cabinet A");
            await Expect(RailButton("players")).Not.ToHaveAttributeAsync("data-filled", string.Empty);
            await Expect(RailButton("players")).ToHaveAttributeAsync("title", text.Players);
            await Expect(Page.Locator(".filter-rail .filter-menu")).ToHaveAccessibleNameAsync(text.ActiveName(3));
            await SaveScreenshotAsync($"filter-rail-every-group-{language}");

            await OpenFilterPanelAsync();
            await Page.Locator("#filter-loc").SelectOptionAsync("unknown");
            await FoldFilterPanelAsync();
            await ExpectRailNameAsync("location", $"{text.Location}: {text.Unknown}");

            await OpenFilterPanelAsync();
            await Page.Locator("#filter-loc").SelectOptionAsync(ids[1]);
            await FoldFilterPanelAsync();
            await ExpectRailNameAsync("location", $"{text.Location}: {LongPlace}");

            await Page.Locator(RailClear).ClickAsync();
            await Expect(Page.Locator(RailCount)).ToBeHiddenAsync();
        }

        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Zero_matches_and_Clear_on_the_rail()
    {
        await OpenAsync("/?sample=65", DockWidths.Push);
        await ChooseViewAsync(1);
        await Expect(Page.Locator("input[name=\"filter-view\"][value=\"location\"]")).ToBeCheckedAsync();

        await OpenFilterPanelAsync();
        var total = await RestingTotalAsync(Words.For("en"));
        await Page.Locator("#filter-panel fieldset[data-filter=\"players\"] input[value=\"4\"]").ClickAsync();
        await Page.Locator("#filter-panel fieldset[data-filter=\"time\"] input[value=\"60\"]").ClickAsync();
        await Page.Locator(Field).FillAsync("qqzzqq");
        await Expect(Page.Locator(PanelCount)).ToHaveTextAsync("No games match");
        await FoldFilterPanelAsync();

        await Expect(Page.Locator(RailCount + " .filter-rail-count-number")).ToHaveTextAsync("0");
        await Expect(Page.Locator(RailCount + " .filter-rail-count-of")).ToHaveTextAsync($"of {total.ToString(CultureInfo.InvariantCulture)}");
        await Expect(Page.Locator(RailClear)).ToHaveAttributeAsync("data-highlighted", string.Empty);
        await ExpectPaintedFillAsync(Page.Locator(RailClear), "at zero matches the rail's Clear is drawn in the inverted wall fill");
        await SaveScreenshotAsync("filter-rail-zero");

        await Page.Locator(RailClear).ClickAsync();

        await Expect(RailButton("search")).ToBeFocusedAsync();
        await Expect(Page.Locator(Field)).ToHaveValueAsync(string.Empty);
        await Expect(Page.Locator("fieldset[data-filter=\"players\"] input[value=\"\"]")).ToBeCheckedAsync();
        await Expect(Page.Locator("fieldset[data-filter=\"time\"] input[value=\"\"]")).ToBeCheckedAsync();
        await Expect(Page.Locator("#filter-loc")).ToHaveValueAsync(string.Empty);
        await Expect(Page.Locator("input[name=\"filter-view\"][value=\"location\"]")).ToBeCheckedAsync();
        await Expect(Page.Locator(Bar)).Not.ToHaveAttributeAsync("data-active", string.Empty);
        await Expect(Page.Locator(RailCount)).ToBeHiddenAsync();
        await Expect(Page.Locator(RailClear)).ToBeHiddenAsync();
        await Expect(Page.Locator(".filter-rail .filter-rail-button[data-filled]")).ToHaveCountAsync(0);
        await Expect(Page.Locator(Live)).ToHaveTextAsync($"Filters cleared, {total.ToString(CultureInfo.InvariantCulture)} games");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_panel_count_line_reads_the_collection_at_rest()
    {
        await OpenAsync("/?sample=65", DockWidths.Push);
        await OpenFilterPanelAsync();
        var english = Words.For("en");

        var total = await RestingTotalAsync(english);
        await ExpectColourAsync(PanelCount, "--room-muted", "400", "the resting count is quiet");
        await Expect(Page.Locator(PanelClear)).ToBeHiddenAsync();

        await Page.Locator(Field).FillAsync("a");
        await Expect(Page.Locator(PanelCount)).ToHaveTextAsync(english.FullCount());
        await ExpectColourAsync(PanelCount, "--room-text", "600", "the count while filtering is strong");
        await Expect(Page.Locator(PanelClear)).ToBeVisibleAsync();
        await Expect(Page.Locator(PanelClear)).Not.ToHaveAttributeAsync("data-highlighted", string.Empty);

        await Page.Locator(Field).FillAsync("qqzzqq");
        await Expect(Page.Locator(PanelCount)).ToHaveTextAsync("No games match");
        await Expect(Page.Locator(PanelClear)).ToHaveAttributeAsync("data-highlighted", string.Empty);

        await Page.Locator(Field).FillAsync(await FindUniqueTitleAsync());
        await Expect(Page.Locator(PanelCount)).ToHaveTextAsync($"1 of {total.ToString(CultureInfo.InvariantCulture)} games");
        await SaveScreenshotAsync("filter-panel-count-one");

        await Page.Locator(PanelClear).ClickAsync();
        await Expect(Page.Locator(PanelCount)).ToHaveTextAsync($"{total.ToString(CultureInfo.InvariantCulture)} games");
        await Expect(Page.Locator(PanelClear)).ToBeHiddenAsync();

        await ChooseLanguageAsync("nl");
        await OpenFilterPanelAsync();
        await Expect(Page.Locator(PanelCount)).ToHaveTextAsync($"{total.ToString(CultureInfo.InvariantCulture)} spellen");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Failed_card_details_switch_off_the_card_buttons_on_the_rail()
    {
        await StartAsync(PrototypeOn);
        await RouteNamesAsync();
        _started = true;
        var requests = await FailCardDataRequestsAsync();
        var stored = JsonSerializer.Serialize(JsonSerializer.Serialize(new { q = string.Empty, players = 4, time = 60, location = (string?)null }));
        await Page.AddInitScriptAsync($"if (sessionStorage.getItem('{StorageKey}') === null) {{ sessionStorage.setItem('{StorageKey}', {stored}); }}");
        await OpenAsync("/?sample=65", DockWidths.Push);
        await WaitForCardDataRequestsAsync(requests, 3);
        await Expect(Page.Locator(".filter-note")).ToHaveTextAsync("Couldn't load the details. Reload the page to try again.");

        foreach (var group in new[] { "players", "time", "location" })
        {
            var button = RailButton(group);

            if (group == "location" && await button.IsHiddenAsync())
            {
                continue;
            }

            await Expect(button).ToHaveAttributeAsync("data-unavailable", string.Empty);
            await Expect(button).Not.ToHaveAttributeAsync("data-filled", string.Empty);
            var look = await button.EvaluateAsync<JsonElement>(ReadUnavailable);
            look.GetProperty("border").GetString().Should().Be("dashed", group);
            look.GetProperty("background").GetString().Should().Be("rgba(0, 0, 0, 0)", $"{group} is never filled while the card details are off");
        }

        await Expect(RailButton("search")).Not.ToHaveAttributeAsync("data-unavailable", string.Empty);
        await SaveScreenshotAsync("filter-rail-card-details-failed");

        await OpenFilterPanelAsync();
        await Expect(Page.Locator("#filter-panel .filter-note")).ToBeVisibleAsync();
        var gap = await Page.EvaluateAsync<JsonElement>(ReadNoteGap);
        gap.GetProperty("gap").GetDouble().Should().BeApproximately(8, 1, "the note sits directly under the count line");

        await Page.Locator(Field).FillAsync("a");
        await FoldFilterPanelAsync();
        await ExpectRailNameAsync("search", "Search games: a");
        await Expect(RailButton("players")).Not.ToHaveAttributeAsync("data-filled", string.Empty);
        await Expect(RailButton("players")).ToHaveAttributeAsync("data-unavailable", string.Empty);
    }

    [Fact]
    public async Task Without_locations_there_is_no_location_on_the_rail_or_in_the_panel()
    {
        await Page.SetViewportSizeAsync(DockWidths.Push, 900);
        await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(5));
        await RouteNamesAsync();
        _started = true;
        await OpenAsync("/", DockWidths.Push);

        await Expect(RailButton("search")).ToBeVisibleAsync();
        await Expect(RailButton("location")).ToBeHiddenAsync();
        (await RailButton("location").EvaluateAsync<int>("(button) => button.getClientRects().length")).Should().Be(0);

        await OpenFilterPanelAsync();
        await Expect(Page.Locator("#filter-panel .filter-view")).ToBeHiddenAsync();
        await Expect(Page.Locator("#filter-panel .filter-location")).ToBeHiddenAsync();
        await Expect(Page.Locator("#filter-panel fieldset[data-filter=\"players\"]")).ToBeVisibleAsync();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Van_400_wraps_and_is_never_clipped()
    {
        await OpenAsync("/?sample=400", DockWidths.Push);
        await ChooseLanguageAsync("nl");

        await OpenFilterPanelAsync();
        await Page.Locator(Field).FillAsync("a");
        await Expect(Page.Locator(PanelCount)).ToHaveTextAsync(Words.For("nl").FullCount());
        await FoldFilterPanelAsync();

        await Expect(Page.Locator(RailCount + " .filter-rail-count-of")).ToHaveTextAsync(VanCount());
        await ExpectSecondLineWholeAsync("plain");
        await SaveScreenshotAsync("filter-rail-van-400");

        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ForcedColors = ForcedColors.Active });
        await ExpectSecondLineWholeAsync("forced colours");
        ConsoleErrors.Should().BeEmpty();
    }

    private ILocator RailButton(string group) => Page.Locator($".filter-rail .filter-rail-button[data-group=\"{group}\"]");

    private async Task ExpectSecondLineWholeAsync(string context)
    {
        var line = await Page.EvaluateAsync<JsonElement>(ReadSecondLine);
        line.GetProperty("clipped").GetBoolean().Should().BeFalse($"{context}: the second line is never clipped ({line})");
        line.GetProperty("left").GetDouble().Should().BeGreaterThanOrEqualTo(line.GetProperty("railLeft").GetDouble() - 0.5, context);
        line.GetProperty("right").GetDouble().Should().BeLessThanOrEqualTo(line.GetProperty("railRight").GetDouble() + 0.5, context);
        line.GetProperty("lines").GetInt32().Should().BeInRange(1, 2, $"{context}: {line}");
        line.GetProperty("words").EnumerateArray().Should().OnlyContain(word => word.GetInt32() == 1, $"{context}: a line breaks only at the space, never inside a word");
    }

    private async Task ExpectRailNameAsync(string group, string name)
    {
        var button = RailButton(group);

        await Expect(button).ToHaveAttributeAsync("data-filled", string.Empty);
        await Expect(button).ToHaveAccessibleNameAsync(name);
        await Expect(button).ToHaveAttributeAsync("title", name);
        await ExpectFilledAsync(button, $"{name}: the chosen group wears the chosen-chip pair");
    }

    private async Task ExpectFilledAsync(ILocator button, string because)
    {
        await Expect(button).ToBeVisibleAsync();
        var fill = await button.EvaluateAsync<JsonElement>(ReadFill);
        fill.GetProperty("background").GetString().Should().Be(fill.GetProperty("wanted").GetString(), because);
    }

    private async Task ExpectPaintedFillAsync(ILocator button, string because)
    {
        await Expect(button).ToBeVisibleAsync();
        var fill = await button.EvaluateAsync<JsonElement>(ReadFill);
        var wanted = fill.GetProperty("wanted").GetString();
        wanted.Should().NotBe("rgba(0, 0, 0, 0)", because);
        await Expect(button).ToHaveCSSAsync("background-color", wanted!);
    }

    private async Task ExpectColourAsync(string selector, string token, string weight, string because)
    {
        var reading = await Page.Locator(selector).EvaluateAsync<JsonElement>(ReadColour, token);
        reading.GetProperty("colour").GetString().Should().Be(reading.GetProperty("wanted").GetString(), because);
        reading.GetProperty("weight").GetString().Should().Be(weight, because);
    }

    private async Task<(int Lit, int Total)> PanelTallyAsync(Words text)
    {
        await Expect(Page.Locator(PanelCount)).ToHaveTextAsync(text.FullCount());
        var match = text.FullCount().Match((await Page.Locator(PanelCount).TextContentAsync())!);

        return (int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    private async Task<int> RestingTotalAsync(Words text)
    {
        await Expect(Page.Locator(PanelCount)).ToHaveTextAsync(text.RestCount());
        var match = text.RestCount().Match((await Page.Locator(PanelCount).TextContentAsync())!);

        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private async Task<string> FindUniqueTitleAsync()
    {
        var labels = await Page.EvaluateAsync<string[]>("() => [...document.querySelectorAll('#cabinet .placement')].map((box) => box.getAttribute('aria-label') ?? '').filter((label) => label.length > 6).slice(0, 12)");

        foreach (var label in labels)
        {
            await Page.Locator(Field).FillAsync(label);
            await Expect(Page.Locator(PanelCount)).ToHaveTextAsync(EnglishFullCount());

            if ((await Page.Locator(PanelCount).TextContentAsync())!.StartsWith("1 ", StringComparison.Ordinal))
            {
                return label;
            }
        }

        throw new InvalidOperationException("No title of the sample matches exactly one game.");
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

        public string Unknown => Dutch ? "Onbekende locatie" : "Unknown location";

        public string Of => Dutch ? "van" : "of";

        public static Words For(string language) => new(language);

        public string ActiveName(int active) => Dutch ? $"Filters · {active} actief" : $"Filters · {active} active";

        public Regex FullCount() => Dutch ? DutchFullCount() : EnglishFullCount();

        public Regex RestCount() => Dutch ? DutchRestCount() : EnglishRestCount();
    }

    [GeneratedRegex(@"^(\d+) of (\d+) games?$")]
    private static partial Regex EnglishFullCount();

    [GeneratedRegex(@"^(\d+) van (\d+) spel(?:len)?$")]
    private static partial Regex DutchFullCount();

    [GeneratedRegex(@"^(\d+) games?$")]
    private static partial Regex EnglishRestCount();

    [GeneratedRegex(@"^(\d+) spel(?:len)?$")]
    private static partial Regex DutchRestCount();

    [GeneratedRegex(@"^van \d+$")]
    private static partial Regex VanCount();
}

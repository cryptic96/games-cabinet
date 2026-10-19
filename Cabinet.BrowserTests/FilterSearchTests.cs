using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that typing in the search field leaves the matching families in the light, puts every other box in shadow
/// where it stands, counts the matches the way the screen-reader list counts its entries, and never takes a shadowed box away from
/// the people who read or open it.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterSearchTests : CabinetPageTest
{
    private const string Boxes = "#cabinet .placement";
    private const string OpenCard = "dialog.card-dialog[open]";
    private const string PanelCount = "#filter-panel .filter-count";
    private const string PanelClear = "#filter-panel .filter-clear";
    private const string PanelLayout = "() => ['#filter-panel', '#filter-panel .filter-panel-head', '#filter-q', '#filter-panel .filter-count'].map((selector) => { const r = document.querySelector(selector).getBoundingClientRect(); return [selector, r.left, r.top, r.width, r.height].join(':'); })";
    private const string BoxRectangles = "() => [...document.querySelectorAll('#cabinet .placement')].map((box) => { const r = box.getBoundingClientRect(); return [box.dataset.entryId, box.dataset.kind, r.left, r.top, r.width, r.height].join(':'); })";
    private const string InstallAnnouncementRecorder = "() => { window.__announced = []; const live = document.getElementById('filter-live'); new MutationObserver(() => { if (live.textContent !== '') { window.__announced.push({ text: live.textContent, at: performance.now() }); } }).observe(live, { childList: true, characterData: true, subtree: true }); }";
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task A_typed_fragment_lights_its_family_and_shadows_the_rest_in_place()
    {
        await OpenAsync("/?sample=65", DockWidths.Push, 900);
        await OpenFilterPanelAsync();
        var cards = await LoadCardsAsync();
        var fragment = FragmentOfABaseGameWithAnExpansion(cards);
        var expected = ExpectedResult(cards, fragment);
        expected.LitBoxCount.Should().BeGreaterThan(0);
        expected.LitEntries.Should().BeLessThan(expected.TotalEntries);

        var noun = expected.TotalEntries == 1 ? "game" : "games";
        await Expect(Page.Locator(PanelCount)).ToHaveTextAsync($"{expected.TotalEntries} {noun}");
        await Expect(Page.Locator(PanelClear)).ToBeHiddenAsync();
        (await Page.Locator(".filter-bar").GetAttributeAsync("data-active")).Should().BeNull();
        (await Page.Locator(Boxes + "[data-dim]").CountAsync()).Should().Be(0);
        var panelBefore = await PanelLayoutAsync();
        var rectanglesBefore = await Page.EvaluateAsync<string[]>(BoxRectangles);

        await Page.Locator("#filter-q").FillAsync(fragment);

        await Expect(Page.Locator(PanelCount)).ToHaveTextAsync($"{expected.LitEntries} of {expected.TotalEntries} {noun}");
        var dimmed = await Page.Locator(Boxes + "[data-dim]").EvaluateAllAsync<string[]>("(boxes) => boxes.map((box) => box.dataset.entryId)");
        var all = await Page.Locator(Boxes).EvaluateAllAsync<string[]>("(boxes) => boxes.map((box) => box.dataset.entryId)");
        dimmed.Should().BeEquivalentTo(all.Where(id => !expected.LitEntryIds.Contains(id)));
        all.Where(id => expected.LitEntryIds.Contains(id)).Should().NotBeEmpty();
        (await Page.EvaluateAsync<string[]>(BoxRectangles)).Should().Equal(rectanglesBefore);
        await Expect(Page.Locator(".filter-bar")).ToHaveAttributeAsync("data-active", string.Empty);
        await Expect(Page.Locator(PanelClear)).ToBeVisibleAsync();
        (await PanelLayoutAsync()).Should().Equal(panelBefore);
        (await Page.Locator(PanelCount).GetAttributeAsync("aria-hidden")).Should().Be("true");
        await SaveScreenshotAsync("filter-search-typed");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_box_in_shadow_keeps_its_name_and_opens_its_card()
    {
        await OpenAsync("/?sample=65", DockWidths.Push, 900);
        await OpenFilterPanelAsync();
        var cards = await LoadCardsAsync();
        await Page.Locator("#filter-q").FillAsync(FragmentOfABaseGameWithAnExpansion(cards));
        var shadowed = Page.Locator(Boxes + "[data-dim]:not([data-kind=\"moreMarker\"])").First;
        await Expect(shadowed).ToBeAttachedAsync();

        var name = await shadowed.GetAttributeAsync("aria-label");
        name.Should().NotBeNullOrWhiteSpace();
        (await shadowed.GetAttributeAsync("title")).Should().Be(name);
        (await shadowed.GetAttributeAsync("aria-disabled")).Should().BeNull();
        (await shadowed.EvaluateAsync<bool>("(box) => box.disabled")).Should().BeFalse();
        (await shadowed.EvaluateAsync<string>("(box) => getComputedStyle(box).cursor")).Should().Be("pointer");
        (await shadowed.EvaluateAsync<string>("(box) => box.lastElementChild.getAttribute('aria-hidden')")).Should().Be("true");

        await shadowed.ClickAsync();

        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Quick_typing_is_announced_once_after_it_stops()
    {
        await OpenAsync("/?sample=65", DockWidths.Push, 900);
        await OpenFilterPanelAsync();
        var cards = await LoadCardsAsync();
        var title = FragmentOfABaseGameWithAnExpansion(cards);
        await Page.EvaluateAsync(InstallAnnouncementRecorder);
        await Page.Locator("#filter-q").FocusAsync();

        for (var length = 1; length <= 4; length++)
        {
            await Page.Keyboard.PressAsync(title[length - 1].ToString());
            var prefix = title[..length];
            var expected = ExpectedResult(cards, prefix);
            var noun = expected.TotalEntries == 1 ? "game" : "games";
            var shown = expected.LitEntries == 0 ? "No games match" : $"{expected.LitEntries} of {expected.TotalEntries} {noun}";
            await Expect(Page.Locator(PanelCount)).ToHaveTextAsync(shown);
        }

        var lastKeyAt = await Page.EvaluateAsync<double>("() => performance.now()");
        (await Page.EvaluateAsync<int>("() => window.__announced.length")).Should().Be(0);

        await Expect(Page.Locator("#filter-live")).Not.ToBeEmptyAsync(new LocatorAssertionsToBeEmptyOptions { Timeout = 3000 });
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        var announced = await Page.EvaluateAsync<JsonElement>("() => window.__announced");
        announced.GetArrayLength().Should().Be(1);
        var final = ExpectedResult(cards, title[..4]);
        var finalNoun = final.TotalEntries == 1 ? "game" : "games";
        announced[0].GetProperty("text").GetString().Should().Be(final.LitEntries == 0 ? "No games match" : $"{final.LitEntries} of {final.TotalEntries} {finalNoun}");
        (announced[0].GetProperty("at").GetDouble() - lastKeyAt).Should().BeGreaterThan(450);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Clear_restores_the_light_announces_and_keeps_focus_in_the_field()
    {
        await OpenAsync("/?sample=65", DockWidths.Push, 900);
        await OpenFilterPanelAsync();
        var cards = await LoadCardsAsync();
        var fragment = FragmentOfABaseGameWithAnExpansion(cards);
        var total = ExpectedResult(cards, fragment).TotalEntries;
        var panelBefore = await PanelLayoutAsync();
        await Page.Locator("#filter-q").FillAsync(fragment);
        await Expect(Page.Locator(".filter-bar")).ToHaveAttributeAsync("data-active", string.Empty);
        (await Page.Locator(Boxes + "[data-dim]").CountAsync()).Should().BeGreaterThan(0);
        (await Page.Locator(PanelClear).GetAttributeAsync("aria-label")).Should().Be("Clear filters");
        await Expect(Page.Locator(PanelClear)).ToHaveTextAsync("Clear");

        await Page.Locator(PanelClear).ClickAsync();

        await Expect(Page.Locator("#filter-q")).ToHaveValueAsync(string.Empty);
        await Expect(Page.Locator(Boxes + "[data-dim]")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".filter-bar")).Not.ToHaveAttributeAsync("data-active", string.Empty);
        await Expect(Page.Locator(PanelClear)).ToBeHiddenAsync();
        await Expect(Page.Locator(PanelCount)).ToHaveTextAsync($"{total} games");
        (await PanelLayoutAsync()).Should().Equal(panelBefore);
        await Expect(Page.Locator("#filter-live")).ToHaveTextAsync($"Filters cleared, {total} games");
        await Expect(Page.Locator("#filter-q")).ToBeFocusedAsync();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Escape_and_slash_behave_as_promised()
    {
        await OpenAsync("/?sample=65", DockWidths.Push, 900);
        await OpenFilterPanelAsync();
        var field = Page.Locator("#filter-q");
        var hint = Page.Locator(".filter-hint");

        await Expect(hint).ToHaveCSSAsync("display", "block");
        await Expect(hint).ToHaveCSSAsync("font-size", "14px");
        await Expect(hint).ToHaveCSSAsync("font-weight", "400");
        var muted = await Page.EvaluateAsync<string>("() => { const probe = document.createElement('span'); probe.style.color = 'var(--room-muted)'; document.body.append(probe); const colour = getComputedStyle(probe).color; probe.remove(); return colour; }");
        await Expect(hint).ToHaveCSSAsync("color", muted);
        var fieldBox = (await field.BoundingBoxAsync())!;
        var hintBox = (await hint.BoundingBoxAsync())!;
        hintBox.X.Should().BeGreaterThanOrEqualTo(fieldBox.X);
        (hintBox.X + hintBox.Width).Should().BeLessThanOrEqualTo(fieldBox.X + fieldBox.Width);
        hintBox.Y.Should().BeGreaterThanOrEqualTo(fieldBox.Y);
        (hintBox.Y + hintBox.Height).Should().BeLessThanOrEqualTo(fieldBox.Y + fieldBox.Height);
        (hintBox.X + (hintBox.Width / 2)).Should().BeGreaterThan(fieldBox.X + (fieldBox.Width * 0.75f));

        await field.FocusAsync();
        await Expect(hint).ToHaveCSSAsync("display", "none");
        await Page.Keyboard.TypeAsync("lan");
        await Expect(Page.Locator(Boxes + "[data-dim]").First).ToBeAttachedAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(field).ToHaveValueAsync(string.Empty);
        await Expect(Page.Locator(Boxes + "[data-dim]")).ToHaveCountAsync(0);
        await Expect(field).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("Escape");
        await Expect(field).ToHaveValueAsync(string.Empty);
        await Expect(field).ToBeFocusedAsync();

        await Page.Keyboard.TypeAsync("abc");
        await Page.Locator("h1").ClickAsync();
        await Expect(field).Not.ToBeFocusedAsync();
        await Expect(hint).ToHaveCSSAsync("display", "none");

        await Page.Keyboard.PressAsync("/");
        await Expect(field).ToBeFocusedAsync();
        await Expect(field).ToHaveValueAsync("abc");
        (await field.EvaluateAsync<string>("(input) => input.selectionStart + ':' + input.selectionEnd")).Should().Be("0:3");

        await Page.Keyboard.PressAsync("End");
        await Page.Keyboard.PressAsync("/");
        await Expect(field).ToHaveValueAsync("abc/");

        await Page.Locator("h1").ClickAsync();
        await Page.Keyboard.PressAsync("Control+/");
        await Expect(field).Not.ToBeFocusedAsync();
        await Expect(field).ToHaveValueAsync("abc/");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Nothing_matching_shadows_everything_and_highlights_Clear()
    {
        await OpenAsync("/?sample=65", DockWidths.Push, 900);
        await OpenFilterPanelAsync();

        await Page.Locator("#filter-q").FillAsync("zzzzqxqx");

        await Expect(Page.Locator(PanelCount)).ToHaveTextAsync("No games match");
        await Expect(Page.Locator(PanelClear)).ToHaveAttributeAsync("data-highlighted", string.Empty);
        await Expect(Page.Locator(PanelClear)).ToHaveCSSAsync("font-weight", "600");
        (await Page.Locator(Boxes + ":not([data-dim])").CountAsync()).Should().Be(0);
        (await Page.Locator(Boxes).CountAsync()).Should().BeGreaterThan(10);
        await SaveScreenshotAsync("filter-search-no-match");

        await Page.Locator("#filter-q").FillAsync("zzzzqxq");
        await Expect(Page.Locator(PanelClear)).ToHaveAttributeAsync("data-highlighted", string.Empty);

        await Page.Locator("#filter-q").FillAsync(string.Empty);
        await Expect(Page.Locator(PanelClear)).Not.ToHaveAttributeAsync("data-highlighted", string.Empty);
        await Expect(Page.Locator(Boxes + "[data-dim]")).ToHaveCountAsync(0);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Enter_closes_the_keyboard_only_on_a_touch_screen()
    {
        await OpenAsync("/?sample=65", DockWidths.Push, 900);
        await OpenFilterPanelAsync();
        await Page.Locator("#filter-q").FocusAsync();
        await Page.Keyboard.TypeAsync("lan");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#filter-q")).ToBeFocusedAsync();

        var phone = await NewContext(new BrowserNewContextOptions
        {
            HasTouch = true,
            IsMobile = true,
            ViewportSize = new ViewportSize { Width = 390, Height = 844 },
        });
        await phone.SetExtraHTTPHeadersAsync(new Dictionary<string, string> { ["Accept-Encoding"] = "identity" });
        var touchPage = await phone.NewPageAsync();
        await touchPage.GotoAsync($"http://127.0.0.1:{PublicPort}/?sample=65");
        await Expect(touchPage.Locator(".filter-bar")).ToBeVisibleAsync();
        await Expect(touchPage.Locator(Boxes).First).ToBeAttachedAsync();
        await Expect(touchPage.Locator(".filter-hint")).ToHaveCSSAsync("display", "none");

        await touchPage.Locator("#filter-q").FocusAsync();
        await touchPage.Keyboard.TypeAsync("lan");
        await Expect(touchPage.Locator("#filter-q")).ToHaveValueAsync("lan");
        await touchPage.Keyboard.PressAsync("Enter");

        await Expect(touchPage.Locator("#filter-q")).Not.ToBeFocusedAsync();
        await Expect(touchPage.Locator("#filter-q")).ToHaveValueAsync("lan");
        await Expect(touchPage.Locator(".filter-hint")).ToHaveCSSAsync("display", "none");
    }

    /// <summary>
    /// Reads where the panel, its head row, the search field and the count line stand, so a test can prove that starting or clearing
    /// a filter moves none of them.
    /// </summary>
    private async Task<string[]> PanelLayoutAsync() => await Page.EvaluateAsync<string[]>(PanelLayout);

    private static string FragmentOfABaseGameWithAnExpansion(IReadOnlyList<CardData> cards)
    {
        var baseGame = cards.First(card => !card.IsExpansion && card.Expansions.Any(link => link.EntryId is not null));

        return baseGame.Title;
    }

    private static ExpectedFilter ExpectedResult(IReadOnlyList<CardData> cards, string fragment)
    {
        var byEntry = cards.ToDictionary(card => card.EntryId);
        var families = new Dictionary<long, HashSet<long>>();

        foreach (var card in cards.Where(card => !card.IsExpansion))
        {
            var family = new HashSet<long> { card.EntryId };
            family.UnionWith(card.Expansions.Where(link => link.EntryId is not null && byEntry.ContainsKey(link.EntryId.Value)).Select(link => link.EntryId!.Value));
            families[card.EntryId] = family;
        }

        var litFamilies = families.Where(pair => pair.Value.Any(entry => Contains(byEntry[entry].Title, fragment))).Select(pair => pair.Key).ToHashSet();
        var lit = new HashSet<long>();

        foreach (var card in cards)
        {
            var belongs = families.Where(pair => pair.Value.Contains(card.EntryId)).Select(pair => pair.Key).ToList();
            var isLit = belongs.Count > 0 ? belongs.Any(litFamilies.Contains) : Contains(card.Title, fragment);

            if (isLit)
            {
                lit.Add(card.EntryId);
            }
        }

        var nested = cards.Where(card => !card.IsExpansion).SelectMany(card => card.Expansions).Where(link => link.EntryId is not null && byEntry.ContainsKey(link.EntryId.Value)).Select(link => link.EntryId!.Value).ToHashSet();
        var topLevel = cards.Where(card => !nested.Contains(card.EntryId)).ToList();
        var litTopLevel = topLevel.Count(card => lit.Contains(card.EntryId) || (families.TryGetValue(card.EntryId, out var family) && family.Any(lit.Contains)));

        return new ExpectedFilter(topLevel.Count, litTopLevel, lit.Select(entry => entry.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToHashSet(StringComparer.Ordinal));
    }

    private static bool Contains(string title, string fragment) =>
        title.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Opens the cabinet with an empty index of other titles, so these tests judge the search on the titles alone and an index
    /// arriving mid-typing cannot light extra games.
    /// </summary>
    private async Task OpenAsync(string path, int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await Page.RouteAsync("**/cabinet/search-names**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "application/json",
            Body = "{\"names\":{}}",
        }));
        await GotoCabinetAsync(path);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
    }

    private async Task<IReadOnlyList<CardData>> LoadCardsAsync()
    {
        var json = await Page.EvaluateAsync<JsonElement>(
            "async address => (await (await fetch(address)).json()).cards",
            "/cabinet/cards?profile=desktop&sample=65");

        return JsonSerializer.Deserialize<List<CardData>>(json.GetRawText(), WebOptions)!;
    }

    private sealed record CardLink(long? EntryId, int GameId, string Title);

    private sealed record CardData(long EntryId, string Title, bool IsExpansion, List<CardLink> Expansions);

    private sealed record ExpectedFilter(int TotalEntries, int LitEntries, HashSet<string> LitEntryIds)
    {
        public int LitBoxCount => LitEntryIds.Count;
    }
}

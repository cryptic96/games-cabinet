using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the filters last for the tab and that the cabinet never shows an unfiltered frame first: a reload
/// keeps the chosen chip and draws every box already in shadow, a new tab starts clean, the filters live only in the tab's session
/// storage and never in the address or a cookie, and storage that refuses to answer breaks nothing.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterPersistenceTests : CabinetPageTest
{
    private const string Boxes = "#cabinet .placement";
    private const string Dimmed = "#cabinet .placement[data-dim]";
    private const string StorageKey = "cabinet-filters";
    private const string Select = "#filter-loc";

    private const string InstallDrawRecorder = """
        (() => {
          const rec = { batches: [], toggles: [], inserted: [], cardRequests: 0 };
          window.__rec = rec;
          const originalFetch = window.fetch;
          window.fetch = (...args) => {
            if (String(args[0]).includes('/cabinet/cards')) { rec.cardRequests += 1; }
            return originalFetch.apply(window, args);
          };
          new MutationObserver((records) => {
            let inserted = 0;
            let shown = false;
            for (const record of records) {
              if (record.type === 'childList') {
                for (const node of record.addedNodes) {
                  if (!(node instanceof Element)) { continue; }
                  const boxes = node.matches('.placement') ? [node] : [...node.querySelectorAll('.placement')];
                  for (const box of boxes) {
                    inserted += 1;
                    rec.inserted.push({ id: box.dataset.entryId, dim: box.hasAttribute('data-dim') });
                  }
                }
              } else if (record.type === 'attributes') {
                if (record.attributeName === 'hidden' && record.target.classList.contains('filter-bar') && record.oldValue !== null && !record.target.hasAttribute('hidden')) {
                  shown = true;
                }
                if (record.attributeName === 'data-dim' && record.target.classList.contains('placement') && record.target.isConnected) {
                  rec.toggles.push(record.target.dataset.entryId);
                }
              }
            }
            if (inserted > 0 || shown) {
              const first = inserted > 0 && rec.batches.every((batch) => batch.inserted === 0);
              rec.batches.push({
                inserted,
                shown,
                cardRequests: rec.cardRequests,
                resources: first ? performance.getEntriesByType('resource').filter((entry) => entry.name.includes('/cabinet/')).map((entry) => ({ name: entry.name, start: entry.startTime, end: entry.responseEnd })) : null,
              });
            }
          }).observe(document, { childList: true, subtree: true, attributes: true, attributeOldValue: true, attributeFilter: ['hidden', 'data-dim'] });
        })();
        """;

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    [Fact]
    public async Task A_reload_keeps_the_filter_and_draws_the_first_frame_dimmed()
    {
        var address = await OpenAsync("/?sample=65", 1440, 900);
        var first = await ReadRecorderAsync();
        AssertBarShownWithTheFirstDraw(first);
        first.Toggles.Should().BeEmpty("nothing was stored, so nothing was dimmed");

        await Page.Locator("fieldset[data-filter=\"players\"] input[value=\"4\"]").ClickAsync();
        await Expect(Page.Locator(Dimmed).First).ToBeAttachedAsync();
        var dimmedBefore = await BoxIdsAsync(Dimmed);
        dimmedBefore.Should().NotBeEmpty();
        await Page.WaitForFunctionAsync($"() => sessionStorage.getItem('{StorageKey}') !== null");

        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync(Boxes, new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();

        await Expect(Page.Locator("fieldset[data-filter=\"players\"] input[value=\"4\"]")).ToBeCheckedAsync();
        (await BoxIdsAsync(Dimmed)).Should().BeEquivalentTo(dimmedBefore);

        var reloaded = await ReadRecorderAsync();
        reloaded.Toggles.Should().BeEmpty("every box that is dimmed was inserted dimmed");
        reloaded.Inserted.Where(box => box.Dim).Select(box => box.Id).Should().BeEquivalentTo(dimmedBefore);
        AssertBarShownWithTheFirstDraw(reloaded);

        var resources = reloaded.Batches.First(batch => batch.Inserted > 0).Resources!;
        var layout = resources.Single(entry => entry.Name.Contains("/cabinet/layout", StringComparison.Ordinal));
        var cards = resources.Single(entry => entry.Name.Contains("/cabinet/cards", StringComparison.Ordinal));
        cards.Start.Should().BeLessThan(layout.End, "the card data was asked for while the layout was still on its way");
        cards.End.Should().BeGreaterThan(0, "the card data had arrived before the first box was inserted");

        Page.Url.Should().Be(address);
        (await Context.CookiesAsync()).Should().NotContain(cookie => cookie.Name.Contains("filter", StringComparison.OrdinalIgnoreCase));
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_new_tab_starts_clean()
    {
        var address = await OpenAsync("/?sample=65", 1440, 900);
        await Page.Locator("fieldset[data-filter=\"players\"] input[value=\"4\"]").ClickAsync();
        await Expect(Page.Locator(Dimmed).First).ToBeAttachedAsync();
        await Page.WaitForFunctionAsync($"() => sessionStorage.getItem('{StorageKey}') !== null");

        var other = await Context.NewPageAsync();
        await other.GotoAsync(address);
        await other.WaitForSelectorAsync(Boxes, new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
        await other.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(other.Locator("fieldset[data-filter=\"players\"] input[value=\"\"]")).ToBeCheckedAsync();
        (await other.Locator(Dimmed).CountAsync()).Should().Be(0);
        (await other.EvaluateAsync<string?>($"() => sessionStorage.getItem('{StorageKey}')")).Should().BeNull();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Failed_card_data_disables_only_the_card_filters()
    {
        await StartAsync(PrototypeOn);
        var fragment = await TitleFragmentAsync();
        var requestCount = await FailCardDataRequestsAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        await PrepareAsync(StoredFilters(fragment, players: 4));

        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();

        var recording = await ReadRecorderAsync();
        AssertBarShownWithTheFirstDraw(recording);
        var firstDraw = recording.Batches.First(batch => batch.Inserted > 0);
        firstDraw.CardRequests.Should().Be(3, "the first try and one retry per delay had failed before the cabinet drew");
        requestCount().Should().Be(3);
        recording.Toggles.Should().BeEmpty("every box that is dimmed was inserted dimmed");

        var dimmed = await BoxIdsAsync(Dimmed);
        dimmed.Should().NotBeEmpty();
        recording.Inserted.Where(box => box.Dim).Select(box => box.Id).Should().BeEquivalentTo(dimmed);
        (await Page.Locator(".filter-group input").EvaluateAllAsync<bool[]>("(inputs) => inputs.map((input) => input.disabled)")).Should().OnlyContain(disabled => disabled);
        (await Page.Locator("#filter-loc").IsDisabledAsync()).Should().BeTrue();
        await Expect(Page.Locator(".filter-note")).ToBeVisibleAsync();
        await Expect(Page.Locator(".filter-note")).ToHaveTextAsync("Couldn't load the details. Reload the page to try again.");
        await Expect(Page.Locator("fieldset[data-filter=\"players\"] input[value=\"\"]")).ToBeCheckedAsync();
        (await Page.Locator("fieldset[data-filter=\"players\"] .filter-chip").First.EvaluateAsync<string>("(chip) => getComputedStyle(chip).borderStyle")).Should().Be("dashed");
        await Expect(Page.Locator("#filter-q")).ToHaveValueAsync(fragment);
        var count = await Page.Locator(".filter-count").InnerTextAsync();
        count.Should().MatchRegex(@"^\d+ of \d+ games?$");

        await Page.Locator("#filter-q").FillAsync(string.Empty);
        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(0);
        await Page.Locator("#filter-q").FillAsync(fragment);
        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(dimmed.Length);
        (await BoxIdsAsync(Dimmed)).Should().BeEquivalentTo(dimmed, "the search alone decides, so typing the same text again dims the same boxes");
        await Expect(Page.Locator(".filter-count")).ToHaveTextAsync(count);

        await Page.Locator(".lang-toggle a[lang=nl]").ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "nl");
        await Expect(Page.Locator(".filter-note")).ToHaveTextAsync("De details konden niet worden geladen. Laad de pagina opnieuw om het nog eens te proberen.", new LocatorAssertionsToHaveTextOptions { Timeout = 20000 });
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(1440)]
    public async Task Failed_card_data_keeps_the_pinned_bar_at_112_px(int width)
    {
        await StartAsync(PrototypeOn);
        await FailCardDataRequestsAsync();
        await Page.SetViewportSizeAsync(width, 900);

        await GotoCabinetAsync("/?sample=65");
        await Expect(Page.Locator(".filter-note")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 20000 });
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        (await Page.EvaluateAsync<double>("() => window.scrollY")).Should().Be(0);
        await AssertNoteUnderBarAsync();

        await Page.EvaluateAsync("() => window.scrollTo(0, document.querySelector('.filter-sentinel').getBoundingClientRect().top + window.scrollY + 2)");
        await Expect(Page.Locator(".filter-bar")).ToHaveAttributeAsync("data-stuck", string.Empty);
        await AssertNoteUnderBarAsync();

        await Page.EvaluateAsync("() => window.scrollTo(0, 700)");
        (await Page.Locator(".filter-bar").BoundingBoxAsync())!.Height.Should().Be(112);
    }

    [Fact]
    public async Task A_failed_layout_keeps_the_bar_hidden()
    {
        await StartAsync(PrototypeOn);
        await Page.RouteAsync("**/cabinet/layout**", route => route.AbortAsync());
        await Page.SetViewportSizeAsync(1440, 900);

        await GotoCabinetAsync("/?sample=65");

        await Expect(Page.Locator(".cabinet-retry")).ToBeVisibleAsync();
        await Expect(Page.Locator(".filter-bar")).ToBeHiddenAsync();
        await Expect(Page.Locator(".filter-note")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task The_language_switch_keeps_the_question()
    {
        await OpenAsync("/?sample=65", 1440, 900);
        var fragment = await TitleFragmentAsync();
        await Page.Locator("#filter-q").FillAsync(fragment);
        await Page.Locator("fieldset[data-filter=\"time\"] input[value=\"60\"]").ClickAsync();
        await Expect(Page.Locator(Dimmed).First).ToBeAttachedAsync();
        var dimmed = await BoxIdsAsync(Dimmed);
        await Page.WaitForFunctionAsync($"() => JSON.parse(sessionStorage.getItem('{StorageKey}') ?? 'null')?.time === 60");

        await Page.Locator(".lang-toggle a[lang=nl]").ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "nl");
        await Page.WaitForSelectorAsync(Boxes, new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();

        await Expect(Page.Locator("#filter-q")).ToHaveValueAsync(fragment);
        await Expect(Page.Locator("fieldset[data-filter=\"time\"] input[value=\"60\"]")).ToBeCheckedAsync();
        (await BoxIdsAsync(Dimmed)).Should().BeEquivalentTo(dimmed);
        var recording = await ReadRecorderAsync();
        recording.Toggles.Should().BeEmpty("the Dutch page drew its boxes already in shadow");
        recording.Inserted.Where(box => box.Dim).Select(box => box.Id).Should().BeEquivalentTo(dimmed);
        AssertBarShownWithTheFirstDraw(recording);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task An_owner_move_redraws_with_the_filter_and_the_scroll_kept()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        var host = await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(120));
        var places = await host.SeedAsync("Cabinet A", "Shelf B");
        var games = host.Cards.DistinctBy(card => card.GameId).Take(3).ToList();
        var moved = games[0];
        await host.PlaceAsync(moved, places[1]);
        await host.PlaceAsync(games[1], places[1]);
        await host.PlaceAsync(games[2], places[0]);
        await PrepareAsync();
        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();

        await Page.Locator(Select).SelectOptionAsync(places[1]);
        await Expect(Page.Locator(Dimmed).First).ToBeAttachedAsync();
        var movedId = (await EntryIdsAsync(moved.GameId))[0];
        var movedBox = Page.Locator($"{Boxes}[data-entry-id=\"{movedId}\"]").First;
        (await movedBox.GetAttributeAsync("data-dim")).Should().BeNull("the moved copy stands in the chosen place before the move");
        var countBefore = await Page.Locator(".filter-count").InnerTextAsync();
        await Page.EvaluateAsync("() => window.scrollTo(0, 400)");
        await Page.WaitForFunctionAsync("() => window.scrollY > 300");
        var scrolledBefore = await Page.EvaluateAsync<double>("() => window.scrollY");
        await Page.EvaluateAsync("() => { window.__rec.inserted.length = 0; window.__rec.toggles.length = 0; window.__rec.batches.length = 0; }");

        await host.PlaceAsync(moved, places[0]);
        host.TellOpenPages();

        await Page.WaitForFunctionAsync("() => window.__rec.inserted.length > 0", null, new PageWaitForFunctionOptions { Timeout = 20000 });
        await Expect(movedBox).ToHaveAttributeAsync("data-dim", string.Empty);
        var recording = await ReadRecorderAsync();
        recording.Toggles.Should().BeEmpty("the redraw drew its boxes already in shadow");
        var dimmedNow = await BoxIdsAsync(Dimmed);
        recording.Inserted.Where(box => box.Dim).Select(box => box.Id).Should().BeEquivalentTo(dimmedNow);
        recording.Inserted.First(box => box.Id == movedId).Dim.Should().BeTrue("the moved copy was dimmed in the first frame of the redraw");
        await Expect(Page.Locator(Select)).ToHaveValueAsync(places[1]);
        await Expect(Page.Locator(".filter-count")).Not.ToHaveTextAsync(countBefore);
        var scrolledAfter = await Page.EvaluateAsync<double>("() => window.scrollY");
        Math.Abs(scrolledAfter - scrolledBefore).Should().BeLessThan(900);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_deleted_location_falls_back_to_Any()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        var host = await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(20));
        var places = await host.SeedAsync("Cabinet A", "Shelf B");
        var games = host.Cards.DistinctBy(card => card.GameId).Take(2).ToList();
        await host.PlaceAsync(games[0], places[0]);
        await host.PlaceAsync(games[1], places[1]);
        await PrepareAsync();
        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        await Page.Locator(Select).SelectOptionAsync(places[1]);
        await Expect(Page.Locator(Dimmed).First).ToBeAttachedAsync();

        await host.DeleteLocationAsync(places[1]);
        host.TellOpenPages();

        await Expect(Page.Locator(Select)).ToHaveValueAsync(string.Empty, new LocatorAssertionsToHaveValueOptions { Timeout = 20000 });
        await Expect(Page.Locator($"{Select} option")).ToHaveCountAsync(3);
        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(0);
        await Page.WaitForFunctionAsync($"() => JSON.parse(sessionStorage.getItem('{StorageKey}') ?? 'null')?.location == null");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Without_locations_the_location_control_goes_away()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        var host = await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(20));
        var places = await host.SeedAsync("Cabinet A", "Shelf B");
        var games = host.Cards.DistinctBy(card => card.GameId).Take(2).ToList();
        await host.PlaceAsync(games[0], places[0]);
        await host.PlaceAsync(games[1], places[1]);
        await PrepareAsync();
        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(Select)).ToBeVisibleAsync();

        await host.DeleteLocationAsync(places[0]);
        await host.DeleteLocationAsync(places[1]);
        host.TellOpenPages();

        await Expect(Page.Locator(Select)).ToBeHiddenAsync(new LocatorAssertionsToBeHiddenOptions { Timeout = 20000 });
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        ConsoleErrors.Should().BeEmpty();
    }

    private async Task<string[]> EntryIdsAsync(int gameId) =>
        await Page.EvaluateAsync<string[]>(
            "async (id) => (await (await fetch('/cabinet/cards?profile=desktop')).json()).cards.filter((card) => card.gameId === id).map((card) => String(card.entryId))",
            gameId);

    private async Task AssertNoteUnderBarAsync()
    {
        var bar = Page.Locator(".filter-bar");
        var note = Page.Locator(".filter-note");
        (await note.EvaluateAsync<bool>("(note) => note.previousElementSibling === document.querySelector('.filter-bar')")).Should().BeTrue("the note is the bar's next sibling");
        (await note.EvaluateAsync<bool>("(note) => document.querySelector('.filter-bar').contains(note)")).Should().BeFalse();
        var barBox = (await bar.BoundingBoxAsync())!;
        var noteBox = (await note.BoundingBoxAsync())!;
        noteBox.Y.Should().BeGreaterThanOrEqualTo(barBox.Y + barBox.Height);
        noteBox.Y.Should().BeLessThanOrEqualTo(barBox.Y + barBox.Height + 17);
        barBox.Height.Should().Be(112);
    }

    private async Task<string> TitleFragmentAsync()
    {
        var response = await Context.APIRequest.GetAsync($"http://127.0.0.1:{PublicPort}/cabinet/cards?profile=desktop&sample=65");
        var document = await response.JsonAsync();
        var title = document!.Value.GetProperty("cards").EnumerateArray()
            .Select(card => card.GetProperty("title").GetString()!)
            .First(candidate => candidate.Length >= 5);

        return title[..5].ToLowerInvariant();
    }

    private static string StoredFilters(string query, int? players = null, int? time = null) =>
        JsonSerializer.Serialize(new { q = query, players, time, location = (string?)null });

    private async Task PrepareAsync(string? stored = null)
    {
        await Page.RouteAsync("**/cabinet/search-names**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "application/json",
            Body = "{\"names\":{}}",
        }));
        await Page.AddInitScriptAsync(InstallDrawRecorder);

        if (stored is not null)
        {
            await Page.AddInitScriptAsync($"if (sessionStorage.getItem('{StorageKey}') === null) {{ sessionStorage.setItem('{StorageKey}', {JsonSerializer.Serialize(stored)}); }}");
        }
    }

    private static void AssertBarShownWithTheFirstDraw(Recording recording)
    {
        var firstDraw = recording.Batches.FindIndex(batch => batch.Inserted > 0);
        firstDraw.Should().BeGreaterThanOrEqualTo(0, "the cabinet drew");
        recording.Batches[firstDraw].Shown.Should().BeTrue("the bar was shown in the same task as the first draw");
        recording.Batches.Take(firstDraw).Should().OnlyContain(batch => !batch.Shown, "the bar stayed hidden until the cabinet drew");
    }

    private async Task<string> OpenAsync(string path, int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await PrepareAsync();
        var address = await GotoCabinetAsync(path);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();

        return address;
    }

    private async Task<Recording> ReadRecorderAsync()
    {
        var json = await Page.EvaluateAsync<JsonElement>("() => window.__rec");

        return JsonSerializer.Deserialize<Recording>(json.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private Task<string[]> BoxIdsAsync(string selector) =>
        Page.Locator(selector).EvaluateAllAsync<string[]>("(boxes) => boxes.map((box) => box.dataset.entryId)");

    private sealed record Recording(List<Batch> Batches, List<string> Toggles, List<InsertedBox> Inserted);

    private sealed record Batch(int Inserted, bool Shown, int CardRequests, List<ResourceTiming>? Resources);

    private sealed record InsertedBox(string Id, bool Dim);

    private sealed record ResourceTiming(string Name, double Start, double End);
}

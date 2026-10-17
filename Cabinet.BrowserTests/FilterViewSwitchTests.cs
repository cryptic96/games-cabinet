using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the visitor's view is remembered on the device and read before anything is fetched, so the wrong
/// cabinet never flashes; that switching is a calm cross-fade of the cabinet area that closes an open card first, scrolls up but never
/// down, and is instant under reduced motion; and that a payload that cannot be had keeps the all-together cabinet on screen, says so,
/// and leaves what the device remembers alone.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterViewSwitchTests : CabinetPageTest
{
    private const string Boxes = "#cabinet .placement";
    private const string Groups = "#cabinet section.location-group";
    private const string Sections = "#cabinet .section";
    private const string Switch = ".filter-view";
    private const string ViewKey = "cabinet-view";
    private const string FiltersKey = "cabinet-filters";
    private const string Dimmed = "#cabinet .placement[data-dim]";
    private const string FailureCopy = "Couldn't load the location view. Showing everything together.";
    private const string LocationRadio = "input[name=\"filter-view\"][value=\"location\"]";
    private const string AllRadio = "input[name=\"filter-view\"][value=\"all\"]";

    private const string InstallRecorder = """
        (() => {
          const rec = { batches: [], toggles: [], inserted: [], cardOpenAtGroups: [] };
          window.__rec = rec;
          new MutationObserver((records) => {
            let inserted = 0;
            let shown = false;
            let groups = 0;
            let plain = 0;
            for (const record of records) {
              if (record.type === 'childList') {
                for (const node of record.addedNodes) {
                  if (!(node instanceof Element)) { continue; }
                  const sections = [...(node.matches('.section') ? [node] : []), ...node.querySelectorAll('.section')];
                  plain += sections.filter((section) => section.closest('.location-group') === null).length;
                  groups += (node.matches('.location-group') ? [node] : [...node.querySelectorAll('.location-group')]).length;
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
            if (groups > 0) {
              rec.cardOpenAtGroups.push(document.querySelector('dialog.card-dialog')?.open === true);
            }
            if (inserted > 0 || shown) {
              const first = inserted > 0 && rec.batches.every((batch) => batch.inserted === 0);
              rec.batches.push({
                inserted,
                shown,
                groups,
                plain,
                resources: first ? performance.getEntriesByType('resource').filter((entry) => entry.name.includes('/cabinet/')).map((entry) => ({ name: entry.name, start: entry.startTime, end: entry.responseEnd })) : null,
              });
            }
          }).observe(document, { childList: true, subtree: true, attributes: true, attributeOldValue: true, attributeFilter: ['hidden', 'data-dim'] });
        })();
        """;

    private const string InstallTransitionSpy = """
        (() => {
          const spy = { calls: 0, marked: null, name: null, moved: null, before: null };
          window.__spy = spy;
          const measure = () => [...document.querySelectorAll('#cabinet .placement')].map((box) => {
            const rect = box.getBoundingClientRect();
            return [box.dataset.entryId, rect.left, rect.top];
          });
          window.__measure = measure;
          const original = document.startViewTransition.bind(document);
          document.startViewTransition = (callback) => {
            spy.calls += 1;
            spy.marked = document.documentElement.hasAttribute('data-switching-view');
            spy.name = getComputedStyle(document.getElementById('cabinet')).viewTransitionName;
            return original(() => {
              spy.moved = JSON.stringify(measure()) !== JSON.stringify(spy.before);
              return callback();
            });
          };
        })();
        """;

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    [Fact]
    public async Task A_remembered_location_view_opens_first_with_no_flash()
    {
        var address = await OpenSampleAsync(1440, 900, remembered: null);
        await Expect(Page.Locator(Switch)).ToBeVisibleAsync();
        await Expect(Page.Locator(AllRadio)).ToBeCheckedAsync();
        (await Page.Locator(Groups).CountAsync()).Should().Be(0, "a first visit opens All together");

        await Page.Locator(".filter-view-segment").Nth(1).ClickAsync();

        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        await Page.WaitForFunctionAsync($"() => localStorage.getItem('{ViewKey}') === 'location'");
        (await Context.CookiesAsync()).Should().NotContain(cookie => cookie.Name.Contains("view", StringComparison.OrdinalIgnoreCase));

        await Page.ReloadAsync();
        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();

        var recording = await ReadRecorderAsync();
        var firstDraw = recording.Batches.First(batch => batch.Inserted > 0);
        firstDraw.Groups.Should().BeGreaterThan(1, "the first boxes drawn stand in location groups");
        recording.Batches.Sum(batch => batch.Plain).Should().Be(0, "no section was ever inserted outside a location group");
        await Expect(Page.Locator(LocationRadio)).ToBeCheckedAsync();
        Page.Url.Should().Be(address, "a switch adds nothing to the address");
        (await Page.EvaluateAsync<string>("() => location.search + location.hash")).Should().Be("?sample=65");

        var fresh = await Browser.NewContextAsync(new BrowserNewContextOptions { ExtraHTTPHeaders = new Dictionary<string, string> { ["Accept-Encoding"] = "identity" } });

        try
        {
            var other = await fresh.NewPageAsync();
            await other.GotoAsync(address);
            await other.WaitForSelectorAsync(Boxes, new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
            await other.WaitForLoadStateAsync(LoadState.NetworkIdle);

            (await other.Locator(Groups).CountAsync()).Should().Be(0, "another device starts with All together");
            await Assertions.Expect(other.Locator(AllRadio)).ToBeCheckedAsync();
        }
        finally
        {
            await fresh.CloseAsync();
        }

        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_remembered_view_and_a_stored_filter_draw_dimmed_groups_first()
    {
        await OpenSampleAsync(1440, 900, remembered: "location", storedFilters: "{\"q\":\"\",\"players\":4,\"time\":null,\"location\":null}");
        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();

        var recording = await ReadRecorderAsync();
        var firstDraw = recording.Batches.First(batch => batch.Inserted > 0);
        firstDraw.Groups.Should().BeGreaterThan(1);
        recording.Batches.Sum(batch => batch.Plain).Should().Be(0);
        recording.Toggles.Should().BeEmpty("every box that is dimmed was inserted dimmed");
        var dimmed = await Page.Locator(Dimmed).EvaluateAllAsync<string[]>("(boxes) => boxes.map((box) => box.dataset.entryId)");
        dimmed.Should().NotBeEmpty();
        recording.Inserted.Where(box => box.Dim).Select(box => box.Id).Should().BeEquivalentTo(dimmed);

        var resources = firstDraw.Resources!;
        var layout = resources.Single(entry => entry.Name.Contains("/cabinet/layout?", StringComparison.Ordinal));
        var byLocation = resources.Single(entry => entry.Name.Contains("/cabinet/layout-by-location", StringComparison.Ordinal));
        var cards = resources.Single(entry => entry.Name.Contains("/cabinet/cards", StringComparison.Ordinal));
        cards.Start.Should().BeLessThan(layout.End, "the card data was asked for while the layout was still on its way");
        byLocation.Start.Should().BeLessThan(layout.End, "the by-location document was asked for while the layout was still on its way");
        await Expect(Page.Locator(LocationRadio)).ToBeCheckedAsync();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_remembered_view_without_locations_falls_back_quietly()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        var host = await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(5));
        host.Cards.Should().NotBeEmpty();
        await PrepareAsync(remembered: "location");

        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        (await Page.Locator(Groups).CountAsync()).Should().Be(0);
        (await Page.Locator(Sections).CountAsync()).Should().BeGreaterThan(0, "All together is drawn");
        await Expect(Page.Locator(Switch)).ToBeHiddenAsync();
        (await Page.EvaluateAsync<string?>($"() => localStorage.getItem('{ViewKey}')")).Should().Be("location");
        (await Page.Locator("#filter-live").InnerTextAsync()).Should().NotBe(FailureCopy, "this is not a failure, so nothing is announced");
        var recording = await ReadRecorderAsync();
        recording.Batches.Sum(batch => batch.Groups).Should().Be(0, "no location group was ever drawn");
        AssertBarShownWithTheFirstDraw(recording);
        ConsoleErrors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(500, "", false)]
    [InlineData(200, "{}", false)]
    [InlineData(500, "", true)]
    public async Task A_remembered_location_view_that_fails_to_load_shows_all_together(int status, string body, bool withFilter)
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        await PrepareAsync(
            remembered: "location",
            storedFilters: withFilter ? "{\"q\":\"\",\"players\":4,\"time\":null,\"location\":null}" : null);
        await Page.RouteAsync("**/cabinet/layout-by-location**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = status,
            ContentType = "application/json",
            Body = body,
        }));

        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        (await Page.Locator(Groups).CountAsync()).Should().Be(0);
        (await Page.Locator(Sections).CountAsync()).Should().BeGreaterThan(0, "the all-together cabinet is drawn");
        await Expect(Page.Locator(Switch)).ToBeVisibleAsync();
        await Expect(Page.Locator(AllRadio)).ToBeCheckedAsync();
        await Expect(Page.Locator("#filter-live")).ToHaveTextAsync(FailureCopy);
        (await Page.EvaluateAsync<string?>($"() => localStorage.getItem('{ViewKey}')")).Should().Be("location", "the next visit tries By location again");

        var recording = await ReadRecorderAsync();
        AssertBarShownWithTheFirstDraw(recording);
        recording.Batches.Sum(batch => batch.Groups).Should().Be(0);

        if (withFilter)
        {
            recording.Toggles.Should().BeEmpty("the fallback draw was already in shadow");
            var dimmed = await Page.Locator(Dimmed).EvaluateAllAsync<string[]>("(boxes) => boxes.map((box) => box.dataset.entryId)");
            dimmed.Should().NotBeEmpty();
            recording.Inserted.Where(box => box.Dim).Select(box => box.Id).Should().BeEquivalentTo(dimmed);
        }
    }

    [Fact]
    public async Task Deleting_every_location_returns_to_All_together_and_keeps_the_choice()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        var host = await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(60));
        var places = await host.SeedAsync("Cabinet A", "Shelf B");
        var games = host.Cards.DistinctBy(card => card.GameId).Take(2).ToList();
        await host.PlaceAsync(games[0], places[0]);
        await host.PlaceAsync(games[1], places[1]);
        await PrepareAsync(remembered: null);
        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(Switch)).ToBeVisibleAsync();

        await Page.Locator(".filter-view-segment").Nth(1).ClickAsync();

        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        await Page.WaitForFunctionAsync($"() => localStorage.getItem('{ViewKey}') === 'location'");

        await host.DeleteLocationAsync(places[0]);
        await host.DeleteLocationAsync(places[1]);
        host.TellOpenPages();

        await Expect(Page.Locator(Groups)).ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = 20000 });
        (await Page.Locator(Sections).CountAsync()).Should().BeGreaterThan(0, "the all-together cabinet is drawn");
        await Expect(Page.Locator(Switch)).ToBeHiddenAsync();
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        (await Page.EvaluateAsync<string?>($"() => localStorage.getItem('{ViewKey}')")).Should().Be("location", "the device keeps remembering By location");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_switch_cross_fades_and_reduced_motion_swaps_at_once()
    {
        await OpenSampleAsync(1440, 900, remembered: null);
        await Page.AddInitScriptAsync(InstallTransitionSpy);
        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync(Boxes, new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(Switch)).ToBeVisibleAsync();
        await Page.EvaluateAsync("() => { window.__spy.before = window.__measure(); }");

        await Page.Locator(".filter-view-segment").Nth(1).ClickAsync();

        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        await Page.WaitForFunctionAsync("() => !document.documentElement.hasAttribute('data-switching-view')");
        var spy = await Page.EvaluateAsync<JsonElement>("() => ({ calls: window.__spy.calls, marked: window.__spy.marked, name: window.__spy.name, moved: window.__spy.moved })");
        spy.GetProperty("calls").GetInt32().Should().Be(1);
        spy.GetProperty("marked").GetBoolean().Should().BeTrue("the root carries the marker while the transition runs");
        spy.GetProperty("name").GetString().Should().Be("cabinet-area", "the cabinet area is the one named snapshot");
        spy.GetProperty("moved").GetBoolean().Should().BeFalse("no box moves inside the old view before the swap");
        (await Page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('cabinet')).viewTransitionName")).Should().Be("none");

        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });
        await Page.EvaluateAsync("() => { window.__spy.calls = 0; }");

        await Page.Locator(".filter-view-segment").Nth(0).ClickAsync();

        await Expect(Page.Locator(Groups)).ToHaveCountAsync(0);
        (await Page.EvaluateAsync<int>("() => window.__spy.calls")).Should().Be(0, "no transition starts under reduced motion");
        (await Page.EvaluateAsync<bool>("() => document.documentElement.hasAttribute('data-switching-view')")).Should().BeFalse();
        await Expect(Page.Locator(AllRadio)).ToBeCheckedAsync();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_current_view_stays_until_the_payload_arrives()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        await PrepareAsync(remembered: null);
        var release = new TaskCompletionSource();
        await Page.RouteAsync("**/cabinet/layout-by-location**", async route =>
        {
            await release.Task;
            await route.ContinueAsync();
        });
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(Switch)).ToBeVisibleAsync();

        await Page.Locator(".filter-view-segment").Nth(1).ClickAsync();
        await Task.Delay(400, TestContext.Current.CancellationToken);

        (await Page.Locator(Groups).CountAsync()).Should().Be(0, "the current view stays on screen while the payload is on its way");
        (await Page.Locator(Sections).CountAsync()).Should().BeGreaterThan(0);
        (await Page.EvaluateAsync<bool>("() => document.documentElement.hasAttribute('data-switching-view')")).Should().BeFalse("the cross-fade starts only when the payload is in hand");
        (await Page.EvaluateAsync<string?>($"() => localStorage.getItem('{ViewKey}')")).Should().BeNull("nothing is remembered before the view is drawn");

        release.SetResult();

        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        await Page.WaitForFunctionAsync($"() => localStorage.getItem('{ViewKey}') === 'location'");
    }

    [Fact]
    public async Task An_open_card_closes_before_the_switch()
    {
        await OpenSampleAsync(1440, 900, remembered: null);
        await Page.Locator(Boxes + "[data-kind=\"cover\"]").First.ClickAsync();
        await Expect(Page.Locator("dialog.card-dialog[open]")).ToBeVisibleAsync();

        await Page.EvaluateAsync("() => document.querySelector('input[name=\"filter-view\"][value=\"location\"]').click()");

        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        await Expect(Page.Locator("dialog.card-dialog[open]")).ToHaveCountAsync(0);
        var recording = await ReadRecorderAsync();
        recording.CardOpenAtGroups.Should().NotBeEmpty();
        recording.CardOpenAtGroups.Should().OnlyContain(open => !open, "the dialog was closed before the groups were drawn");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_switch_scrolls_up_but_never_down()
    {
        await OpenSampleAsync(1440, 420, remembered: null);
        await Expect(Page.Locator(Switch)).ToBeVisibleAsync();

        await Page.EvaluateAsync("() => window.scrollTo(0, document.documentElement.scrollHeight)");
        await Page.WaitForFunctionAsync("() => window.scrollY > 300");
        var before = await Page.EvaluateAsync<double>("() => window.scrollY");

        await Page.Locator(".filter-view-segment").Nth(1).ClickAsync();

        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        await Page.WaitForFunctionAsync("() => !document.documentElement.hasAttribute('data-switching-view')");
        var gap = await Page.EvaluateAsync<double>("() => document.getElementById('cabinet').getBoundingClientRect().top - document.querySelector('.filter-bar').getBoundingClientRect().bottom");
        var after = await Page.EvaluateAsync<double>("() => window.scrollY");
        after.Should().BeLessThanOrEqualTo(before + 0.5, "the page never scrolls down");
        gap.Should().BeApproximately(16, 1, "the cabinet's top sits 16 px under the bar");

        await Page.EvaluateAsync("() => window.scrollTo(0, 0)");
        await Page.WaitForFunctionAsync("() => window.scrollY === 0");

        await Page.Locator(".filter-view-segment").Nth(0).ClickAsync();

        await Expect(Page.Locator(Groups)).ToHaveCountAsync(0);
        await Page.WaitForFunctionAsync("() => !document.documentElement.hasAttribute('data-switching-view')");
        (await Page.EvaluateAsync<double>("() => window.scrollY")).Should().Be(0, "at the top of the page a switch does not scroll");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_failed_payload_keeps_All_together_and_says_so()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        await PrepareAsync(remembered: null);
        var requests = 0;
        var failing = true;
        await Page.RouteAsync("**/cabinet/layout-by-location**", async route =>
        {
            requests += 1;

            if (failing)
            {
                await route.FulfillAsync(new RouteFulfillOptions { Status = 500, ContentType = "application/json", Body = "{}" });
            }
            else
            {
                await route.ContinueAsync();
            }
        });
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(Switch)).ToBeVisibleAsync();
        var sectionsBefore = await Page.Locator(Sections).CountAsync();

        await Page.Locator(".filter-view-segment").Nth(1).ClickAsync();

        await Expect(Page.Locator("#filter-live")).ToHaveTextAsync(FailureCopy);
        await Expect(Page.Locator(AllRadio)).ToBeCheckedAsync();
        (await Page.Locator(Groups).CountAsync()).Should().Be(0);
        (await Page.Locator(Sections).CountAsync()).Should().Be(sectionsBefore, "All together stayed on screen");
        (await Page.EvaluateAsync<string?>($"() => localStorage.getItem('{ViewKey}')")).Should().BeNull("the earlier value is kept");
        requests.Should().Be(1);

        failing = false;
        await Page.Locator(".filter-view-segment").Nth(1).ClickAsync();

        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        requests.Should().Be(2, "the next switch asks again");
        await Page.WaitForFunctionAsync($"() => localStorage.getItem('{ViewKey}') === 'location'");
        await Expect(Page.Locator(LocationRadio)).ToBeCheckedAsync();
    }

    private async Task PrepareAsync(string? remembered, string? storedFilters = null)
    {
        await Page.RouteAsync("**/cabinet/search-names**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "application/json",
            Body = "{\"names\":{}}",
        }));
        await Page.AddInitScriptAsync(InstallRecorder);

        if (remembered is not null)
        {
            await Page.AddInitScriptAsync($"if (localStorage.getItem('{ViewKey}') === null) {{ localStorage.setItem('{ViewKey}', '{remembered}'); }}");
        }

        if (storedFilters is not null)
        {
            await Page.AddInitScriptAsync($"if (sessionStorage.getItem('{FiltersKey}') === null) {{ sessionStorage.setItem('{FiltersKey}', {JsonSerializer.Serialize(storedFilters)}); }}");
        }
    }

    private async Task<string> OpenSampleAsync(int width, int height, string? remembered, string? storedFilters = null)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await PrepareAsync(remembered, storedFilters);
        var address = await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();

        return address;
    }

    private static void AssertBarShownWithTheFirstDraw(Recording recording)
    {
        var firstDraw = recording.Batches.FindIndex(batch => batch.Inserted > 0);
        firstDraw.Should().BeGreaterThanOrEqualTo(0, "the cabinet drew");
        recording.Batches[firstDraw].Shown.Should().BeTrue("the bar was shown in the same task as the first draw");
        recording.Batches.Take(firstDraw).Should().OnlyContain(batch => !batch.Shown, "the bar stayed hidden until the cabinet drew");
    }

    private async Task<Recording> ReadRecorderAsync()
    {
        var json = await Page.EvaluateAsync<JsonElement>("() => window.__rec");

        return JsonSerializer.Deserialize<Recording>(json.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private sealed record Recording(List<Batch> Batches, List<string> Toggles, List<InsertedBox> Inserted, List<bool> CardOpenAtGroups);

    private sealed record Batch(int Inserted, bool Shown, int Groups, int Plain, List<ResourceTiming>? Resources);

    private sealed record InsertedBox(string Id, bool Dim);

    private sealed record ResourceTiming(string Name, double Start, double End);
}

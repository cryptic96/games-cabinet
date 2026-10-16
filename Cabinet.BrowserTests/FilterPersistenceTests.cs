using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
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

    private const string InstallDrawRecorder = """
        (() => {
          const rec = { batches: [], toggles: [], inserted: [] };
          window.__rec = rec;
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
                if (record.attributeName === 'data-dim' && record.target.classList.contains('placement')) {
                  rec.toggles.push(record.target.dataset.entryId);
                }
              }
            }
            if (inserted > 0 || shown) {
              const first = inserted > 0 && rec.batches.every((batch) => batch.inserted === 0);
              rec.batches.push({
                inserted,
                shown,
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
        await Page.RouteAsync("**/cabinet/search-names**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "application/json",
            Body = "{\"names\":{}}",
        }));
        await Page.AddInitScriptAsync(InstallDrawRecorder);
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

    private sealed record Batch(int Inserted, bool Shown, List<ResourceTiming>? Resources);

    private sealed record InsertedBox(string Id, bool Dim);

    private sealed record ResourceTiming(string Name, double Start, double End);
}

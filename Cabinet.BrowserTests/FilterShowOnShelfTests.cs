using System.Globalization;
using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the location on a card is a quiet button that takes the visitor to the game's box on its shelf: the
/// card closes, the by-location view is shown when it is not, the page scrolls to the box with the location's heading under the bar
/// when both fit and the box centred otherwise, and the box is outlined for two seconds with its veil lifted, whatever the filters
/// do. The outline goes at the next press and never by moving the pointer, a payload that is slow or fails is handled without a
/// stray outline, and a game with no place has no button.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterShowOnShelfTests : CabinetPageTest
{
    private readonly TemporaryDirectory _storage = new();

    private const string Boxes = "#cabinet .placement";
    private const string Groups = "#cabinet section.location-group";
    private const string Sections = "#cabinet .section";
    private const string OpenCard = "dialog.card-dialog[open]";
    private const string Found = "#cabinet .placement[data-found]";
    private const string FailureCopy = "Couldn't load the location view. Showing everything together.";
    private const string FiltersKey = "cabinet-filters";
    private const string LocationRadio = "input[name=\"filter-view\"][value=\"location\"]";
    private const string AllRadio = "input[name=\"filter-view\"][value=\"all\"]";
    private const string NothingMatches = "zzzzqqqq";

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private const string InstallRecorder = """
        (() => {
          const rec = { shown: [], cleared: [] };
          window.__found = rec;
          new MutationObserver((records) => {
            for (const record of records) {
              const box = record.target;
              if (box.hasAttribute('data-found')) {
                const bar = document.querySelector('.filter-bar');
                const group = box.closest('.location-group');
                const heading = group === null ? null : group.querySelector('.location-heading');
                const rect = box.getBoundingClientRect();
                const style = getComputedStyle(box);
                const entry = {
                  id: box.dataset.entryId,
                  kind: box.dataset.kind,
                  at: performance.now(),
                  top: rect.top,
                  bottom: rect.bottom,
                  headingTop: heading === null ? null : heading.getBoundingClientRect().top,
                  barBottom: bar.getBoundingClientRect().bottom,
                  pinned: getComputedStyle(bar).position === 'sticky',
                  viewport: window.innerHeight,
                  location: group === null ? null : group.dataset.locationId,
                  dimmed: box.hasAttribute('data-dim'),
                  outlineStyle: style.outlineStyle,
                  outlineWidth: style.outlineWidth,
                  outlineOffset: style.outlineOffset,
                  zIndex: style.zIndex,
                  veil: null,
                };
                rec.shown.push(entry);
                setTimeout(() => { entry.veil = getComputedStyle(box.querySelector('.placement-veil')).opacity; }, 500);
              } else {
                rec.cleared.push({ id: box.dataset.entryId, at: performance.now() });
              }
            }
          }).observe(document, { subtree: true, attributes: true, attributeFilter: ['data-found'] });
        })();
        """;

    private const string MeasureGroups = """
        () => [...document.querySelectorAll('#cabinet .location-group')].map((group) => {
          const heading = group.querySelector('.location-heading').getBoundingClientRect();
          return {
            locationId: group.dataset.locationId,
            boxes: [...group.querySelectorAll('.placement:not([data-kind="moreMarker"])')].map((box) => ({
              id: box.dataset.entryId,
              span: box.getBoundingClientRect().bottom - heading.top,
            })),
          };
        })
        """;

    [Fact]
    public async Task Show_on_shelf_lands_on_the_box_outlined()
    {
        var cards = await OpenSampleAsync(1440, 900);
        var pick = await PickBoxAsync(cards, tallest: false, lastGroupOnly: true);
        await Page.Locator("#filter-q").FillAsync(NothingMatches);
        await Expect(Page.Locator(Boxes + "[data-dim]").First).ToBeAttachedAsync();

        await OpenCardAsync(pick.Id);
        await Expect(Page.Locator("button.card-where-link")).ToHaveAccessibleNameAsync($"Show on shelf: {pick.Location}");
        await Page.Locator("button.card-where-link").ClickAsync();

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        await Expect(Page.Locator(LocationRadio)).ToBeCheckedAsync();
        await Expect(Page.Locator(Found)).ToHaveAttributeAsync("data-entry-id", pick.Id);
        var shown = await ReadShownAsync();
        shown.Should().ContainSingle();
        var landing = shown[0];

        landing.Location.Should().Be(pick.LocationId, "the box stands in the cabinet of its own location");
        landing.Dimmed.Should().BeTrue("a filter that dims every box is on, and the answer is shown anyway");
        landing.Top.Should().BeGreaterThanOrEqualTo(landing.PinnedBarBottom, "the box is not under the bar");
        landing.Bottom.Should().BeLessThanOrEqualTo(landing.Viewport, "the box is inside the viewport");
        landing.Fits.Should().BeTrue("a box in the first rows of a cabinet fits with its heading");
        landing.HeadingTop!.Value.Should().BeApproximately(landing.PinnedBarBottom + 16, 1.5, "the heading is aligned under the bar");
        landing.OutlineStyle.Should().Be("solid");
        landing.OutlineWidth.Should().Be("2px");
        landing.OutlineOffset.Should().Be("2px");
        landing.ZIndex.Should().Be("4");
        await Page.WaitForFunctionAsync("() => window.__found.shown.length > 0 && window.__found.shown[0].veil !== null");
        (await ReadShownAsync())[0].Veil.Should().Be("0", "the veil is lifted so the filter never hides the answer");
        await Expect(Page.Locator(Boxes + "[data-dim]").First).ToBeAttachedAsync();
        (await Page.Locator("#filter-q").InputValueAsync()).Should().Be(NothingMatches, "Show on shelf leaves the filter as it is");
        ConsoleErrors.Should().BeEmpty();
    }

    private async Task<IReadOnlyList<ViewCard>> OpenSampleAsync(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await PrepareAsync();
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();

        var profile = width < 640 ? "phone" : "desktop";
        var json = await Page.EvaluateAsync<JsonElement>("async address => (await (await fetch(address)).json()).cards", $"/cabinet/cards?profile={profile}&sample=65");

        return JsonSerializer.Deserialize<List<ViewCard>>(json.GetRawText(), WebOptions)!;
    }

    private async Task PrepareAsync()
    {
        await Page.RouteAsync("**/cabinet/search-names**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "application/json",
            Body = "{\"names\":{}}",
        }));
        await Page.AddInitScriptAsync(InstallRecorder);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<Pick> PickBoxAsync(IReadOnlyList<ViewCard> cards, bool tallest, bool lastGroupOnly)
    {
        await Page.Locator(".filter-view-segment").Nth(1).ClickAsync();
        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        var json = await Page.EvaluateAsync<JsonElement>(MeasureGroups);
        var groups = JsonSerializer.Deserialize<List<MeasuredGroup>>(json.GetRawText(), WebOptions)!
            .Where(group => group.LocationId != "unknown")
            .ToList();
        var byId = cards.ToDictionary(card => card.EntryId.ToString(CultureInfo.InvariantCulture));
        var candidates = (lastGroupOnly ? groups.TakeLast(1) : groups)
            .SelectMany(group => group.Boxes.Select(box => (Group: group, Box: box)))
            .Where(item => byId.TryGetValue(item.Box.Id, out var card) && !card.IsExpansion && card.Location is not null && card.LocationId == item.Group.LocationId)
            .ToList();

        candidates.Should().NotBeEmpty("the sample keeps games in owner locations");
        var chosen = tallest
            ? candidates.OrderByDescending(item => item.Box.Span).First()
            : candidates.OrderBy(item => item.Box.Span).First();

        await Page.Locator(".filter-view-segment").Nth(0).ClickAsync();
        await Expect(Page.Locator(Groups)).ToHaveCountAsync(0);
        await Page.EvaluateAsync("() => window.scrollTo(0, 0)");
        await Page.EvaluateAsync("() => { window.__found.shown.length = 0; window.__found.cleared.length = 0; }");

        var card = byId[chosen.Box.Id];

        return new Pick(chosen.Box.Id, card.Location!, card.LocationId!);
    }

    private async Task OpenCardAsync(string entryId)
    {
        var box = Page.Locator($"{Boxes}[data-entry-id=\"{entryId}\"]:not([data-kind=\"moreMarker\"])").First;

        await box.ScrollIntoViewIfNeededAsync();
        await box.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
    }

    private async Task<List<Landing>> ReadShownAsync()
    {
        var json = await Page.EvaluateAsync<JsonElement>("() => window.__found.shown");

        return JsonSerializer.Deserialize<List<Landing>>(json.GetRawText(), WebOptions)!;
    }

    private sealed record Pick(string Id, string Location, string LocationId);

    private sealed record MeasuredBox(string Id, double Span);

    private sealed record MeasuredGroup(string LocationId, List<MeasuredBox> Boxes);

    private sealed record ViewCard(long EntryId, string Title, bool IsExpansion, string? Location, string? LocationId);

    private sealed record Landing(
        string Id,
        string Kind,
        double At,
        double Top,
        double Bottom,
        double? HeadingTop,
        double BarBottom,
        bool Pinned,
        double Viewport,
        string? Location,
        bool Dimmed,
        string OutlineStyle,
        string OutlineWidth,
        string OutlineOffset,
        string ZIndex,
        string? Veil)
    {
        public double PinnedBarBottom => Pinned ? BarBottom : 0;

        public bool Fits => HeadingTop is not null && Bottom - HeadingTop.Value <= Viewport - PinnedBarBottom - 32;
    }
}

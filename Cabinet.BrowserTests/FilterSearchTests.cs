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
    private const string BoxRectangles = "() => [...document.querySelectorAll('#cabinet .placement')].map((box) => { const r = box.getBoundingClientRect(); return [box.dataset.entryId, box.dataset.kind, r.left, r.top, r.width, r.height].join(':'); })";
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task A_typed_fragment_lights_its_family_and_shadows_the_rest_in_place()
    {
        await OpenAsync("/?sample=65", 1440, 900);
        var cards = await LoadCardsAsync();
        var fragment = FragmentOfABaseGameWithAnExpansion(cards);
        var expected = ExpectedResult(cards, fragment);
        expected.LitBoxCount.Should().BeGreaterThan(0);
        expected.LitEntries.Should().BeLessThan(expected.TotalEntries);

        var status = Page.Locator(".filter-bar .filter-status");
        (await status.GetAttributeAsync("hidden")).Should().BeNull();
        (await VisibilityAsync(status)).Should().Be("hidden");
        (await Page.Locator(".filter-bar").GetAttributeAsync("data-active")).Should().BeNull();
        (await Page.Locator(Boxes + "[data-dim]").CountAsync()).Should().Be(0);
        var heightBefore = (await Page.Locator(".filter-bar").BoundingBoxAsync())!.Height;
        var rectanglesBefore = await Page.EvaluateAsync<string[]>(BoxRectangles);

        await Page.Locator("#filter-q").FillAsync(fragment);

        var noun = expected.TotalEntries == 1 ? "game" : "games";
        await Expect(Page.Locator(".filter-count")).ToHaveTextAsync($"{expected.LitEntries} of {expected.TotalEntries} {noun}");
        var dimmed = await Page.Locator(Boxes + "[data-dim]").EvaluateAllAsync<string[]>("(boxes) => boxes.map((box) => box.dataset.entryId)");
        var all = await Page.Locator(Boxes).EvaluateAllAsync<string[]>("(boxes) => boxes.map((box) => box.dataset.entryId)");
        dimmed.Should().BeEquivalentTo(all.Where(id => !expected.LitEntryIds.Contains(id)));
        all.Where(id => expected.LitEntryIds.Contains(id)).Should().NotBeEmpty();
        (await Page.EvaluateAsync<string[]>(BoxRectangles)).Should().Equal(rectanglesBefore);
        await Expect(Page.Locator(".filter-bar")).ToHaveAttributeAsync("data-active", string.Empty);
        (await VisibilityAsync(status)).Should().Be("visible");
        (await status.GetAttributeAsync("hidden")).Should().BeNull();
        (await Page.Locator(".filter-bar").BoundingBoxAsync())!.Height.Should().Be(heightBefore);
        (await Page.Locator(".filter-count").GetAttributeAsync("aria-hidden")).Should().Be("true");
        await SaveScreenshotAsync("filter-search-typed");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_box_in_shadow_keeps_its_name_and_opens_its_card()
    {
        await OpenAsync("/?sample=65", 1440, 900);
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

    private static async Task<string> VisibilityAsync(ILocator locator) =>
        await locator.EvaluateAsync<string>("(element) => getComputedStyle(element).visibility");

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

    private async Task OpenAsync(string path, int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
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

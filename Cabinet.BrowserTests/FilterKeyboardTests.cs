using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that while a filter is active the keyboard follows the light: the arrow keys, Home and End move only
/// between the games that match, the cabinet's single tab stop sits on a match, and nothing traps a keyboard user when no game matches.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterKeyboardTests : CabinetPageTest
{
    private const string SamplePath = "/?sample=65";
    private const string Boxes = "#cabinet .placement";
    private const string BoxesOnAShelf = Boxes + "[data-shelf]";
    private const string Field = "#filter-q";
    private const string PanelClear = "#filter-panel .filter-clear";
    private const string TabStops = Boxes + "[tabindex=\"0\"]";
    private const string OpenCard = "dialog.card-dialog[open]";
    private const string NoMatchQuery = "zzzzqxqx";
    private const string ProbeScript = "(q) => { const field = document.getElementById('filter-q'); field.value = q; field.dispatchEvent(new Event('input', { bubbles: true })); const read = (box) => { const r = box.getBoundingClientRect(); return { key: box.dataset.entryId + '|' + box.dataset.kind, shelf: box.dataset.shelf ?? '', left: r.left, top: r.top, right: r.right, bottom: r.bottom }; }; const all = [...document.querySelectorAll('#cabinet .placement')]; return JSON.stringify({ lit: all.filter((box) => !box.hasAttribute('data-dim')).map(read), dim: all.filter((box) => box.hasAttribute('data-dim')).map(read) }); }";
    private const string LitInReadingOrderScript = "() => { const lit = [...document.querySelectorAll('#cabinet .placement:not([data-dim])')].map((box) => { const r = box.getBoundingClientRect(); return { key: box.dataset.entryId + '|' + box.dataset.kind, left: r.left, top: r.top }; }); lit.sort((a, b) => a.top - b.top || a.left - b.left); const rows = []; let row = []; let rowTop = 0; for (const item of lit) { if (row.length > 0 && item.top > rowTop + 2) { rows.push(row); row = []; } if (row.length === 0) { rowTop = item.top; } row.push(item); } if (row.length > 0) { rows.push(row); } return rows.flatMap((one) => one.sort((a, b) => a.left - b.left)).map((item) => item.key); }";
    private const string FocusedScript = "(() => { const e = document.activeElement; return { isBox: e.classList.contains('placement'), isDim: e.hasAttribute('data-dim'), key: (e.dataset.entryId ?? '') + '|' + (e.dataset.kind ?? '') }; })()";
    private const string TabStopScript = "(() => [...document.querySelectorAll('#cabinet .placement[tabindex=\"0\"]')].map((box) => box.dataset.entryId + '|' + box.dataset.kind + '|' + (box.hasAttribute('data-dim') ? 'dim' : 'lit')))()";
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private sealed class Rect
    {
        public string Key { get; set; } = string.Empty;

        public string Shelf { get; set; } = string.Empty;

        public double Left { get; set; }

        public double Top { get; set; }

        public double Right { get; set; }

        public double Bottom { get; set; }
    }

    private sealed class Probe
    {
        public List<Rect> Lit { get; set; } = [];

        public List<Rect> Dim { get; set; } = [];
    }

    private sealed class Focused
    {
        public bool IsBox { get; set; }

        public bool IsDim { get; set; }

        public string Key { get; set; } = string.Empty;
    }

    private sealed record ShadowCase(string WideQuery, string Held, string NarrowQuery, string KeepQuery);

    [Fact]
    public async Task Arrows_and_Home_and_End_visit_only_the_matches()
    {
        var query = await FindQueryAsync(Spread);
        await Page.Locator(Field).FillAsync(query);
        await Expect(Page.Locator(Boxes + "[data-dim]").First).ToBeAttachedAsync();

        var order = await ReadingOrderAsync();
        order.Count.Should().BeGreaterThanOrEqualTo(3);
        await BoxByKey(order[0]).FocusAsync();
        var visited = new HashSet<string> { order[0] };
        var keys = new[] { "ArrowRight", "ArrowDown", "ArrowRight", "ArrowDown", "ArrowLeft", "ArrowUp", "ArrowRight", "ArrowDown" };

        for (var round = 0; round < 3; round++)
        {
            foreach (var key in keys)
            {
                await Page.Keyboard.PressAsync(key);
                var focused = await FocusedAsync();
                focused.IsBox.Should().BeTrue();
                focused.IsDim.Should().BeFalse($"{key} must land on a match");
                visited.Add(focused.Key);
            }
        }

        visited.Count.Should().BeGreaterThan(1);

        await Page.Keyboard.PressAsync("End");
        (await FocusedAsync()).Key.Should().Be(order[^1]);
        await Page.Keyboard.PressAsync("Home");
        (await FocusedAsync()).Key.Should().Be(order[0]);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_focused_box_in_shadow_moves_to_a_match_on_the_next_arrow()
    {
        var probes = await ProbeAllAsync();
        var pick = probes
            .Select(candidate => (Query: candidate.Key, Shadowed: candidate.Value.Dim.FirstOrDefault(dim => candidate.Value.Lit.Any(lit => lit.Shelf == dim.Shelf && lit.Left >= dim.Right)), candidate.Value.Lit.Count))
            .First(candidate => candidate.Count >= 1 && candidate.Shadowed is not null);
        await Page.Locator(Field).FillAsync(pick.Query);
        await Expect(BoxByKey(pick.Shadowed!.Key)).ToHaveAttributeAsync("data-dim", string.Empty);

        await BoxByKey(pick.Shadowed.Key).FocusAsync();
        (await FocusedAsync()).Key.Should().Be(pick.Shadowed.Key);

        await Page.Keyboard.PressAsync("ArrowRight");

        var moved = await FocusedAsync();
        moved.IsBox.Should().BeTrue();
        moved.IsDim.Should().BeFalse();
        moved.Key.Should().NotBe(pick.Shadowed.Key);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Tab_enters_the_cabinet_on_the_first_match()
    {
        var probes = await ProbeAllAsync();
        var spread = probes.First(candidate => Spread(candidate.Value)).Key;
        var chain = probes
            .Where(narrow => narrow.Key.Length == 1 && narrow.Value.Lit.Count >= 3 && narrow.Value.Dim.Count >= 1)
            .SelectMany(narrow => probes
                .Where(wide => wide.Key.Length == 2 && wide.Key[0] == narrow.Key[0] && wide.Value.Lit.Count >= 1 && wide.Value.Dim.Count >= 1 && FirstInReadingOrder(wide.Value.Lit) != FirstInReadingOrder(narrow.Value.Lit))
                .Select(wide => (First: narrow.Key, Then: wide.Key)))
            .First();

        await Page.Locator(Field).FillAsync(spread);
        await Expect(Page.Locator(Boxes + "[data-dim]").First).ToBeAttachedAsync();
        var expected = (await ReadingOrderAsync())[0];
        (await TabIntoTheCabinetAsync()).Should().Be(expected);

        await ReloadAsync();
        await Page.Locator(Field).FillAsync(chain.First);
        await Expect(Page.Locator(Boxes + "[data-dim]").First).ToBeAttachedAsync();
        await Page.Locator(Field).FillAsync(chain.Then);
        await Expect(Page.Locator(Boxes + "[data-dim]").First).ToBeAttachedAsync();
        var thenFirst = (await ReadingOrderAsync())[0];
        thenFirst.Should().Be(FirstInReadingOrder(probes[chain.Then].Lit));
        (await TabIntoTheCabinetAsync()).Should().Be(thenFirst);

        await ReloadAsync();
        await Page.Locator(BoxesOnAShelf).First.FocusAsync();
        await Page.Keyboard.PressAsync("End");
        var moved = await FocusedAsync();
        await Page.Locator(Field).FocusAsync();
        await Page.Locator(Field).FillAsync(spread);
        await Expect(Page.Locator(Boxes + "[data-dim]").First).ToBeAttachedAsync();
        var firstMatch = (await ReadingOrderAsync())[0];
        firstMatch.Should().NotBe(moved.Key);
        (await TabIntoTheCabinetAsync()).Should().Be(firstMatch);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_tab_stop_leaves_a_box_that_falls_into_shadow()
    {
        var probes = await ProbeAllAsync();
        var pick = FindShadowCase(probes);

        await Page.Locator(Field).FillAsync(pick.WideQuery);
        await Expect(Page.Locator(Boxes + "[data-dim]").First).ToBeAttachedAsync();
        await BoxByKey(pick.Held).FocusAsync();
        await Page.Locator(Field).FocusAsync();
        await Expect(Page.Locator(TabStops)).ToHaveCountAsync(1);
        await Expect(BoxByKey(pick.Held)).ToHaveAttributeAsync("tabindex", "0");

        await Page.Locator(Field).FillAsync(pick.KeepQuery);
        await Expect(Page.Locator(TabStops)).ToHaveCountAsync(1);
        await Expect(BoxByKey(pick.Held)).ToHaveAttributeAsync("tabindex", "0");

        await Page.Locator(Field).FillAsync(pick.NarrowQuery);
        await Expect(BoxByKey(pick.Held)).ToHaveAttributeAsync("data-dim", string.Empty);
        await Expect(Page.Locator(TabStops)).ToHaveCountAsync(1);
        var stop = (await Page.EvaluateAsync<string[]>(TabStopScript)).Single().Split('|');
        stop[2].Should().Be("lit");
        var after = await ProbeAsync(pick.NarrowQuery);
        var old = after.Dim.Single(dim => dim.Key == pick.Held);
        var chosen = after.Lit.Single(lit => lit.Key == $"{stop[0]}|{stop[1]}");
        Gap(old, chosen).Should().BeApproximately(after.Lit.Min(lit => Gap(old, lit)), 2.5);

        await ReloadAsync();
        await Page.Locator(Field).FillAsync(pick.WideQuery);
        await Expect(Page.Locator(Boxes + "[data-dim]").First).ToBeAttachedAsync();
        await BoxByKey(pick.Held).FocusAsync();

        await ProbeAsync(pick.NarrowQuery);

        var focused = await FocusedAsync();
        focused.Key.Should().Be(pick.Held);
        focused.IsDim.Should().BeTrue();
        await Expect(BoxByKey(pick.Held)).ToHaveAttributeAsync("tabindex", "-1");
        await Expect(Page.Locator(TabStops)).ToHaveCountAsync(1);
        (await Page.EvaluateAsync<string[]>(TabStopScript)).Single().Should().EndWith("|lit");

        await Page.Keyboard.PressAsync("ArrowRight");

        var moved = await FocusedAsync();
        moved.IsBox.Should().BeTrue();
        moved.IsDim.Should().BeFalse();
        moved.Key.Should().NotBe(pick.Held);
        await Expect(Page.Locator(TabStops)).ToHaveCountAsync(1);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Zero_matches_and_Clear_restore_every_box_to_the_arrows()
    {
        await OpenAsync();
        var first = Page.Locator(BoxesOnAShelf).First;
        await first.FocusAsync();
        var firstKey = await KeyOfAsync(first);

        await Page.Locator(Field).FillAsync(NoMatchQuery);
        await Expect(Page.Locator(Boxes + ":not([data-dim])")).ToHaveCountAsync(0);
        await Expect(Page.Locator(TabStops)).ToHaveCountAsync(1);
        (await Page.EvaluateAsync<string[]>(TabStopScript)).Single().Should().StartWith(firstKey);

        await first.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        var right = await FocusedAsync();
        right.IsBox.Should().BeTrue();
        right.IsDim.Should().BeTrue();
        right.Key.Should().NotBe(firstKey);

        await Page.Keyboard.PressAsync("End");
        await Expect(Page.Locator(Boxes).Last).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Home");
        await Expect(Page.Locator(Boxes).First).ToBeFocusedAsync();
        var stopBeforeClear = (await Page.EvaluateAsync<string[]>(TabStopScript)).Single()[..^4];

        await Page.Locator(PanelClear).ClickAsync();

        await Expect(Page.Locator(Boxes + "[data-dim]")).ToHaveCountAsync(0);
        await Expect(Page.Locator(TabStops)).ToHaveCountAsync(1);
        (await Page.EvaluateAsync<string[]>(TabStopScript)).Single().Should().StartWith(stopBeforeClear);

        await Page.Locator(Boxes).Nth(3).FocusAsync();
        await Page.Keyboard.PressAsync("End");
        await Expect(Page.Locator(Boxes).Last).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Home");
        await Expect(Page.Locator(Boxes).First).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        (await FocusedAsync()).Key.Should().NotBe(await KeyOfAsync(Page.Locator(Boxes).First));
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Enter_on_a_box_in_shadow_opens_its_card()
    {
        var query = await FindQueryAsync(Spread);
        await Page.Locator(Field).FillAsync(query);
        var shadowed = Page.Locator(Boxes + "[data-dim]:not([data-kind=\"moreMarker\"])").First;
        await Expect(shadowed).ToBeAttachedAsync();

        await shadowed.FocusAsync();
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        ConsoleErrors.Should().BeEmpty();
    }

    private static bool Spread(Probe probe) =>
        probe.Lit.Count >= 3 && probe.Lit.Count <= probe.Dim.Count && probe.Lit.Select(box => box.Shelf).Distinct().Count() >= 2;

    private static double Gap(Rect from, Rect to) =>
        Math.Sqrt(Math.Pow(Math.Max(0, Math.Max(to.Left - from.Right, from.Left - to.Right)), 2) + Math.Pow(Math.Max(0, Math.Max(to.Top - from.Bottom, from.Top - to.Bottom)), 2));

    private static string FirstInReadingOrder(List<Rect> lit)
    {
        var rows = new List<List<Rect>>();
        var row = new List<Rect>();
        var rowTop = 0.0;

        foreach (var item in lit.OrderBy(box => box.Top).ThenBy(box => box.Left))
        {
            if (row.Count > 0 && item.Top > rowTop + 2)
            {
                rows.Add(row);
                row = [];
            }

            if (row.Count == 0)
            {
                rowTop = item.Top;
            }

            row.Add(item);
        }

        rows.Add(row);

        return rows[0].OrderBy(box => box.Left).First().Key;
    }

    private static ShadowCase FindShadowCase(Dictionary<string, Probe> probes)
    {
        foreach (var wide in probes.Where(candidate => Spread(candidate.Value)))
        {
            foreach (var held in wide.Value.Lit)
            {
                var keep = probes.FirstOrDefault(candidate => candidate.Key != wide.Key && candidate.Value.Dim.Count >= 1 && candidate.Value.Lit.Any(lit => lit.Key == held.Key));

                if (keep.Key is null)
                {
                    continue;
                }

                foreach (var narrow in probes.Where(candidate => candidate.Value.Lit.Count >= 1))
                {
                    var old = narrow.Value.Dim.FirstOrDefault(dim => dim.Key == held.Key);

                    if (old is not null && narrow.Value.Lit.Any(lit => lit.Shelf == old.Shelf && lit.Left >= old.Right))
                    {
                        return new ShadowCase(wide.Key, held.Key, narrow.Key, keep.Key);
                    }
                }
            }
        }

        throw new InvalidOperationException("No pair of queries on the sample cabinet lets a lit box fall into shadow with a match beside it.");
    }

    private ILocator BoxByKey(string key)
    {
        var parts = key.Split('|');

        return Page.Locator($"{Boxes}[data-entry-id=\"{parts[0]}\"][data-kind=\"{parts[1]}\"]");
    }

    private async Task<string> KeyOfAsync(ILocator box) =>
        await box.GetAttributeAsync("data-entry-id") + "|" + await box.GetAttributeAsync("data-kind");

    /// <summary>
    /// Opens the cabinet at the width where the filter panel stands beside it without making the page modal, and opens the panel, so
    /// the search field is in reach while the boxes still take the keyboard. The index of other titles is answered empty, so what a
    /// query lights depends on the titles alone and an index arriving between two fills cannot change it.
    /// </summary>
    private async Task OpenAsync()
    {
        await Page.SetViewportSizeAsync(DockWidths.Push, 900);
        await StartAsync(PrototypeOn);
        await Page.RouteAsync("**/cabinet/search-names**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "application/json",
            Body = "{\"names\":{}}",
        }));
        await GotoCabinetAsync(SamplePath);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        await OpenFilterPanelAsync();
    }

    /// <summary>
    /// Clears the filters and loads the page again, then makes sure the filter panel is open, whether or not the page brought it back
    /// open by itself.
    /// </summary>
    private async Task ReloadAsync()
    {
        await Page.EvaluateAsync("(selector) => document.querySelector(selector).click()", PanelClear);
        await GotoCabinetAsync(SamplePath);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(TabStops)).ToHaveCountAsync(1);
        await OpenFilterPanelAsync();
    }

    private async Task<Probe> ProbeAsync(string query) =>
        JsonSerializer.Deserialize<Probe>(await Page.EvaluateAsync<string>(ProbeScript, query), WebOptions)!;

    private async Task<IReadOnlyList<string>> ReadingOrderAsync() => await Page.EvaluateAsync<string[]>(LitInReadingOrderScript);

    private async Task<Focused> FocusedAsync() => await Page.EvaluateAsync<Focused>(FocusedScript);

    private async Task<string> TabIntoTheCabinetAsync()
    {
        await Page.Locator(Field).FocusAsync();

        for (var press = 0; press < 6; press++)
        {
            await Page.Keyboard.PressAsync("Tab");
            var focused = await FocusedAsync();

            if (focused.IsBox)
            {
                return focused.Key;
            }
        }

        throw new InvalidOperationException("Tab did not reach a box within six presses.");
    }

    /// <summary>
    /// Opens the page, tries every one and two letter query on the filter field and gives back what each leaves lit and in shadow, then
    /// loads the page again so the keyboard state starts clean.
    /// </summary>
    private async Task<Dictionary<string, Probe>> ProbeAllAsync()
    {
        await OpenAsync();
        const string letters = "abcdefghijklmnopqrstuvwxyz";
        var candidates = letters.Select(letter => letter.ToString()).Concat(letters.SelectMany(first => letters.Select(second => string.Concat(first, second))));
        var probes = new Dictionary<string, Probe>();

        foreach (var candidate in candidates)
        {
            probes[candidate] = await ProbeAsync(candidate);
        }

        await ReloadAsync();

        return probes;
    }

    private async Task<string> FindQueryAsync(Func<Probe, bool> accept)
    {
        var probes = await ProbeAllAsync();

        return probes.First(candidate => accept(candidate.Value)).Key;
    }
}

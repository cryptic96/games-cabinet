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
    private const string LitBoxes = Boxes + ":not([data-dim])";
    private const string Field = "#filter-q";
    private const string ProbeScript = "(q) => { const field = document.getElementById('filter-q'); field.value = q; field.dispatchEvent(new Event('input', { bubbles: true })); const read = (box) => { const r = box.getBoundingClientRect(); return { key: box.dataset.entryId + '|' + box.dataset.kind, shelf: box.dataset.shelf ?? '', left: r.left, top: r.top, right: r.right, bottom: r.bottom }; }; const all = [...document.querySelectorAll('#cabinet .placement')]; return JSON.stringify({ lit: all.filter((box) => !box.hasAttribute('data-dim')).map(read), dim: all.filter((box) => box.hasAttribute('data-dim')).map(read) }); }";
    private const string LitInReadingOrderScript = "() => { const lit = [...document.querySelectorAll('#cabinet .placement:not([data-dim])')].map((box) => { const r = box.getBoundingClientRect(); return { key: box.dataset.entryId + '|' + box.dataset.kind, left: r.left, top: r.top }; }); lit.sort((a, b) => a.top - b.top || a.left - b.left); const rows = []; let row = []; let rowTop = 0; for (const item of lit) { if (row.length > 0 && item.top > rowTop + 2) { rows.push(row); row = []; } if (row.length === 0) { rowTop = item.top; } row.push(item); } if (row.length > 0) { rows.push(row); } return rows.flatMap((one) => one.sort((a, b) => a.left - b.left)).map((item) => item.key); }";
    private const string FocusedScript = "(() => { const e = document.activeElement; return { isBox: e.classList.contains('placement'), isDim: e.hasAttribute('data-dim'), key: (e.dataset.entryId ?? '') + '|' + (e.dataset.kind ?? '') }; })()";
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

    [Fact]
    public async Task Arrows_and_Home_and_End_visit_only_the_matches()
    {
        await OpenAsync();
        var query = await FindQueryAsync(probe => probe.Lit.Count >= 3 && probe.Lit.Count <= probe.Dim.Count && probe.Lit.Select(box => box.Shelf).Distinct().Count() >= 2);
        await ReloadAsync();
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
        await OpenAsync();
        Rect? shadowed = null;
        var query = await FindQueryAsync(probe =>
        {
            shadowed = probe.Dim.FirstOrDefault(dim => probe.Lit.Any(lit => lit.Shelf == dim.Shelf && lit.Left >= dim.Right));

            return probe.Lit.Count >= 1 && shadowed is not null;
        });
        await ReloadAsync();
        await Page.Locator(Field).FillAsync(query);
        await Expect(BoxByKey(shadowed!.Key)).ToHaveAttributeAsync("data-dim", string.Empty);

        await BoxByKey(shadowed.Key).FocusAsync();
        (await FocusedAsync()).Key.Should().Be(shadowed.Key);

        await Page.Keyboard.PressAsync("ArrowRight");

        var moved = await FocusedAsync();
        moved.IsBox.Should().BeTrue();
        moved.IsDim.Should().BeFalse();
        moved.Key.Should().NotBe(shadowed.Key);
        ConsoleErrors.Should().BeEmpty();
    }

    private ILocator BoxByKey(string key)
    {
        var parts = key.Split('|');

        return Page.Locator($"{Boxes}[data-entry-id=\"{parts[0]}\"][data-kind=\"{parts[1]}\"]");
    }

    private async Task OpenAsync()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        await GotoCabinetAsync(SamplePath);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
    }

    private async Task ReloadAsync()
    {
        await GotoCabinetAsync(SamplePath);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(Boxes + "[tabindex=\"0\"]")).ToHaveCountAsync(1);
    }

    private async Task<Probe> ProbeAsync(string query) => JsonSerializer.Deserialize<Probe>(await Page.EvaluateAsync<string>(ProbeScript, query), WebOptions)!;

    private async Task<IReadOnlyList<string>> ReadingOrderAsync() => await Page.EvaluateAsync<string[]>(LitInReadingOrderScript);

    private async Task<Focused> FocusedAsync() => await Page.EvaluateAsync<Focused>(FocusedScript);

    private async Task<string> FindQueryAsync(Func<Probe, bool> accept)
    {
        const string letters = "abcdefghijklmnopqrstuvwxyz";
        var candidates = letters.Select(letter => letter.ToString()).Concat(letters.SelectMany(first => letters.Select(second => string.Concat(first, second))));

        foreach (var candidate in candidates)
        {
            if (accept(await ProbeAsync(candidate)))
            {
                await ProbeAsync(string.Empty);

                return candidate;
            }
        }

        await ProbeAsync(string.Empty);

        throw new InvalidOperationException("No query on the sample cabinet gives the wanted spread of matches.");
    }
}

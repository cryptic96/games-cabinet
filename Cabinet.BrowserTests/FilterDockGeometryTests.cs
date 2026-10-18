using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser how the filters sit on a desktop: the page loads with a folded rail at the left edge and no filter option
/// in sight, the rail's three-line button opens a panel beside the cabinet that holds the one control set, a typed title dims
/// everything outside its family, and the panel folds back to the rail. Nothing filter-related sits above the cabinet, the rail is
/// pinned against the left, top and bottom edges while the page scrolls, and the dock's states are drawn by the style sheet.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterDockGeometryTests : CabinetPageTest
{
    private const int RailWidth = 64;
    private const int PanelWidth = 336;
    private const int PageGap = 24;
    private const string Boxes = "#cabinet .placement";
    private const string Cabinet = "#cabinet";
    private const double Rem = 16;
    private const double FullSection = 40 * Rem;
    private const int ColumnGap = 24;
    private const string Groups = "#cabinet .location-group";

    /// <summary>A push width between the narrowest push and a wide desktop, where two sections almost reach their full width.</summary>
    private const int PushedMiddle = 1680;

    /// <summary>A push width wide enough for three sections beside the open panel.</summary>
    private const int PushedThreeColumns = 2304;
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private const string ReadRenderedOptions = """
        () => {
          const rendered = (element) => element !== null && element.getClientRects().length > 0;
          return [
            ...[document.querySelector('#filter-q')],
            ...document.querySelectorAll('.filter-chip'),
            ...document.querySelectorAll('input[name="filter-view"]'),
            ...[document.querySelector('#filter-loc')],
          ].filter(rendered).map((element) => element.id || element.className || element.name);
        }
        """;

    private const string ReadDocumentOrder = """
        () => {
          const panel = document.getElementById('filter-panel');
          const wanted = ['#filter-panel-title', '#filter-q', '.filter-count', '.filter-view', '.filter-group[data-filter="players"]', '.filter-group[data-filter="time"]', '#filter-loc'];
          const found = wanted.map((selector) => panel.querySelector(selector));
          const missing = wanted.filter((selector, index) => found[index] === null);
          const ordered = found.every((element, index) => index === 0 || (found[index - 1] !== null && element !== null && (found[index - 1].compareDocumentPosition(element) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0));
          return { missing, ordered };
        }
        """;

    private const string ReadPanelOverlap = """
        () => {
          const cabinet = document.getElementById('cabinet').getBoundingClientRect();
          const boxes = [document.getElementById('filter-panel'), ...document.querySelectorAll('#filter-panel *')]
            .map((element) => element.getBoundingClientRect())
            .filter((rect) => rect.width > 0 && rect.height > 0);
          return boxes.filter((rect) => rect.right > cabinet.left + 0.5 && rect.left < cabinet.right - 0.5 && rect.bottom > cabinet.top + 0.5 && rect.top < cabinet.bottom - 0.5).length;
        }
        """;

    [Fact]
    public async Task The_rail_opens_into_a_panel_beside_the_cabinet_with_the_controls_inside()
    {
        await OpenAsync("/?sample=65", DockWidths.Push, 900);

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        var rail = (await Page.Locator(".filter-rail").BoundingBoxAsync())!;
        ((double)rail.Width).Should().BeApproximately(RailWidth, 0.5);
        ((double)rail.X).Should().BeApproximately(0, 0.5);
        (await Page.EvaluateAsync<string[]>(ReadRenderedOptions)).Should().BeEmpty("no filter option is visible on a first visit");

        await Page.Locator(".filter-rail .filter-menu").ClickAsync();

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "push");
        var panel = (await Page.Locator("#filter-panel").BoundingBoxAsync())!;
        ((double)panel.Width).Should().BeApproximately(PanelWidth, 0.5);
        ((double)panel.X).Should().BeApproximately(0, 0.5);
        var cabinet = (await Page.Locator("#cabinet").BoundingBoxAsync())!;
        ((double)cabinet.X).Should().BeGreaterThanOrEqualTo(PanelWidth + PageGap - 0.5);
        (await Page.EvaluateAsync<int>(ReadPanelOverlap)).Should().Be(0, "nothing of the pushed panel overlaps the cabinet");

        var order = await Page.EvaluateAsync<JsonElement>(ReadDocumentOrder);
        order.GetProperty("missing").GetArrayLength().Should().Be(0, order.GetProperty("missing").GetRawText());
        order.GetProperty("ordered").GetBoolean().Should().BeTrue("the panel holds its parts in the designed order");
        (await Page.Locator("input[name='filter-players']").CountAsync()).Should().Be(9);
        (await Page.Locator("input[name='filter-time']").CountAsync()).Should().Be(6);
        (await Page.Locator("#filter-panel input[name='filter-players']").CountAsync()).Should().Be(9, "the one players group was moved into the panel");
        (await Page.Locator("#filter-panel input[name='filter-time']").CountAsync()).Should().Be(6, "the one time group was moved into the panel");

        var cards = await LoadCardsAsync();
        var title = TitleOfABaseGameWithAnExpansion(cards);
        var lit = LitEntries(cards, title);
        await Page.Locator("#filter-q").FillAsync(title);

        await Expect(Page.Locator(".filter-bar")).ToHaveAttributeAsync("data-active", string.Empty);
        var all = await Page.Locator(Boxes).EvaluateAllAsync<string[]>("(boxes) => boxes.filter((box) => box.dataset.kind !== 'moreMarker').map((box) => box.dataset.entryId)");
        var dimmed = await Page.Locator(Boxes + "[data-dim]").EvaluateAllAsync<string[]>("(boxes) => boxes.filter((box) => box.dataset.kind !== 'moreMarker').map((box) => box.dataset.entryId)");
        dimmed.Should().BeEquivalentTo(all.Where(id => !lit.Contains(id)), "every box outside the family is in shadow");
        all.Where(id => lit.Contains(id)).Should().NotBeEmpty("the family stays in the light");

        await Page.Locator("#filter-panel .filter-menu").ClickAsync();

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        await Expect(Page.Locator(".filter-rail .filter-menu")).ToBeFocusedAsync();
        await Expect(Page.Locator(".filter-rail .filter-menu")).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(Page.Locator("#filter-panel .filter-menu")).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(Page.Locator(".filter-rail .filter-menu")).ToHaveAttributeAsync("aria-controls", "filter-panel");
        await Expect(Page.Locator("#filter-panel .filter-menu")).ToHaveAttributeAsync("aria-controls", "filter-panel");

        await Page.EvaluateAsync("() => document.activeElement.blur()");
        await Page.Keyboard.PressAsync("/");

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "push");
        await Expect(Page.Locator("#filter-q")).ToBeFocusedAsync();
        (await Page.EvaluateAsync<bool>("() => { const field = document.getElementById('filter-q'); return field.selectionStart === 0 && field.selectionEnd === field.value.length && field.value.length > 0; }"))
            .Should().BeTrue("the slash selects the text already in the field");
        await SaveScreenshotAsync("filter-dock-push-filtering");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task No_filter_element_sits_above_the_cabinet_on_a_desktop()
    {
        await OpenAsync("/?sample=400", DockWidths.NarrowestDesktop, 900);

        foreach (var width in new[] { DockWidths.NarrowestDesktop, DockWidths.Push, DockWidths.Wide })
        {
            await Page.SetViewportSizeAsync(width, 900);
            await Page.EvaluateAsync("() => window.scrollTo(0, 0)");
            await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
            await Page.WaitForFunctionAsync("() => getComputedStyle(document.documentElement).getPropertyValue('--filter-bar-h').trim() === '0px'");

            var frame = await Page.EvaluateAsync<JsonElement>(ReadPageFrame);
            var label = $"at {width} px";
            frame.GetProperty("above").GetArrayLength().Should().Be(0, $"{label} nothing filter-related sits above the cabinet: {frame.GetProperty("above").GetRawText()}");
            frame.GetProperty("sentinelRendered").GetBoolean().Should().BeFalse(label);
            frame.GetProperty("cabinetTop").GetDouble().Should().BeApproximately(frame.GetProperty("headerBottom").GetDouble() + 32, 1, label);

            await ExpectRailAgainstTheEdgesAsync(label);
            await Page.EvaluateAsync("() => window.scrollTo(0, 1200)");
            await Page.WaitForFunctionAsync("() => window.scrollY >= 1199");
            await ExpectRailAgainstTheEdgesAsync($"{label} after scrolling");
        }

        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_dock_wears_its_states_from_the_stylesheet()
    {
        await OpenAsync("/?sample=65", DockWidths.Over, 720);

        await Page.EvaluateAsync("() => { document.documentElement.dataset.filterDock = 'over'; }");
        await Page.WaitForFunctionAsync("() => getComputedStyle(document.getElementById('filter-panel')).translate === 'none' || getComputedStyle(document.getElementById('filter-panel')).translate === '0px'");

        var over = await Page.EvaluateAsync<JsonElement>(ReadOverPanel);
        over.GetProperty("position").GetString().Should().Be("fixed");
        over.GetProperty("left").GetDouble().Should().BeApproximately(0, 0.5);
        over.GetProperty("top").GetDouble().Should().BeApproximately(0, 0.5);
        over.GetProperty("width").GetDouble().Should().BeApproximately(PanelWidth, 0.5);
        over.GetProperty("height").GetDouble().Should().BeApproximately(over.GetProperty("viewportHeight").GetDouble(), 0.5);
        over.GetProperty("background").GetString().Should().Be(over.GetProperty("wall").GetString(), "the panel over the cabinet stands on the wall colour");
        over.GetProperty("shadow").GetString().Should().Contain(over.GetProperty("shade").GetString(), "the shadow is tinted with the wall's shade");

        await Page.EvaluateAsync("() => { document.documentElement.dataset.filterDock = 'rail'; document.querySelector('.filter-rail-button[data-group=\"players\"]').setAttribute('data-filled', ''); document.querySelector('.filter-rail-button[data-group=\"time\"]').setAttribute('data-unavailable', ''); }");

        var states = await Page.EvaluateAsync<JsonElement>(ReadRailStates);
        states.GetProperty("filled").GetString().Should().Be(states.GetProperty("text").GetString(), "a filled rail button takes the wall text as its fill");
        states.GetProperty("unavailableBorder").GetString().Should().Be("dashed");
        states.GetProperty("unavailableFill").GetString().Should().NotBe(states.GetProperty("text").GetString());

        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });
        await Page.EvaluateAsync("() => { document.documentElement.dataset.filterDock = 'over'; }");
        (await Page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('filter-panel')).transitionDuration"))
            .Split(',').Select(part => part.Trim()).Should().OnlyContain(part => part == "0s");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Pushed_at_1440_the_columns_follow_the_room()
    {
        await OpenAsync("/?sample=400", DockWidths.Push, 900);

        await ExpectSectionsAsync(Cabinet, 2, FullSection, "folded");

        await OpenFilterPanelAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "push");

        var pushed = await ExpectSectionsAsync(Cabinet, 2, 32.25 * Rem, "pushed");
        pushed.Room.Should().BeApproximately(66 * Rem, 1, "the room beside the pushed panel is the page column");

        await FoldFilterPanelAsync();

        await ExpectSectionsAsync(Cabinet, 2, FullSection, "folded again");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_columns_follow_the_room_at_every_width()
    {
        await OpenAsync("/?sample=400", PushedMiddle, 900);

        await OpenFilterPanelAsync();
        await ExpectSectionsAsync(Cabinet, 2, 39.75 * Rem, $"pushed at {PushedMiddle} px");

        await Page.SetViewportSizeAsync(DockWidths.Wide, 900);
        await OpenFilterPanelAsync();
        await ExpectSectionsAsync(Cabinet, 2, FullSection, $"pushed at {DockWidths.Wide} px");

        await Page.SetViewportSizeAsync(PushedThreeColumns, 900);
        await OpenFilterPanelAsync();
        await ExpectSectionsAsync(Cabinet, 3, 39 * Rem, $"pushed at {PushedThreeColumns} px");

        await FoldFilterPanelAsync();
        await Page.SetViewportSizeAsync(DockWidths.Wide, 900);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        await ExpectSectionsAsync(Cabinet, 3, (DockWidths.Wide - RailWidth - 2 * PageGap - 2 * ColumnGap) / 3.0, $"folded at {DockWidths.Wide} px");

        await Page.SetViewportSizeAsync(DockWidths.Over, 900);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        var folded = await ExpectSectionsAsync(Cabinet, 2, (DockWidths.Over - RailWidth - 2 * PageGap - ColumnGap) / 2.0, $"folded at {DockWidths.Over} px");
        folded.Widths.Should().OnlyContain(width => width >= 35.5 * Rem, "beside the rail a section is never under 35.5rem");

        await OpenFilterPanelAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "over");
        var over = await ReadSectionsAsync(Cabinet);
        over.Widths.Should().Equal(folded.Widths, (actual, expected) => Math.Abs(actual - expected) <= 1, "the panel that lies over the cabinet leaves its sections as they were");
        over.FirstRow.Should().Be(folded.FirstRow);
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");

        await Page.SetViewportSizeAsync(DockWidths.Push, 900);
        await ExpectViewSwitchAvailableAsync();
        await OpenFilterPanelAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "push");
        await ChooseViewAsync(1);
        await Page.WaitForFunctionAsync("() => document.querySelectorAll('#cabinet .location-group').length > 1");
        await ExpectSectionsAsync(Groups, null, 32.25 * Rem, $"by location, pushed at {DockWidths.Push} px");

        ConsoleErrors.Should().BeEmpty();
    }

    private const string ReadPageFrame = """
        () => {
          const cabinet = document.getElementById('cabinet').getBoundingClientRect();
          const header = document.querySelector('body > header').getBoundingClientRect();
          const above = [...document.querySelectorAll('.filter-bar *')]
            .filter((element) => element.getClientRects().length > 0)
            .map((element) => ({ element, rect: element.getBoundingClientRect() }))
            .filter(({ rect }) => rect.width > 0 && rect.height > 0 && rect.right > header.left + 0.5 && rect.top < cabinet.top - 0.5)
            .map(({ element }) => element.className || element.id || element.tagName);
          return {
            above,
            sentinelRendered: document.querySelector('.filter-sentinel').getClientRects().length > 0,
            cabinetTop: cabinet.top,
            headerBottom: header.bottom,
          };
        }
        """;

    private const string ReadRail = """
        () => {
          const rect = document.querySelector('.filter-rail').getBoundingClientRect();
          return { left: rect.left, top: rect.top, bottom: rect.bottom, width: rect.width, viewportHeight: window.innerHeight };
        }
        """;

    private const string ReadOverPanel = """
        () => {
          const panel = document.getElementById('filter-panel');
          const rect = panel.getBoundingClientRect();
          const style = getComputedStyle(panel);
          const probe = document.createElement('span');
          probe.style.setProperty('background-color', 'var(--room)');
          document.body.append(probe);
          const wall = getComputedStyle(probe).backgroundColor;
          probe.remove();
          const shade = getComputedStyle(document.documentElement).getPropertyValue('--room-shade-rgb').trim().split(/\s+/).join(', ');
          return {
            position: style.position,
            left: rect.left,
            top: rect.top,
            width: rect.width,
            height: rect.height,
            viewportHeight: window.innerHeight,
            background: style.backgroundColor,
            wall,
            shadow: style.boxShadow,
            shade: `rgba(${shade}, 0.7)`,
          };
        }
        """;

    private const string ReadRailStates = """
        () => {
          const probe = document.createElement('span');
          probe.style.setProperty('background-color', 'var(--room-text)');
          document.body.append(probe);
          const text = getComputedStyle(probe).backgroundColor;
          probe.remove();
          const filled = getComputedStyle(document.querySelector('.filter-rail-button[data-group="players"]'));
          const unavailable = getComputedStyle(document.querySelector('.filter-rail-button[data-group="time"]'));
          return { text, filled: filled.backgroundColor, unavailableBorder: unavailable.borderTopStyle, unavailableFill: unavailable.backgroundColor };
        }
        """;

    private const string ReadSections = """
        (parent) => {
          const sections = [...document.querySelectorAll(parent)]
            .flatMap((element) => [...element.children])
            .filter((element) => element.classList.contains('section'))
            .map((element) => element.getBoundingClientRect());
          return {
            widths: sections.map((rect) => rect.width),
            firstRow: sections.filter((rect) => sections.length > 0 && Math.abs(rect.bottom - sections[0].bottom) <= 1).length,
            room: document.querySelector('.cabinet-room').getBoundingClientRect().width,
          };
        }
        """;

    /// <summary>
    /// Waits a moment for every section under the given parents to be drawn at the expected width, then checks the width and the
    /// number of sections in the first row, so a width that never arrives fails with the widths that were measured.
    /// </summary>
    private async Task<SectionReading> ExpectSectionsAsync(string parent, int? columns, double width, string label)
    {
        try
        {
            await Page.WaitForFunctionAsync(
                $"([parent, width]) => ({ReadSections})(parent).widths.every((actual) => Math.abs(actual - width) <= 1)",
                new object[] { parent, width },
                new PageWaitForFunctionOptions { Timeout = 5000 });
        }
        catch (PlaywrightException)
        {
        }

        var reading = await ReadSectionsAsync(parent);

        reading.Widths.Should().NotBeEmpty(label);
        reading.Widths.Should().OnlyContain(actual => Math.Abs(actual - width) <= 1, $"{label}: every section is {width / Rem}rem wide, measured {string.Join(", ", reading.Widths.Distinct())}");
        if (columns is not null)
        {
            reading.FirstRow.Should().Be(columns, $"{label}: the first row holds {columns} sections");
        }

        return reading;
    }

    private async Task<SectionReading> ReadSectionsAsync(string parent) =>
        JsonSerializer.Deserialize<SectionReading>((await Page.EvaluateAsync<JsonElement>(ReadSections, parent)).GetRawText(), WebOptions)!;

    private async Task ExpectRailAgainstTheEdgesAsync(string label)
    {
        var rail = await Page.EvaluateAsync<JsonElement>(ReadRail);
        rail.GetProperty("width").GetDouble().Should().BeApproximately(RailWidth, 0.5, label);
        rail.GetProperty("left").GetDouble().Should().BeApproximately(0, 0.5, $"{label}: the rail touches the left edge");
        rail.GetProperty("top").GetDouble().Should().BeApproximately(0, 0.5, $"{label}: the rail touches the top edge");
        rail.GetProperty("bottom").GetDouble().Should().BeApproximately(rail.GetProperty("viewportHeight").GetDouble(), 0.5, $"{label}: the rail touches the bottom edge");
    }

    private async Task OpenAsync(string path, int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await GotoCabinetAsync(path);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        await Page.EvaluateAsync("() => document.fonts.ready");
    }

    private async Task<IReadOnlyList<CardData>> LoadCardsAsync()
    {
        var json = await Page.EvaluateAsync<JsonElement>(
            "async address => (await (await fetch(address)).json()).cards",
            "/cabinet/cards?profile=desktop&sample=65");

        return JsonSerializer.Deserialize<List<CardData>>(json.GetRawText(), WebOptions)!;
    }

    private static string TitleOfABaseGameWithAnExpansion(IReadOnlyList<CardData> cards) =>
        cards.First(card => !card.IsExpansion && card.Expansions.Any(link => link.EntryId is not null)).Title;

    private static HashSet<string> LitEntries(IReadOnlyList<CardData> cards, string fragment)
    {
        var byEntry = cards.ToDictionary(card => card.EntryId);
        var families = cards
            .Where(card => !card.IsExpansion)
            .Select(card => new HashSet<long>([card.EntryId, .. card.Expansions.Where(link => link.EntryId is not null && byEntry.ContainsKey(link.EntryId.Value)).Select(link => link.EntryId!.Value)]))
            .ToList();
        var lit = new HashSet<string>(StringComparer.Ordinal);

        foreach (var card in cards)
        {
            var belongs = families.Where(family => family.Contains(card.EntryId)).ToList();
            var isLit = belongs.Count > 0
                ? belongs.Any(family => family.Any(entry => Contains(byEntry[entry].Title, fragment)))
                : Contains(card.Title, fragment);

            if (isLit)
            {
                lit.Add(card.EntryId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return lit;
    }

    private static bool Contains(string title, string fragment) => title.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    private sealed record SectionReading(List<double> Widths, int FirstRow, double Room);

    private sealed record CardLink(long? EntryId, int GameId, string Title);

    private sealed record CardData(long EntryId, string Title, bool IsExpansion, List<CardLink> Expansions);
}

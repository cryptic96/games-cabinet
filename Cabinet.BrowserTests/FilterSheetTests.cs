using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that below 64rem the bar is a slim bar with a Filters button and a bottom sheet that applies every tap
/// at once over a clear backdrop: the controls move between the desktop filter panel and the sheet one by one (on a desktop the sheet
/// never opens), the chips form grids every thumb can
/// hit, the sheet scrolls inside with its grip kept at the top, and it closes by its primary button, Escape, a tap on the cabinet, a
/// drag down and Back, each doing one thing. Only the primary button brings the first match into view.
/// </summary>
[Trait("Category", "Browser")]
public sealed partial class FilterSheetTests : CabinetPageTest
{
    private const string Bar = ".filter-bar";
    private const string OpenSheet = "dialog.filter-dialog[open]";
    private const string Sheet = ".filter-sheet";
    private const string Done = ".filter-done";
    private const string Boxes = "#cabinet .placement";
    private const string Dimmed = "#cabinet .placement[data-dim]";
    private const string OpenCard = "dialog.card-dialog[open]";
    private const string BlankPage = "about:blank";
    private const string PlayersGroup = "fieldset[data-filter=\"players\"]";
    private const string TimeGroup = "fieldset[data-filter=\"time\"]";
    private const string FailedDetails = "Couldn't load the details. Reload the page to try again.";

    private const string DescribeChildren = """
        (host) => [...host.children].filter((child) => !child.matches('label')).map((child) => {
          const kind = child.tagName.toLowerCase() + '.' + child.classList[0];
          return child.dataset.filter ? kind + '[' + child.dataset.filter + ']' : kind;
        })
        """;

    private const string ReadChipBoxes = """
        (group) => [...document.querySelector(group).querySelectorAll('.filter-chip')].map((chip) => {
          const rect = chip.getBoundingClientRect();
          return [rect.left, rect.top, rect.width, rect.height];
        })
        """;

    private const string LitBoxesInView = """
        () => {
          const bar = document.querySelector('.filter-bar').getBoundingClientRect();
          const band = Math.max(0, bar.bottom);
          return [...document.querySelectorAll('#cabinet .placement:not([data-dim]):not([data-kind="moreMarker"])')]
            .filter((box) => { const rect = box.getBoundingClientRect(); return rect.height > 0 && rect.bottom > band && rect.top < window.innerHeight; })
            .length;
        }
        """;

    private const string FindEmptyScroll = """
        () => {
          const band = document.querySelector('.filter-bar').offsetHeight;
          const lit = [...document.querySelectorAll('#cabinet .placement:not([data-dim]):not([data-kind="moreMarker"])')]
            .map((box) => { const rect = box.getBoundingClientRect(); return [rect.top + window.scrollY, rect.bottom + window.scrollY]; });
          const maxScroll = document.documentElement.scrollHeight - window.innerHeight;
          for (let y = 100; y <= maxScroll; y += 10) {
            const free = lit.every(([top, bottom]) => bottom <= y + band || top >= y + window.innerHeight);
            const behind = lit.some(([top]) => top >= y + band);
            if (free && behind) { return y; }
          }
          return -1;
        }
        """;

    private const string FindLitInView = """
        () => {
          const lit = [...document.querySelectorAll('#cabinet .placement:not([data-dim]):not([data-kind="moreMarker"])')];
          const target = lit[Math.floor(lit.length / 2)];
          target.scrollIntoView({ block: 'center', behavior: 'instant' });
          return window.scrollY;
        }
        """;

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private bool _started;

    [Fact]
    public async Task Filters_opens_a_live_sheet_and_Show_closes_it()
    {
        await OpenAsync(390, 844);

        await Expect(Page.Locator($"{Bar} #filter-q")).ToBeVisibleAsync();
        await Expect(Page.Locator($"{Bar} .filter-open")).ToBeVisibleAsync();
        await Expect(Page.Locator($"{Bar} .filter-open-text")).ToHaveTextAsync("Filters");
        (await Page.Locator($"{Bar} .filter-chip").CountAsync()).Should().Be(0, "the chips are in the sheet, not in the slim bar");
        (await Page.Locator($"{Bar} #filter-loc").CountAsync()).Should().Be(0);
        (await Page.Locator($"{Bar} fieldset").CountAsync()).Should().Be(0);

        await Page.Locator($"{Bar} .filter-open").ClickAsync();

        await Expect(Page.Locator(OpenSheet)).ToBeVisibleAsync();
        (await Page.Locator(".filter-sheet-groups").EvaluateAsync<string[]>(DescribeChildren)).Should().Equal(
            "fieldset.filter-view",
            "fieldset.filter-group[players]",
            "fieldset.filter-group[time]",
            "div.filter-location",
            "p.filter-note");
        (await Page.EvaluateAsync<bool>("() => document.activeElement.matches('input[name=\"filter-view\"]:checked')")).Should().BeTrue();
        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(0);

        await Page.Locator($"{PlayersGroup} input[value=\"4\"]").ClickAsync();

        await Expect(Page.Locator(Dimmed).First).ToBeAttachedAsync();
        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(1);
        var shown = await LitCountOfTheBarAsync();
        await Expect(Page.Locator(Done)).ToHaveTextAsync($"Show {Games(shown)}");
        await Expect(Page.Locator($"{Bar} .filter-open-text")).ToHaveTextAsync("Filters");
        await Expect(Page.Locator($"{Bar} .filter-open-count")).ToHaveTextAsync("1");
        await Expect(Page.Locator($"{Bar} .filter-open-suffix")).ToHaveTextAsync(" · 1 active");
        var dimmed = await Page.Locator(Dimmed).CountAsync();

        await Page.Locator(Done).ClickAsync();

        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        await Expect(Page.Locator(".filter-open")).ToBeFocusedAsync();
        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(dimmed);
        await Expect(Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Filters · 1 active" })).ToBeVisibleAsync();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task On_a_desktop_the_controls_are_in_the_panel_and_the_sheet_never_opens()
    {
        await OpenAsync(DockWidths.Over, 900);

        (await Page.Locator(".filter-open").EvaluateAsync<int>("(button) => button.getClientRects().length")).Should().Be(0, "the Filters button has no box on a desktop");
        await ExpectPanelOrderAsync("on a desktop");
        await Page.Locator(".filter-open").DispatchEventAsync("click");
        await Task.Delay(300, TestContext.Current.CancellationToken);
        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);

        await Page.SetViewportSizeAsync(390, 844);
        await Expect(Page.Locator(".filter-sheet-groups > fieldset.filter-view")).ToHaveCountAsync(1);
        (await Page.Locator(".filter-sheet-groups").EvaluateAsync<string[]>(DescribeChildren)).Should().Equal(
            "fieldset.filter-view",
            "fieldset.filter-group[players]",
            "fieldset.filter-group[time]",
            "div.filter-location",
            "p.filter-note");
        (await Page.Locator(PlayersGroup).CountAsync()).Should().Be(1);

        await Page.Locator($"{Bar} .filter-open").ClickAsync();
        await Expect(Page.Locator(OpenSheet)).ToBeVisibleAsync();
        (await Page.Locator(".filter-sheet-groups").EvaluateAsync<string[]>(DescribeChildren)).Take(4).Should().Equal(
            "fieldset.filter-view",
            "fieldset.filter-group[players]",
            "fieldset.filter-group[time]",
            "div.filter-location");

        await Page.SetViewportSizeAsync(DockWidths.Over, 900);
        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        await ExpectPanelOrderAsync("after a desktop, a phone and a desktop again");
        (await Page.Locator(PlayersGroup).CountAsync()).Should().Be(1);
        await Page.Locator(".filter-open").DispatchEventAsync("click");
        await Task.Delay(300, TestContext.Current.CancellationToken);
        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_card_details_note_moves_between_the_panel_and_the_sheet()
    {
        await StartAsync(PrototypeOn);
        await FailCardDataRequestsAsync();
        var note = Page.Locator("p.filter-note");

        foreach (var width in new[] { DockWidths.NarrowestDesktop, DockWidths.Push })
        {
            await Page.SetViewportSizeAsync(width, 900);
            await GotoCabinetAsync("/?sample=65");
            await Page.WaitForFunctionAsync("() => document.querySelector('.filter-note').textContent !== ''", null, new PageWaitForFunctionOptions { Timeout = 30000 });
            await OpenFilterPanelAsync();
            await Expect(note).ToBeVisibleAsync();
            await Expect(note).ToHaveTextAsync(FailedDetails);

            await ExpectNoteUnderTheCountLineAsync($"at {width} px");
            await FoldFilterPanelAsync();
        }

        await Page.SetViewportSizeAsync(390, 844);
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForFunctionAsync("() => document.querySelector('.filter-note').textContent !== ''", null, new PageWaitForFunctionOptions { Timeout = 30000 });
        await Expect(note).ToBeHiddenAsync();
        (await note.EvaluateAsync<bool>("(element) => element.closest('.filter-sheet') !== null")).Should().BeTrue("on a phone the note lives in the sheet");

        await Page.Locator($"{Bar} .filter-open").ClickAsync();
        await Expect(Page.Locator(OpenSheet)).ToBeVisibleAsync();
        await Expect(note).ToBeVisibleAsync();
        await Expect(note).ToHaveTextAsync(FailedDetails);
        (await Page.Locator(".filter-sheet-groups").EvaluateAsync<string[]>(DescribeChildren)).Last().Should().Be("p.filter-note");
        (await Page.EvaluateAsync<bool>("() => (document.querySelector('.filter-location').compareDocumentPosition(document.querySelector('.filter-note')) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0")).Should().BeTrue();

        await Page.SetViewportSizeAsync(DockWidths.Push, 900);
        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        await OpenFilterPanelAsync();
        await Expect(note).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30000 });
        await ExpectNoteUnderTheCountLineAsync("after the phone width");
        ConsoleErrors.Should().NotContain(error => error.Contains("Content Security", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task The_sheet_chips_form_their_grids_at_phone_widths()
    {
        await OpenAsync(320, 640);
        await OpenTheSheetAsync();
        var players = await ChipBoxesAsync(PlayersGroup);
        var time = await ChipBoxesAsync(TimeGroup);

        Columns(players).Should().Be(5, "players are five columns at 320 px");
        Rows(players).Should().Be(2);
        Columns(time).Should().Be(3, "time is three columns at 320 px");
        Rows(time).Should().Be(2);
        AtLeast44(players).Should().BeTrue();
        AtLeast44(time).Should().BeTrue();
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= document.documentElement.clientWidth")).Should().BeTrue("the page does not scroll sideways at 320 px");

        await OpenAsync(390, 844);
        await OpenTheSheetAsync();
        players = await ChipBoxesAsync(PlayersGroup);
        time = await ChipBoxesAsync(TimeGroup);

        Columns(players).Should().Be(5);
        Columns(time).Should().Be(6, "time is one row of six at 390 px");
        Rows(time).Should().Be(1);
        AtLeast44(players).Should().BeTrue();
        AtLeast44(time).Should().BeTrue();

        await OpenAsync(800, 1000);
        await OpenTheSheetAsync();
        players = await ChipBoxesAsync(PlayersGroup);

        Columns(players).Should().Be(9, "players are one row of nine in the 40rem sheet");
        Rows(players).Should().Be(1);
        AtLeast44(players).Should().BeTrue();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_sheet_shows_the_View_legend_and_the_Location_label()
    {
        await OpenAsync(390, 844);

        foreach (var language in new[] { "en", "nl" })
        {
            if (language == "nl")
            {
                await Page.Locator(".lang-toggle a[lang=nl]").ClickAsync();
                await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "nl");
                await Expect(Page.Locator(Bar)).ToBeVisibleAsync();
            }

            await OpenTheSheetAsync();
            var legend = Page.Locator(".filter-view > legend");
            var label = Page.Locator("label[for=\"filter-loc\"]");

            await Expect(legend).ToHaveTextAsync(language == "en" ? "View" : "Weergave");
            await Expect(label).ToHaveTextAsync(language == "en" ? "Location" : "Locatie");
            var sheetColour = await Page.Locator(Sheet).EvaluateAsync<string>("(element) => getComputedStyle(element).color");

            foreach (var element in new[] { legend, label })
            {
                var box = (await element.BoundingBoxAsync())!;
                box.Width.Should().BeGreaterThan(1);
                box.Height.Should().BeGreaterThan(1);
                await Expect(element).ToHaveCSSAsync("font-size", "14px");
                await Expect(element).ToHaveCSSAsync("font-weight", "600");
                await Expect(element).ToHaveCSSAsync("color", sheetColour);
                await Expect(element).ToHaveCSSAsync("clip-path", "none");
            }

            await Page.Locator(Done).ClickAsync();
            await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        }

        await Page.SetViewportSizeAsync(1280, 900);

        foreach (var selector in new[] { ".filter-view > legend", "label[for=\"filter-loc\"]" })
        {
            await Page.WaitForFunctionAsync(
                "(selector) => { const rect = document.querySelector(selector).getBoundingClientRect(); return rect.width <= 1.5 && rect.height <= 1.5; }",
                selector);
        }

        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_tall_sheet_scrolls_inside_with_the_grip_sticky()
    {
        await OpenAsync(320, 480);
        await OpenTheSheetAsync();
        var before = await Page.EvaluateAsync<double>("() => window.scrollY");

        var metrics = await Page.Locator(Sheet).EvaluateAsync<JsonElement>("(sheet) => ({ height: sheet.getBoundingClientRect().height, scrollHeight: sheet.scrollHeight, clientHeight: sheet.clientHeight })");

        metrics.GetProperty("height").GetDouble().Should().BeLessThanOrEqualTo((480 * 0.7) + 1);
        metrics.GetProperty("scrollHeight").GetDouble().Should().BeGreaterThan(metrics.GetProperty("clientHeight").GetDouble());

        await Page.Locator(Sheet).EvaluateAsync("(sheet) => { sheet.scrollTop = sheet.scrollHeight; }");
        await Expect(Page.Locator(Sheet)).Not.ToHaveJSPropertyAsync("scrollTop", 0);
        var tops = await Page.EvaluateAsync<double[]>("() => [document.querySelector('.filter-sheet').getBoundingClientRect().top, document.querySelector('.filter-grip').getBoundingClientRect().top]");

        tops[1].Should().BeApproximately(tops[0], 1);
        (await Page.EvaluateAsync<double>("() => window.scrollY")).Should().Be(before);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task With_no_filter_the_sheet_opens_on_Any_and_offers_every_game()
    {
        await OpenAsync(390, 844);
        var total = await TotalGamesAsync();

        await Expect(Page.Locator($"{Bar} #filter-q")).ToBeVisibleAsync();
        await Expect(Page.Locator($"{Bar} .filter-open")).ToBeVisibleAsync();
        await Expect(Page.Locator($"{Bar} .filter-count")).ToBeHiddenAsync();
        await Expect(Page.Locator($"{Bar} .filter-clear")).ToBeHiddenAsync();

        await OpenTheSheetAsync();

        await Expect(Page.Locator($"{PlayersGroup} input[value=\"\"]")).ToBeCheckedAsync();
        await Expect(Page.Locator($"{TimeGroup} input[value=\"\"]")).ToBeCheckedAsync();
        (await Page.Locator("#filter-loc").EvaluateAsync<string>("(select) => select.selectedOptions[0].textContent")).Should().Be("Any location");
        await Expect(Page.Locator(".filter-sheet-clear")).ToBeHiddenAsync();
        await Expect(Page.Locator(Done)).ToHaveTextAsync($"Show {Games(total)}");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Without_locations_or_card_data_the_sheet_holds_what_works()
    {
        await Page.SetViewportSizeAsync(390, 844);
        var host = await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(5));
        host.Cards.Should().NotBeEmpty();
        _started = true;
        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(Bar)).ToBeVisibleAsync();

        await OpenTheSheetAsync();

        await Expect(Page.Locator(".filter-view")).ToBeHiddenAsync();
        await Expect(Page.Locator(".filter-location")).ToBeHiddenAsync();
        (await Page.EvaluateAsync<bool>("() => document.activeElement.matches('fieldset[data-filter=\"players\"] input:checked')")).Should().BeTrue();
        (await Page.Locator(Sheet).EvaluateAsync<string[]>("(sheet) => [...sheet.querySelectorAll('.filter-sheet-groups > *:not([hidden])')].map((element) => element.className)")).Should().NotContain(["filter-view", "filter-location"]);
        await Page.Locator(Done).ClickAsync();
        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Failing_card_data_switches_off_the_card_based_choices_and_search_still_works()
    {
        await StartAsync(PrototypeOn);
        _started = true;
        await FailCardDataRequestsAsync();
        await Page.SetViewportSizeAsync(390, 844);
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForFunctionAsync("() => document.querySelector('.filter-note').textContent !== ''", null, new PageWaitForFunctionOptions { Timeout = 30000 });
        await Expect(Page.Locator(Bar)).ToBeVisibleAsync();

        await OpenTheSheetAsync();

        await Expect(Page.Locator($"{PlayersGroup} input").First).ToBeDisabledAsync();
        (await Page.Locator($"{PlayersGroup} input, {TimeGroup} input").EvaluateAllAsync<bool>("(inputs) => inputs.every((input) => input.disabled)")).Should().BeTrue();
        await Expect(Page.Locator("#filter-loc")).ToBeDisabledAsync();
        await Expect(Page.Locator($"{PlayersGroup} .filter-chip").First).ToHaveCSSAsync("border-top-style", "dashed");
        await Expect(Page.Locator("p.filter-note")).ToBeVisibleAsync();
        await Expect(Page.Locator("p.filter-note")).ToHaveTextAsync(FailedDetails);
        (await Page.Locator("p.filter-note").EvaluateAsync<bool>("(note) => note.closest('.filter-sheet') !== null")).Should().BeTrue();

        await Page.Locator(Done).ClickAsync();
        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        await Page.Locator("#filter-q").FillAsync("zzzzqxqx");

        await Expect(Page.Locator($"{Bar} .filter-count")).ToHaveTextAsync(NothingLit());
        (await Page.Locator(Boxes + ":not([data-dim])").CountAsync()).Should().Be(0);
        (await Page.Locator(Boxes).CountAsync()).Should().BeGreaterThan(10);
    }

    [Fact]
    public async Task A_tap_on_the_cabinet_closes_the_sheet_and_opens_nothing()
    {
        await OpenAsync(390, 1100);
        await OpenTheSheetAsync();
        var sheetTop = await Page.Locator(Sheet).EvaluateAsync<double>("(sheet) => sheet.getBoundingClientRect().top");
        var point = await Page.EvaluateAsync<double[]>(
            "(limit) => { const box = [...document.querySelectorAll('#cabinet .placement')].find((candidate) => { const rect = candidate.getBoundingClientRect(); return rect.top > 0 && rect.bottom < limit - 4 && rect.width > 6 && rect.height > 6; }); const rect = box.getBoundingClientRect(); return [rect.left + (rect.width / 2), rect.top + (rect.height / 2)]; }",
            sheetTop);

        await Page.Mouse.ClickAsync((float)point[0], (float)point[1]);

        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        await Task.Delay(400, TestContext.Current.CancellationToken);
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);

        await OpenTheSheetAsync();
        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        await Expect(Page.Locator(".filter-open")).ToBeFocusedAsync();
        await WaitUntilTheCardStepIsGoneAsync();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_drag_closes_and_a_short_drag_springs_back()
    {
        await OpenAsync(390, 844);
        await OpenTheSheetAsync();
        var sheet = (await Page.Locator(Sheet).BoundingBoxAsync())!;
        var grip = (await Page.Locator(".filter-grip").BoundingBoxAsync())!;
        var x = grip.X + (grip.Width / 2);
        var y = grip.Y + (grip.Height / 2);

        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(x, y + (sheet.Height * 0.08f), new MouseMoveOptions { Steps = 6 });
        await Expect(Page.Locator($"{Sheet}[data-moving]")).ToHaveCountAsync(1);
        await Task.Delay(250, TestContext.Current.CancellationToken);
        await Page.Mouse.UpAsync();

        await Expect(Page.Locator(Sheet)).ToHaveCSSAsync("translate", AtRest(), new LocatorAssertionsToHaveCSSOptions { Timeout = 600 });
        await Expect(Page.Locator($"{Sheet}[data-moving]")).ToHaveCountAsync(0);
        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(1);

        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(x, y + (sheet.Height * 0.3f), new MouseMoveOptions { Steps = 10 });
        await Page.Mouse.UpAsync();

        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        await Expect(Page.Locator(".filter-open")).ToBeFocusedAsync();
        await WaitUntilTheCardStepIsGoneAsync();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Back_closes_one_thing_at_a_time()
    {
        var address = await OpenAsync(390, 844, blankFirst: true);
        await OpenTheSheetAsync();
        await Page.Locator($"{PlayersGroup} input[value=\"4\"]").ClickAsync();
        await Expect(Page.Locator(Dimmed).First).ToBeAttachedAsync();
        var dimmed = await Page.Locator(Dimmed).CountAsync();

        await Page.GoBackAsync();

        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        Page.Url.Should().Be(address);
        await Expect(Page.Locator($"{PlayersGroup} input[value=\"4\"]")).ToBeCheckedAsync();
        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(dimmed);
        await Expect(Page.Locator(".filter-open")).ToBeFocusedAsync();
        (await Page.EvaluateAsync<bool>("() => window.history.state === null")).Should().BeTrue();

        await OpenTheSheetAsync();
        await Page.Locator(Done).ClickAsync();
        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        await WaitUntilTheCardStepIsGoneAsync();
        Page.Url.Should().Be(address);

        var lit = Page.Locator("#cabinet .placement:not([data-dim]):not([data-kind=\"moreMarker\"])").First;
        await lit.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator("html[data-pull-kind]")).ToHaveCountAsync(0);

        await Page.GoBackAsync();

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        Page.Url.Should().Be(address);
        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        await Expect(Page.Locator($"{PlayersGroup} input[value=\"4\"]")).ToBeCheckedAsync();

        await Page.GoBackAsync();

        Page.Url.Should().Be(BlankPage);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Show_brings_the_next_match_under_the_bar()
    {
        await OpenAsync(390, 420);
        var chosen = await ChoosePlayersWithFewMatchesAsync();
        await Page.Locator(Done).ClickAsync();
        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        var start = await Page.EvaluateAsync<double>(FindEmptyScroll);
        start.Should().BeGreaterThanOrEqualTo(0, "there is a scroll position with no lit box in view and one further down");

        await Page.EvaluateAsync("(y) => window.scrollTo({ top: y, behavior: 'instant' })", start);
        await Page.WaitForFunctionAsync("(y) => Math.abs(window.scrollY - y) < 2", start);
        (await Page.EvaluateAsync<int>(LitBoxesInView)).Should().Be(0);

        await OpenTheSheetAsync(scrollIntoView: false);
        await Page.Locator(Done).ClickAsync();

        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        await Page.WaitForFunctionAsync("(y) => Math.abs(window.scrollY - y) > 2", start);
        var landed = await Page.EvaluateAsync<JsonElement>("""
            () => {
              const band = Math.max(0, document.querySelector('.filter-bar').getBoundingClientRect().bottom);
              const tops = [...document.querySelectorAll('#cabinet .placement:not([data-dim]):not([data-kind="moreMarker"])')].map((box) => box.getBoundingClientRect().top - band);
              return { tops, atEnd: window.scrollY >= document.documentElement.scrollHeight - window.innerHeight - 1, litInView: tops.some((top) => top >= 0 && top < window.innerHeight) };
            }
            """);
        var gap = landed.GetProperty("tops").EnumerateArray().Select(top => Math.Abs(top.GetDouble() - 16)).Min();
        (gap <= 1 || landed.GetProperty("atEnd").GetBoolean()).Should().BeTrue("the next lit box sits 16 px under the bar, or the page ended first");
        landed.GetProperty("litInView").GetBoolean().Should().BeTrue();
        chosen.Should().BeGreaterThan(0);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Closing_any_other_way_never_scrolls_and_zero_matches_never_scroll()
    {
        await OpenAsync(390, 420);
        await ChoosePlayersWithFewMatchesAsync();
        await Page.Locator(Done).ClickAsync();
        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        var start = await Page.EvaluateAsync<double>(FindEmptyScroll);
        start.Should().BeGreaterThanOrEqualTo(0);
        await Page.EvaluateAsync("(y) => window.scrollTo({ top: y, behavior: 'instant' })", start);
        await Page.WaitForFunctionAsync("(y) => Math.abs(window.scrollY - y) < 2", start);

        await OpenTheSheetAsync(scrollIntoView: false);
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        await ExpectScrollAsync(start, "after Escape");

        await OpenTheSheetAsync(scrollIntoView: false);
        var grip = (await Page.Locator(".filter-grip").BoundingBoxAsync())!;
        var sheet = (await Page.Locator(Sheet).BoundingBoxAsync())!;
        await Page.Mouse.MoveAsync(grip.X + (grip.Width / 2), grip.Y + (grip.Height / 2));
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(grip.X + (grip.Width / 2), grip.Y + (sheet.Height * 0.5f), new MouseMoveOptions { Steps = 8 });
        await Page.Mouse.UpAsync();
        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        await ExpectScrollAsync(start, "after a drag");

        await OpenTheSheetAsync(scrollIntoView: false);
        await Page.Mouse.ClickAsync(195, 10);
        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        await ExpectScrollAsync(start, "after a tap on the cabinet");

        await OpenTheSheetAsync(scrollIntoView: false);
        await Page.GoBackAsync();
        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        await ExpectScrollAsync(start, "after Back");

        await Page.EvaluateAsync("() => window.scrollTo({ top: 0, behavior: 'instant' })");
        await Page.Locator("#filter-q").FillAsync("zzzzqxqx");
        await Expect(Page.Locator($"{Bar} .filter-count")).ToHaveTextAsync(NothingLit());
        await Page.EvaluateAsync("() => window.scrollTo({ top: 60, behavior: 'instant' })");
        await OpenTheSheetAsync(scrollIntoView: false);

        await Expect(Page.Locator(Done)).ToHaveTextAsync("Close");
        await Expect(Page.Locator(Done)).ToHaveAttributeAsync("data-outlined", string.Empty);
        await Expect(Page.Locator(".filter-sheet-clear")).ToHaveAttributeAsync("data-highlighted", string.Empty);
        var before = await Page.EvaluateAsync<double>("() => window.scrollY");

        await Page.Locator(Done).ClickAsync();

        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        await ExpectScrollAsync(before, "with nothing matching");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Clear_in_the_sheet_hands_focus_to_the_primary_button()
    {
        await OpenAsync(390, 844);
        var total = await TotalGamesAsync();
        await OpenTheSheetAsync();
        var view = await Page.EvaluateAsync<string>("() => document.querySelector('input[name=\"filter-view\"]:checked').value");
        await Page.Locator($"{PlayersGroup} input[value=\"4\"]").ClickAsync();
        await Expect(Page.Locator(Dimmed).First).ToBeAttachedAsync();
        await Expect(Page.Locator(".filter-sheet-clear")).ToBeVisibleAsync();

        await Page.Locator(".filter-sheet-clear").FocusAsync();
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(0);
        await Expect(Page.Locator($"{PlayersGroup} input[value=\"\"]")).ToBeCheckedAsync();
        (await Page.EvaluateAsync<string>("() => document.querySelector('input[name=\"filter-view\"]:checked').value")).Should().Be(view);
        await Expect(Page.Locator(".filter-sheet-clear")).ToBeHiddenAsync();
        await Expect(Page.Locator(Done)).ToBeFocusedAsync();
        await Expect(Page.Locator(Done)).ToHaveTextAsync($"Show {Games(total)}");
        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(1);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Show_does_not_scroll_when_a_match_is_already_in_view()
    {
        await OpenAsync(390, 844);
        await OpenTheSheetAsync();
        await Page.Locator($"{PlayersGroup} input[value=\"4\"]").ClickAsync();
        await Expect(Page.Locator(Dimmed).First).ToBeAttachedAsync();
        await Page.Locator(Done).ClickAsync();
        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        await Page.EvaluateAsync<double>(FindLitInView);
        await Page.WaitForFunctionAsync("() => window.scrollY > 0");
        (await Page.EvaluateAsync<int>(LitBoxesInView)).Should().BeGreaterThan(0);
        await OpenTheSheetAsync(scrollIntoView: false);
        var before = await Page.EvaluateAsync<double>("() => window.scrollY");

        await Page.Locator(Done).ClickAsync();

        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        await ExpectScrollAsync(before, "with a match in view");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Rotation_closes_and_a_live_redraw_keeps_the_sheet()
    {
        await Page.SetViewportSizeAsync(390, 844);
        var host = await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(40));
        var places = await host.SeedAsync("Cabinet A", "Shelf B");
        var games = host.Cards.DistinctBy(card => card.GameId).Take(3).ToList();
        await host.PlaceAsync(games[0], places[1]);
        await host.PlaceAsync(games[1], places[1]);
        await host.PlaceAsync(games[2], places[0]);
        _started = true;
        await Page.RouteAsync("**/cabinet/search-names**", route => route.FulfillAsync(new RouteFulfillOptions { Status = 200, ContentType = "application/json", Body = "{\"names\":{}}" }));
        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(Bar)).ToBeVisibleAsync();
        await OpenTheSheetAsync();

        await Page.Locator("#filter-loc").SelectOptionAsync(places[1]);
        await Expect(Page.Locator(Dimmed).First).ToBeAttachedAsync();
        var before = await LitCountOfTheBarAsync();
        await Expect(Page.Locator(Done)).ToHaveTextAsync($"Show {Games(before)}");

        await host.PlaceAsync(games[0], places[0]);
        host.TellOpenPages();

        await Expect(Page.Locator(Done)).Not.ToHaveTextAsync($"Show {Games(before)}", new LocatorAssertionsToHaveTextOptions { Timeout = 20000 });
        var after = await LitCountOfTheBarAsync();
        after.Should().BeLessThan(before);
        await Expect(Page.Locator(Done)).ToHaveTextAsync($"Show {Games(after)}");
        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(1);
        await Expect(Page.Locator("#filter-loc")).ToHaveValueAsync(places[1]);

        await Page.SetViewportSizeAsync(DockWidths.Over, 900);

        await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        await Expect(Page.Locator($"#filter-panel {PlayersGroup}")).ToHaveCountAsync(1);
        await Expect(Page.Locator("#filter-panel #filter-loc")).ToHaveCountAsync(1);
        await Expect(Page.Locator(".filter-sheet-groups > *")).ToHaveCountAsync(0);
        await ExpectPanelOrderAsync("after the rotation");
        await Expect(Page.Locator("#filter-loc")).ToHaveValueAsync(places[1]);
        ConsoleErrors.Should().BeEmpty();
    }

    private static string Games(int count) => count == 1 ? "1 game" : $"{count.ToString(CultureInfo.InvariantCulture)} games";

    private static int Columns(IReadOnlyList<double[]> boxes) => boxes.Select(box => Math.Round(box[0])).Distinct().Count();

    private static int Rows(IReadOnlyList<double[]> boxes) => boxes.Select(box => Math.Round(box[1])).Distinct().Count();

    private static bool AtLeast44(IReadOnlyList<double[]> boxes) => boxes.All(box => box[2] >= 43.99 && box[3] >= 43.99);

    [GeneratedRegex(@"^(none|0px|0px 0px|matrix\(1, 0, 0, 1, 0, 0\))$")]
    private static partial Regex AtRest();

    [GeneratedRegex(@"^0 of \d+$")]
    private static partial Regex NothingLit();

    [GeneratedRegex(@"^(\d+) of")]
    private static partial Regex LeadingCount();

    private async Task<string> OpenAsync(int width, int height, bool blankFirst = false)
    {
        await Page.SetViewportSizeAsync(width, height);

        if (!_started)
        {
            await StartAsync(PrototypeOn);
            await Page.RouteAsync("**/cabinet/search-names**", route => route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "application/json",
                Body = "{\"names\":{}}",
            }));
            _started = true;
        }

        if (blankFirst)
        {
            await Page.GotoAsync(BlankPage);
        }

        var address = await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(Bar)).ToBeVisibleAsync();

        return address;
    }

    private async Task OpenTheSheetAsync(bool scrollIntoView = true)
    {
        var button = Page.Locator($"{Bar} .filter-open");

        if (scrollIntoView)
        {
            await button.ClickAsync();
        }
        else
        {
            await button.DispatchEventAsync("click");
        }

        await Expect(Page.Locator(OpenSheet)).ToBeVisibleAsync();
        await Page.WaitForFunctionAsync("() => getComputedStyle(document.querySelector('.filter-sheet')).translate === 'none' || getComputedStyle(document.querySelector('.filter-sheet')).translate === '0px'");
    }

    private async Task ExpectPanelOrderAsync(string moment)
    {
        (await Page.Locator("#filter-panel .filter-panel-groups").EvaluateAsync<string[]>(DescribeChildren)).Should().Equal(
            ["fieldset.filter-view", "fieldset.filter-group[players]", "fieldset.filter-group[time]", "div.filter-location"],
            $"the panel's groups {moment}");
        (await Page.Locator("#filter-panel .filter-panel-find > .filter-find").CountAsync()).Should().Be(1, $"the search field is in the panel {moment}");
        (await Page.Locator("#filter-panel .filter-panel-tally > .filter-count").CountAsync()).Should().Be(1, $"the count is in the panel {moment}");
        (await Page.Locator("#filter-panel .filter-panel-note > p.filter-note").CountAsync()).Should().Be(1, $"the note is in the panel {moment}");
        (await Page.Locator("input[name=\"filter-players\"]").CountAsync()).Should().Be(9, $"one players group {moment}");
        (await Page.Locator("input[name=\"filter-time\"]").CountAsync()).Should().Be(6, $"one time group {moment}");
    }

    private async Task ExpectNoteUnderTheCountLineAsync(string moment)
    {
        var reading = await Page.EvaluateAsync<JsonElement>("""
            () => {
              const count = document.querySelector('#filter-panel .filter-panel-tally .filter-count').getBoundingClientRect();
              const note = document.querySelector('p.filter-note');
              const rect = note.getBoundingClientRect();
              return { inside: document.getElementById('filter-panel').contains(note), gap: rect.top - count.bottom, leftGap: rect.left - count.left, visible: note.getClientRects().length > 0 };
            }
            """);

        reading.GetProperty("visible").GetBoolean().Should().BeTrue(moment);
        reading.GetProperty("inside").GetBoolean().Should().BeTrue($"the note sits in the panel {moment}");
        reading.GetProperty("gap").GetDouble().Should().BeApproximately(8, 1, $"the note sits 8px under the count line {moment}");
        reading.GetProperty("leftGap").GetDouble().Should().BeApproximately(0, 1, $"the note lines up with the count line {moment}");
    }

    private async Task<List<double[]>> ChipBoxesAsync(string group) =>
        [.. await Page.EvaluateAsync<double[][]>(ReadChipBoxes, group)];

    private async Task<int> LitCountOfTheBarAsync()
    {
        var text = (await Page.Locator($"{Bar} .filter-count").TextContentAsync())!;

        if (text == "No games match")
        {
            return 0;
        }

        var match = LeadingCount().Match(text);

        match.Success.Should().BeTrue($"the bar's count reads '{text}'");

        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private async Task<int> TotalGamesAsync()
    {
        var json = await Page.EvaluateAsync<JsonElement>(
            "async address => (await (await fetch(address)).json()).cards",
            "/cabinet/cards?profile=phone&sample=65");
        var cards = JsonSerializer.Deserialize<List<FilterBarTests.CardData>>(json.GetRawText(), WebOptions)!;
        var byEntry = cards.ToDictionary(card => card.EntryId);
        var nested = cards.Where(card => !card.IsExpansion)
            .SelectMany(card => card.Expansions)
            .Where(link => link.EntryId is not null && byEntry.ContainsKey(link.EntryId.Value))
            .Select(link => link.EntryId!.Value)
            .ToHashSet();

        return cards.Count(card => !nested.Contains(card.EntryId));
    }

    private async Task<int> ChoosePlayersWithFewMatchesAsync()
    {
        await OpenTheSheetAsync();
        var best = 0;
        var fewest = int.MaxValue;

        foreach (var players in new[] { 8, 7, 6, 1 })
        {
            await Page.Locator($"{PlayersGroup} input[value=\"{players.ToString(CultureInfo.InvariantCulture)}\"]").ClickAsync();
            await Expect(Page.Locator(Dimmed).First).ToBeAttachedAsync();
            var lit = await LitCountOfTheBarAsync();

            if (lit > 0 && lit < fewest)
            {
                fewest = lit;
                best = players;
            }
        }

        await Page.Locator($"{PlayersGroup} input[value=\"{best.ToString(CultureInfo.InvariantCulture)}\"]").ClickAsync();
        await Expect(Page.Locator(Done)).ToHaveTextAsync($"Show {Games(fewest)}");

        return best;
    }

    private async Task ExpectScrollAsync(double expected, string moment)
    {
        await Task.Delay(300, TestContext.Current.CancellationToken);
        (await Page.EvaluateAsync<double>("() => window.scrollY")).Should().BeApproximately(expected, 1, moment);
    }
}

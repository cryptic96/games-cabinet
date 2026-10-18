using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser how the desktop filter panel behaves for the keyboard, screen readers and Back. Over the cabinet it is a
/// modal fly-over: the panel is the dialog, everything else is inert, Tab stays inside, and every close route (the head button,
/// Escape, one Back, a press outside) does exactly one thing and gives focus back to the rail. Beside the cabinet it is a plain
/// neighbour: no dialog, no inert page, no history step, and opening or folding it keeps the reader's place.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterDockFocusTests : CabinetPageTest
{
    private const int PanelWidth = 336;
    private const string RailMenu = ".filter-rail .filter-menu";
    private const string HeadMenu = "#filter-panel .filter-menu";
    private const string Panel = "#filter-panel";
    private const string OpenCard = "dialog.card-dialog[open]";
    private const string BlankPage = "about:blank";
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly string[] OutsideParts = [".skip-link", "body > header", "body > main", "body > footer", ".filter-rail"];

    private const string ReadModality = """
        () => {
          const panel = document.getElementById('filter-panel');
          const parts = ['.skip-link', 'body > header', 'body > main', 'body > footer', '.filter-rail'];
          return {
            role: panel.getAttribute('role'),
            modal: panel.getAttribute('aria-modal'),
            labelledBy: panel.getAttribute('aria-labelledby'),
            inert: parts.filter((selector) => document.querySelector(selector).hasAttribute('inert')),
            anyInert: document.querySelectorAll('[inert]').length,
            dockInert: document.querySelector('.filter-bar').hasAttribute('inert'),
            dockRole: document.querySelector('.filter-bar').getAttribute('role'),
          };
        }
        """;

    private const string ReadBox = """
        (selector) => {
          const rect = document.querySelector(selector).getBoundingClientRect();
          return [rect.left, rect.top, rect.width, rect.height];
        }
        """;

    [Fact]
    public async Task The_fly_over_is_modal_and_Escape_folds_it()
    {
        await OpenAsync("/?sample=65", DockWidths.Over, 800);
        var cabinetBefore = await BoxAsync("#cabinet");

        await Page.Locator(RailMenu).ClickAsync();

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "over");
        await ExpectOpenFlyOverAsync();
        await Expect(Page.Locator("#filter-q")).ToBeFocusedAsync();
        var cabinetOpen = await BoxAsync("#cabinet");

        for (var index = 0; index < 4; index++)
        {
            cabinetOpen[index].Should().BeApproximately(cabinetBefore[index], 1, "the fly-over never resizes the cabinet");
        }

        await ExpectPanelOnTheViewportsLeftAsync("at the laptop width");

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        await ExpectNotModalAsync("after Escape");
        await Expect(Page.Locator(RailMenu)).ToBeFocusedAsync();

        await Page.SetViewportSizeAsync(DockWidths.NarrowestDesktop, 800);
        await Page.WaitForFunctionAsync($"() => window.innerWidth === {DockWidths.NarrowestDesktop}");
        await Page.Locator(RailMenu).ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "over");
        await ExpectPanelOnTheViewportsLeftAsync("at the narrowest desktop");
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task One_Back_folds_the_fly_over_and_only_the_fly_over()
    {
        var address = await OpenAsync("/?sample=65", DockWidths.Over, 800, blankFirst: true);
        var lengthBefore = await Page.EvaluateAsync<int>("() => window.history.length");

        await OpenFlyOverAsync();
        await Page.Locator(HeadMenu).ClickAsync();
        await ExpectFoldedAsync("after the head button");
        await WaitUntilTheCardStepIsGoneAsync();

        await OpenFlyOverAsync();
        await Page.Keyboard.PressAsync("Escape");
        await ExpectFoldedAsync("after Escape");
        await WaitUntilTheCardStepIsGoneAsync();

        await OpenFlyOverAsync();
        var outside = await BoxPointAsync(PanelWidth + 40);
        await Page.Mouse.ClickAsync((float)outside[0], (float)outside[1]);
        await ExpectFoldedAsync("after a press outside");
        await WaitUntilTheCardStepIsGoneAsync();
        (await Page.EvaluateAsync<int>("() => window.history.length")).Should().BeLessThanOrEqualTo(lengthBefore + 1, "every close removed its step");

        var box = await BoxPointAsync(PanelWidth + 40);
        await Page.Mouse.ClickAsync((float)box[0], (float)box[1]);
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await WaitUntilTheCardStepIsGoneAsync();

        await OpenFlyOverAsync();
        await Page.GoBackAsync();

        await ExpectFoldedAsync("after one Back");
        Page.Url.Should().Be(address, "one Back folds the panel and stays on the page");
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        (await Page.EvaluateAsync<bool>("() => window.history.state === null")).Should().BeTrue();

        await Page.GoBackAsync();

        Page.Url.Should().Be(BlankPage, "the next Back leaves the page as before");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_press_outside_folds_the_fly_over_and_opens_no_card()
    {
        await OpenAsync("/?sample=65", DockWidths.Over, 800);
        await OpenFlyOverAsync();
        var point = await BoxPointAsync(PanelWidth + 40);

        await Page.Mouse.ClickAsync((float)point[0], (float)point[1]);

        await ExpectFoldedAsync("after a press on a box");
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);

        await OpenFlyOverAsync();
        await Page.EvaluateAsync("() => document.documentElement.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, cancelable: true, clientX: document.documentElement.clientWidth + 2, clientY: 200, isPrimary: true }))");
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "over");

        var before = await Page.EvaluateAsync<double>("() => window.scrollY");
        await Page.Mouse.MoveAsync((float)point[0], (float)point[1]);
        await Page.Mouse.WheelAsync(0, 600);
        await Page.WaitForFunctionAsync("(start) => window.scrollY > start + 100", before);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "over");
        await ExpectOpenFlyOverAsync();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_drag_after_a_press_outside_does_not_eat_the_next_tap()
    {
        await OpenAsync("/?sample=65", DockWidths.Over, 800);
        await OpenFlyOverAsync();
        var start = await BoxPointAsync(PanelWidth + 40);

        await Page.Mouse.MoveAsync((float)start[0], (float)start[1]);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync((float)start[0] + 80, (float)start[1], new MouseMoveOptions { Steps = 8 });
        await Page.Mouse.UpAsync();

        await ExpectFoldedAsync("after a press that turned into a drag");
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);

        var box = await BoxPointAsync(PanelWidth + 40);
        await Page.Mouse.ClickAsync((float)box[0], (float)box[1]);

        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Escape_in_the_search_field_clears_first()
    {
        await OpenAsync("/?sample=65", DockWidths.Over, 800);
        await OpenFlyOverAsync();
        await Expect(Page.Locator("#filter-q")).ToBeFocusedAsync();
        await Page.Keyboard.TypeAsync("ca");
        await Expect(Page.Locator(".filter-bar")).ToHaveAttributeAsync("data-active", string.Empty);

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator("#filter-q")).ToHaveValueAsync(string.Empty);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "over");
        await ExpectOpenFlyOverAsync();

        await Page.Keyboard.PressAsync("Escape");

        await ExpectFoldedAsync("after the second Escape");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Tab_stays_inside_the_fly_over()
    {
        await OpenAsync("/?sample=65", DockWidths.Over, 800);
        await OpenFlyOverAsync();

        for (var step = 0; step < 12; step++)
        {
            await Page.Keyboard.PressAsync("Tab");
            (await Page.EvaluateAsync<bool>("() => document.getElementById('filter-panel').contains(document.activeElement)")).Should().BeTrue($"Tab {step + 1} stays inside the panel");
        }

        await Page.Locator(HeadMenu).FocusAsync();
        await Page.Keyboard.PressAsync("Shift+Tab");
        (await Page.EvaluateAsync<bool>(IsOnLastStop)).Should().BeTrue("Shift+Tab from the first stop goes to the last");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator(HeadMenu)).ToBeFocusedAsync();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Each_rail_button_opens_the_panel_at_its_own_group()
    {
        await Page.SetViewportSizeAsync(DockWidths.Over, 800);
        var host = await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(20));
        var places = await host.SeedAsync("Cabinet A", "Shelf B");
        await host.PlaceAsync(host.Cards[0], places[0]);
        await host.PlaceAsync(host.Cards[1], places[1]);
        await Page.RouteAsync("**/cabinet/search-names**", route => route.FulfillAsync(new RouteFulfillOptions { Status = 200, ContentType = "application/json", Body = "{\"names\":{}}" }));
        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-rail-button[data-group=\"location\"]")).ToBeVisibleAsync();

        await OpenFlyOverAsync();
        await Page.Keyboard.TypeAsync("e");
        await Page.Locator(HeadMenu).ClickAsync();
        await ExpectFoldedAsync("before the rail buttons");
        await WaitUntilTheCardStepIsGoneAsync();

        foreach (var (width, open) in new[] { (DockWidths.Over, "over"), (DockWidths.Push, "push") })
        {
            await Page.SetViewportSizeAsync(width, 800);
            await Page.WaitForFunctionAsync($"() => window.innerWidth === {width}");
            await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");

            await PressRailButtonAsync("search", open);
            await Expect(Page.Locator("#filter-q")).ToBeFocusedAsync();
            (await FieldTextSelectedAsync()).Should().BeTrue($"the Search button selects the field's text at {width} px");
            await FoldAsync();

            await PressRailButtonAsync("players", open);
            (await Page.EvaluateAsync<bool>("() => document.activeElement.matches('#filter-panel input[name=\"filter-players\"]:checked')")).Should().BeTrue($"players at {width} px");
            await FoldAsync();

            await PressRailButtonAsync("time", open);
            (await Page.EvaluateAsync<bool>("() => document.activeElement.matches('#filter-panel input[name=\"filter-time\"]:checked')")).Should().BeTrue($"time at {width} px");
            await FoldAsync();

            await PressRailButtonAsync("location", open);
            await Expect(Page.Locator("#filter-loc")).ToBeFocusedAsync();
            await FoldAsync();

            await Page.EvaluateAsync("() => document.activeElement.blur()");
            await Page.Keyboard.PressAsync("/");
            await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", open);
            await Expect(Page.Locator("#filter-q")).ToBeFocusedAsync();
            (await FieldTextSelectedAsync()).Should().BeTrue($"the slash selects the field's text at {width} px");
            await FoldAsync();
        }

        await FailCardDataRequestsAsync();
        await Page.SetViewportSizeAsync(DockWidths.Over, 800);
        await GotoCabinetAsync("/");
        await Expect(Page.Locator("p.filter-note")).ToHaveTextAsync("Couldn't load the details. Reload the page to try again.", new LocatorAssertionsToHaveTextOptions { Timeout = 30000 });
        await Page.EvaluateAsync("() => document.querySelector('.filter-rail-button[data-group=\"players\"]').setAttribute('data-unavailable', '')");

        await PressRailButtonAsync("players", "over");

        (await Page.EvaluateAsync<bool>("() => document.getElementById('filter-panel').contains(document.activeElement)")).Should().BeTrue("a switched-off group still opens the panel with focus inside it");
        await ExpectOpenFlyOverAsync();
        ConsoleErrors.Should().NotContain(error => error.Contains("Content Security", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task The_pushed_panel_is_not_modal()
    {
        await OpenAsync("/?sample=65", DockWidths.Push, 900);
        var lengthBefore = await Page.EvaluateAsync<int>("() => window.history.length");

        await Page.Locator(RailMenu).ClickAsync();

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "push");
        await Expect(Page.Locator(HeadMenu)).ToBeFocusedAsync();
        await ExpectNotModalAsync("while pushed open");

        await Page.Keyboard.PressAsync("Escape");
        await Task.Delay(200, TestContext.Current.CancellationToken);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "push");
        (await Page.EvaluateAsync<int>("() => window.history.length")).Should().Be(lengthBefore, "the pushed panel takes no history step");
        (await Page.EvaluateAsync<bool>("() => window.history.state === null")).Should().BeTrue();

        await Page.EvaluateAsync(FocusLastStop);
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator("#cabinet .placement[tabindex=\"0\"]")).ToBeFocusedAsync();

        await Page.Locator(HeadMenu).ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        await Expect(Page.Locator(RailMenu)).ToBeFocusedAsync();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_card_opens_over_the_pushed_panel_and_gives_focus_back_to_its_box()
    {
        await OpenAsync("/?sample=65", DockWidths.Push, 900);
        await Page.Locator(RailMenu).ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "push");
        var entry = await Page.EvaluateAsync<string>("(left) => [...document.querySelectorAll('#cabinet .placement:not([data-kind=\"moreMarker\"])')].find((box) => { const rect = box.getBoundingClientRect(); return rect.left > left && rect.top > 0 && rect.bottom < window.innerHeight && rect.width > 6; }).dataset.entryId", PanelWidth);
        var box = Page.Locator($"#cabinet .placement[data-entry-id=\"{entry}\"]:not([data-kind=\"moreMarker\"])").First;

        await box.ClickAsync();

        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "push");

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "push");
        await Expect(box).ToBeFocusedAsync();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Opening_and_folding_the_push_keeps_your_place()
    {
        await OpenAsync("/?sample=400", DockWidths.Push, 900);
        (await Page.Locator("#cabinet").EvaluateAsync<string>("(cabinet) => getComputedStyle(cabinet).overflowAnchor")).Should().Be("none");
        await Page.EvaluateAsync("() => window.scrollTo(0, 1600)");
        await Page.WaitForFunctionAsync("() => window.scrollY >= 1599");
        var anchor = await Page.EvaluateAsync<int>(FindAnchor);
        var before = await AnchorTopAsync(anchor);

        await Page.Locator(RailMenu).ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "push");

        (await AnchorTopAsync(anchor)).Should().BeApproximately(before, 1, "opening the pushed panel keeps the anchored box in place");

        await Page.Locator(HeadMenu).ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");

        (await AnchorTopAsync(anchor)).Should().BeApproximately(before, 1, "folding it keeps the anchored box in place too");
        ConsoleErrors.Should().BeEmpty();
    }

    private const string PanelStops = """
        const stops = [...document.getElementById('filter-panel').querySelectorAll('a[href], button, input, select, textarea, [tabindex]')]
          .filter((element) => !element.disabled && element.tabIndex >= 0 && element.getClientRects().length > 0 && getComputedStyle(element).visibility !== 'hidden')
          .filter((element) => element.type !== 'radio' || element.checked);
        """;

    private const string IsOnLastStop = "() => { " + PanelStops + " return document.activeElement === stops[stops.length - 1]; }";

    private const string FocusLastStop = "() => { " + PanelStops + " stops[stops.length - 1].focus(); }";

    private const string FindAnchor = """
        () => {
          const boxes = [...document.querySelectorAll('#cabinet .placement')];
          let best = -1;
          let bestTop = Infinity;
          boxes.forEach((box, index) => { const rect = box.getBoundingClientRect(); if (rect.height > 0 && rect.top >= 0 && rect.top < bestTop) { best = index; bestTop = rect.top; } });
          return best;
        }
        """;

    private async Task<double> AnchorTopAsync(int index) =>
        await Page.EvaluateAsync<double>("(index) => document.querySelectorAll('#cabinet .placement')[index].getBoundingClientRect().top", index);

    private async Task OpenFlyOverAsync()
    {
        await Page.Locator(RailMenu).ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "over");
        await Page.WaitForFunctionAsync("() => window.history.state !== null && window.history.state.dock === true");
        await ExpectOpenFlyOverAsync();
    }

    private async Task ExpectFoldedAsync(string moment)
    {
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        await ExpectNotModalAsync(moment);
        await Expect(Page.Locator(RailMenu)).ToBeFocusedAsync();
    }

    private async Task PressRailButtonAsync(string group, string open)
    {
        await Page.Locator($".filter-rail-button[data-group=\"{group}\"]").ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", open);
    }

    private async Task FoldAsync()
    {
        await Page.Locator(HeadMenu).ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        await WaitUntilTheCardStepIsGoneAsync();
    }

    private async Task<bool> FieldTextSelectedAsync() =>
        await Page.EvaluateAsync<bool>("() => { const field = document.getElementById('filter-q'); return field.value.length > 0 && field.selectionStart === 0 && field.selectionEnd === field.value.length; }");

    private async Task<double[]> BoxPointAsync(double left) => await Page.EvaluateAsync<double[]>(
        "(left) => { const box = [...document.querySelectorAll('#cabinet .placement:not([data-kind=\"moreMarker\"])')].find((candidate) => { const rect = candidate.getBoundingClientRect(); return rect.left > left && rect.top > 40 && rect.bottom < window.innerHeight - 40 && rect.width > 6 && rect.height > 6; }); const rect = box.getBoundingClientRect(); return [rect.left + (rect.width / 2), rect.top + (rect.height / 2)]; }",
        left);

    private async Task ExpectOpenFlyOverAsync()
    {
        var modality = await Page.EvaluateAsync<JsonElement>(ReadModality);
        modality.GetProperty("role").GetString().Should().Be("dialog");
        modality.GetProperty("modal").GetString().Should().Be("true");
        modality.GetProperty("labelledBy").GetString().Should().Be("filter-panel-title");
        modality.GetProperty("inert").EnumerateArray().Select(part => part.GetString()).Should().BeEquivalentTo(OutsideParts);
        modality.GetProperty("dockInert").GetBoolean().Should().BeFalse("the dock holds the panel and is never inert");
        modality.GetProperty("dockRole").GetString().Should().Be("search", "the dock stays the search landmark");
    }

    private async Task ExpectNotModalAsync(string moment)
    {
        var modality = await Page.EvaluateAsync<JsonElement>(ReadModality);
        modality.GetProperty("role").ValueKind.Should().Be(JsonValueKind.Null, moment);
        modality.GetProperty("modal").ValueKind.Should().Be(JsonValueKind.Null, moment);
        modality.GetProperty("anyInert").GetInt32().Should().Be(0, moment);
    }

    private async Task ExpectPanelOnTheViewportsLeftAsync(string moment)
    {
        await Page.WaitForFunctionAsync("() => { const translate = getComputedStyle(document.getElementById('filter-panel')).translate; return translate === 'none' || translate === '0px'; }");
        var panel = await BoxAsync(Panel);
        var height = await Page.EvaluateAsync<double>("() => window.innerHeight");
        panel[0].Should().BeApproximately(0, 0.5, moment);
        panel[1].Should().BeApproximately(0, 0.5, moment);
        panel[2].Should().BeApproximately(PanelWidth, 0.5, moment);
        panel[3].Should().BeApproximately(height, 0.5, moment);
    }

    private async Task<double[]> BoxAsync(string selector) => await Page.EvaluateAsync<double[]>(ReadBox, selector);

    private async Task<string> OpenAsync(string path, int width, int height, bool blankFirst = false)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await Page.RouteAsync("**/cabinet/search-names**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "application/json",
            Body = "{\"names\":{}}",
        }));

        if (blankFirst)
        {
            await Page.GotoAsync(BlankPage);
        }

        var address = await GotoCabinetAsync(path);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        await Page.EvaluateAsync("() => document.fonts.ready");

        return address;
    }
}

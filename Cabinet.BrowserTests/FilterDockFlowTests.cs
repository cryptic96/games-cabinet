using System.Globalization;
using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using FluentAssertions;
using Microsoft.Playwright;
using SkiaSharp;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the flows that cross the desktop filter dock land where they should: Show on shelf lands the found
/// box in the page column beside a pushed panel or the folded rail, inside the viewport and ringed, with nothing above it but the
/// viewport's top; and the view switch fades only the cabinet, under an open panel that is drawn exactly as it is at rest, and
/// scrolls up to the cabinet's top when the visitor had scrolled past it; and a live redraw after an owner change keeps the dock as it
/// is, folded, pushed or flying over, with its scroll position and focus, while the counts and Clear change in the same task as the
/// new boxes. When the owner removes every location, the location controls go and the cabinet shows all games together.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterDockFlowTests : CabinetPageTest
{
    private const string Boxes = "#cabinet .placement";
    private const string Groups = "#cabinet section.location-group";
    private const string OpenCard = "dialog.card-dialog[open]";
    private const string Found = "#cabinet .placement[data-found]";
    private const string LocationRadio = "input[name=\"filter-view\"][value=\"location\"]";
    private const string NothingMatches = "zzzzqqqq";
    private const double HeadingGap = 16;
    private const string PlayersFour = "#filter-panel fieldset[data-filter=\"players\"] input[value=\"4\"]";

    /// <summary>A viewport low enough that the open panel is taller than it and scrolls inside itself.</summary>
    private const int ShortViewport = 420;

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

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

    /// <summary>How fast the page's animations run while the middle of the view fade is captured: twenty times slower than normal.</summary>
    private const double SlowPlayback = 0.05;

    /// <summary>How long after the fade starts the slowed fade is captured, in real milliseconds: early in the old view's fade-out.</summary>
    private const int MidFadeDelayMs = 1000;

    /// <summary>The distance between two sampled pixels inside the panel, in CSS pixels.</summary>
    private const int SampleStep = 12;

    /// <summary>The largest difference of one colour channel between two samples that still counts as the same pixel.</summary>
    private const int ChannelTolerance = 8;

    /// <summary>
    /// Records the view transition of a view switch: the transition names of the panel and the dock while the switch runs, and when
    /// the fade is ready and finished.
    /// </summary>
    private const string InstallSwitchSpy = """
        (() => {
          const spy = { readyAt: null, finishedAt: null, panelName: null, dockName: null };
          window.__switch = spy;
          const original = document.startViewTransition?.bind(document);
          if (original === undefined) {
            return;
          }
          document.startViewTransition = (callback) => {
            const transition = original(callback);
            if (document.documentElement.hasAttribute('data-switching-view')) {
              spy.panelName = getComputedStyle(document.getElementById('filter-panel')).viewTransitionName;
              spy.dockName = getComputedStyle(document.querySelector('.filter-bar')).viewTransitionName;
              transition.ready.then(() => { spy.readyAt = performance.now(); }, () => {});
              transition.finished.then(() => { spy.finishedAt = performance.now(); }, () => {});
            }
            return transition;
          };
        })();
        """;

    /// <summary>Measures the panel and the view switch inside it, which changes its own look when it is pressed.</summary>
    private const string MeasurePanelScript = """
        () => {
          const box = (element) => {
            const rect = element.getBoundingClientRect();
            return { left: rect.left, top: rect.top, right: rect.right, bottom: rect.bottom };
          };
          return { panel: box(document.getElementById('filter-panel')), view: box(document.querySelector('#filter-panel .filter-view')) };
        }
        """;

    /// <summary>
    /// Records, in the same task as every redraw that puts boxes on the page, the dock's state, the scroll positions of the dock and
    /// the panel, the focused element and what the rail and the panel say, so a test can tell they were already right when the new
    /// boxes appeared.
    /// </summary>
    private const string InstallRedrawRecorder = """
        (() => {
          const rec = { redraws: [] };
          window.__redraw = rec;
          new MutationObserver((records) => {
            const drew = records.some((record) => [...record.addedNodes].some((node) => node instanceof Element && (node.matches('.placement') || node.querySelector('.placement') !== null)));
            if (!drew) {
              return;
            }
            const text = (selector) => document.querySelector(selector)?.textContent ?? null;
            const hidden = (selector) => document.querySelector(selector)?.hidden ?? null;
            rec.redraws.push({
              dock: document.documentElement.dataset.filterDock ?? null,
              dockScroll: document.querySelector('.filter-bar').scrollTop,
              panelScroll: document.getElementById('filter-panel').scrollTop,
              focus: document.activeElement?.id || document.activeElement?.getAttribute('data-group') || document.activeElement?.value || null,
              railCount: text('.filter-rail-count-number'),
              railCountHidden: hidden('.filter-rail-count'),
              panelCount: text('#filter-panel .filter-count'),
              panelClearHidden: hidden('#filter-panel .filter-clear'),
              railClearHidden: hidden('.filter-rail-clear'),
            });
          }).observe(document, { childList: true, subtree: true });
        })();
        """;

    /// <summary>Reads the same things the redraw recorder reads, as they stand now.</summary>
    private const string ReadDockNowScript = """
        () => {
          const text = (selector) => document.querySelector(selector)?.textContent ?? null;
          const hidden = (selector) => document.querySelector(selector)?.hidden ?? null;
          return {
            dock: document.documentElement.dataset.filterDock ?? null,
            dockScroll: document.querySelector('.filter-bar').scrollTop,
            panelScroll: document.getElementById('filter-panel').scrollTop,
            focus: document.activeElement?.id || document.activeElement?.getAttribute('data-group') || document.activeElement?.value || null,
            railCount: text('.filter-rail-count-number'),
            railCountHidden: hidden('.filter-rail-count'),
            panelCount: text('#filter-panel .filter-count'),
            panelClearHidden: hidden('#filter-panel .filter-clear'),
            railClearHidden: hidden('.filter-rail-clear'),
          };
        }
        """;

    private const string DockRightScript = """
        () => Math.max(...[...document.querySelectorAll('.filter-rail, #filter-panel')]
          .map((part) => part.getBoundingClientRect())
          .filter((rect) => rect.width > 0)
          .map((rect) => rect.right))
        """;

    [Fact]
    public async Task Show_on_shelf_lands_beside_the_pushed_panel()
    {
        await OpenSampleAsync(DockWidths.Push, 900);
        await OpenFilterPanelAsync();
        (await DockStateAsync()).Should().Be("push");
        var pick = await PickFirstRowBoxAsync(lastGroup: true);
        await Page.Locator("#filter-q").FillAsync(NothingMatches);
        await Expect(Page.Locator(Boxes + "[data-dim]").First).ToBeAttachedAsync();

        await OpenCardAsync(pick);
        await Page.Locator("button.card-where-link").ClickAsync();

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        await Expect(Page.Locator(LocationRadio)).ToBeCheckedAsync();
        await Expect(Page.Locator(Found)).ToHaveAttributeAsync("data-entry-id", pick);
        var pushed = (await FilterShowOnShelfTests.ReadLandingsAsync(Page)).Single();
        var panelRight = await Page.Locator("#filter-panel").EvaluateAsync<double>("(panel) => panel.getBoundingClientRect().right");

        (await DockStateAsync()).Should().Be("push", "Show on shelf leaves the pushed panel open");
        pushed.Dimmed.Should().BeTrue("a filter that dims every box is on, and the answer is shown anyway");
        pushed.Veil.Should().Be("0", "the veil is lifted while the box is marked");
        pushed.Left.Should().BeGreaterThan(panelRight, "the box lands in the page column, never under the panel");
        AssertLanding(pushed, "beside the pushed panel");
        (await Page.Locator("#filter-q").InputValueAsync()).Should().Be(NothingMatches, "Show on shelf leaves the filter as it is");

        await FoldFilterPanelAsync();
        var second = await PickFirstRowBoxAsync(lastGroup: false);

        await OpenCardAsync(second);
        await Page.Locator("button.card-where-link").ClickAsync();

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        await Expect(Page.Locator(Found)).ToHaveAttributeAsync("data-entry-id", second);
        var folded = (await FilterShowOnShelfTests.ReadLandingsAsync(Page)).Single();
        var railRight = await Page.EvaluateAsync<double>(DockRightScript);

        (await DockStateAsync()).Should().Be("rail");
        folded.Left.Should().BeGreaterThan(railRight, "the box lands in the page column, right of the rail");
        AssertLanding(folded, "beside the folded rail");
        ConsoleErrors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(DockWidths.Over, "over")]
    [InlineData(DockWidths.Push, "push")]
    public async Task The_view_fade_never_paints_over_an_open_panel(int width, string state)
    {
        await OpenSampleAsync(width, 900, InstallSwitchSpy);
        await ExpectViewSwitchAvailableAsync();
        await OpenFilterPanelAsync();
        (await DockStateAsync()).Should().Be(state);
        var segment = Page.Locator("#filter-panel .filter-view-segment").Nth(1);
        await segment.HoverAsync();
        await Page.Locator(LocationRadio).FocusAsync();
        await Page.WaitForFunctionAsync("() => document.getElementById('filter-panel').getAnimations({ subtree: true }).length === 0");
        var measured = await Page.EvaluateAsync<JsonElement>(MeasurePanelScript);
        var before = await Page.ScreenshotAsync();
        var session = await Page.Context.NewCDPSessionAsync(Page);
        await session.SendAsync("Animation.enable");
        await session.SendAsync("Animation.setPlaybackRate", new Dictionary<string, object> { ["playbackRate"] = SlowPlayback });

        await segment.ClickAsync();

        await Page.WaitForFunctionAsync("() => window.__switch.readyAt !== null");
        await Task.Delay(MidFadeDelayMs, TestContext.Current.CancellationToken);
        var during = await Page.ScreenshotAsync();
        var running = await Page.EvaluateAsync<bool>("() => window.__switch.finishedAt === null && document.documentElement.hasAttribute('data-switching-view')");
        var names = await Page.EvaluateAsync<string[]>("() => [window.__switch.panelName, window.__switch.dockName]");
        await session.SendAsync("Animation.setPlaybackRate", new Dictionary<string, object> { ["playbackRate"] = 1 });
        await Page.WaitForFunctionAsync("() => !document.documentElement.hasAttribute('data-switching-view')");

        running.Should().BeTrue("the second picture was taken while the fade was still running");
        var differing = DifferingSamples(before, during, measured);
        differing.Sampled.Should().BeGreaterThan(200, "the panel is sampled all over");
        differing.Points.Should().BeEmpty("the panel looks exactly as it did at rest: nothing painted over it, not brighter, not doubled");
        names.Should().Equal(["filter-panel", "none"], "the panel, never the dock, is named while the view switches");
        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        (await DockStateAsync()).Should().Be(state, "the panel is still open after the switch");
        (await Page.EvaluateAsync<bool>($"() => document.activeElement === document.querySelector('{LocationRadio}')")).Should().BeTrue("focus stays where it was");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_switch_scrolls_up_to_the_cabinet_top_on_a_desktop()
    {
        await OpenSampleAsync(DockWidths.Push, 420);
        await ExpectViewSwitchAvailableAsync();
        await OpenFilterPanelAsync();
        await Page.EvaluateAsync("() => window.scrollTo(0, document.documentElement.scrollHeight)");
        await Page.WaitForFunctionAsync("() => window.scrollY > 300 && document.getElementById('cabinet').getBoundingClientRect().top < 0");
        var before = await Page.EvaluateAsync<double>("() => window.scrollY");

        await Page.Locator("#filter-panel .filter-view-segment").Nth(1).ClickAsync();

        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        await Page.WaitForFunctionAsync("() => !document.documentElement.hasAttribute('data-switching-view')");
        (await Page.EvaluateAsync<double>("() => window.scrollY")).Should().BeLessThanOrEqualTo(before + 0.5, "the page never scrolls down");
        (await Page.EvaluateAsync<double>("() => document.getElementById('cabinet').getBoundingClientRect().top")).Should().BeApproximately(HeadingGap, 1, "the cabinet's top sits 16 px under the viewport top");
        (await DockStateAsync()).Should().Be("push");

        await Page.EvaluateAsync("() => window.scrollTo(0, 0)");
        await Page.WaitForFunctionAsync("() => window.scrollY === 0");

        await Page.Locator("#filter-panel .filter-view-segment").Nth(0).ClickAsync();

        await Expect(Page.Locator(Groups)).ToHaveCountAsync(0);
        await Page.WaitForFunctionAsync("() => !document.documentElement.hasAttribute('data-switching-view')");
        (await Page.EvaluateAsync<double>("() => window.scrollY")).Should().Be(0, "with the cabinet's top in view a switch does not scroll");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_live_redraw_keeps_the_dock_as_it_is()
    {
        await Page.SetViewportSizeAsync(DockWidths.Push, ShortViewport);
        var host = await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(60));
        var places = await host.SeedAsync("Cabinet A", "Shelf B");

        foreach (var game in host.Cards.DistinctBy(card => card.GameId).Take(30))
        {
            await host.PlaceAsync(game, places[1]);
        }

        await Page.AddInitScriptAsync(InstallRedrawRecorder);
        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        await OpenFilterPanelAsync();
        await Page.Locator(PlayersFour).ClickAsync();
        await Page.Locator("#filter-loc").SelectOptionAsync(places[1]);
        await Expect(Page.Locator(Boxes + "[data-dim]").First).ToBeAttachedAsync();
        await FoldFilterPanelAsync();

        await Page.Locator(".filter-rail-button[data-group=\"players\"]").FocusAsync();
        var folded = await RedrawAfterMovingALitGameAsync(host, places[0]);
        folded.Dock.Should().Be("rail", "a redraw leaves the folded rail folded");
        folded.Focus.Should().Be("players", "focus stays on the rail button");
        folded.RailCountHidden.Should().BeFalse("the rail still shows the count while filtering");

        await Page.Locator(".filter-rail .filter-menu").ClickAsync();
        await Page.WaitForFunctionAsync("() => document.documentElement.dataset.filterDock === 'push'");
        var pushedScroll = await ScrollTheDockAsync(".filter-bar");
        await Page.Locator(PlayersFour).FocusAsync();
        var pushed = await RedrawAfterMovingALitGameAsync(host, places[0]);
        pushed.Dock.Should().Be("push", "a redraw leaves the pushed panel open");
        pushed.DockScroll.Should().BeApproximately(pushedScroll, 1, "the panel keeps its own scroll position");
        pushed.Focus.Should().Be("4", "focus stays on the chosen chip");
        pushed.PanelClearHidden.Should().BeFalse("the panel's Clear stays while filtering");

        await Page.SetViewportSizeAsync(DockWidths.Over, ShortViewport);
        await Page.WaitForFunctionAsync("() => document.documentElement.dataset.filterDock === 'rail'");
        await Page.Locator(".filter-rail .filter-menu").ClickAsync();
        await Page.WaitForFunctionAsync("() => document.documentElement.dataset.filterDock === 'over' && history.state?.dock !== undefined");
        await Page.WaitForFunctionAsync("() => document.getElementById('filter-panel').getAnimations().length === 0");
        var overScroll = await ScrollTheDockAsync("#filter-panel");
        await Page.Locator(PlayersFour).FocusAsync();
        var over = await RedrawAfterMovingALitGameAsync(host, places[0]);
        over.Dock.Should().Be("over", "the redraw ran while the fly-over was open, not after it folded");
        over.PanelScroll.Should().BeApproximately(overScroll, 1, "the fly-over keeps its own scroll position");
        over.Focus.Should().Be("4", "focus stays on the chosen chip inside the fly-over");
        (await Page.Locator("#filter-panel").GetAttributeAsync("role")).Should().Be("dialog", "the fly-over is still modal");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Removing_every_location_takes_the_location_controls_away()
    {
        await Page.SetViewportSizeAsync(DockWidths.Push, 900);
        var host = await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(60));
        var places = await host.SeedAsync("Cabinet A", "Shelf B");
        var games = host.Cards.DistinctBy(card => card.GameId).Take(2).ToList();
        await host.PlaceAsync(games[0], places[0]);
        await host.PlaceAsync(games[1], places[1]);
        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        await OpenFilterPanelAsync();
        await ChooseViewAsync(1);
        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        await Page.WaitForFunctionAsync("() => localStorage.getItem('cabinet-view') === 'location'");
        await Expect(Page.Locator(".filter-rail-button[data-group=\"location\"]")).ToBeAttachedAsync();
        await Expect(Page.Locator("#filter-panel .filter-view")).ToBeVisibleAsync();
        await Expect(Page.Locator("#filter-panel .filter-location")).ToBeVisibleAsync();

        await host.DeleteLocationAsync(places[0]);
        await host.DeleteLocationAsync(places[1]);
        host.TellOpenPages();

        await Expect(Page.Locator(Groups)).ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions { Timeout = 20000 });
        (await Page.Locator("#cabinet .section").CountAsync()).Should().BeGreaterThan(0, "the cabinet shows all games together");
        await Expect(Page.Locator("input[name=\"filter-view\"][value=\"all\"]")).ToBeCheckedAsync();
        (await RenderedBoxesAsync(".filter-rail-button[data-group=\"location\"]")).Should().Be(0, "the Location rail button goes");
        (await RenderedBoxesAsync("#filter-panel .filter-view")).Should().Be(0, "the View group goes");
        (await RenderedBoxesAsync("#filter-panel .filter-location")).Should().Be(0, "the Location group goes");
        (await DockStateAsync()).Should().Be("push", "the panel stays open");
        (await Page.EvaluateAsync<string?>("() => localStorage.getItem('cabinet-view')")).Should().Be("location", "falling back to all together is not remembered");
        ConsoleErrors.Should().BeEmpty();
    }

    /// <summary>
    /// Moves one game the filters light out of the chosen place, tells the open page, and returns what the page showed in the task
    /// that put the new boxes on the page. The game has no expansion, because a lit expansion box keeps its base game's entry in the
    /// count. Before returning it checks that the rail's count, the panel's count line and the two Clear buttons already showed their
    /// new state in that task: the numbers changed and they are the ones the page keeps afterwards.
    /// </summary>
    /// <param name="host">The running host.</param>
    /// <param name="elsewhere">The location the game is moved to.</param>
    /// <returns>The dock as it was when the redraw drew.</returns>
    private async Task<DockReading> RedrawAfterMovingALitGameAsync(OwnerHost host, string elsewhere)
    {
        var lit = await Page.EvaluateAsync<int>("""
            async () => {
              const cards = (await (await fetch('/cabinet/cards?profile=desktop')).json()).cards;
              const lit = new Set([...document.querySelectorAll('#cabinet .placement:not([data-dim]):not([data-kind="moreMarker"])')].map((box) => box.dataset.entryId));
              return cards.find((card) => !card.isExpansion && card.expansions.length === 0 && lit.has(String(card.entryId))).gameId;
            }
            """);
        var before = await ReadDockAsync(ReadDockNowScript);
        await Page.EvaluateAsync("() => { window.__redraw.redraws.length = 0; }");

        await host.PlaceAsync(host.Cards.First(card => card.GameId == lit), elsewhere);
        host.TellOpenPages();

        await Page.WaitForFunctionAsync("() => window.__redraw.redraws.length > 0", null, new PageWaitForFunctionOptions { Timeout = 20000 });
        var json = await Page.EvaluateAsync<JsonElement>("() => window.__redraw.redraws[0]");
        var drawn = JsonSerializer.Deserialize<DockReading>(json.GetRawText(), WebOptions)!;
        var after = await ReadDockAsync(ReadDockNowScript);

        drawn.RailCount.Should().NotBe(before.RailCount, "the rail's count changed with the redraw");
        drawn.RailCount.Should().Be(after.RailCount, "the rail's count was already new when the boxes were drawn");
        drawn.PanelCount.Should().NotBe(before.PanelCount, "the panel's count line changed with the redraw");
        drawn.PanelCount.Should().Be(after.PanelCount, "the panel's count line was already new when the boxes were drawn");
        drawn.RailClearHidden.Should().Be(after.RailClearHidden, "the rail's Clear was already right when the boxes were drawn");
        drawn.PanelClearHidden.Should().Be(after.PanelClearHidden, "the panel's Clear was already right when the boxes were drawn");
        drawn.Dock.Should().Be(after.Dock, "the dock did not change after the redraw either");

        return drawn;
    }

    /// <summary>Scrolls the element that scrolls the open panel to a little below its top and returns where it came to rest.</summary>
    /// <param name="scroller">The element that scrolls: the dock while the panel is pushed, the panel itself while it flies over.</param>
    /// <returns>The scroll position.</returns>
    private async Task<double> ScrollTheDockAsync(string scroller)
    {
        var position = await Page.Locator(scroller).EvaluateAsync<double>("(element) => { element.scrollTop = 120; return element.scrollTop; }");
        position.Should().BeGreaterThan(0, "the panel is taller than the short viewport, so it scrolls inside itself");

        return position;
    }

    private async Task<DockReading> ReadDockAsync(string script)
    {
        var json = await Page.EvaluateAsync<JsonElement>(script);

        return JsonSerializer.Deserialize<DockReading>(json.GetRawText(), WebOptions)!;
    }

    private Task<int> RenderedBoxesAsync(string selector) =>
        Page.EvaluateAsync<int>("(selector) => [...document.querySelectorAll(selector)].filter((element) => element.getClientRects().length > 0).length", selector);

    /// <summary>
    /// Compares two screenshots at points spread over the panel, leaving out the view switch itself (which is pressed) with a margin
    /// for its focus ring.
    /// </summary>
    /// <param name="first">The screenshot at rest.</param>
    /// <param name="second">The screenshot in the middle of the fade.</param>
    /// <param name="measured">The panel's and the view switch's boxes.</param>
    /// <returns>How many points were sampled and where the two differ.</returns>
    private static (int Sampled, List<string> Points) DifferingSamples(byte[] first, byte[] second, JsonElement measured)
    {
        using var rest = SKBitmap.Decode(first);
        using var fading = SKBitmap.Decode(second);
        var panel = Edges.Of(measured.GetProperty("panel"));
        var view = Edges.Of(measured.GetProperty("view"));
        var points = new List<string>();
        var sampled = 0;

        for (var y = panel.Top + 2; y < Math.Min(panel.Bottom, rest.Height) - 2; y += SampleStep)
        {
            for (var x = panel.Left + 2; x < Math.Min(panel.Right, rest.Width) - 2; x += SampleStep)
            {
                if (x > view.Left - 10 && x < view.Right + 10 && y > view.Top - 10 && y < view.Bottom + 10)
                {
                    continue;
                }

                sampled += 1;
                var a = rest.GetPixel((int)x, (int)y);
                var b = fading.GetPixel((int)x, (int)y);

                if (Math.Abs(a.Red - b.Red) > ChannelTolerance || Math.Abs(a.Green - b.Green) > ChannelTolerance || Math.Abs(a.Blue - b.Blue) > ChannelTolerance)
                {
                    points.Add($"({(int)x}, {(int)y}): {a} became {b}");
                }
            }
        }

        return (sampled, points);
    }

    /// <summary>
    /// Checks a desktop landing: measured on a still layout, nothing above it but the viewport's top, inside the viewport, the heading
    /// 16 px under the top when heading and box both fit and the box centred otherwise, and the found ring in the default wood's light
    /// colour.
    /// </summary>
    /// <param name="landing">The landing the recorder measured.</param>
    /// <param name="where">Where the dock stood, for the failure message.</param>
    private static void AssertLanding(FilterShowOnShelfTests.Landing landing, string where)
    {
        landing.Settled.Should().BeTrue($"the landing {where} is measured once the layout stopped moving");
        landing.BarEdge.Should().Be(0, "on a desktop nothing covers the top of the page");
        landing.Top.Should().BeGreaterThanOrEqualTo(0, $"the box {where} is inside the viewport");
        landing.Bottom.Should().BeLessThanOrEqualTo(landing.Viewport, $"the box {where} is inside the viewport");

        if (landing.Fits)
        {
            landing.HeadingTop!.Value.Should().BeApproximately(HeadingGap, 1.5, $"the heading lands 16 px under the viewport top {where}");
        }
        else
        {
            ((landing.Top + landing.Bottom) / 2).Should().BeApproximately(landing.Viewport / 2, 2, $"the box {where} is centred in the viewport");
        }

        landing.OutlineStyle.Should().Be("solid");
        landing.OutlineWidth.Should().Be("3px", "the found ring is 3px so it stays distinct from the 2px focus ring");
        landing.OutlineOffset.Should().Be("2px");
        landing.OutlineColor.Should().Be(RgbOf(landing.WoodLight), "the found ring is the wood's light colour");
    }

    /// <summary>Writes a six-digit hexadecimal colour the way the browser reports a computed colour.</summary>
    /// <param name="hex">The colour, such as <c>#d9b98a</c>.</param>
    /// <returns>The colour as <c>rgb(r, g, b)</c>.</returns>
    private static string RgbOf(string hex)
    {
        hex.Should().MatchRegex("^#[0-9a-fA-F]{6}$", "the default wood writes its light colour as a hexadecimal value");
        var channel = (int start) => int.Parse(hex.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        return $"rgb({channel(1)}, {channel(3)}, {channel(5)})";
    }

    private async Task OpenSampleAsync(int width, int height, string? extraScript = null)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await Page.RouteAsync("**/cabinet/search-names**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "application/json",
            Body = "{\"names\":{}}",
        }));
        await Page.AddInitScriptAsync(FilterShowOnShelfTests.InstallRecorder);

        if (extraScript is not null)
        {
            await Page.AddInitScriptAsync(extraScript);
        }

        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Picks a placed game whose box stands in the first row of the first or the last location cabinet, measured in the layout the
    /// page has now, so its heading and box fit in the viewport together, then shows all games together again at the top of the page.
    /// The first cabinet's heading can always be scrolled to the top; the last one's only while the page below it is tall enough,
    /// as it is beside the pushed panel.
    /// </summary>
    /// <param name="lastGroup">Whether to pick from the last location cabinet rather than the first.</param>
    /// <returns>The entry id of the picked box.</returns>
    private async Task<string> PickFirstRowBoxAsync(bool lastGroup)
    {
        var json = await Page.EvaluateAsync<JsonElement>("async address => (await (await fetch(address)).json()).cards", "/cabinet/cards?profile=desktop&sample=65");
        var cards = JsonSerializer.Deserialize<List<ViewCard>>(json.GetRawText(), WebOptions)!
            .ToDictionary(card => card.EntryId.ToString(CultureInfo.InvariantCulture));

        await ChooseViewAsync(1);
        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        var measured = await Page.EvaluateAsync<JsonElement>(MeasureGroups);
        var placed = JsonSerializer.Deserialize<List<MeasuredGroup>>(measured.GetRawText(), WebOptions)!
            .Where(group => group.LocationId != "unknown")
            .ToList();
        var group = lastGroup ? placed[^1] : placed[0];
        var chosen = group.Boxes
            .Where(box => cards.TryGetValue(box.Id, out var card) && !card.IsExpansion && card.LocationId == group.LocationId)
            .OrderBy(box => box.Span)
            .First();

        await ChooseViewAsync(0);
        await Expect(Page.Locator(Groups)).ToHaveCountAsync(0);
        await Page.EvaluateAsync("() => { window.scrollTo(0, 0); window.__found.shown.length = 0; window.__found.cleared.length = 0; }");

        return chosen.Id;
    }

    private async Task OpenCardAsync(string entryId)
    {
        var box = Page.Locator($"{Boxes}[data-entry-id=\"{entryId}\"]:not([data-kind=\"moreMarker\"])").First;

        await box.ScrollIntoViewIfNeededAsync();
        await box.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
    }

    /// <summary>The edges of a box in the viewport, in CSS pixels.</summary>
    /// <param name="Left">The left edge.</param>
    /// <param name="Top">The top edge.</param>
    /// <param name="Right">The right edge.</param>
    /// <param name="Bottom">The bottom edge.</param>
    private sealed record Edges(double Left, double Top, double Right, double Bottom)
    {
        /// <summary>Reads the edges the page measured.</summary>
        /// <param name="box">The measured box.</param>
        /// <returns>The edges.</returns>
        public static Edges Of(JsonElement box) => new(
            box.GetProperty("left").GetDouble(),
            box.GetProperty("top").GetDouble(),
            box.GetProperty("right").GetDouble(),
            box.GetProperty("bottom").GetDouble());
    }

    /// <summary>The dock and what it says, read in the page.</summary>
    /// <param name="Dock">The dock's state: rail, push or over.</param>
    /// <param name="DockScroll">How far the dock is scrolled inside itself.</param>
    /// <param name="PanelScroll">How far the panel is scrolled inside itself.</param>
    /// <param name="Focus">The focused element's id, rail group or value.</param>
    /// <param name="RailCount">The number the rail's count shows.</param>
    /// <param name="RailCountHidden">Whether the rail's count is hidden.</param>
    /// <param name="PanelCount">The panel's count line.</param>
    /// <param name="PanelClearHidden">Whether the panel's Clear is hidden.</param>
    /// <param name="RailClearHidden">Whether the rail's Clear is hidden.</param>
    private sealed record DockReading(
        string? Dock,
        double DockScroll,
        double PanelScroll,
        string? Focus,
        string? RailCount,
        bool? RailCountHidden,
        string? PanelCount,
        bool? PanelClearHidden,
        bool? RailClearHidden);

    private sealed record MeasuredBox(string Id, double Span);

    private sealed record MeasuredGroup(string LocationId, List<MeasuredBox> Boxes);

    private sealed record ViewCard(long EntryId, bool IsExpansion, string? LocationId);
}

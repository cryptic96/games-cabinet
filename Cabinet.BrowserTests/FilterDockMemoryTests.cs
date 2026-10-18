using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the device remembers whether the visitor left the desktop filter panel open, and that the page reads
/// it before the first paint: a panel left open at a width where it stands beside the cabinet is open in the very first frame after a
/// reload, with the cabinet's column already where it settles, and a folded one gives the rail. The value is read by a small external
/// script in the head, so the page carries no inline script and the strict content security policy is never broken. The stored value
/// only ever says what the visitor chose at such a width: a width change, a fly-over and the page itself never write it, the page never
/// loads with a fly-over, storage that throws keeps the choice in memory, and the value is apart from the tab's filters and the view.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterDockMemoryTests : CabinetPageTest
{
    private const string Bar = ".filter-bar";
    private const string Boxes = "#cabinet .placement";
    private const string PanelKey = "cabinet-filter-panel";
    private const string ViewKey = "cabinet-view";
    private const string FiltersKey = "cabinet-filters";
    private const string RailMenu = ".filter-rail .filter-menu";
    private const string Dimmed = "#cabinet .placement[data-dim]";
    private const string PanelFourPlayers = "#filter-panel fieldset[data-filter=\"players\"] input[value=\"4\"]";

    /// <summary>Makes every attempt to reach the device's local storage throw, as a browser in a strict private mode does.</summary>
    private const string BlockLocalStorage = """
        Object.defineProperty(window, 'localStorage', {
          configurable: true,
          get() {
            throw new DOMException('The operation is insecure.', 'SecurityError');
          },
        });
        """;
    private const double Tolerance = 1;
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    /// <summary>
    /// Records, from the first animation frame on (which runs before the first paint), the dock's state on the root, the left edge of
    /// the cabinet's column once it exists and how many boxes are drawn, for a few seconds of frames. It also records every content
    /// security policy violation.
    /// </summary>
    private const string InstallFrameRecorder = """
        (() => {
          const rec = { frames: [], violations: [] };
          window.__memory = rec;
          document.addEventListener('securitypolicyviolation', (event) => rec.violations.push(event.violatedDirective));
          const record = () => {
            const room = document.querySelector('.cabinet-room');
            rec.frames.push({
              dock: document.documentElement.dataset.filterDock ?? null,
              roomLeft: room === null ? null : room.getBoundingClientRect().left,
              boxes: document.querySelectorAll('#cabinet .placement').length,
              dimmed: document.querySelectorAll('#cabinet .placement[data-dim]').length,
            });
            if (rec.frames.length < 240) {
              requestAnimationFrame(record);
            }
          };
          requestAnimationFrame(record);
        })();
        """;

    private bool _started;

    [Fact]
    public async Task Left_open_at_a_push_width_it_is_open_in_the_first_frame()
    {
        await OpenAsync(DockWidths.Push);
        (await DockStateAsync()).Should().Be("rail", "a first visit starts folded");
        (await StoredPanelAsync()).Should().BeNull("nothing is written until the visitor opens or folds the panel");

        await OpenFilterPanelAsync();
        (await DockStateAsync()).Should().Be("push");
        await ExpectStoredPanelAsync("open");

        await ReloadAsync();
        (await DockStateAsync()).Should().Be("push", "the panel the visitor left open comes back open");
        await AssertFirstFrameAsync("push");

        await FoldFilterPanelAsync();
        await ExpectStoredPanelAsync("folded");

        await ReloadAsync();
        (await DockStateAsync()).Should().Be("rail", "the panel the visitor folded comes back folded");
        await AssertFirstFrameAsync("rail");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_head_script_is_an_external_file()
    {
        await OpenAsync(DockWidths.Push);

        var json = await Page.EvaluateAsync<JsonElement>(
            "() => [...document.head.querySelectorAll('script')].map((element) => ({ src: element.getAttribute('src') ?? '', type: element.type, async: element.async, defer: element.defer }))");
        var scripts = JsonSerializer.Deserialize<List<HeadScript>>(json.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var dockScripts = scripts.Where(script => script.Src.Contains("/js/filter-dock-head", StringComparison.Ordinal)).ToList();

        dockScripts.Should().ContainSingle("the head holds the dock's head script once");
        var dockScript = dockScripts[0];
        dockScript.Src.Should().MatchRegex(
            @"^/js/filter-dock-head\.[a-z0-9]+\.js$|^/js/filter-dock-head\.js\?v=\S+$",
            "it is the external file, versioned (in a fingerprinted name or a version query) so a new release is never served from an old cache");
        dockScript.Type.Should().BeEmpty("it is a classic script, not a module");
        dockScript.Async.Should().BeFalse("it blocks parsing, so it runs before the first paint");
        dockScript.Defer.Should().BeFalse("it blocks parsing, so it runs before the first paint");

        (await Page.EvaluateAsync<int>("() => document.querySelectorAll('script:not([src])').length")).Should().Be(0, "the page carries no inline script");
        (await Page.EvaluateAsync<string[]>("() => window.__memory.violations")).Should().BeEmpty();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_stored_choice_is_honest()
    {
        await OpenAsync(DockWidths.Push);
        (await DockStateAsync()).Should().Be("rail", "with nothing stored the page starts folded");

        await Page.Keyboard.PressAsync("/");
        await WaitForDockAsync("push");
        await ExpectStoredPanelAsync("open");
        await Expect(Page.Locator("#filter-q")).ToBeFocusedAsync();

        await FoldFilterPanelAsync();
        await ExpectStoredPanelAsync("folded");

        await OpenFilterPanelAsync();
        await ExpectStoredPanelAsync("open");

        await Page.SetViewportSizeAsync(DockWidths.Over, 900);
        await WaitForDockAsync("rail");
        (await StoredPanelAsync()).Should().Be("open", "a width change never writes the choice");

        await Page.SetViewportSizeAsync(DockWidths.Push, 900);
        await WaitForDockAsync("push");
        (await StoredPanelAsync()).Should().Be("open");

        await Page.SetViewportSizeAsync(DockWidths.Over, 900);
        await WaitForDockAsync("rail");
        await ReloadAsync();
        (await DockStateAsync()).Should().Be("rail", "below the push width the page always loads folded, never with a fly-over");
        await AssertFirstFrameAsync("rail");
        (await StoredPanelAsync()).Should().Be("open", "loading at a narrower width leaves the choice as it was");

        await OpenFilterPanelAsync();
        (await DockStateAsync()).Should().Be("over");
        await FoldFilterPanelAsync();
        (await StoredPanelAsync()).Should().Be("open", "a fold over the cabinet is never remembered");

        await Page.EvaluateAsync($"() => localStorage.removeItem('{PanelKey}')");
        await OpenFilterPanelAsync();
        (await StoredPanelAsync()).Should().BeNull("an open over the cabinet is never remembered");
        await FoldFilterPanelAsync();
        (await StoredPanelAsync()).Should().BeNull("a fold over the cabinet is never remembered");

        await Page.SetViewportSizeAsync(DockWidths.Push, 900);
        await ReloadAsync();
        (await DockStateAsync()).Should().Be("rail", "an absent value means folded");
        (await StoredPanelAsync()).Should().BeNull("the page never writes the choice on its own");

        await Page.EvaluateAsync($"() => localStorage.setItem('{PanelKey}', 'pushed')");
        await ReloadAsync();
        (await DockStateAsync()).Should().Be("rail", "an unknown value means folded");
        await AssertFirstFrameAsync("rail");
        (await StoredPanelAsync()).Should().Be("pushed", "the page never writes the choice on its own");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Storage_that_throws_keeps_the_choice_in_memory()
    {
        await Page.AddInitScriptAsync(BlockLocalStorage);
        await OpenAsync(DockWidths.Push);
        (await DockStateAsync()).Should().Be("rail", "without storage the page starts folded");

        await OpenFilterPanelAsync();
        (await DockStateAsync()).Should().Be("push");

        await Page.SetViewportSizeAsync(DockWidths.Over, 900);
        await WaitForDockAsync("rail");
        await Page.SetViewportSizeAsync(DockWidths.Push, 900);
        await WaitForDockAsync("push");

        await FoldFilterPanelAsync();
        await Page.SetViewportSizeAsync(DockWidths.Over, 900);
        await WaitForDockAsync("rail");
        await Page.SetViewportSizeAsync(DockWidths.Push, 900);
        await Page.WaitForFunctionAsync("() => window.matchMedia('(min-width: 90rem)').matches");
        (await DockStateAsync()).Should().Be("rail", "the fold was kept in memory");

        await Page.Locator(RailMenu).ClickAsync();
        await WaitForDockAsync("push");
        await FoldFilterPanelAsync();

        await ReloadAsync();
        (await DockStateAsync()).Should().Be("rail");
        (await Page.EvaluateAsync<string[]>("() => window.__memory.violations")).Should().BeEmpty();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Filters_view_and_panel_are_remembered_separately()
    {
        await Page.AddInitScriptAsync(
            $"if (sessionStorage.getItem('{FiltersKey}') === null) {{ sessionStorage.setItem('{FiltersKey}', JSON.stringify({{ q: '', players: 4, time: null, location: null }})); }}");
        await OpenAsync(DockWidths.Push);

        (await DockStateAsync()).Should().Be("rail", "stored filters do not open the panel");
        await AssertFirstDrawDimmedAsync();
        await Expect(Page.Locator(".filter-rail .filter-rail-button[data-group=\"players\"]")).ToHaveAttributeAsync("data-filled", string.Empty);
        await Expect(Page.Locator(".filter-rail .filter-rail-count")).ToBeVisibleAsync();
        await Expect(Page.Locator(".filter-rail .filter-rail-clear")).ToBeVisibleAsync();
        (await StoredPanelAsync()).Should().BeNull();

        await Page.EvaluateAsync($"() => localStorage.setItem('{PanelKey}', 'open')");
        await ReloadAsync();
        (await DockStateAsync()).Should().Be("push", "the panel remembered open comes back open, with the filters");
        await AssertFirstDrawDimmedAsync();
        await Expect(Page.Locator(PanelFourPlayers)).ToBeCheckedAsync();

        await ExpectViewSwitchAvailableAsync();
        await Page.Locator("#filter-panel .filter-view-segment").Nth(1).ClickAsync();
        await Page.WaitForFunctionAsync($"() => localStorage.getItem('{ViewKey}') === 'location'");
        (await StoredPanelAsync()).Should().Be("open", "the view is remembered apart from the panel");
        (await DockStateAsync()).Should().Be("push");

        await Page.Locator("#filter-panel .filter-clear").ClickAsync();
        await Page.WaitForFunctionAsync($"() => sessionStorage.getItem('{FiltersKey}') === null");
        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(0);
        (await StoredViewAsync()).Should().Be("location", "Clear never changes the view");
        (await StoredPanelAsync()).Should().Be("open", "Clear never changes the panel");

        await Page.Locator(PanelFourPlayers).ClickAsync();
        await Expect(Page.Locator(Dimmed).First).ToBeAttachedAsync();
        await FoldFilterPanelAsync();
        await ExpectStoredPanelAsync("folded");
        await Page.Locator(".filter-rail .filter-rail-clear").ClickAsync();
        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(0);
        (await StoredPanelAsync()).Should().Be("folded", "the rail's Clear never changes the panel");
        (await StoredViewAsync()).Should().Be("location", "the rail's Clear never changes the view");
        ConsoleErrors.Should().BeEmpty();
    }

    private async Task AssertFirstDrawDimmedAsync()
    {
        await Page.WaitForFunctionAsync("() => window.__memory.frames.some((frame) => frame.boxes > 0)");
        var frames = await ReadFramesAsync();
        var firstDrawn = frames.First(frame => frame.Boxes > 0);

        firstDrawn.Dimmed.Should().BeGreaterThan(0, "the first frame with boxes already shows the stored filters' shadow");
    }

    private Task WaitForDockAsync(string state) => Page.WaitForFunctionAsync($"() => document.documentElement.dataset.filterDock === '{state}'");

    private Task<string?> StoredViewAsync() => Page.EvaluateAsync<string?>($"() => localStorage.getItem('{ViewKey}')");

    private async Task AssertFirstFrameAsync(string expectedState)
    {
        await Page.WaitForFunctionAsync("() => window.__memory.frames.some((frame) => frame.boxes > 0)");
        var settledLeft = await Page.Locator(".cabinet-room").EvaluateAsync<double>("(room) => room.getBoundingClientRect().left");
        var frames = await ReadFramesAsync();

        frames.Should().NotBeEmpty();
        frames[0].Dock.Should().Be(expectedState, "the head script set the dock's state before the first paint");
        var measured = frames.Where(frame => frame.RoomLeft is not null).ToList();
        measured.Should().NotBeEmpty("the cabinet's column exists before the first box is drawn");
        measured[0].RoomLeft!.Value.Should().BeApproximately(settledLeft, Tolerance, "the cabinet's column stands where it settles from its first frame");
        measured.Should().OnlyContain(frame => Math.Abs(frame.RoomLeft!.Value - settledLeft) <= Tolerance, "the cabinet never shifts sideways");
    }

    private async Task<List<Frame>> ReadFramesAsync()
    {
        var json = await Page.EvaluateAsync<JsonElement>("() => window.__memory.frames");

        return JsonSerializer.Deserialize<List<Frame>>(json.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private Task<string?> StoredPanelAsync() => Page.EvaluateAsync<string?>($"() => localStorage.getItem('{PanelKey}')");

    private Task ExpectStoredPanelAsync(string value) => Page.WaitForFunctionAsync($"() => localStorage.getItem('{PanelKey}') === '{value}'");

    private async Task ReloadAsync()
    {
        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync(Boxes, new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(Bar)).ToBeVisibleAsync();
    }

    private async Task OpenAsync(int width, int height = 900, string pathAndQuery = "/?sample=65")
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
            await Page.AddInitScriptAsync(InstallFrameRecorder);
            _started = true;
        }

        await GotoCabinetAsync(pathAndQuery);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(Bar)).ToBeVisibleAsync();
    }

    private sealed record Frame(string? Dock, double? RoomLeft, int Boxes, int Dimmed);

    private sealed record HeadScript(string Src, string Type, bool Async, bool Defer);
}

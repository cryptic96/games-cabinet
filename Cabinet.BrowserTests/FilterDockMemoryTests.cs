using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the device remembers whether the visitor left the desktop filter panel open, and that the page reads
/// it before the first paint: a panel left open at a width where it stands beside the cabinet is open in the very first frame after a
/// reload, with the cabinet's column already where it settles, and a folded one gives the rail. The value is read by a small external
/// script in the head, so the page carries no inline script and the strict content security policy is never broken.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterDockMemoryTests : CabinetPageTest
{
    private const string Bar = ".filter-bar";
    private const string Boxes = "#cabinet .placement";
    private const string PanelKey = "cabinet-filter-panel";
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

    private sealed record Frame(string? Dock, double? RoomLeft, int Boxes);

    private sealed record HeadScript(string Src, string Type, bool Async, bool Defer);
}

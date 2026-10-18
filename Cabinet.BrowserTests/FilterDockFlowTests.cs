using System.Globalization;
using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using SkiaSharp;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the flows that cross the desktop filter dock land where they should: Show on shelf lands the found
/// box in the page column beside a pushed panel or the folded rail, inside the viewport and ringed, with nothing above it but the
/// viewport's top; and the view switch fades only the cabinet, under an open panel that is drawn exactly as it is at rest, and
/// scrolls up to the cabinet's top when the visitor had scrolled past it.
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

    private sealed record MeasuredBox(string Id, double Span);

    private sealed record MeasuredGroup(string LocationId, List<MeasuredBox> Boxes);

    private sealed record ViewCard(long EntryId, bool IsExpansion, string? LocationId);
}

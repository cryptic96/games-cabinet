using System.Globalization;
using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the flows that cross the desktop filter dock land where they should: Show on shelf lands the found
/// box in the page column beside a pushed panel or the folded rail, inside the viewport and ringed, with nothing above it but the
/// viewport's top.
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

    private async Task OpenSampleAsync(int width, int height)
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

    private sealed record MeasuredBox(string Id, double Span);

    private sealed record MeasuredGroup(string LocationId, List<MeasuredBox> Boxes);

    private sealed record ViewCard(long EntryId, bool IsExpansion, string? LocationId);
}

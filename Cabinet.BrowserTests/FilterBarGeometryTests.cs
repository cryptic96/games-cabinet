using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser how the slim filter bar of a phone or tablet sits: it stays pinned while the cabinet scrolls, draws its
/// fill and shadow only once it is stuck (found without a scroll handler), and keeps a focused box clear of itself. On a desktop,
/// where the filters stand in the dock at the left edge, a focused box lands clear of the viewport's top edge and never under the
/// panel that pushes the cabinet aside.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterBarGeometryTests : CabinetPageTest
{
    private const string Bar = ".filter-bar";
    private const int FocusMargin = 16;
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private const string RefuseScrollListeners = """
        () => {
          window.__scrollListeners = 0;
          for (const target of [window, document]) {
            const original = target.addEventListener.bind(target);
            target.addEventListener = (type, ...rest) => {
              if (type === 'scroll') {
                window.__scrollListeners += 1;
                return undefined;
              }
              return original(type, ...rest);
            };
          }
        }
        """;

    private const string ReadBar = """
        () => {
          const bar = document.querySelector('.filter-bar');
          const after = getComputedStyle(bar, '::after');
          return {
            stuck: bar.hasAttribute('data-stuck'),
            fill: Number(after.opacity),
            top: bar.getBoundingClientRect().top,
            height: bar.getBoundingClientRect().height,
            property: getComputedStyle(document.documentElement).getPropertyValue('--filter-bar-h').trim(),
            sticky: getComputedStyle(bar).position,
          };
        }
        """;

    [Fact]
    public async Task The_bar_pins_and_shows_its_shadow_only_once_stuck()
    {
        await OpenAsync("/?sample=65", DockWidths.Tablet, 900, refuseScroll: true);

        var resting = await ReadBarAsync();
        resting.Sticky.Should().Be("sticky");
        resting.Stuck.Should().BeFalse();
        resting.Fill.Should().Be(0);

        await Page.EvaluateAsync("() => window.scrollTo(0, 700)");
        await Page.WaitForFunctionAsync("() => document.querySelector('.filter-bar').hasAttribute('data-stuck')");
        await Page.WaitForFunctionAsync("() => Number(getComputedStyle(document.querySelector('.filter-bar'), '::after').opacity) === 1");

        var stuck = await ReadBarAsync();
        stuck.Top.Should().BeApproximately(0, 0.5);
        stuck.Fill.Should().Be(1);

        await Page.EvaluateAsync("() => window.scrollTo(0, 0)");
        await Page.WaitForFunctionAsync("() => !document.querySelector('.filter-bar').hasAttribute('data-stuck')");
        await Page.WaitForFunctionAsync("() => Number(getComputedStyle(document.querySelector('.filter-bar'), '::after').opacity) === 0");

        (await Page.EvaluateAsync<int>("() => window.__scrollListeners")).Should().BeLessThanOrEqualTo(
            1,
            "the only scroll listener the slim bar asks for is its slide, and it was refused, so the bar pinned and stuck without one");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_focused_box_is_never_hidden_under_the_bar()
    {
        await OpenAsync("/?sample=65", DockWidths.Tablet, 900);

        await Page.EvaluateAsync("() => window.scrollTo(0, 700)");
        await Page.WaitForFunctionAsync("() => document.querySelector('.filter-bar').hasAttribute('data-stuck')");

        var bar = await ReadBarAsync();
        bar.Property.Should().Be($"{(int)Math.Round(bar.Height)}px");

        var box = await PutABoxAtTheTopAsync();
        await Page.EvaluateAsync($"() => document.querySelector('#cabinet .placement[data-entry-id=\"{box}\"]').focus()");

        await Page.WaitForFunctionAsync(
            $"() => document.querySelector('#cabinet .placement[data-entry-id=\"{box}\"]').getBoundingClientRect().top >= document.querySelector('.filter-bar').getBoundingClientRect().bottom - 0.5");

        await Page.SetViewportSizeAsync(DockWidths.Push, 900);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        await ExpectAFocusedBoxClearOfTheTopAsync("folded");

        await OpenFilterPanelAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "push");
        await ExpectAFocusedBoxClearOfTheTopAsync("beside the pushed panel");
        ConsoleErrors.Should().BeEmpty();
    }

    /// <summary>
    /// Scrolls a box that stands well down the cabinet to 4px under the viewport's top edge, inside the margin a focused box keeps,
    /// focuses it, and checks it lands at least 16px under the top edge and, while the panel pushes, to the right of the panel.
    /// </summary>
    private async Task ExpectAFocusedBoxClearOfTheTopAsync(string label)
    {
        await Page.EvaluateAsync("() => document.activeElement?.blur()");
        var box = await PutABoxAtTheTopAsync();
        var target = $"document.querySelector('#cabinet .placement[data-entry-id=\"{box}\"]')";

        await Page.EvaluateAsync($"() => {target}.focus({{ focusVisible: true }})");
        await Page.WaitForFunctionAsync($"() => {target}.getBoundingClientRect().top >= {FocusMargin} - 1");

        var landed = await Page.EvaluateAsync<JsonElement>(
            $"() => {{ const panel = document.getElementById('filter-panel'); const rect = {target}.getBoundingClientRect(); return {{ top: rect.top, left: rect.left, panelRight: panel.getClientRects().length > 0 ? panel.getBoundingClientRect().right : 0, focused: document.activeElement === {target} }}; }}");

        landed.GetProperty("focused").GetBoolean().Should().BeTrue(label);
        landed.GetProperty("top").GetDouble().Should().BeGreaterThanOrEqualTo(FocusMargin - 1, $"{label}: the box lands 16px under the top edge");
        landed.GetProperty("left").GetDouble().Should().BeGreaterThanOrEqualTo(landed.GetProperty("panelRight").GetDouble(), $"{label}: the box is never under the panel");
    }

    private Task<string> PutABoxAtTheTopAsync() => Page.EvaluateAsync<string>(
        """
        () => {
          const boxes = [...document.querySelectorAll('#cabinet .placement')].filter((box) => box.getBoundingClientRect().top > 400);
          const target = boxes[0];
          window.scrollBy(0, target.getBoundingClientRect().top - 4);
          return target.dataset.entryId;
        }
        """);

    private async Task OpenAsync(string path, int width, int height, bool refuseScroll = false)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);

        if (refuseScroll)
        {
            await Page.AddInitScriptAsync($"({RefuseScrollListeners})()");
        }

        await GotoCabinetAsync(path);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(Bar)).ToBeVisibleAsync();
    }

    private async Task<BarReading> ReadBarAsync()
    {
        var json = await Page.EvaluateAsync<JsonElement>(ReadBar);

        return JsonSerializer.Deserialize<BarReading>(json.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private sealed record BarReading(bool Stuck, double Fill, double Top, double Height, string Property, string Sticky);
}

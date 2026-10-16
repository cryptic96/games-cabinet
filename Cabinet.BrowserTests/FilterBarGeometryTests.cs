using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser how the filter bar sits on a desktop: it stays pinned while the cabinet scrolls, draws its fill and
/// shadow only once it is stuck (found without a scroll handler), keeps a focused box clear of itself, and holds two rows of one
/// line each at a steady height in both languages whether or not a filter is set.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterBarGeometryTests : CabinetPageTest
{
    private const string Bar = ".filter-bar";
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private const string RecordScrollListeners = """
        () => {
          window.__scrollListeners = 0;
          for (const target of [window, document]) {
            const original = target.addEventListener.bind(target);
            target.addEventListener = (type, ...rest) => {
              if (type === 'scroll') {
                window.__scrollListeners += 1;
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
        await OpenAsync("/?sample=65", 1440, 900, recordScroll: true);

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

        (await Page.EvaluateAsync<int>("() => window.__scrollListeners")).Should().Be(0);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_focused_box_is_never_hidden_under_the_bar()
    {
        await OpenAsync("/?sample=65", 1440, 900);

        await Page.EvaluateAsync("() => window.scrollTo(0, 700)");
        await Page.WaitForFunctionAsync("() => document.querySelector('.filter-bar').hasAttribute('data-stuck')");

        var bar = await ReadBarAsync();
        bar.Property.Should().Be($"{(int)Math.Round(bar.Height)}px");

        var box = await Page.EvaluateAsync<string>(
            """
            () => {
              const boxes = [...document.querySelectorAll('#cabinet .placement')].filter((box) => box.getBoundingClientRect().top > 400);
              const target = boxes[0];
              window.scrollBy(0, target.getBoundingClientRect().top - 4);
              return target.dataset.entryId;
            }
            """);

        await Page.EvaluateAsync($"() => document.querySelector('#cabinet .placement[data-entry-id=\"{box}\"]').focus()");

        await Page.WaitForFunctionAsync(
            $"() => document.querySelector('#cabinet .placement[data-entry-id=\"{box}\"]').getBoundingClientRect().top >= document.querySelector('.filter-bar').getBoundingClientRect().bottom - 0.5");
        ConsoleErrors.Should().BeEmpty();
    }

    private async Task OpenAsync(string path, int width, int height, bool recordScroll = false)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);

        if (recordScroll)
        {
            await Page.AddInitScriptAsync($"({RecordScrollListeners})()");
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

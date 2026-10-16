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
    private const int SteadyHeight = 112;
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

    private const string ReadRows = """
        () => [...document.querySelectorAll('.filter-bar .filter-row')].map((row) => {
          const controls = [...row.querySelectorAll('.filter-search, .filter-chip, .filter-clear')].map((control) => control.getBoundingClientRect());
          const rect = row.getBoundingClientRect();
          return {
            top: rect.top,
            bottom: rect.bottom,
            controls: controls.length,
            spread: Math.max(...controls.map((control) => control.top)) - Math.min(...controls.map((control) => control.top)),
            lowest: Math.max(...controls.map((control) => control.bottom)),
            highest: Math.min(...controls.map((control) => control.top)),
          };
        })
        """;

    private const string ReadStatus = """
        () => {
          const status = document.querySelector('.filter-bar .filter-status');
          return { hidden: status.hasAttribute('hidden'), visibility: getComputedStyle(status).visibility };
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

    [Fact]
    public async Task Two_rows_and_a_steady_height_at_every_desktop_width()
    {
        await OpenAsync("/?sample=65", 1440, 900);

        foreach (var language in new[] { "en", "nl" })
        {
            if (language == "nl")
            {
                await Page.Locator(".lang-toggle a[lang=nl]").ClickAsync();
                await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "nl");
                await Expect(Page.Locator(Bar)).ToBeVisibleAsync();
            }

            foreach (var width in new[] { 1024, 1440, 1920 })
            {
                await Page.SetViewportSizeAsync(width, 900);
                await Page.EvaluateAsync("() => document.fonts.ready");
                await ExpectSteadyAsync(language, width, "at rest");

                await Page.Locator("#filter-q").FillAsync("a");
                await Expect(Page.Locator(Bar)).ToHaveAttributeAsync("data-active", string.Empty);
                await ExpectSteadyAsync(language, width, "while filtering");
                (await Page.EvaluateAsync<JsonElement>(ReadStatus)).GetProperty("visibility").GetString().Should().Be("visible", $"{language} {width}");

                await Page.Locator("#filter-q").FillAsync("zzqqxx");
                await Expect(Page.Locator(".filter-clear[data-highlighted]")).ToHaveCountAsync(1);
                await ExpectSteadyAsync(language, width, "with nothing matching");

                await Page.Locator(".filter-clear").ClickAsync();
                await Expect(Page.Locator(Bar)).Not.ToHaveAttributeAsync("data-active", string.Empty);
                (await Page.EvaluateAsync<JsonElement>(ReadStatus)).GetProperty("visibility").GetString().Should().Be("hidden", $"{language} {width}");
            }
        }

        ConsoleErrors.Should().BeEmpty();
    }

    private async Task ExpectSteadyAsync(string language, int width, string moment)
    {
        var label = $"{language} at {width} px {moment}";

        await Page.WaitForFunctionAsync($"() => document.querySelector('.filter-bar').offsetHeight === {SteadyHeight}");

        var bar = await ReadBarAsync();
        bar.Height.Should().BeApproximately(SteadyHeight, 0.5, label);

        var status = await Page.EvaluateAsync<JsonElement>(ReadStatus);
        status.GetProperty("hidden").GetBoolean().Should().BeFalse(label);

        var rows = JsonSerializer.Deserialize<List<Row>>((await Page.EvaluateAsync<JsonElement>(ReadRows)).GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        rows.Should().HaveCount(2, label);

        foreach (var row in rows)
        {
            row.Controls.Should().BeGreaterThan(0, label);
            row.Spread.Should().BeLessThanOrEqualTo(1, $"{label}: a row stays on one line");
        }

        rows[1].Highest.Should().BeGreaterThanOrEqualTo(rows[0].Lowest - 0.5, $"{label}: the second row sits below the first");
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

    private sealed record Row(double Top, double Bottom, int Controls, double Spread, double Lowest, double Highest);
}

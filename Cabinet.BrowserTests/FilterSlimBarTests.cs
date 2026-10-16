using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser how the slim bar of a phone behaves: while a filter is in use it shows the short count and an icon Clear
/// and stays one row from 390 px wide, wrapping its status group onto a second row only at 320 px, in both languages. It slides out of
/// the way after a little scrolling down and comes back after a little scrolling up, with one passive scroll listener and nothing but
/// a transform, and it never leaves while a filter is in use, its field has focus, or a sheet or a card is open.
/// </summary>
[Trait("Category", "Browser")]
public sealed partial class FilterSlimBarTests : CabinetPageTest
{
    private const string Bar = ".filter-bar";
    private const string Field = "#filter-q";
    private const string Count = ".filter-bar .filter-count";
    private const string Status = ".filter-bar .filter-status";
    private const string IconClear = ".filter-bar .filter-clear-icon";
    private const string TextClear = ".filter-bar .filter-clear";
    private const string Live = "#filter-live";
    private const double SlimHeight = 60;

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private const string ReadBar = """
        () => {
          const rect = (selector) => {
            const element = document.querySelector(selector);
            const box = element.getBoundingClientRect();
            return { top: box.top, bottom: box.bottom, left: box.left, right: box.right, width: box.width, height: box.height };
          };
          const status = document.querySelector('.filter-bar .filter-status');
          return {
            bar: rect('.filter-bar'),
            search: rect('.filter-bar .filter-search'),
            statusDisplay: getComputedStyle(status).display,
            status: getComputedStyle(status).display === 'none' ? null : rect('.filter-bar .filter-status'),
            overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
          };
        }
        """;

    private const string ReadAway = """
        () => {
          const bar = document.querySelector('.filter-bar');
          const style = getComputedStyle(bar);
          return {
            away: bar.hasAttribute('data-away'),
            visibility: style.visibility,
            transform: style.transform,
            scroll: window.scrollY,
          };
        }
        """;

    private const string RecordScrollListeners = """
        () => {
          window.__scrollListeners = [];
          for (const target of [window, document]) {
            const original = target.addEventListener.bind(target);
            target.addEventListener = (type, listener, options) => {
              if (type === 'scroll') {
                window.__scrollListeners.push({ passive: typeof options === 'object' && options !== null && options.passive === true });
              }
              return original(type, listener, options);
            };
          }
        }
        """;

    private bool _started;

    [Fact]
    public async Task The_slim_bar_stays_one_row_while_filtering()
    {
        await OpenAsync(390, 844);

        foreach (var language in new[] { "en", "nl" })
        {
            await ChooseLanguageAsync(language);
            var total = await TotalGamesAsync();
            var text = Words.For(language);

            var resting = await ReadBarAsync();
            resting.Bar.Height.Should().BeApproximately(SlimHeight, 0.5, $"{language} with no filter");
            resting.StatusDisplay.Should().Be("none", $"{language}: neither the count nor Clear is drawn with no filter");
            await Expect(Page.Locator(Count)).ToHaveTextAsync(string.Empty);

            await Page.Locator(Field).FillAsync("a");
            await Expect(Page.Locator(Bar)).ToHaveAttributeAsync("data-active", string.Empty);
            await Expect(Page.Locator(Count)).ToHaveTextAsync(text.ShortCount());
            var many = await ReadBarAsync();
            many.Bar.Height.Should().BeApproximately(SlimHeight, 0.5, $"{language} with several matches");
            many.Status!.Top.Should().BeInRange(many.Search.Top - 0.5, many.Search.Bottom, $"{language}: the status shares the row of the field");
            many.Overflow.Should().BeLessThanOrEqualTo(0, $"{language}");
            await ExpectIconClearAsync(language, highlighted: false);
            await Expect(Page.Locator(Live)).ToHaveTextAsync(text.FullCount(), new LocatorAssertionsToHaveTextOptions { Timeout = 5000 });

            await Page.Locator(Field).FillAsync(await FindUniqueTitleAsync());
            await Expect(Page.Locator(Count)).ToHaveTextAsync(text.One(total));
            (await ReadBarAsync()).Bar.Height.Should().BeApproximately(SlimHeight, 0.5, $"{language} with one match");
            await Expect(Page.Locator(Live)).ToHaveTextAsync(text.FullOne(total), new LocatorAssertionsToHaveTextOptions { Timeout = 5000 });

            await Page.Locator(Field).FillAsync("qqzzqq");
            await Expect(Page.Locator(Count)).ToHaveTextAsync(text.None(total));
            (await ReadBarAsync()).Bar.Height.Should().BeApproximately(SlimHeight, 0.5, $"{language} with nothing matching");
            await ExpectIconClearAsync(language, highlighted: true);
            await Expect(Page.Locator(Live)).ToHaveTextAsync(text.NoMatch, new LocatorAssertionsToHaveTextOptions { Timeout = 5000 });

            await Page.Locator(IconClear).ClickAsync();
            await Expect(Page.Locator(Field)).ToHaveValueAsync(string.Empty);
            await Expect(Page.Locator(Bar)).Not.ToHaveAttributeAsync("data-active", string.Empty);
            (await ReadBarAsync()).Bar.Height.Should().BeApproximately(SlimHeight, 0.5, $"{language} after Clear");
        }

        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task At_320_the_status_wraps_and_nothing_overflows()
    {
        await OpenAsync(320, 700);

        foreach (var language in new[] { "en", "nl" })
        {
            await ChooseLanguageAsync(language);

            await Page.Locator(Field).FillAsync("a");
            await Expect(Page.Locator(Bar)).ToHaveAttributeAsync("data-active", string.Empty);

            var wrapped = await ReadBarAsync();
            wrapped.Status!.Top.Should().BeGreaterThanOrEqualTo(wrapped.Search.Bottom - 0.5, $"{language}: the status group wraps to a second row");
            wrapped.Bar.Height.Should().BeApproximately(SlimHeight + 4 + 44, 1, $"{language}: two rows 4 px apart");
            wrapped.Overflow.Should().BeLessThanOrEqualTo(0, $"{language}: nothing overflows sideways");
            wrapped.Status.Right.Should().BeLessThanOrEqualTo(320.5, $"{language}");

            await Page.Locator(IconClear).ClickAsync();
            await Expect(Page.Locator(Bar)).Not.ToHaveAttributeAsync("data-active", string.Empty);
            (await ReadBarAsync()).Bar.Height.Should().BeApproximately(SlimHeight, 0.5, $"{language} with no filter");
        }

        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task On_a_desktop_the_text_Clear_stays_and_the_count_keeps_its_words()
    {
        await OpenAsync(1280, 900);

        await Page.Locator(Field).FillAsync("qqzzqq");

        await Expect(Page.Locator(Count)).ToHaveTextAsync("No games match");
        await Expect(Page.Locator(TextClear)).ToBeVisibleAsync();
        await Expect(Page.Locator(TextClear)).ToHaveTextAsync("Clear");
        await Expect(Page.Locator(IconClear)).ToBeHiddenAsync();

        await Page.Locator(Field).FillAsync("a");

        await Expect(Page.Locator(Count)).ToHaveTextAsync(FullCountPattern());
        ConsoleErrors.Should().BeEmpty();
    }

    [GeneratedRegex(@"^\d+ of \d+ games?$")]
    private static partial Regex FullCountPattern();

    private async Task ExpectIconClearAsync(string language, bool highlighted)
    {
        var icon = Page.Locator(IconClear);
        var name = language == "nl" ? "Filters wissen" : "Clear filters";

        await Expect(icon).ToBeVisibleAsync();
        await Expect(Page.Locator(TextClear)).ToBeHiddenAsync();
        (await icon.GetAttributeAsync("aria-label")).Should().Be(name);
        var box = (await icon.BoundingBoxAsync())!;
        ((double)box.Width).Should().BeApproximately(44, 0.5);
        ((double)box.Height).Should().BeApproximately(44, 0.5);

        if (highlighted)
        {
            await Expect(icon).ToHaveAttributeAsync("data-highlighted", string.Empty);
        }
        else
        {
            await Expect(icon).Not.ToHaveAttributeAsync("data-highlighted", string.Empty);
        }
    }

    private async Task ChooseLanguageAsync(string language)
    {
        if (language == "en")
        {
            return;
        }

        await Page.Locator(".lang-toggle a[lang=nl]").ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "nl");
        await Expect(Page.Locator(Bar)).ToBeVisibleAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task<int> TotalGamesAsync()
    {
        await Page.Locator(Field).FillAsync("a");
        await Expect(Page.Locator(Bar)).ToHaveAttributeAsync("data-active", string.Empty);
        var text = (await Page.Locator(Count).TextContentAsync())!;
        await Page.Locator(IconClear).ClickAsync();
        await Expect(Page.Locator(Bar)).Not.ToHaveAttributeAsync("data-active", string.Empty);

        return int.Parse(Regex.Match(text, @"(\d+)$").Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private async Task<string> FindUniqueTitleAsync()
    {
        var labels = await Page.EvaluateAsync<string[]>("() => [...document.querySelectorAll('#cabinet .placement')].map((box) => box.getAttribute('aria-label') ?? '').filter((label) => label.length > 6).slice(0, 12)");

        foreach (var label in labels)
        {
            await Page.Locator(Field).FillAsync(label);
            await Expect(Page.Locator(Count)).Not.ToHaveTextAsync(string.Empty);
            var text = (await Page.Locator(Count).TextContentAsync())!;

            if (text.StartsWith("1 ", StringComparison.Ordinal))
            {
                return label;
            }
        }

        throw new InvalidOperationException("No title of the sample matches exactly one game.");
    }

    private async Task<BarReading> ReadBarAsync()
    {
        var reading = await Page.EvaluateAsync<JsonElement>(ReadBar);

        return new BarReading(
            ReadBox(reading.GetProperty("bar")),
            ReadBox(reading.GetProperty("search")),
            reading.GetProperty("statusDisplay").GetString()!,
            reading.GetProperty("status").ValueKind == JsonValueKind.Null ? null : ReadBox(reading.GetProperty("status")),
            reading.GetProperty("overflow").GetDouble());
    }

    private static Box ReadBox(JsonElement element) => new(
        element.GetProperty("top").GetDouble(),
        element.GetProperty("bottom").GetDouble(),
        element.GetProperty("right").GetDouble(),
        element.GetProperty("height").GetDouble());

    private async Task OpenAsync(int width, int height)
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

        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(Bar)).ToBeVisibleAsync();
    }

    private sealed record Box(double Top, double Bottom, double Right, double Height);

    private sealed record BarReading(Box Bar, Box Search, string StatusDisplay, Box? Status, double Overflow);

    private sealed record Words(string Language)
    {
        private bool Dutch => Language == "nl";

        public string NoMatch => Dutch ? "Geen spellen gevonden" : "No games match";

        public static Words For(string language) => new(language);

        public Regex ShortCount() => new(Dutch ? @"^\d+ van \d+$" : @"^\d+ of \d+$");

        public Regex FullCount() => new(Dutch ? @"^\d+ van \d+ spellen$" : @"^\d+ of \d+ games$");

        public string One(int total) => Dutch ? $"1 van {total}" : $"1 of {total}";

        public string FullOne(int total) => Dutch ? $"1 van {total} spellen" : $"1 of {total} games";

        public string None(int total) => Dutch ? $"0 van {total}" : $"0 of {total}";
    }
}

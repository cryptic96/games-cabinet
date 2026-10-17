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

    private const string ReadHint = """
        () => {
          const field = document.querySelector('#filter-q');
          const style = getComputedStyle(field);
          const probe = document.createElement('span');
          probe.style.cssText = 'position:absolute;visibility:hidden;white-space:nowrap;font:' + style.font;
          probe.textContent = field.placeholder;
          document.body.append(probe);
          const hint = probe.getBoundingClientRect().width;
          probe.remove();
          const opacity = getComputedStyle(field, '::placeholder').opacity;
          return {
            hint,
            room: field.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight),
            hintDrawn: opacity !== '0',
            glyph: getComputedStyle(document.querySelector('.filter-glass')).display,
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
    public async Task The_search_hint_is_never_cut_in_the_middle_of_a_word_while_a_filter_is_in_use()
    {
        await OpenAsync(390, 844);

        foreach (var language in new[] { "en", "nl" })
        {
            await ChooseLanguageAsync(language);
            await Page.Locator(".filter-bar .filter-open").ClickAsync();
            await Page.Locator("dialog.filter-dialog[open] fieldset[data-filter=\"players\"] input[value=\"4\"]").ClickAsync();
            await Page.Locator("dialog.filter-dialog[open] .filter-done").ClickAsync();
            await Expect(Page.Locator(Bar)).ToHaveAttributeAsync("data-active", string.Empty);

            foreach (var width in new[] { 320, 360, 390, 414, 430 })
            {
                await ExpectHintWholeOrReplacedAsync($"{language} at {width} px", width);
            }

            await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ForcedColors = ForcedColors.Active });
            await ExpectHintWholeOrReplacedAsync($"{language} at 390 px in forced colours", 390);
            await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ForcedColors = ForcedColors.None });

            await Page.SetViewportSizeAsync(390, 844);
            await Page.Locator(IconClear).ClickAsync();
            await Expect(Page.Locator(Bar)).Not.ToHaveAttributeAsync("data-active", string.Empty);
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

    [Fact]
    public async Task Scrolling_down_slides_the_bar_away_and_up_brings_it_back()
    {
        await OpenAsync(390, 844, recordScroll: true);

        (await ReadAwayAsync()).Away.Should().BeFalse();
        var listeners = await Page.EvaluateAsync<JsonElement>("() => window.__scrollListeners");
        listeners.GetArrayLength().Should().Be(1, "one scroll listener serves the slide");
        listeners[0].GetProperty("passive").GetBoolean().Should().BeTrue();

        await ScrollToAsync(200);
        await Expect(Page.Locator(Bar)).ToHaveAttributeAsync("data-away", string.Empty);
        await Expect(Page.Locator(Bar)).ToHaveCSSAsync("visibility", "hidden");
        await Expect(Page.Locator(Field)).ToBeHiddenAsync();
        await Expect(Page.Locator(".filter-bar .filter-open")).ToBeHiddenAsync();
        (await ReadAwayAsync()).Transform.Should().Be("matrix(1, 0, 0, 1, 0, -60)");
        await Expect(Page.Locator(Bar)).ToHaveCSSAsync("transition-property", "transform, visibility");

        await ScrollToAsync(180);
        await Expect(Page.Locator(Bar)).Not.ToHaveAttributeAsync("data-away", string.Empty);
        await Expect(Page.Locator(Bar)).ToHaveCSSAsync("visibility", "visible");
        await Expect(Page.Locator(Field)).ToBeVisibleAsync();
        await Expect(Page.Locator(Bar)).ToHaveCSSAsync("transform", "none");
        await Expect(Page.Locator(Bar)).ToHaveCSSAsync("transition-property", "transform, visibility");

        await ScrollToAsync(186);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        (await ReadAwayAsync()).Away.Should().BeFalse("a few pixels down are not enough");

        await ScrollToAsync(200);
        await Expect(Page.Locator(Bar)).ToHaveAttributeAsync("data-away", string.Empty);

        await ScrollToAsync(0);
        await Expect(Page.Locator(Bar)).Not.ToHaveAttributeAsync("data-away", string.Empty);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_bar_stays_while_it_matters()
    {
        await OpenAsync(390, 844);

        await ScrollToAsync(40);
        await Task.Delay(150, TestContext.Current.CancellationToken);
        (await ReadAwayAsync()).Away.Should().BeFalse("within one bar height of the top");

        await Page.Locator(Field).FillAsync("a");
        await Expect(Page.Locator(Bar)).ToHaveAttributeAsync("data-active", string.Empty);
        await Page.Locator(Field).BlurAsync();
        await ScrollToAsync(300);
        await Task.Delay(150, TestContext.Current.CancellationToken);
        (await ReadAwayAsync()).Away.Should().BeFalse("a filter is in use");
        await Page.Locator(IconClear).DispatchEventAsync("click");
        await Expect(Page.Locator(Bar)).Not.ToHaveAttributeAsync("data-active", string.Empty);
        await Page.Locator(Field).BlurAsync();

        await ScrollToAsync(0);
        await Page.Locator(Field).FocusAsync();
        await ScrollToAsync(300);
        await Task.Delay(150, TestContext.Current.CancellationToken);
        (await ReadAwayAsync()).Away.Should().BeFalse("the field has focus");
        await Page.Locator(Field).BlurAsync();

        await ScrollToAsync(0);
        await Page.Locator(".filter-bar .filter-open").DispatchEventAsync("click");
        await Expect(Page.Locator("dialog.filter-dialog[open]")).ToBeVisibleAsync();
        await ScrollToAsync(300);
        await Task.Delay(150, TestContext.Current.CancellationToken);
        (await ReadAwayAsync()).Away.Should().BeFalse("the sheet is open");
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator("dialog.filter-dialog[open]")).ToHaveCountAsync(0);
        await Page.EvaluateAsync("() => document.activeElement?.blur()");

        await ScrollToAsync(0);
        await Page.Locator("#cabinet .placement").First.DispatchEventAsync("click");
        await Expect(Page.Locator("dialog.card-dialog[open]")).ToBeVisibleAsync();
        await ScrollToAsync(300);
        await Task.Delay(150, TestContext.Current.CancellationToken);
        (await ReadAwayAsync()).Away.Should().BeFalse("a card is open");
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator("dialog.card-dialog[open]")).ToHaveCountAsync(0);
        await WaitUntilTheCardStepIsGoneAsync();
        await Page.EvaluateAsync("() => document.activeElement?.blur()");

        await ScrollToAsync(0);
        await ScrollToAsync(300);
        await Expect(Page.Locator(Bar)).ToHaveAttributeAsync("data-away", string.Empty);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Slash_brings_an_away_bar_back_and_focuses_the_field()
    {
        await OpenAsync(390, 844);
        await ScrollToAsync(300);
        await Expect(Page.Locator(Bar)).ToHaveAttributeAsync("data-away", string.Empty);

        await Page.Keyboard.PressAsync("/");

        await Expect(Page.Locator(Bar)).Not.ToHaveAttributeAsync("data-away", string.Empty);
        await Expect(Page.Locator(Field)).ToBeFocusedAsync();
        await ScrollToAsync(500);
        await Task.Delay(150, TestContext.Current.CancellationToken);
        (await ReadAwayAsync()).Away.Should().BeFalse("the field keeps focus");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_full_bar_never_slides_and_the_listener_follows_the_width()
    {
        await OpenAsync(1280, 900, recordScroll: true);

        (await Page.EvaluateAsync<int>("() => window.__scrollListeners.length")).Should().Be(0);
        var reach = await Page.EvaluateAsync<double>("() => document.documentElement.scrollHeight - window.innerHeight");
        reach.Should().BeGreaterThan(300, "the page can be scrolled well past the bar");
        await ScrollToAsync(Math.Min(600, reach));
        await Task.Delay(150, TestContext.Current.CancellationToken);
        (await ReadAwayAsync()).Away.Should().BeFalse();
        (await Page.Locator(Bar).EvaluateAsync<string>("(bar) => getComputedStyle(bar).position")).Should().Be("sticky");

        await Page.SetViewportSizeAsync(390, 844);
        await Page.WaitForFunctionAsync("() => window.__scrollListeners.length === 1");
        await Page.WaitForFunctionAsync("() => document.documentElement.scrollHeight - window.innerHeight > 400");
        await ScrollToAsync(0);
        await ScrollToAsync(300);
        await Expect(Page.Locator(Bar)).ToHaveAttributeAsync("data-away", string.Empty);

        await Page.SetViewportSizeAsync(1280, 900);
        await Expect(Page.Locator(Bar)).Not.ToHaveAttributeAsync("data-away", string.Empty);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Under_reduced_motion_the_bar_goes_and_returns_at_once()
    {
        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });
        await OpenAsync(390, 844);

        await Expect(Page.Locator(Bar)).ToHaveCSSAsync("transition-duration", "0s");
        await ScrollToAsync(300);
        await Expect(Page.Locator(Bar)).ToHaveAttributeAsync("data-away", string.Empty);
        await Expect(Page.Locator(Bar)).ToHaveCSSAsync("transition-duration", "0s");
        await Expect(Page.Locator(Bar)).ToHaveCSSAsync("visibility", "hidden");
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

    private async Task ExpectHintWholeOrReplacedAsync(string context, int width)
    {
        await Page.SetViewportSizeAsync(width, 844);
        await Page.WaitForTimeoutAsync(200);

        var reading = await Page.EvaluateAsync<JsonElement>(ReadHint);
        var fits = reading.GetProperty("hint").GetDouble() <= reading.GetProperty("room").GetDouble();
        var glyph = reading.GetProperty("glyph").GetString() != "none";
        var hintDrawn = reading.GetProperty("hintDrawn").GetBoolean();

        (fits ? hintDrawn && !glyph : !hintDrawn && glyph).Should().BeTrue(
            $"{context}: the hint is drawn whole, or replaced by the magnifier, never clipped ({reading})");
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

    private async Task ScrollToAsync(double top)
    {
        await Page.EvaluateAsync("(y) => window.scrollTo({ top: y, behavior: 'instant' })", top);
        await Page.WaitForFunctionAsync("(y) => Math.abs(window.scrollY - y) < 1.5", top);
    }

    private async Task<AwayReading> ReadAwayAsync()
    {
        var reading = await Page.EvaluateAsync<JsonElement>(ReadAway);

        return new AwayReading(reading.GetProperty("away").GetBoolean(), reading.GetProperty("transform").GetString()!);
    }

    private async Task OpenAsync(int width, int height, bool recordScroll = false)
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

        if (recordScroll)
        {
            await Page.AddInitScriptAsync($"({RecordScrollListeners})()");
        }

        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(Bar)).ToBeVisibleAsync();
    }

    private sealed record AwayReading(bool Away, string Transform);

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

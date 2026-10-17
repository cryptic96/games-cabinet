using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the filters work for every visitor's settings: a chosen chip and a box in shadow stay
/// distinguishable in forced colours, nothing in the bar, the sheet, the shadow or the view switch moves under reduced motion, the
/// strict content security policy is never broken, no width is made wider than the screen in either language, and long names and
/// Dutch labels fit the controls that show them.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterAccessTests : CabinetPageTest
{
    private const string Bar = ".filter-bar";
    private const string OpenSheet = "dialog.filter-dialog[open]";
    private const string Dimmed = "#cabinet .placement[data-dim]";
    private const string Lit = "#cabinet .placement:not([data-dim]):not([data-kind=\"moreMarker\"])";
    private const string Headings = "#cabinet .location-heading";
    private const string GroupTitles = ".games-list-groups > h3";
    private const int FullBarWidth = 1024;
    private const int PhoneHeadingWrapWidth = 640;
    private const double SheetControlHeight = 44;
    private const double Tolerance = 0.5;

    private const string PolicyRecorder = """
        (() => {
          window.__violations = [];
          document.addEventListener('securitypolicyviolation', (event) => window.__violations.push(event.violatedDirective));
        })();
        """;

    private const string ViewTransitionSpy = """
        (() => {
          window.__transitions = 0;
          const original = document.startViewTransition?.bind(document);
          if (original !== undefined) {
            document.startViewTransition = (callback) => {
              window.__transitions += 1;
              return original(callback);
            };
          }
        })();
        """;

    private const string ReadOutline = """
        (selector) => {
          const style = getComputedStyle(document.querySelector(selector));
          return { style: style.outlineStyle, width: Number.parseFloat(style.outlineWidth) };
        }
        """;

    private const string ReadTransitions = """
        (elements) => elements.map(({ selector, pseudo }) => {
          const style = getComputedStyle(document.querySelector(selector), pseudo);
          const properties = style.transitionProperty.split(',').map((value) => value.trim());
          const durations = style.transitionDuration.split(',').map((value) => Number.parseFloat(value));
          const moving = properties.some((property) => property !== 'none') && durations.some((duration) => duration > 0);
          return { selector: selector + (pseudo ?? ''), moving };
        })
        """;

    private const string ReadSelect = """
        ({ name, selector }) => {
          const select = document.querySelector(selector);
          const style = getComputedStyle(select);
          const probe = document.createElement('span');
          probe.textContent = name;
          probe.style.setProperty('position', 'absolute');
          probe.style.setProperty('white-space', 'nowrap');
          probe.style.setProperty('font', style.font);
          document.body.append(probe);
          const natural = probe.getBoundingClientRect().width;
          probe.remove();
          return {
            width: select.getBoundingClientRect().width,
            natural,
            container: select.parentElement.getBoundingClientRect().width,
            holdsFullName: [...select.options].some((option) => option.textContent === name),
          };
        }
        """;

    private const string ReadControls = """
        () => [...document.querySelectorAll('.filter-chip, .filter-view-segment')]
          .filter((control) => control.getBoundingClientRect().width > 0)
          .map((control) => {
            const box = control.getBoundingClientRect();
            const text = control.querySelector('.filter-chip-text, .filter-view-text');
            const words = text.getBoundingClientRect();
            return {
              name: control.className + ' ' + text.textContent,
              height: box.height,
              outside: words.left < box.left - 0.5 || words.right > box.right + 0.5 || words.top < box.top - 0.5 || words.bottom > box.bottom + 0.5,
              clipped: text.scrollWidth > text.clientWidth + 1,
            };
          })
        """;

    private const double RingContrast = 3;

    private const string ReadRing = """
        () => {
          const segment = document.querySelector('.filter-view-segment:has(input:focus-visible)');
          if (segment === null) {
            return null;
          }
          const resolve = (value) => {
            const probe = document.createElement('span');
            probe.style.setProperty('color', value);
            document.body.append(probe);
            const colour = getComputedStyle(probe).color;
            probe.remove();
            return colour;
          };
          const style = getComputedStyle(segment);
          const box = segment.getBoundingClientRect();
          return {
            style: style.outlineStyle,
            width: Number.parseFloat(style.outlineWidth),
            offset: Number.parseFloat(style.outlineOffset),
            ring: style.outlineColor,
            wall: resolve('var(--room)'),
            clipped: segment.closest('.filter-view') !== null && getComputedStyle(segment.closest('.filter-view')).overflow !== 'visible',
            bar: document.querySelector('.filter-bar').getBoundingClientRect().height,
            top: box.top,
          };
        }
        """;

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private readonly TemporaryDirectory _storage = new();

    [Theory]
    [InlineData("brown", 1440)]
    [InlineData("birch", 1440)]
    [InlineData("brown", 390)]
    [InlineData("birch", 390)]
    public async Task The_view_switch_ring_stands_outside_the_segment_and_reads_on_the_wall(string wood, int width)
    {
        var look = new CabinetLook("classic", wood, "wood", "off", "shaded");
        OwnerDataSeed.Write(_storage.FullPath, OwnerData.Empty with { Revision = 1, Look = look });
        await StartAsync(new Dictionary<string, string?>(PrototypeOn) { ["Storage:Directory"] = _storage.FullPath });
        await StubNamesAsync();
        await Page.SetViewportSizeAsync(width, 900);
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await ExpectViewSwitchAvailableAsync();

        if (width < FullBarWidth)
        {
            await Page.Locator($"{Bar} .filter-open").ClickAsync();
            await Expect(Page.Locator(OpenSheet)).ToBeVisibleAsync();
        }

        await Page.Keyboard.PressAsync("Shift");
        await Page.Locator("input[name=\"filter-view\"]:checked").FocusAsync();
        await Expect(Page.Locator(".filter-view-segment:has(input:focus-visible)")).ToHaveCountAsync(1);

        var ring = await ReadAsync<RingReading>(ReadRing, null);
        ring.Style.Should().Be("solid");
        ring.Width.Should().Be(2);
        ring.Offset.Should().Be(2, "the ring stands outside the segment like the ring of every other control");
        ring.Clipped.Should().BeFalse("nothing in the switch clips its ring");
        Contrast(ring.Ring, ring.Wall).Should().BeGreaterThanOrEqualTo(RingContrast, "the ring reads on the wall");

        if (width >= FullBarWidth)
        {
            ring.Bar.Should().BeApproximately(112, Tolerance, "the desktop bar keeps its two rows");
        }
    }

    [Fact]
    public async Task Forced_colours_and_reduced_motion_are_honoured()
    {
        await StartAsync(PrototypeOn);
        await StubNamesAsync();
        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ForcedColors = ForcedColors.Active });
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.Locator("fieldset[data-filter=\"players\"] input[value=\"5\"]").ClickAsync();
        await Expect(Page.Locator(Dimmed).First).ToBeAttachedAsync();

        var outline = await ReadAsync<OutlineReading>(ReadOutline, ".filter-chip:has(input[value=\"5\"]:checked)");
        outline.Style.Should().Be("solid", "a chosen chip is drawn with the system outline");
        outline.Width.Should().BeGreaterThanOrEqualTo(2);
        await Expect(Page.Locator(Dimmed).First).ToHaveCSSAsync("opacity", "0.5");
        await Expect(Page.Locator(Lit).First).ToHaveCSSAsync("opacity", "1");

        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ForcedColors = ForcedColors.None, ReducedMotion = ReducedMotion.Reduce });
        await Page.EvaluateAsync(ViewTransitionSpy);
        await ExpectViewSwitchAvailableAsync();
        await ChooseViewAsync(1);
        await Expect(Page.Locator("#cabinet section.location-group").First).ToBeAttachedAsync();
        await ChooseViewAsync(0);
        await ExpectNothingMovesAsync(
        [
            new Quiet(Dimmed + " > .placement-veil", null),
            new Quiet(Bar, "::after"),
            new Quiet(".filter-view-segment", null),
        ]);
        (await Page.EvaluateAsync<int>("() => window.__transitions")).Should().Be(0, "the view switch starts no view transition");

        await Page.SetViewportSizeAsync(390, 844);
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.EvaluateAsync(ViewTransitionSpy);
        await Page.Locator("#filter-q").FillAsync("a");
        await Expect(Page.Locator(Bar)).ToHaveAttributeAsync("data-active", string.Empty);
        await Page.Locator($"{Bar} .filter-open").ClickAsync();
        await Expect(Page.Locator(OpenSheet)).ToBeVisibleAsync();
        await Page.Locator(".filter-sheet .filter-view-segment").Nth(1).ClickAsync();
        await Expect(Page.Locator("#cabinet section.location-group").First).ToBeAttachedAsync();
        await ExpectNothingMovesAsync(
        [
            new Quiet(Bar, null),
            new Quiet(Bar, "::after"),
            new Quiet(".filter-sheet", null),
            new Quiet(".filter-view-segment", null),
        ]);
        (await Page.EvaluateAsync<int>("() => window.__transitions")).Should().Be(0, "the sheet and the view switch start no view transition");
        ConsoleErrors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(320, "en")]
    [InlineData(320, "nl")]
    [InlineData(390, "en")]
    [InlineData(390, "nl")]
    [InlineData(1024, "en")]
    [InlineData(1024, "nl")]
    [InlineData(1440, "en")]
    [InlineData(1440, "nl")]
    public async Task No_policy_violation_and_no_sideways_scroll(int width, string language)
    {
        await StartAsync(PrototypeOn);
        await StubNamesAsync();
        await ChooseLanguageAsync(language);
        await Page.AddInitScriptAsync(PolicyRecorder);
        await Page.SetViewportSizeAsync(width, 900);
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await ExpectViewSwitchAvailableAsync();
        await ExpectNoSidewaysScrollAsync(width);

        await Page.Locator("#filter-q").FillAsync("a");
        await Expect(Page.Locator(Bar)).ToHaveAttributeAsync("data-active", string.Empty);
        var sheet = width < FullBarWidth;

        if (sheet)
        {
            await Page.Locator($"{Bar} .filter-open").ClickAsync();
            await Expect(Page.Locator(OpenSheet)).ToBeVisibleAsync();
            await ExpectNoSidewaysScrollAsync(width);
        }

        await Page.Locator("fieldset[data-filter=\"players\"] input[value=\"3\"]").ClickAsync();
        var place = await Page.Locator("#filter-loc option").Nth(1).GetAttributeAsync("value");
        await Page.Locator("#filter-loc").SelectOptionAsync(place!);

        if (sheet)
        {
            await Page.Locator(".filter-done").ClickAsync();
            await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        }

        await ChooseViewAsync(1);
        await Expect(Page.Locator("#cabinet section.location-group").First).ToBeAttachedAsync();
        await ExpectNoSidewaysScrollAsync(width);

        (await Page.EvaluateAsync<string[]>("() => window.__violations")).Should().BeEmpty("no inline style or script breaks the content security policy");
        ConsoleErrors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(320, "en")]
    [InlineData(320, "nl")]
    [InlineData(390, "en")]
    [InlineData(390, "nl")]
    [InlineData(1440, "en")]
    [InlineData(1440, "nl")]
    public async Task Long_names_and_Dutch_labels_fit(int width, string language)
    {
        var longName = string.Concat(Enumerable.Repeat("Lengthy", 8));
        await Page.SetViewportSizeAsync(width, 900);
        var host = await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(6));
        var places = await host.SeedAsync(longName, "Shelf B");
        var games = host.Cards.DistinctBy(card => card.GameId).Take(2).ToList();
        await host.PlaceAsync(games[0], places[0]);
        await host.PlaceAsync(games[1], places[1]);
        await StubNamesAsync();
        await ChooseLanguageAsync(language);
        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await ExpectViewSwitchAvailableAsync();
        var sheet = width < FullBarWidth;

        if (sheet)
        {
            await Page.Locator($"{Bar} .filter-open").ClickAsync();
            await Expect(Page.Locator(OpenSheet)).ToBeVisibleAsync();
        }

        var select = await ReadAsync<SelectReading>(ReadSelect, new { name = longName, selector = "#filter-loc" });
        select.HoldsFullName.Should().BeTrue("the list holds the whole name");
        select.Natural.Should().BeGreaterThan(select.Width, "the name is longer than the select, so the select cuts it");
        select.Width.Should().BeLessThanOrEqualTo(select.Container + Tolerance, "a long name never widens the select");
        await ExpectNoSidewaysScrollAsync(width);

        var controls = await ReadAsync<List<ControlReading>>(ReadControls, null);
        controls.Should().NotBeEmpty();
        controls.Should().OnlyContain(control => !control.Outside && !control.Clipped, "every label stays inside its own control");
        controls.Should().OnlyContain(control => control.Height >= SheetControlHeight - Tolerance, "every control is at least 44 px tall");

        if (sheet)
        {
            await Page.Locator(".filter-done").ClickAsync();
            await Expect(Page.Locator(OpenSheet)).ToHaveCountAsync(0);
        }

        await ChooseViewAsync(1);
        var heading = Page.Locator(Headings).First;
        await Expect(heading).ToContainTextAsync(longName);
        var box = (await heading.BoundingBoxAsync())!;
        box.Width.Should().BeLessThanOrEqualTo(width);

        if (width < PhoneHeadingWrapWidth)
        {
            box.Height.Should().BeGreaterThan(60, "the heading wraps the long name over more than one line");
        }

        await ExpectNoSidewaysScrollAsync(width);

        await Page.Locator(".skip-link").FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator("#games-list-start")).ToBeFocusedAsync();
        await Expect(Page.Locator(GroupTitles).First).ToContainTextAsync(longName);
        (await Page.Locator(".games-list").EvaluateAsync<bool>("(panel) => panel.scrollWidth <= panel.clientWidth")).Should().BeTrue("the list wraps the long name");
        ConsoleErrors.Should().BeEmpty();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private static double Contrast(string first, string second)
    {
        static double Luminance(string colour)
        {
            var channels = System.Text.RegularExpressions.Regex.Matches(colour, @"[\d.]+").Select(match => double.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture)).Take(3).ToArray();
            var linear = channels.Select(value => value / 255 <= 0.04045 ? value / 255 / 12.92 : Math.Pow((value / 255 + 0.055) / 1.055, 2.4)).ToArray();

            return (0.2126 * linear[0]) + (0.7152 * linear[1]) + (0.0722 * linear[2]);
        }

        var one = Luminance(first);
        var two = Luminance(second);

        return (Math.Max(one, two) + 0.05) / (Math.Min(one, two) + 0.05);
    }

    private Task StubNamesAsync() =>
        Page.RouteAsync("**/cabinet/search-names**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "application/json",
            Body = "{\"names\":{}}",
        }));

    private async Task ChooseLanguageAsync(string language)
    {
        if (language != "en")
        {
            await Context.AddCookiesAsync([new Cookie { Name = "lang", Value = language, Url = $"http://127.0.0.1:{PublicPort}" }]);
        }
    }

    private async Task ExpectNoSidewaysScrollAsync(int width)
    {
        var reading = await ReadAsync<ScrollReading>("() => ({ scroll: document.documentElement.scrollWidth, viewport: document.documentElement.clientWidth })", null);

        reading.Scroll.Should().BeLessThanOrEqualTo(reading.Viewport, "the page never scrolls sideways");
        reading.Viewport.Should().BeLessThanOrEqualTo(width);
    }

    private async Task ExpectNothingMovesAsync(IReadOnlyList<Quiet> elements)
    {
        var readings = await ReadAsync<List<TransitionReading>>(ReadTransitions, elements.Select(element => new { selector = element.Selector, pseudo = element.Pseudo }).ToList());

        readings.Should().HaveCount(elements.Count);
        readings.Where(reading => reading.Moving).Select(reading => reading.Selector).Should().BeEmpty("nothing transitions under reduced motion");
    }

    private async Task<T> ReadAsync<T>(string script, object? argument)
    {
        var element = await Page.EvaluateAsync<JsonElement>(script, argument);

        return JsonSerializer.Deserialize<T>(element.GetRawText(), WebOptions)!;
    }

    private sealed record RingReading(string Style, double Width, double Offset, string Ring, string Wall, bool Clipped, double Bar);

    private sealed record Quiet(string Selector, string? Pseudo);

    private sealed record OutlineReading(string Style, double Width);

    private sealed record TransitionReading(string Selector, bool Moving);

    private sealed record SelectReading(double Width, double Natural, double Container, bool HoldsFullName);

    private sealed record ControlReading(string Name, double Height, bool Outside, bool Clipped);

    private sealed record ScrollReading(double Scroll, double Viewport);
}

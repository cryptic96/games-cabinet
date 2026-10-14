using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Owner;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using SkiaSharp;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves the cubby lights with a real browser: with the lights always on the light layer is drawn at once but starts at nothing
/// and fades in after the first paint, a box face is the same pixel with the lights off and on because the light stays under the
/// boxes, and the page never breaks the content security policy.
/// </summary>
[Trait("Category", "Browser")]
public sealed class LightsTests : CabinetPageTest
{
    private const string LayerSelector = "#cabinet .section-light";

    private const int FadeWaitMilliseconds = 6000;

    private const string ViewerZone = "Europe/Berlin";

    private const int EdgeTolerance = 3;

    private const int BackAboveBoxPixels = 3;

    private const double SoftEdge = 1.3;

    private const double HardEdge = 1.15;

    private const double MostSoftEdgesShare = 0.05;

    /// <summary>
    /// The few box tops a pale wood back may have under the hard limit: the shelf's own shade takes that back down to the lightness
    /// of a dark box, by day as well as with the lights on, and it carries no keyline.
    /// </summary>
    private const double PaleBackHardEdgesShare = 0.01;

    private const double ShadeFadesOverTopShare = 0.2;

    private const string RecordFirstDraw = """
        (() => {
          window.__lamp = { drawn: null, nextFrame: null };
          document.addEventListener('cabinet:drawn', () => {
            const layer = document.querySelector('#cabinet .section-light');
            const read = () => {
              const style = getComputedStyle(layer);
              const root = document.documentElement;
              return {
                display: style.display,
                opacity: Number(style.opacity),
                lit: root.hasAttribute('data-lit'),
                settled: root.hasAttribute('data-settled'),
                animations: layer.getAnimations().length,
              };
            };
            window.__lamp.drawn = read();
            requestAnimationFrame(() => { window.__lamp.nextFrame = read(); });
          }, { once: true });
        })();
        """;

    private const string RecordDomState = """
        (() => {
          window.__dom = { lit: null, evening: null, bridge: null, violations: [] };
          document.addEventListener('securitypolicyviolation', (event) => window.__dom.violations.push(event.violatedDirective));
          document.addEventListener('DOMContentLoaded', () => {
            window.__dom.lit = document.documentElement.hasAttribute('data-lit');
            window.__dom.evening = document.documentElement.hasAttribute('data-evening');
            window.__dom.bridge = typeof window.cabinetEvening;
          });
        })();
        """;

    private const string StorageThatThrows = """
        Object.defineProperty(window, 'sessionStorage', { get() { throw new DOMException('denied', 'SecurityError'); } });
        """;

    private static readonly Regex EveningScriptAddress = new(@"/js/evening\.([^/?]+\.)?js(\?|$)");

    private static readonly (string Wood, string Backs)[] BackPairs =
    [
        ("brown", "wood"),
        ("walnut", "wood"),
        ("oak", "wood"),
        ("birch", "wood"),
        ("mahogany", "wood"),
        ("brown", "painted"),
        ("birch", "painted"),
    ];

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private readonly TemporaryDirectory _storage = new();

    /// <summary>Makes the browser read the clock in a Central European zone, which has summer time.</summary>
    public override BrowserNewContextOptions ContextOptions()
    {
        var options = base.ContextOptions();
        options.TimezoneId = ViewerZone;

        return options;
    }

    [Theory]
    [InlineData("2027-01-15T20:00:00+01:00", true)]
    [InlineData("2027-01-15T12:00:00+01:00", false)]
    [InlineData("2027-07-15T21:00:00+02:00", false)]
    [InlineData("2027-07-15T22:00:00+02:00", true)]
    public async Task With_evenings_only_the_page_is_evening_from_the_first_frame_only_when_it_is_dark_for_the_visitor(string moment, bool evening)
    {
        await StartLitAsync("evenings");
        await Page.Clock.InstallAsync(new ClockInstallOptions { TimeString = moment });
        await Page.AddInitScriptAsync(RecordDomState);
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoCabinetAsync("/?sample=65");

        (await ReadDomAsync()).Evening.Should().Be(evening, "the head script marks the evening before the first paint");
        await Page.WaitForSelectorAsync("html[data-settled]", new PageWaitForSelectorOptions { Timeout = FadeWaitMilliseconds });

        if (evening)
        {
            await WaitUntilLitAsync();
            await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-evening", string.Empty);
        }
        else
        {
            await Expect(Page.Locator("html")).Not.ToHaveAttributeAsync("data-evening", string.Empty);
            await Expect(Page.Locator("html")).Not.ToHaveAttributeAsync("data-lit", string.Empty);
            (await Page.EvaluateAsync<double>($"() => Number(getComputedStyle(document.querySelector('{LayerSelector}')).opacity)")).Should().Be(0);
        }

        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task With_the_lights_always_on_the_cubbies_start_dark_and_fade_in_after_the_first_paint()
    {
        await StartLitAsync("on");
        await Page.AddInitScriptAsync(RecordFirstDraw);
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoCabinetAsync("/?sample=65");

        var first = JsonSerializer.Deserialize<LayerReading>(await Page.EvaluateAsync<string>("() => JSON.stringify(window.__lamp.drawn)"), WebOptions)!;
        first.Display.Should().Be("block", "the light layer is drawn as soon as the cabinet is");
        first.Opacity.Should().Be(0, "the first frame has no light");
        first.Lit.Should().BeFalse();
        first.Settled.Should().BeFalse();

        await Page.WaitForSelectorAsync("html[data-lit][data-settled]", new PageWaitForSelectorOptions { Timeout = FadeWaitMilliseconds });
        await Page.WaitForFunctionAsync(
            "(selector) => [...document.querySelectorAll(selector)].every((layer) => Number(getComputedStyle(layer).opacity) === 1)",
            LayerSelector,
            new PageWaitForFunctionOptions { Timeout = FadeWaitMilliseconds });

        (await Page.EvaluateAsync<int>("() => document.querySelectorAll('#cabinet .section-light').length")).Should().BeGreaterThan(0);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-lit", string.Empty);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-settled", string.Empty);
        await Expect(Page.Locator("html")).Not.ToHaveAttributeAsync("data-fading", string.Empty);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_light_sits_under_the_boxes_so_a_box_face_is_the_same_with_the_lights_off_and_on()
    {
        await StartLitAsync("on");
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoCabinetAsync("/?sample=65");
        await WaitUntilLitAsync();
        await FitViewportToPageAsync();

        await SaveScreenshotAsync("lights-on");
        var faces = await BoxFacesAsync();
        var lit = await Page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true, Animations = ScreenshotAnimations.Disabled });

        await Page.EvaluateAsync("() => { const root = document.documentElement; root.dataset.lights = 'off'; root.removeAttribute('data-lit'); }");
        await FitViewportToPageAsync();
        var dark = await Page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true, Animations = ScreenshotAnimations.Disabled });

        faces.Should().NotBeEmpty();
        PixelComparison.Compare(lit, dark).DifferingPixels.Should().BeGreaterThan(0, "the light does change the back");
        FacePixelDifferences(lit, dark, faces).Should().Be(0, "no light is painted over a box face");
    }

    [Fact]
    public async Task A_later_load_in_the_same_tab_starts_lit_from_the_first_frame_with_no_fade()
    {
        await StartLitAsync("on");
        await Page.AddInitScriptAsync(RecordDomState);
        await Page.AddInitScriptAsync(RecordFirstDraw);
        await Page.SetViewportSizeAsync(1440, 900);
        var address = await GotoCabinetAsync("/?sample=65");

        (await ReadDomAsync()).Lit.Should().BeFalse("the first load in a tab is not lit before its first paint");
        (await ReadDrawAsync("drawn")).Opacity.Should().Be(0);
        await WaitUntilLitAsync();

        await Page.GotoAsync(address);
        await Page.WaitForSelectorAsync("#cabinet .placement", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
        await Page.WaitForFunctionAsync("() => window.__lamp.nextFrame !== null");

        (await ReadDomAsync()).Lit.Should().BeTrue("the head script lit the page before the first paint");
        var drawn = await ReadDrawAsync("drawn");
        var nextFrame = await ReadDrawAsync("nextFrame");
        drawn.Opacity.Should().Be(1);
        drawn.Animations.Should().Be(0, "no transition is running");
        nextFrame.Opacity.Should().Be(1);
        nextFrame.Animations.Should().Be(0);
        await Page.WaitForSelectorAsync("html[data-settled]");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Storage_that_refuses_to_be_used_makes_every_load_fade_on()
    {
        await StartLitAsync("on");
        await Page.AddInitScriptAsync(StorageThatThrows);
        await Page.AddInitScriptAsync(RecordDomState);
        await Page.AddInitScriptAsync(RecordFirstDraw);
        await Page.SetViewportSizeAsync(1440, 900);
        var address = await GotoCabinetAsync("/?sample=65");
        await WaitUntilLitAsync();

        await Page.GotoAsync(address);
        await Page.WaitForSelectorAsync("#cabinet .placement", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
        await Page.WaitForFunctionAsync("() => window.__lamp.drawn !== null");

        (await ReadDomAsync()).Lit.Should().BeFalse();
        (await ReadDrawAsync("drawn")).Opacity.Should().Be(0, "every load fades on when nothing can be remembered");
        await WaitUntilLitAsync();
    }

    [Fact]
    public async Task With_the_lights_off_the_head_script_is_not_requested_and_the_light_layer_is_not_displayed()
    {
        var scriptRequests = new List<string>();
        await StartLitAsync("off");
        Page.Request += (_, request) =>
        {
            if (EveningScriptAddress.IsMatch(request.Url))
            {
                scriptRequests.Add(request.Url);
            }
        };
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoCabinetAsync("/?sample=65");
        await Task.Delay(300, TestContext.Current.CancellationToken);

        scriptRequests.Should().BeEmpty();
        (await Page.EvaluateAsync<int>("() => document.head.querySelectorAll('script[src*=\"/js/evening.\"]').length")).Should().Be(0);
        (await Page.EvaluateAsync<string>("() => document.querySelector('meta[name=\"cabinet-evening-script\"]').content")).Should().MatchRegex(@"^/js/evening\.js\?v=\S+$");
        (await Page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('#cabinet .section-light')).display")).Should().Be("none");
        await Expect(Page.Locator("html")).Not.ToHaveAttributeAsync("data-lit", string.Empty);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task With_the_lights_on_the_head_script_loads_synchronously_in_the_head_and_breaks_no_security_rule()
    {
        await StartLitAsync("on");
        await Page.AddInitScriptAsync(RecordDomState);
        await Page.SetViewportSizeAsync(1440, 900);
        var request = await Page.RunAndWaitForRequestAsync(
            () => GotoCabinetAsync("/?sample=65"),
            candidate => EveningScriptAddress.IsMatch(candidate.Url));

        request.Url.Should().MatchRegex(EveningScriptAddress.ToString());
        var script = await Page.EvaluateAsync<string[]>(
            "() => { const element = document.head.querySelector('script[src*=\"/js/evening.\"]'); return [element.getAttribute('src'), String(element.async), String(element.defer), element.type]; }");
        var meta = await Page.EvaluateAsync<string>("() => document.querySelector('meta[name=\"cabinet-evening-script\"]').content");

        script[0].Should().MatchRegex(@"^/js/evening\.[^/]+\.js$|^/js/evening\.js\?v=\S+$");
        script[1].Should().Be("false");
        script[2].Should().Be("false");
        script[3].Should().BeEmpty("it is a classic script, not a module");
        meta.Should().MatchRegex(@"^/js/evening\.js\?v=\S+$", "the page names the versioned address of the head script");
        (await ReadDomAsync()).Bridge.Should().Be("object", "the head script had run when the document finished parsing");
        (await Page.EvaluateAsync<string[]>("() => window.__dom.violations")).Should().BeEmpty();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Lit_oak_glows_on_the_floor_in_front_of_the_base_and_lit_brown_does_not_and_nothing_leaves_the_section()
    {
        await StartLitAsync("on", wood: "oak");
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoCabinetAsync("/?sample=65");
        await WaitUntilLitAsync();
        await FitViewportToPageAsync();

        var bands = await FloorBandsAsync();
        await SaveScreenshotAsync("lights-on-oak");
        var lit = await FullPageAsync();
        var edges = EdgeDifferences(lit, await SectionRectsAsync());

        await SwitchLightsOffAsync();
        var dark = await FullPageAsync();

        bands.Should().NotBeEmpty();
        foreach (var band in bands)
        {
            WarmthOf(lit, band).Should().BeGreaterThan(WarmthOf(dark, band) + 4, "the floor in front of a lit cabinet catches a warm glow");
        }

        edges.Should().OnlyContain(difference => difference <= EdgeTolerance, "the glow ends inside the section box");

        await Page.EvaluateAsync("() => { const root = document.documentElement; root.dataset.wood = 'brown'; root.dataset.lights = 'on'; root.setAttribute('data-lit', ''); }");
        await FitViewportToPageAsync();
        var brownBands = await FloorBandsAsync();
        var brownLit = await FullPageAsync();
        await SwitchLightsOffAsync();
        var brownDark = await FullPageAsync();

        foreach (var band in brownBands)
        {
            BandDifferences(brownLit, brownDark, band).Should().Be(0, "a wood without a floor has no glow on it");
        }
    }

    [Fact]
    public async Task In_deep_the_lamp_brightens_the_top_face_of_every_shelf_board()
    {
        await StartLitAsync("on", depth: "deep");
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoCabinetAsync("/?sample=65");
        await WaitUntilLitAsync();
        await FitViewportToPageAsync();
        await SaveScreenshotAsync("lights-on-deep");

        var bands = await BoardTopBandsAsync();
        var lit = await FullPageAsync();
        await SwitchLightsOffAsync();
        var dark = await FullPageAsync();

        bands.Should().NotBeEmpty();
        foreach (var band in bands)
        {
            BrightnessOf(lit, band).Should().BeGreaterThan(BrightnessOf(dark, band) + 10, "the lamp lights the top face of the board");
        }
    }

    [Theory]
    [InlineData("classic")]
    [InlineData("modern")]
    public async Task With_the_lights_on_nearly_every_box_top_keeps_an_edge_against_every_back(string design)
    {
        await StartLitAsync("on");
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoCabinetAsync("/?sample=400");
        await WaitUntilLitAsync();
        await Page.EvaluateAsync(
            "(css) => { const sheet = new CSSStyleSheet(); sheet.replaceSync(css); document.adoptedStyleSheets = [...document.adoptedStyleSheets, sheet]; }",
            ".placement { visibility: hidden !important; }");

        foreach (var (wood, backs) in BackPairs)
        {
            await Page.EvaluateAsync(
                "([design, wood, backs]) => { const root = document.documentElement; root.dataset.design = design; root.dataset.wood = wood; root.dataset.backs = backs; root.setAttribute('data-lit', ''); }",
                new[] { design, wood, backs });
            await FitViewportToPageAsync();

            var edges = await BoxTopEdgesAsync();
            var label = $"{design} with {wood} and {backs} backs";

            edges.Count.Should().BeGreaterThan(300, label);
            edges.Count(edge => edge < SoftEdge).Should().BeLessThanOrEqualTo((int)Math.Floor(edges.Count * MostSoftEdgesShare), $"{label}: at most 5 % of box tops under {SoftEdge}");

            var hardAllowed = wood == "birch" && backs == "wood" ? (int)Math.Ceiling(edges.Count * PaleBackHardEdgesShare) : 0;
            edges.Count(edge => edge < HardEdge).Should().BeLessThanOrEqualTo(hardAllowed, $"{label}: no box top under {HardEdge}, except a pale back's own few");
        }
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task StartLitAsync(string lights, string wood = "brown", string depth = "shaded")
    {
        var look = new CabinetLook("classic", wood, "wood", lights, depth);
        OwnerDataSeed.Write(_storage.FullPath, OwnerData.Empty with { Revision = 1, Look = look });
        await StartAsync(new Dictionary<string, string?>(PrototypeOn) { ["Storage:Directory"] = _storage.FullPath });
    }

    private Task WaitUntilLitAsync() =>
        Page.WaitForFunctionAsync(
            "(selector) => document.documentElement.hasAttribute('data-lit') && [...document.querySelectorAll(selector)].every((layer) => Number(getComputedStyle(layer).opacity) === 1)",
            LayerSelector,
            new PageWaitForFunctionOptions { Timeout = FadeWaitMilliseconds });

    /// <summary>
    /// Makes the viewport as tall as the page, at its current width, until the height stops changing, so a full-page picture never
    /// makes the browser resize the page while it captures (which would reload the cabinet on a desktop width).
    /// </summary>
    private async Task FitViewportToPageAsync()
    {
        var width = (Page.ViewportSize ?? throw new InvalidOperationException("The page has no fixed viewport.")).Width;
        var settled = 0;
        var last = -1;

        while (settled < 3)
        {
            var height = await Page.EvaluateAsync<int>("() => document.documentElement.scrollHeight");
            await Page.SetViewportSizeAsync(width, height);
            await Page.EvaluateAsync("() => document.fonts.ready");
            await Page.EvaluateAsync("() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
            settled = height == last ? settled + 1 : 0;
            last = height;
        }
    }

    private async Task SwitchLightsOffAsync()
    {
        await Page.EvaluateAsync("() => { const root = document.documentElement; root.dataset.lights = 'off'; root.removeAttribute('data-lit'); }");
        await FitViewportToPageAsync();
    }

    private async Task<byte[]> FullPageAsync() =>
        await Page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true, Animations = ScreenshotAnimations.Disabled });

    private async Task<DomState> ReadDomAsync() =>
        JsonSerializer.Deserialize<DomState>(await Page.EvaluateAsync<string>("() => JSON.stringify(window.__dom)"), WebOptions)!;

    private async Task<LayerReading> ReadDrawAsync(string moment) =>
        JsonSerializer.Deserialize<LayerReading>(await Page.EvaluateAsync<string>("(moment) => JSON.stringify(window.__lamp[moment])", moment), WebOptions)!;

    private async Task<double[][]> SectionRectsAsync() =>
        await Page.EvaluateAsync<double[][]>(
            "() => [...document.querySelectorAll('#cabinet .section')].map((element) => { const r = element.getBoundingClientRect(); return [r.x + scrollX, r.y + scrollY, r.width, r.height]; })");

    /// <summary>
    /// The rectangle of the floor margin under every section, over the middle half of its width: the strip in front of the base
    /// where the lamp's floor glow falls.
    /// </summary>
    private async Task<List<int[]>> FloorBandsAsync()
    {
        var margin = await Page.EvaluateAsync<double>(
            "() => { const style = getComputedStyle(document.querySelector('#cabinet .cubby')); return parseFloat(style.getPropertyValue('--u')) * parseFloat(style.getPropertyValue('--floor-margin')); }");
        var bands = new List<int[]>();

        foreach (var section in await SectionRectsAsync())
        {
            var bottom = (int)Math.Floor(section[1] + section[3]);
            var top = (int)Math.Ceiling(section[1] + section[3] - margin) + 1;
            var left = (int)Math.Round(section[0] + (section[2] * 0.25));
            var right = (int)Math.Round(section[0] + (section[2] * 0.75));
            bands.Add([left, top, right, bottom - 1]);
        }

        return bands;
    }

    /// <summary>
    /// The rectangle of the top face of the shelf board under every cubby: the first rows below the cubby, over the middle half of
    /// its width, as many rows as the lamp's wash covers.
    /// </summary>
    private async Task<List<int[]>> BoardTopBandsAsync()
    {
        var rows = await Page.EvaluateAsync<double>(
            "() => { const style = getComputedStyle(document.querySelector('#cabinet .light-pool')); return Math.ceil(0.32 * parseFloat(style.getPropertyValue('--u')) * parseFloat(style.getPropertyValue('--frame'))); }");
        var cubbies = await Page.EvaluateAsync<double[][]>(
            "() => [...document.querySelectorAll('#cabinet .cubby')].map((element) => { const r = element.getBoundingClientRect(); return [r.x + scrollX, r.y + scrollY, r.width, r.height]; })");
        var bands = new List<int[]>();

        foreach (var cubby in cubbies)
        {
            var top = (int)Math.Ceiling(cubby[1] + cubby[3]);
            bands.Add([(int)Math.Round(cubby[0] + (cubby[2] * 0.25)), top, (int)Math.Round(cubby[0] + (cubby[2] * 0.75)), top + Math.Max(1, (int)rows) - 1]);
        }

        return bands;
    }

    private static double WarmthOf(byte[] picture, int[] band) =>
        MeanOver(picture, band, pixel => pixel.Red - pixel.Blue);

    private static double BrightnessOf(byte[] picture, int[] band) =>
        MeanOver(picture, band, pixel => pixel.Red + pixel.Green + pixel.Blue);

    private static double MeanOver(byte[] picture, int[] band, Func<SKColor, int> measure)
    {
        using var bitmap = SKBitmap.Decode(picture) ?? throw new InvalidOperationException("The picture is not readable.");
        var total = 0.0;
        var count = 0;

        for (var y = band[1]; y <= band[3]; y++)
        {
            for (var x = band[0]; x <= band[2]; x++)
            {
                total += measure(bitmap.GetPixel(x, y));
                count++;
            }
        }

        return total / count;
    }

    private static int BandDifferences(byte[] first, byte[] second, int[] band)
    {
        using var left = SKBitmap.Decode(first) ?? throw new InvalidOperationException("The first picture is not readable.");
        using var right = SKBitmap.Decode(second) ?? throw new InvalidOperationException("The second picture is not readable.");
        var differing = 0;

        for (var y = band[1]; y <= band[3]; y++)
        {
            for (var x = band[0]; x <= band[2]; x++)
            {
                if (left.GetPixel(x, y) != right.GetPixel(x, y))
                {
                    differing++;
                }
            }
        }

        return differing;
    }

    /// <summary>
    /// Reads, for every section, how far the pixels on its left, right and bottom edge are from the pixels three pixels outside
    /// them. Anything the lamp paints beyond the section's box shows up as a difference.
    /// </summary>
    private static List<int> EdgeDifferences(byte[] picture, double[][] sections)
    {
        using var bitmap = SKBitmap.Decode(picture) ?? throw new InvalidOperationException("The picture is not readable.");
        var differences = new List<int>();

        foreach (var section in sections)
        {
            var left = (int)Math.Round(section[0]);
            var top = (int)Math.Round(section[1]);
            var right = left + (int)Math.Round(section[2]) - 1;
            var bottom = top + (int)Math.Round(section[3]) - 1;

            for (var x = left + 1; x < right; x += 3)
            {
                differences.Add(Distance(bitmap.GetPixel(x, bottom), bitmap.GetPixel(x, bottom + 3)));
            }

            for (var y = top + 1; y < bottom; y += 3)
            {
                differences.Add(Distance(bitmap.GetPixel(left, y), bitmap.GetPixel(left - 3, y)));
                differences.Add(Distance(bitmap.GetPixel(right, y), bitmap.GetPixel(right + 3, y)));
            }
        }

        return differences;
    }

    private static int Distance(SKColor first, SKColor second) =>
        Math.Max(Math.Abs(first.Red - second.Red), Math.Max(Math.Abs(first.Green - second.Green), Math.Abs(first.Blue - second.Blue)));

    private async Task<List<double[]>> BoxFacesAsync()
    {
        var rects = await Page.EvaluateAsync<double[][]>(
            "() => [...document.querySelectorAll('#cabinet .placement[data-kind=\"spine\"], #cabinet .placement[data-kind=\"flatBox\"]')].map((element) => { const r = element.getBoundingClientRect(); return [r.x + scrollX, r.y + scrollY, r.width, r.height]; })");

        return [.. rects];
    }

    private static int FacePixelDifferences(byte[] first, byte[] second, List<double[]> faces)
    {
        using var left = SKBitmap.Decode(first) ?? throw new InvalidOperationException("The first picture is not readable.");
        using var right = SKBitmap.Decode(second) ?? throw new InvalidOperationException("The second picture is not readable.");
        const int Inset = 3;
        var differing = 0;

        foreach (var face in faces)
        {
            var startX = (int)Math.Ceiling(face[0]) + Inset;
            var endX = (int)Math.Floor(face[0] + face[2]) - Inset;
            var startY = (int)Math.Ceiling(face[1]) + Inset;
            var endY = (int)Math.Floor(face[1] + face[3]) - Inset;

            for (var y = startY; y < endY; y++)
            {
                for (var x = startX; x < endX; x++)
                {
                    if (left.GetPixel(x, y) != right.GetPixel(x, y))
                    {
                        differing++;
                    }
                }
            }
        }

        return differing;
    }

    /// <summary>
    /// Measures, for every box, how far its top edge stands out from the back above it. The boxes are hidden and the back is read
    /// three pixels above the box's top edge; the box colour is its own colour after the shade that lies over the top fifth of the
    /// cubby. The edge is the larger of the box against the back and the weaker side of the keyline against the back and the box.
    /// </summary>
    private async Task<List<double>> BoxTopEdgesAsync()
    {
        var boxes = JsonSerializer.Deserialize<List<BoxTop>>(
            await Page.EvaluateAsync<string>(
                """
                () => JSON.stringify([...document.querySelectorAll('#cabinet .cubby')].flatMap((cubby) => {
                  const cubbyRect = cubby.getBoundingClientRect();
                  const style = getComputedStyle(cubby);
                  const shade = parseFloat(style.getPropertyValue('--shade-alpha')) || 0;
                  const shadeRgb = style.getPropertyValue('--box-shade-rgb').trim().split(/\s+/).map(Number);
                  return [...cubby.querySelectorAll('.placement')].map((box) => {
                    const rect = box.getBoundingClientRect();
                    return {
                      centre: rect.x + rect.width / 2,
                      width: rect.width,
                      top: rect.y + window.scrollY,
                      cubbyTop: cubbyRect.y + window.scrollY,
                      cubbyHeight: cubbyRect.height,
                      colour: getComputedStyle(box).backgroundColor.match(/\d+/g).slice(0, 3).map(Number),
                      shade,
                      shadeRgb,
                      keyline: parseFloat(getComputedStyle(box).getPropertyValue('--keyline')) || 0,
                    };
                  });
                }))
                """),
            WebOptions)!;
        using var bitmap = SKBitmap.Decode(await FullPageAsync()) ?? throw new InvalidOperationException("The picture is not readable.");
        var edges = new List<double>();

        foreach (var box in boxes)
        {
            var back = AverageOver(bitmap, box);
            var fraction = (box.Top - box.CubbyTop) / box.CubbyHeight;
            var shade = fraction < ShadeFadesOverTopShare ? box.Shade * (1 - (fraction / ShadeFadesOverTopShare)) : 0;
            var face = Mix(box.Colour, [.. box.ShadeRgb], shade);
            var line = Mix(back, [.. box.ShadeRgb], box.Keyline);
            var faceOverBack = ContrastRatio(face, back);

            edges.Add(Math.Max(faceOverBack, Math.Min(ContrastRatio(line, back), ContrastRatio(line, face))));
        }

        return edges;
    }

    private static double[] AverageOver(SKBitmap bitmap, BoxTop box)
    {
        var y = (int)Math.Round(box.Top) - BackAboveBoxPixels;
        var left = (int)Math.Round(box.Centre - Math.Max(1, box.Width * 0.3));
        var width = Math.Max(2, (int)Math.Round(box.Width * 0.6));
        double[] sum = [0, 0, 0];
        var count = 0;

        for (var row = y; row < y + 2; row++)
        {
            for (var x = left; x < left + width; x++)
            {
                var pixel = bitmap.GetPixel(x, row);
                sum[0] += pixel.Red;
                sum[1] += pixel.Green;
                sum[2] += pixel.Blue;
                count++;
            }
        }

        return [sum[0] / count, sum[1] / count, sum[2] / count];
    }

    private static double[] Mix(double[] below, double[] above, double aboveShare) =>
        [.. below.Select((channel, index) => (channel * (1 - aboveShare)) + (above[index] * aboveShare))];

    private static double ContrastRatio(double[] first, double[] second)
    {
        var one = Luminance(first);
        var two = Luminance(second);

        return (Math.Max(one, two) + 0.05) / (Math.Min(one, two) + 0.05);
    }

    private static double Luminance(double[] channels)
    {
        static double Linear(double channel)
        {
            var share = channel / 255;
            return share <= 0.03928 ? share / 12.92 : Math.Pow((share + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linear(channels[0])) + (0.7152 * Linear(channels[1])) + (0.0722 * Linear(channels[2]));
    }

    private sealed record BoxTop(double Centre, double Width, double Top, double CubbyTop, double CubbyHeight, double[] Colour, double Shade, double[] ShadeRgb, double Keyline);

    private sealed record DomState(bool Lit, bool Evening, string Bridge);

    private sealed record LayerReading(string Display, double Opacity, bool Lit, bool Settled, int Animations);
}

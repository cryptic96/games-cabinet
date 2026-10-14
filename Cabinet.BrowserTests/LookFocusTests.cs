using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using SkiaSharp;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the focus ring is always seen whole. A focused box wears the same light ring on every wood and on
/// both kinds of back, with an ink halo outside it on the woods whose frame is pale, and on a box at the left or right edge of a
/// section none of it is cut off by the section's box: the ring is present on all four sides. The page's own controls on the wall wear
/// the wall's ring colour on every wall.
/// </summary>
[Trait("Category", "Browser")]
public sealed partial class LookFocusTests : CabinetPageTest, IDisposable
{
    private const string BoxRing = "rgb(217, 185, 138)";

    private const double RingAgainstPaintedBack = 3;

    private const int ColourTolerance = 24;

    private const int Margin = 10;

    private const int Corner = 4;

    private const int RingFrom = 1;

    private const int RingTo = 4;

    private const int HaloTo = 5;

    private const double WholeRing = 0.95;

    private const int ViewportHeight = 1200;

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private static readonly int[] Widths = [1440, 390];

    private static readonly string[] BackKinds = ["wood", "painted"];

    private static readonly string[] HaloWoods = ["oak", "birch"];

    private readonly TemporaryDirectory _storage = new();

    /// <summary>Every wood at the two widths the cabinet is checked at.</summary>
    public static TheoryData<string, int> WoodsAtWidths { get; } = BuildWoodsAtWidths();

    /// <summary>Every wood.</summary>
    public static TheoryData<string> Woods { get; } = BuildWoods();

    /// <summary>
    /// A box at the left or right edge of a section, on any design, with either kind of back, wears the light ring in full on all four
    /// sides; on the woods with a pale frame the ring stands between two bands of ink, so it is seen against the frame. The ring is the same
    /// colour on every wood and reads against the painted back.
    /// </summary>
    /// <param name="wood">The wood to draw.</param>
    /// <param name="width">The width of the page.</param>
    [Theory]
    [MemberData(nameof(WoodsAtWidths))]
    public async Task A_box_at_the_edge_of_a_section_wears_its_whole_ring_on_every_design_and_back(string wood, int width)
    {
        await StartWithLookAsync(new CabinetLook("classic", wood, "wood", "off", "shaded"), "/?sample=65", width);

        foreach (var design in CabinetLook.Designs)
        {
            foreach (var backs in BackKinds)
            {
                await SetAsync(("design", design), ("backs", backs));
                await FitViewportToPageAsync();

                var where = $"{design} on {wood} with {backs} backs at {width} px";
                var edges = await EdgeBoxesAsync();
                edges.Should().NotBeEmpty(where);

                foreach (var edge in edges)
                {
                    var label = $"{where}, section {edge.Section + 1}, the box at its {edge.Side} edge";
                    var look = await ReadRingAsync(edge);

                    look.FocusVisible.Should().BeTrue($"{label}: a keyboard focus shows the ring");
                    look.OutlineColour.Should().Be(BoxRing, $"{label}: the ring is the light accent on every wood");
                    look.OutlineStyle.Should().Be("solid", label);
                    look.OutlineWidth.Should().Be("2px", label);
                    look.OutlineOffset.Should().Be("2px", label);

                    var halo = HaloWoods.Contains(wood) ? ToSkia(ParseColour(look.Halo)) : (SKColor?)null;
                    var coverage = await RingCoverageAsync(edge, new SKColor(217, 185, 138), halo);
                    coverage.Top.Should().BeGreaterThanOrEqualTo(WholeRing, $"{label}: the top of the ring is whole");
                    coverage.Bottom.Should().BeGreaterThanOrEqualTo(WholeRing, $"{label}: the bottom of the ring is whole");
                    coverage.Left.Should().BeGreaterThanOrEqualTo(WholeRing, $"{label}: the left of the ring is whole");
                    coverage.Right.Should().BeGreaterThanOrEqualTo(WholeRing, $"{label}: the right of the ring is whole");

                    if (halo is not null)
                    {
                        look.Halo.Should().NotBe("rgba(0, 0, 0, 0)", $"{label}: this wood draws an ink halo");
                        look.BoxShadow.Should().Contain(look.Halo, $"{label}: the halo is part of the focused box's shadow").And.Contain("6px", label);
                    }
                    else
                    {
                        look.Halo.Should().Be("rgba(0, 0, 0, 0)", $"{label}: this wood draws no halo");
                    }
                }
            }

            await Page.EvaluateAsync("() => document.activeElement && document.activeElement.blur()");
        }

        await SetAsync(("backs", "painted"));
        var paintedBack = await RootColourAsync("--back-painted");
        SpineColour.ContrastRatio(ParseColour(BoxRing), paintedBack).Should().BeGreaterThanOrEqualTo(RingAgainstPaintedBack, $"{wood}: the ring reads against the painted back");
        ConsoleErrors.Should().BeEmpty();
    }

    /// <summary>
    /// The page's own controls on the wall (a language link, a link that switches the collection, the sync button) wear a two pixel ring in
    /// the wall's ring colour, and that colour reaches 3:1 against the wall, on every wall.
    /// </summary>
    /// <param name="wood">The wood whose wall is drawn.</param>
    [Theory]
    [MemberData(nameof(Woods))]
    public async Task The_controls_on_the_wall_wear_the_wall_ring_on_every_wall(string wood)
    {
        await StartWithLookAsync(new CabinetLook("classic", wood, "wood", "off", "shaded"), "/", 1440);

        var ring = await RootColourAsync("--room-ring");
        var wall = ParseColour(await Page.EvaluateAsync<string>("() => getComputedStyle(document.body).backgroundColor"));
        SpineColour.ContrastRatio(ring, wall).Should().BeGreaterThanOrEqualTo(RingAgainstPaintedBack, $"{wood}: the wall ring reads against its wall");

        await Page.Locator(".sync-button").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

        foreach (var selector in new[] { ".lang-toggle a[lang=en]", ".lang-toggle a[lang=nl]", ".sample-switcher a", ".sync-button" })
        {
            var control = Page.Locator(selector).First;
            await control.FocusAsync();

            var style = await control.EvaluateAsync<string[]>(
                "(element) => { const s = getComputedStyle(element); return [element.matches(':focus-visible') ? 'yes' : 'no', s.outlineColor, s.outlineStyle, s.outlineWidth, s.outlineOffset]; }");

            style[0].Should().Be("yes", $"{wood}: {selector} shows its ring on a keyboard focus");
            ParseColour(style[1]).Should().Be(ring, $"{wood}: {selector} wears the wall's ring colour");
            style[2].Should().Be("solid", $"{wood}: {selector}");
            style[3].Should().Be("2px", $"{wood}: {selector}");
            style[4].Should().Be("2px", $"{wood}: {selector}");

            await FitViewportToPageAsync();
            var coverage = await ControlRingCoverageAsync(control, new SKColor(ring.R, ring.G, ring.B));
            new[] { coverage.Top, coverage.Bottom, coverage.Left, coverage.Right }
                .Should().OnlyContain(side => side >= WholeRing, $"{wood}: the ring of {selector} is whole on all four sides");
        }

        ConsoleErrors.Should().BeEmpty();
    }

    /// <summary>
    /// The ring measurement has teeth: when a cubby cuts off what is drawn outside it, the ring of the box at the section's left edge
    /// is no longer whole there, and the measurement says so.
    /// </summary>
    [Fact]
    public async Task A_ring_that_a_cubby_cuts_off_is_reported_as_not_whole()
    {
        await StartWithLookAsync(CabinetLook.Default, "/?sample=65", 1440);
        await Page.EvaluateAsync(
            "(css) => { const sheet = new CSSStyleSheet(); sheet.replaceSync(css); document.adoptedStyleSheets = [...document.adoptedStyleSheets, sheet]; }",
            ".cubby { overflow: hidden; }");
        await FitViewportToPageAsync();

        var leftEdge = (await EdgeBoxesAsync()).Single(edge => edge.Side == "left");
        var coverage = await RingCoverageAsync(leftEdge, new SKColor(217, 185, 138), null);

        coverage.Left.Should().BeLessThan(WholeRing);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private static TheoryData<string, int> BuildWoodsAtWidths()
    {
        var data = new TheoryData<string, int>();

        foreach (var wood in CabinetLook.Woods)
        {
            foreach (var width in Widths)
            {
                data.Add(wood, width);
            }
        }

        return data;
    }

    private static TheoryData<string> BuildWoods()
    {
        var data = new TheoryData<string>();

        foreach (var wood in CabinetLook.Woods)
        {
            data.Add(wood);
        }

        return data;
    }

    private static RgbColour ParseColour(string computed)
    {
        var match = ComputedColour().Match(computed);

        match.Success.Should().BeTrue($"'{computed}' is a computed rgb colour");

        return new RgbColour(
            byte.Parse(match.Groups["r"].Value, CultureInfo.InvariantCulture),
            byte.Parse(match.Groups["g"].Value, CultureInfo.InvariantCulture),
            byte.Parse(match.Groups["b"].Value, CultureInfo.InvariantCulture));
    }

    private static SKColor ToSkia(RgbColour colour) => new(colour.R, colour.G, colour.B);

    private static int Distance(SKColor first, SKColor second) =>
        Math.Max(Math.Abs(first.Red - second.Red), Math.Max(Math.Abs(first.Green - second.Green), Math.Abs(first.Blue - second.Blue)));

    [GeneratedRegex(@"^rgba?\((?<r>\d+), (?<g>\d+), (?<b>\d+)(, [0-9.]+)?\)$")]
    private static partial Regex ComputedColour();

    private async Task StartWithLookAsync(CabinetLook look, string path, int width)
    {
        OwnerDataSeed.Write(_storage.FullPath, OwnerData.Empty with { Revision = 1, Look = look });
        await StartAsync(new Dictionary<string, string?>(PrototypeOn) { ["Storage:Directory"] = _storage.FullPath });
        await Page.SetViewportSizeAsync(width, ViewportHeight);
        await GotoCabinetAsync(path);
    }

    private async Task SetAsync(params (string Name, string Value)[] choices)
    {
        foreach (var (name, value) in choices)
        {
            await Page.EvaluateAsync("([name, value]) => { document.documentElement.dataset[name] = value; }", new[] { name, value });
        }
    }

    private async Task<RgbColour> RootColourAsync(string property)
    {
        var colour = await Page.EvaluateAsync<string>(
            """
            (name) => {
              const probe = document.createElement('span');
              probe.style.color = 'var(' + name + ')';
              document.body.append(probe);
              const resolved = getComputedStyle(probe).color;
              probe.remove();
              return resolved;
            }
            """,
            property);

        return ParseColour(colour);
    }

    /// <summary>
    /// Makes the viewport as tall as the page, at its current width, until the height stops changing, so every picture can be taken
    /// from the top of the page without the browser scrolling or resizing it.
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

    /// <summary>
    /// For the first section of the page, the box standing furthest to the left and the one standing furthest to the right.
    /// </summary>
    private async Task<List<EdgeBox>> EdgeBoxesAsync()
    {
        var found = await Page.EvaluateAsync<string>(
            """
            () => JSON.stringify([...document.querySelectorAll('#cabinet .section')].slice(0, 1).flatMap((section, index) => {
              const boxes = [...section.querySelectorAll('.placement')];
              if (boxes.length === 0) {
                return [];
              }
              const left = boxes.reduce((best, box) => (box.getBoundingClientRect().left < best.getBoundingClientRect().left ? box : best));
              const right = boxes.reduce((best, box) => (box.getBoundingClientRect().right > best.getBoundingClientRect().right ? box : best));
              const at = (box) => [...document.querySelectorAll('#cabinet .placement')].indexOf(box);
              return [
                { section: index, side: 'left', position: at(left) },
                { section: index, side: 'right', position: at(right) },
              ];
            }))
            """);

        return JsonSerializer.Deserialize<List<EdgeBox>>(found, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private async Task<RingLook> ReadRingAsync(EdgeBox edge)
    {
        var read = await Page.EvaluateAsync<string[]>(
            """
            (position) => {
              const box = document.querySelectorAll('#cabinet .placement')[position];
              box.focus();
              const style = getComputedStyle(box);
              const halo = getComputedStyle(document.documentElement).getPropertyValue('--ring-halo').trim();
              const probe = document.createElement('span');
              probe.style.color = halo;
              document.body.append(probe);
              const resolved = getComputedStyle(probe).color;
              probe.remove();
              return [
                box.matches(':focus-visible') ? 'yes' : 'no',
                style.outlineColor,
                style.outlineStyle,
                style.outlineWidth,
                style.outlineOffset,
                resolved,
                style.boxShadow,
              ];
            }
            """,
            edge.Position);

        return new RingLook(read[0] == "yes", read[1], read[2], read[3], read[4], read[5], read[6]);
    }

    private async Task<byte[]> ClipAsync(double[] rect)
    {
        var size = Page.ViewportSize ?? throw new InvalidOperationException("The page has no fixed viewport.");
        var left = Math.Max(0, (int)Math.Floor(rect[0]) - Margin);
        var top = Math.Max(0, (int)Math.Floor(rect[1]) - Margin);
        var right = Math.Min(size.Width, (int)Math.Ceiling(rect[0] + rect[2]) + Margin);
        var bottom = Math.Min(size.Height, (int)Math.Ceiling(rect[1] + rect[3]) + Margin);

        return await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Animations = ScreenshotAnimations.Disabled,
            Clip = new Clip { X = left, Y = top, Width = right - left, Height = bottom - top },
        });
    }

    private async Task<Sides> RingCoverageAsync(EdgeBox edge, SKColor ring, SKColor? halo)
    {
        await Page.EvaluateAsync("() => document.activeElement && document.activeElement.blur()");
        var rect = await Page.EvaluateAsync<double[]>(
            "(position) => { const r = document.querySelectorAll('#cabinet .placement')[position].getBoundingClientRect(); return [r.left, r.top, r.width, r.height]; }",
            edge.Position);
        var unfocused = await ClipAsync(rect);
        await Page.EvaluateAsync("(position) => document.querySelectorAll('#cabinet .placement')[position].focus()", edge.Position);
        var focused = await ClipAsync(rect);

        return Cover(rect, unfocused, focused, halo is null ? RingShows(ring) : RingBetweenHalos(ring, halo.Value));
    }

    private async Task<Sides> ControlRingCoverageAsync(ILocator control, SKColor ring)
    {
        await control.EvaluateAsync("(element) => element.blur()");
        var rect = await control.EvaluateAsync<double[]>("(element) => { const r = element.getBoundingClientRect(); return [r.left + scrollX, r.top + scrollY, r.width, r.height]; }");
        var unfocused = await ClipAsync(rect);
        await control.FocusAsync();
        var focused = await ClipAsync(rect);

        return Cover(rect, unfocused, focused, RingShows(ring));
    }

    /// <summary>The ring is there when a pixel in its band of rows outside the edge is the ring colour and is not what the page showed before.</summary>
    private static Func<Func<int, SKColor>, Func<int, SKColor>, bool> RingShows(SKColor ring) =>
        (focused, unfocused) => Enumerable.Range(RingFrom, RingTo - RingFrom + 1)
            .Any(k => Distance(focused(k), ring) <= ColourTolerance && Distance(focused(k), unfocused(k)) > ColourTolerance);

    /// <summary>
    /// On a pale frame the ring colour is the colour of the surface itself, so the ring is there when it stands between two bands of
    /// the halo's ink, in order, going outwards from the box.
    /// </summary>
    private static Func<Func<int, SKColor>, Func<int, SKColor>, bool> RingBetweenHalos(SKColor ring, SKColor halo) =>
        (focused, _) =>
        {
            var ink = Enumerable.Range(0, HaloTo + 1).Where(k => Distance(focused(k), halo) <= ColourTolerance).ToList();
            var light = Enumerable.Range(RingFrom, RingTo - RingFrom + 1).Where(k => Distance(focused(k), ring) <= ColourTolerance).ToList();

            return light.Any(k => ink.Any(inner => inner < k) && ink.Any(outer => outer > k));
        };

    /// <summary>
    /// For each side of a rectangle, the share of positions along it (leaving out the corners) where the ring shows going outwards from
    /// the edge, judged by the given test on the focused and the unfocused picture.
    /// </summary>
    private static Sides Cover(double[] rect, byte[] unfocused, byte[] focused, Func<Func<int, SKColor>, Func<int, SKColor>, bool> shows)
    {
        using var before = SKBitmap.Decode(unfocused) ?? throw new InvalidOperationException("The unfocused picture is not readable.");
        using var after = SKBitmap.Decode(focused) ?? throw new InvalidOperationException("The focused picture is not readable.");
        var origin = ClipOrigin(rect);
        var left = (int)Math.Round(rect[0]) - origin.X;
        var top = (int)Math.Round(rect[1]) - origin.Y;
        var right = (int)Math.Round(rect[0] + rect[2]) - origin.X;
        var bottom = (int)Math.Round(rect[1] + rect[3]) - origin.Y;

        SKColor At(SKBitmap picture, int x, int y) =>
            x >= 0 && y >= 0 && x < picture.Width && y < picture.Height ? picture.GetPixel(x, y) : SKColors.Transparent;

        double Along(int from, int to, Func<int, int, (int X, int Y)> outward)
        {
            var hits = 0;
            var total = 0;

            for (var position = from + Corner; position < to - Corner; position++)
            {
                total++;
                var here = position;
                if (shows(k => At(after, outward(here, k).X, outward(here, k).Y), k => At(before, outward(here, k).X, outward(here, k).Y)))
                {
                    hits++;
                }
            }

            return total == 0 ? 1 : (double)hits / total;
        }

        return new Sides(
            Along(left, right, (x, k) => (x, top - 1 - k)),
            Along(left, right, (x, k) => (x, bottom + k)),
            Along(top, bottom, (y, k) => (left - 1 - k, y)),
            Along(top, bottom, (y, k) => (right + k, y)));
    }

    private static (int X, int Y) ClipOrigin(double[] rect) =>
        ((int)Math.Max(0, Math.Floor(rect[0]) - Margin), (int)Math.Max(0, Math.Floor(rect[1]) - Margin));

    private sealed record EdgeBox(int Section, string Side, int Position);

    private sealed record RingLook(bool FocusVisible, string OutlineColour, string OutlineStyle, string OutlineWidth, string OutlineOffset, string Halo, string BoxShadow);

    private sealed record Sides(double Top, double Bottom, double Left, double Right);
}

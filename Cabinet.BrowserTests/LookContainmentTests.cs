using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Owner;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using SkiaSharp;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that nothing a design draws ever leaves its section box: the one-pixel ring at the edge of
/// every section is the wall, so no shadow or board is cut off by the box, and every part of the furniture, including the
/// parts a stylesheet draws without an element of their own, lies inside the section's rectangle. A part that stuck out
/// would be cut off in a hard line, or push other content around while the page loads. The left, right and bottom rings are
/// checked along their whole length; the top ring is checked everywhere except under and right beside the topmost member,
/// whose own soft shadow falls there. The ring is compared with the wall just outside the box, because a wall can carry a soft
/// falloff of its own. The same holds for every design on every wood, with either kind of back, in the state where the lights
/// reach furthest (Deep, lit), and for the empty cabinet. A box face never changes when the lights come on, on any look.
/// </summary>
[Trait("Category", "Browser")]
public sealed class LookContainmentTests : CabinetPageTest, IDisposable
{
    private const int ChannelTolerance = 3;

    private const double RectangleTolerance = 0.51;

    private const int ViewportHeight = 1600;

    private const double SoftShadowReach = 8;

    private const int OutsideDistance = 3;

    private const int FaceInset = 3;

    private const string LitOnTheWall = "() => document.documentElement.hasAttribute('data-lit') && [...document.querySelectorAll('#cabinet .section-light')].every((layer) => Number(getComputedStyle(layer).opacity) === 1)";

    private const string StatusColourIsWallText = """
        () => {
          const probe = document.createElement('span');
          probe.style.color = 'var(--room-text)';
          document.body.append(probe);
          const wallText = getComputedStyle(probe).color;
          probe.remove();
          return [getComputedStyle(document.querySelector('.status')).color, wallText];
        }
        """;

    private static readonly int[] Widths = [1440, 390];

    private static readonly string[] BackKinds = ["wood", "painted"];

    private static readonly string[] Depths = ["flat", "shaded", "deep"];

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private static readonly JsonSerializerOptions ReadingOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly (int Width, string Path)[] Views =
    [
        (1440, "/?sample=65"),
        (1440, "/?sample=400"),
        (390, "/?sample=65"),
        (390, "/?sample=400"),
    ];

    private readonly TemporaryDirectory _storage = new();

    /// <summary>Every design with the lights off and with the lights on.</summary>
    public static TheoryData<string, string> DesignsAndLights { get; } = BuildDesignsAndLights();

    /// <summary>Every design on every wood.</summary>
    public static TheoryData<string, string> DesignsOnWoods { get; } = BuildDesignsOnWoods();

    /// <summary>Every design.</summary>
    public static TheoryData<string> Designs { get; } = BuildDesigns();

    [Theory]
    [MemberData(nameof(DesignsAndLights))]
    public async Task Every_section_ends_inside_its_own_box_with_the_wall_at_its_edge(string design, string lights)
    {
        OwnerDataSeed.Write(
            _storage.FullPath,
            OwnerData.Empty with { Revision = 1, Look = new CabinetLook(design, "brown", "wood", lights, "shaded") });
        await StartAsync(new Dictionary<string, string?>(PrototypeOn) { ["Storage:Directory"] = _storage.FullPath });

        foreach (var (width, path) in Views)
        {
            await Page.SetViewportSizeAsync(width, ViewportHeight);
            await GotoCabinetAsync(path);
            await Page.WaitForSelectorAsync("#cabinet .section", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });

            await CheckEverySectionAsync($"{design} with the lights {lights}, {width} px, {path}");

            if (path.EndsWith("65", StringComparison.Ordinal))
            {
                await SaveScreenshotAsync($"design-{design}-{lights}-{width}");
            }
        }

        ConsoleErrors.Should().BeEmpty();
    }

    /// <summary>
    /// The furthest the furniture's paint can reach is Deep with the lights lit: the floor glow, the pools and the inner walls are all
    /// drawn. In that state, on both kinds of back, every design on every wood still ends inside its box, and the page never scrolls
    /// sideways.
    /// </summary>
    /// <param name="design">The design to draw.</param>
    /// <param name="wood">The wood to draw it in.</param>
    [Theory]
    [MemberData(nameof(DesignsOnWoods))]
    public async Task Every_design_on_every_wood_stays_inside_its_box_in_Deep_with_the_lights_lit_on_either_back(string design, string wood)
    {
        await StartWithLookAsync(new CabinetLook(design, wood, "wood", "on", "deep"));
        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });

        foreach (var width in Widths)
        {
            await Page.SetViewportSizeAsync(width, ViewportHeight);
            await GotoCabinetAsync("/?sample=65");
            await Page.WaitForFunctionAsync(LitOnTheWall);

            foreach (var backs in BackKinds)
            {
                await SetAsync(("backs", backs));

                var where = $"{design} on {wood} with {backs} backs, Deep, lit, {width} px";
                await CheckEverySectionAsync(where);
                (await SidewaysOverflowAsync()).Should().BeLessThanOrEqualTo(0, where);
            }

            await SaveScreenshotAsync($"design-{design}-{wood}-deep-lit-{width}");
        }

        ConsoleErrors.Should().BeEmpty();
    }

    /// <summary>
    /// The light sits under the boxes: for every wood and every depth, the face of every spine and flat box is the same pixel with
    /// the lights lit and with them off, while the back around it does change.
    /// </summary>
    /// <param name="design">The design to draw.</param>
    [Theory]
    [MemberData(nameof(Designs))]
    public async Task A_box_face_is_the_same_pixel_with_the_lights_off_and_on_on_every_wood_and_depth(string design)
    {
        await StartWithLookAsync(new CabinetLook(design, "brown", "wood", "on", "shaded"));
        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForFunctionAsync(LitOnTheWall);

        foreach (var wood in CabinetLook.Woods)
        {
            foreach (var depth in Depths)
            {
                var where = $"{design} on {wood} at {depth}";

                await SetAsync(("wood", wood), ("depth", depth));
                var (faces, backChange, faceChange) = await LitAgainstDarkAsync();

                faces.Should().NotBeEmpty(where);
                backChange.Should().BeGreaterThan(0, $"{where}: the light does change the back");
                faceChange.Should().Be(0, $"{where}: no light is painted over a box face");
            }
        }

        ConsoleErrors.Should().BeEmpty();
    }

    /// <summary>
    /// A collection with no games still stands as furniture under every design: one minimum section of bare cubbies, all of it inside
    /// its box, on every wood, and the status line is in the wall's own text colour on every wall.
    /// </summary>
    /// <param name="design">The design to draw.</param>
    [Theory]
    [MemberData(nameof(Designs))]
    public async Task An_empty_collection_still_stands_as_one_bare_section_inside_its_box_on_every_wall(string design)
    {
        await StartWithLookAsync(new CabinetLook(design, "brown", "wood", "off", "shaded"));

        foreach (var width in Widths)
        {
            await Page.SetViewportSizeAsync(width, ViewportHeight);
            await Page.GotoAsync($"http://127.0.0.1:{PublicPort}/?sample=0");
            await Page.WaitForSelectorAsync("#cabinet .section", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });

            foreach (var wood in CabinetLook.Woods)
            {
                await SetAsync(("wood", wood));

                var where = $"{design} on {wood}, empty, {width} px";
                (await Page.Locator("#cabinet .section").CountAsync()).Should().Be(1, $"{where}: one minimum section");
                (await Page.Locator("#cabinet .cubby").CountAsync()).Should().BeGreaterThan(0, $"{where}: bare cubbies");
                (await Page.Locator("#cabinet .placement").CountAsync()).Should().Be(0, $"{where}: nothing in them");

                await CheckEverySectionAsync(where);
                (await SidewaysOverflowAsync()).Should().BeLessThanOrEqualTo(0, where);

                var colours = await Page.EvaluateAsync<string[]>(StatusColourIsWallText);
                colours[0].Should().Be(colours[1], $"{where}: the status line is in the wall's text colour");
            }
        }

        ConsoleErrors.Should().BeEmpty();
    }

    /// <summary>
    /// The edge measurement has teeth: a tint that runs out to the edge of the section's box, where the box cuts it off, is reported.
    /// </summary>
    [Fact]
    public async Task A_tint_that_runs_out_to_the_edge_of_the_box_is_reported()
    {
        await StartAsync(PrototypeOn);
        await Page.SetViewportSizeAsync(1440, ViewportHeight);
        await GotoCabinetAsync("/?sample=65");
        await AdoptAsync(".section-base { box-shadow: 0 0 40px 40px rgb(255 0 0 / 0.8); }");

        (await EdgeRingOffendersAsync(await ReadSectionAsync(0))).Should().NotBeEmpty();
    }

    /// <summary>
    /// The light-order measurement has teeth: a light laid over the boxes changes their faces, and the measurement says so.
    /// </summary>
    [Fact]
    public async Task A_light_laid_over_the_boxes_is_reported_as_changing_their_faces()
    {
        await StartWithLookAsync(new CabinetLook("classic", "brown", "wood", "on", "shaded"));
        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForFunctionAsync(LitOnTheWall);
        await AdoptAsync(".section > .section-light { z-index: 3 !important; }");

        var (_, _, faceChange) = await LitAgainstDarkAsync();

        faceChange.Should().BeGreaterThan(0);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private static TheoryData<string, string> BuildDesignsAndLights()
    {
        var data = new TheoryData<string, string>();

        foreach (var design in CabinetLook.Designs)
        {
            data.Add(design, "off");
            data.Add(design, "on");
        }

        return data;
    }

    private static TheoryData<string, string> BuildDesignsOnWoods()
    {
        var data = new TheoryData<string, string>();

        foreach (var design in CabinetLook.Designs)
        {
            foreach (var wood in CabinetLook.Woods)
            {
                data.Add(design, wood);
            }
        }

        return data;
    }

    private static TheoryData<string> BuildDesigns()
    {
        var data = new TheoryData<string>();

        foreach (var design in CabinetLook.Designs)
        {
            data.Add(design);
        }

        return data;
    }

    private static bool InsideTheBox(PartReading part, BoxReading box) =>
        part.Left >= box.Left - RectangleTolerance
        && part.Top >= box.Top - RectangleTolerance
        && part.Left + part.Width <= box.Left + box.Width + RectangleTolerance
        && part.Top + part.Height <= box.Top + box.Height + RectangleTolerance;

    private async Task AdoptAsync(string css) =>
        await Page.EvaluateAsync(
            "(css) => { const sheet = new CSSStyleSheet(); sheet.replaceSync(css); document.adoptedStyleSheets = [...document.adoptedStyleSheets, sheet]; }",
            css);

    /// <summary>
    /// Draws the page with the lights lit and then with them off, and reports where the boxes are, how many pixels of the page differ
    /// and how many of those lie on a box face.
    /// </summary>
    private async Task<(List<double[]> Faces, long BackChange, int FaceChange)> LitAgainstDarkAsync()
    {
        await Page.EvaluateAsync("() => { const root = document.documentElement; root.dataset.lights = 'on'; root.setAttribute('data-lit', ''); }");
        await FitViewportToPageAsync();
        var faces = await BoxFacesAsync();
        var lit = await FullPageAsync();

        await Page.EvaluateAsync("() => { const root = document.documentElement; root.dataset.lights = 'off'; root.removeAttribute('data-lit'); }");
        await FitViewportToPageAsync();
        var dark = await FullPageAsync();

        return (faces, PixelComparison.Compare(lit, dark).DifferingPixels, FacePixelDifferences(lit, dark, faces));
    }

    private async Task StartWithLookAsync(CabinetLook look)
    {
        OwnerDataSeed.Write(_storage.FullPath, OwnerData.Empty with { Revision = 1, Look = look });
        await StartAsync(new Dictionary<string, string?>(PrototypeOn) { ["Storage:Directory"] = _storage.FullPath });
    }

    private async Task SetAsync(params (string Name, string Value)[] choices)
    {
        foreach (var (name, value) in choices)
        {
            await Page.EvaluateAsync("([name, value]) => { document.documentElement.dataset[name] = value; }", new[] { name, value });
        }
    }

    private Task<double> SidewaysOverflowAsync() =>
        Page.EvaluateAsync<double>("() => document.documentElement.scrollWidth - window.innerWidth");

    /// <summary>
    /// Reads every section of the page: each part of its furniture must lie inside its box, and the one-pixel ring at its edge must be
    /// what the wall is just outside it.
    /// </summary>
    /// <param name="where">What is being drawn, for the failure message.</param>
    private async Task CheckEverySectionAsync(string where)
    {
        var count = await Page.Locator("#cabinet .section").CountAsync();
        count.Should().BeGreaterThan(0, where);

        for (var index = 0; index < count; index++)
        {
            var section = $"{where}, section {index + 1} of {count}";            var reading = await ReadSectionAsync(index);

            foreach (var part in reading.Parts)
            {
                InsideTheBox(part, reading.Box).Should().BeTrue($"{section}: {part.Name} {part.Left:F1},{part.Top:F1},{part.Width:F1},{part.Height:F1} is outside {reading.Box.Left:F1},{reading.Box.Top:F1},{reading.Box.Width:F1},{reading.Box.Height:F1}");
            }

            var offenders = await EdgeRingOffendersAsync(reading);
            if (offenders.Count > 0)
            {
                offenders = await EdgeRingOffendersAsync(await ReadSectionAsync(index));
            }

            offenders.Should().BeEmpty($"{section}: the edge ring must be the wall");
        }
    }

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

    private async Task<byte[]> FullPageAsync() =>
        await Page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true, Animations = ScreenshotAnimations.Disabled });

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
        var differing = 0;

        foreach (var face in faces)
        {
            var startX = (int)Math.Ceiling(face[0]) + FaceInset;
            var endX = (int)Math.Floor(face[0] + face[2]) - FaceInset;
            var startY = (int)Math.Ceiling(face[1]) + FaceInset;
            var endY = (int)Math.Floor(face[1] + face[3]) - FaceInset;

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

    private async Task<SectionRead> ReadSectionAsync(int index)
    {
        var json = await Page.EvaluateAsync<string>(ReadScript, index);

        return JsonSerializer.Deserialize<SectionRead>(json, ReadingOptions)!;
    }

    private async Task<List<string>> EdgeRingOffendersAsync(SectionRead reading)
    {
        var box = reading.Box;
        var clipLeft = (int)Math.Floor(box.Left) - OutsideDistance - 1;
        var clipTop = (int)Math.Floor(box.Top) - OutsideDistance - 1;
        var clipRight = (int)Math.Ceiling(box.Left + box.Width) + OutsideDistance + 1;
        var clipBottom = (int)Math.Ceiling(box.Top + box.Height) + OutsideDistance + 1;

        var png = await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Animations = ScreenshotAnimations.Disabled,
            Clip = new Clip { X = clipLeft, Y = clipTop, Width = clipRight - clipLeft, Height = clipBottom - clipTop },
        });

        using var bitmap = SKBitmap.Decode(png);
        var left = (int)Math.Ceiling(box.Left);
        var right = (int)Math.Floor(box.Left + box.Width) - 1;
        var top = (int)Math.Ceiling(box.Top);
        var bottom = (int)Math.Floor(box.Top + box.Height) - 1;
        var spanLeft = (int)Math.Floor(reading.SpanLeft - SoftShadowReach * reading.Unit);
        var spanRight = (int)Math.Ceiling(reading.SpanRight + SoftShadowReach * reading.Unit);
        var offenders = new List<string>();

        for (var y = top; y <= bottom; y++)
        {
            Check(left, y, left - OutsideDistance, y);
            Check(right, y, right + OutsideDistance, y);
        }

        for (var x = left; x <= right; x++)
        {
            Check(x, bottom, x, bottom + OutsideDistance);

            if (x < spanLeft || x >= spanRight)
            {
                Check(x, top, x, top - OutsideDistance);
            }
        }

        return [.. offenders.Take(5)];

        void Check(int x, int y, int wallX, int wallY)
        {
            var found = bitmap.GetPixel(x - clipLeft, y - clipTop);
            var wall = bitmap.GetPixel(wallX - clipLeft, wallY - clipTop);

            if (Math.Abs(found.Red - wall.Red) > ChannelTolerance
                || Math.Abs(found.Green - wall.Green) > ChannelTolerance
                || Math.Abs(found.Blue - wall.Blue) > ChannelTolerance)
            {
                offenders.Add($"({x},{y}) is {found.Red},{found.Green},{found.Blue}, the wall outside is {wall.Red},{wall.Green},{wall.Blue}");
            }
        }
    }

    private const string ReadScript = """
        async (index) => {
          const frames = () => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
          const section = document.querySelectorAll('#cabinet .section')[index];
          section.scrollIntoView({ block: 'center' });
          await frames();
          const box = section.getBoundingClientRect();
          const parts = [];
          const keep = (name, rect) => {
            if (rect.width > 0 || rect.height > 0) {
              parts.push({ name, left: rect.left, top: rect.top, width: rect.width, height: rect.height });
            }
          };
          const members = ['.section-body', '.section-top', '.section-trim', '.section-base', '.section-light', '.section-floor'];
          for (const selector of members) {
            const element = section.querySelector(selector);
            keep(selector, element.getBoundingClientRect());
            for (const pseudo of ['::before', '::after']) {
              const style = getComputedStyle(element, pseudo);
              if (getComputedStyle(element).display !== 'none' && style.content !== 'none' && style.content !== 'normal') {
                const owner = element.getBoundingClientRect();
                keep(selector + pseudo, {
                  left: owner.left + parseFloat(style.left),
                  top: owner.top + parseFloat(style.top),
                  width: parseFloat(style.width),
                  height: parseFloat(style.height),
                });
              }
            }
          }
          for (const element of section.querySelectorAll('.light-pool, .cubby, .placement')) {
            keep(element.className.split(' ')[0], element.getBoundingClientRect());
          }
          const span = ['.section-top', '.section-body', '.section-trim']
            .map((selector) => section.querySelector(selector).getBoundingClientRect())
            .filter((rect) => rect.width > 0);
          return JSON.stringify({
            box: { left: box.left, top: box.top, width: box.width, height: box.height },
            parts,
            spanLeft: Math.min(...span.map((rect) => rect.left)),
            spanRight: Math.max(...span.map((rect) => rect.right)),
            unit: box.width / (parseFloat(getComputedStyle(section).getPropertyValue('--section-w')) + 2 * 32),
          });
        }
        """;

    private sealed record BoxReading(double Left, double Top, double Width, double Height);

    private sealed record PartReading(string Name, double Left, double Top, double Width, double Height);

    private sealed record SectionRead(BoxReading Box, List<PartReading> Parts, double SpanLeft, double SpanRight, double Unit);
}

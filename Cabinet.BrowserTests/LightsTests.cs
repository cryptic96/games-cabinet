using System.Text.Json;
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
              };
            };
            window.__lamp.drawn = read();
            requestAnimationFrame(() => { window.__lamp.nextFrame = read(); });
          }, { once: true });
        })();
        """;

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private readonly TemporaryDirectory _storage = new();

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

    private sealed record LayerReading(string Display, double Opacity, bool Lit, bool Settled);
}

using System.Globalization;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Owner;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using SkiaSharp;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves the depth choice and the painted-back choice with a real browser. Every design, at both widths, is drawn with each
/// depth and each kind of back and nothing moves and nothing paints outside its section box; Flat draws no shading but keeps the
/// back's construction; a painted back is one flat colour that still carries the shade of the shelf above; and Deep draws the
/// inner walls of a cubby.
/// </summary>
[Trait("Category", "Browser")]
public sealed class LookDepthTests : CabinetPageTest
{
    private const string BoxesHidden = ".placement { visibility: hidden !important; }";

    private const int EdgeTolerance = 3;

    private const int WallColourDifference = 12;

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private static readonly string[] Depths = ["flat", "shaded", "deep"];

    private static readonly string[] BackKinds = ["wood", "painted"];

    private readonly TemporaryDirectory _storage = new();

    /// <summary>Every design at the two widths the cabinet is checked at.</summary>
    public static TheoryData<string, int> DesignsAtWidths { get; } = BuildDesignsAtWidths();

    [Fact]
    public async Task A_stored_deep_depth_draws_the_inner_walls_with_the_deep_shade_and_moves_nothing()
    {
        var look = new CabinetLook("classic", "brown", "wood", "off", "deep");
        OwnerDataSeed.Write(_storage.FullPath, OwnerData.Empty with { Revision = 1, Look = look });
        await StartAsync(new Dictionary<string, string?>(PrototypeOn) { ["Storage:Directory"] = _storage.FullPath });
        await OpenBareAsync(1440);

        (await ReadAsync<string>("() => document.documentElement.dataset.depth")).Should().Be("deep");
        (await CubbyPropertyAsync("--shade-alpha")).Should().Be("0.12");

        var deepRects = await RectsAsync();
        var deepShot = await CubbyShotAsync();
        var deepEdges = await EdgeDifferencesAsync();
        await SaveScreenshotAsync("depth-deep-bare");

        await SetLookAsync(("depth", "shaded"));
        var shadedRects = await RectsAsync();
        var shadedShot = await CubbyShotAsync();

        await SaveScreenshotAsync("depth-shaded-bare");
        (await CubbyPropertyAsync("--shade-alpha")).Should().Be("0.20");
        deepRects.Should().Equal(shadedRects);
        deepEdges.Should().OnlyContain(difference => difference <= EdgeTolerance);
        PixelComparison.Compare(deepShot, shadedShot).DifferingPixels.Should().BeGreaterThan(0);
        InnerLeftPixelDistance(deepShot, shadedShot).Should().BeGreaterThan(WallColourDifference);
    }

    [Theory]
    [MemberData(nameof(DesignsAtWidths))]
    public async Task Every_depth_and_back_leaves_the_boxes_where_they_are_and_paints_inside_them(string design, int width)
    {
        await StartAsync(PrototypeOn);
        await OpenBareAsync(width);
        await SetLookAsync(("design", design), ("depth", "shaded"), ("backs", "wood"));
        var reference = await RectsAsync();

        foreach (var depth in Depths)
        {
            foreach (var backs in BackKinds)
            {
                await SetLookAsync(("depth", depth), ("backs", backs));

                var label = $"{design} at {width} with {depth} and {backs} backs";
                (await RectsAsync()).Should().Equal(reference, label);
                (await EdgeDifferencesAsync()).Should().OnlyContain(difference => difference <= EdgeTolerance, label);
            }
        }
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private static TheoryData<string, int> BuildDesignsAtWidths()
    {
        var data = new TheoryData<string, int>();

        foreach (var design in CabinetLook.Designs)
        {
            data.Add(design, 1440);
            data.Add(design, 390);
        }

        return data;
    }

    private static int[] Columns(double[] rect, double from = 0.1, double to = 0.9)
    {
        var start = (int)Math.Round(rect[0] + (rect[2] * from));
        var end = (int)Math.Round(rect[0] + (rect[2] * to));

        return [.. Enumerable.Range(start, end - start)];
    }

    private static SKColor ParseHex(string value)
    {
        var hex = value.Trim().TrimStart('#');

        return new SKColor(
            byte.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static int Distance(SKColor first, SKColor second) =>
        Math.Max(Math.Abs(first.Red - second.Red), Math.Max(Math.Abs(first.Green - second.Green), Math.Abs(first.Blue - second.Blue)));

    private static int Sum(SKColor colour) => colour.Red + colour.Green + colour.Blue;

    private static int InnerLeftPixelDistance(byte[] first, byte[] second)
    {
        using var left = SKBitmap.Decode(first);
        using var right = SKBitmap.Decode(second);
        var y = left.Height / 3;

        return Distance(left.GetPixel(2, y), right.GetPixel(2, y));
    }

    private async Task OpenBareAsync(int width)
    {
        await Page.SetViewportSizeAsync(width, 900);
        await GotoCabinetAsync("/?sample=65");
        await Page.EvaluateAsync(
            "(css) => { const sheet = new CSSStyleSheet(); sheet.replaceSync(css); document.adoptedStyleSheets = [...document.adoptedStyleSheets, sheet]; }",
            BoxesHidden);
        await GrowAsync(width);
    }

    private async Task GrowAsync(int width)
    {
        var settled = 0;
        var last = -1;

        while (settled < 3)
        {
            var height = await ReadAsync<int>("() => document.documentElement.scrollHeight");
            await Page.SetViewportSizeAsync(width, height);
            await Page.EvaluateAsync("() => document.fonts.ready");
            await NextFramesAsync();
            settled = height == last ? settled + 1 : 0;
            last = height;
        }
    }

    private async Task SetLookAsync(params (string Name, string Value)[] choices)
    {
        foreach (var (name, value) in choices)
        {
            await Page.EvaluateAsync("([name, value]) => { document.documentElement.dataset[name] = value; }", new[] { name, value });
        }

        await NextFramesAsync();
    }

    private Task NextFramesAsync() =>
        Page.EvaluateAsync("() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)))");

    private async Task<T> ReadAsync<T>(string script) => await Page.EvaluateAsync<T>(script);

    private async Task<string> CubbyPropertyAsync(string name) =>
        (await Page.EvaluateAsync<string>("(name) => getComputedStyle(document.querySelector('#cabinet .cubby')).getPropertyValue(name)", name)).Trim();

    private async Task<string> RootPropertyAsync(string name) =>
        await Page.EvaluateAsync<string>("(name) => getComputedStyle(document.documentElement).getPropertyValue(name)", name);

    private async Task<string[]> RectsAsync()
    {
        var rects = await Page.EvaluateAsync<double[][]>(
            "() => [...document.querySelectorAll('#cabinet .section, #cabinet .cubby')].map((element) => { const r = element.getBoundingClientRect(); return [r.x + scrollX, r.y + scrollY, r.width, r.height]; })");

        return [.. rects.Select(rect => string.Join(',', rect.Select(value => Math.Round(value, 2).ToString(CultureInfo.InvariantCulture))))];
    }

    private Task<double[][]> CubbyRectsAsync() =>
        Page.EvaluateAsync<double[][]>(
            "() => [...document.querySelectorAll('#cabinet .cubby')].map((element) => { const r = element.getBoundingClientRect(); return [r.x + scrollX, r.y + scrollY, r.width, r.height]; })");

    private Task<double[][]> SectionRectsAsync() =>
        Page.EvaluateAsync<double[][]>(
            "() => [...document.querySelectorAll('#cabinet .section')].map((element) => { const r = element.getBoundingClientRect(); return [r.x + scrollX, r.y + scrollY, r.width, r.height]; })");

    private async Task<byte[]> CubbyShotAsync()
    {
        var cubby = (await CubbyRectsAsync())[0];

        return await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            FullPage = true,
            Animations = ScreenshotAnimations.Disabled,
            Clip = new Clip { X = (float)cubby[0], Y = (float)cubby[1], Width = (float)cubby[2], Height = (float)cubby[3] + 12 },
        });
    }

    private async Task<Frame> FrameAsync() => new(await Page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true, Animations = ScreenshotAnimations.Disabled }));

    /// <summary>
    /// Reads, for every section, how far the pixels on its left, right and bottom edge are from the pixels three pixels outside
    /// them. Anything the look paints beyond its box shows up as a difference. The top edge is left out because the furniture's
    /// top board starts exactly there.
    /// </summary>
    private async Task<List<int>> EdgeDifferencesAsync()
    {
        using var frame = await FrameAsync();
        var differences = new List<int>();

        foreach (var section in await SectionRectsAsync())
        {
            var left = (int)Math.Round(section[0]);
            var top = (int)Math.Round(section[1]);
            var right = left + (int)Math.Round(section[2]) - 1;
            var bottom = top + (int)Math.Round(section[3]) - 1;

            for (var x = left + 1; x < right; x += 3)
            {
                differences.Add(Distance(frame.Pixel(x, bottom), frame.Pixel(x, bottom + 3)));
            }

            for (var y = top + 1; y < bottom; y += 3)
            {
                differences.Add(Distance(frame.Pixel(left, y), frame.Pixel(left - 3, y)));
                differences.Add(Distance(frame.Pixel(right, y), frame.Pixel(right + 3, y)));
            }
        }

        return differences;
    }

    private sealed class Frame(byte[] png) : IDisposable
    {
        private readonly SKBitmap _bitmap = SKBitmap.Decode(png) ?? throw new InvalidOperationException("The screenshot is not a readable picture.");

        public SKColor Pixel(int x, int y) => _bitmap.GetPixel(x, y);

        public void Dispose() => _bitmap.Dispose();
    }
}

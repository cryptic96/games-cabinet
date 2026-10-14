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
/// whose own soft shadow falls there.
/// </summary>
[Trait("Category", "Browser")]
public sealed class LookContainmentTests : CabinetPageTest, IDisposable
{
    private const int ChannelTolerance = 3;

    private const double RectangleTolerance = 0.51;

    private const int ViewportHeight = 1600;

    private const double SoftShadowReach = 8;

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

    [Theory]
    [InlineData("classic")]
    [InlineData("modern")]
    [InlineData("display")]
    [InlineData("legs")]
    [InlineData("library")]
    [InlineData("plywood")]
    [InlineData("plywood-legs")]
    public async Task Every_section_ends_inside_its_own_box_with_the_wall_at_its_edge(string design)
    {
        OwnerDataSeed.Write(
            _storage.FullPath,
            OwnerData.Empty with { Revision = 1, Look = new CabinetLook(design, "brown", "wood", "off", "shaded") });
        await StartAsync(new Dictionary<string, string?>(PrototypeOn) { ["Storage:Directory"] = _storage.FullPath });

        foreach (var (width, path) in Views)
        {
            await Page.SetViewportSizeAsync(width, ViewportHeight);
            await GotoCabinetAsync(path);
            await Page.WaitForSelectorAsync("#cabinet .section", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });

            var count = await Page.Locator("#cabinet .section").CountAsync();
            count.Should().BeGreaterThan(0);

            for (var index = 0; index < count; index++)
            {
                var where = $"{design}, section {index + 1} of {count}, {width} px, {path}";
                var reading = await ReadSectionAsync(index);

                foreach (var part in reading.Parts)
                {
                    InsideTheBox(part, reading.Box).Should().BeTrue($"{where}: {part.Name} {part.Left:F1},{part.Top:F1},{part.Width:F1},{part.Height:F1} is outside {reading.Box.Left:F1},{reading.Box.Top:F1},{reading.Box.Width:F1},{reading.Box.Height:F1}");
                }

                var offenders = await EdgeRingOffendersAsync(reading);
                if (offenders.Count > 0)
                {
                    offenders = await EdgeRingOffendersAsync(await ReadSectionAsync(index));
                }

                offenders.Should().BeEmpty($"{where}: the edge ring must be the wall");
            }

            if (path.EndsWith("65", StringComparison.Ordinal))
            {
                await SaveScreenshotAsync($"design-{design}-{width}");
            }
        }

        ConsoleErrors.Should().BeEmpty();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private static bool InsideTheBox(PartReading part, BoxReading box) =>
        part.Left >= box.Left - RectangleTolerance
        && part.Top >= box.Top - RectangleTolerance
        && part.Left + part.Width <= box.Left + box.Width + RectangleTolerance
        && part.Top + part.Height <= box.Top + box.Height + RectangleTolerance;

    private async Task<SectionRead> ReadSectionAsync(int index)
    {
        var json = await Page.EvaluateAsync<string>(ReadScript, index);

        return JsonSerializer.Deserialize<SectionRead>(json, ReadingOptions)!;
    }

    private async Task<List<string>> EdgeRingOffendersAsync(SectionRead reading)
    {
        var box = reading.Box;
        var clipLeft = (int)Math.Floor(box.Left) - 2;
        var clipTop = (int)Math.Floor(box.Top) - 2;
        var clipRight = (int)Math.Ceiling(box.Left + box.Width) + 2;
        var clipBottom = (int)Math.Ceiling(box.Top + box.Height) + 2;

        var png = await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Animations = ScreenshotAnimations.Disabled,
            Clip = new Clip { X = clipLeft, Y = clipTop, Width = clipRight - clipLeft, Height = clipBottom - clipTop },
        });

        using var bitmap = SKBitmap.Decode(png);
        var wall = new SKColor((byte)reading.Wall[0], (byte)reading.Wall[1], (byte)reading.Wall[2]);
        var left = (int)Math.Ceiling(box.Left);
        var right = (int)Math.Floor(box.Left + box.Width) - 1;
        var top = (int)Math.Ceiling(box.Top);
        var bottom = (int)Math.Floor(box.Top + box.Height) - 1;
        var spanLeft = (int)Math.Floor(reading.SpanLeft - SoftShadowReach * reading.Unit);
        var spanRight = (int)Math.Ceiling(reading.SpanRight + SoftShadowReach * reading.Unit);
        var offenders = new List<string>();

        for (var y = top; y <= bottom; y++)
        {
            Check(left, y);
            Check(right, y);
        }

        for (var x = left; x <= right; x++)
        {
            Check(x, bottom);

            if (x < spanLeft || x >= spanRight)
            {
                Check(x, top);
            }
        }

        return [.. offenders.Take(5)];

        void Check(int x, int y)
        {
            var found = bitmap.GetPixel(x - clipLeft, y - clipTop);

            if (Math.Abs(found.Red - wall.Red) > ChannelTolerance
                || Math.Abs(found.Green - wall.Green) > ChannelTolerance
                || Math.Abs(found.Blue - wall.Blue) > ChannelTolerance)
            {
                offenders.Add($"({x},{y}) is {found.Red},{found.Green},{found.Blue}, the wall is {wall.Red},{wall.Green},{wall.Blue}");
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
              if (style.content !== 'none' && style.content !== 'normal') {
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
          const wall = getComputedStyle(document.body).backgroundColor.match(/[\d.]+/g).map(Number);
          return JSON.stringify({
            box: { left: box.left, top: box.top, width: box.width, height: box.height },
            parts,
            wall,
            spanLeft: Math.min(...span.map((rect) => rect.left)),
            spanRight: Math.max(...span.map((rect) => rect.right)),
            unit: box.width / (parseFloat(getComputedStyle(section).getPropertyValue('--section-w')) + 2 * 32),
          });
        }
        """;

    private sealed record BoxReading(double Left, double Top, double Width, double Height);

    private sealed record PartReading(string Name, double Left, double Top, double Width, double Height);

    private sealed record SectionRead(BoxReading Box, List<PartReading> Parts, double[] Wall, double SpanLeft, double SpanRight, double Unit);
}

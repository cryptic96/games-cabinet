using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves that the cabinet's standard look has not changed since the last release. The same page is drawn twice in one browser:
/// once with the stylesheets the host serves and once with frozen copies of the released cabinet and site stylesheets. Both
/// renders share the browser, the fonts and the layout the server answers with, so any differing pixel comes from the
/// stylesheets alone.
/// </summary>
[Trait("Category", "Browser")]
public sealed class DefaultLookIdentityTests : CabinetPageTest
{
    private const string FrozenFolder = "LegacyStyles";

    private const string StepChange = ":root { --wood-mid: #6b4227; }";

    private const string LinkedLayerSheets = """
        () => [...document.querySelectorAll('link[rel="stylesheet"]')]
          .filter((link) => /\/css\/cabinet-[^/]*$/.test(new URL(link.href).pathname)).length
        """;

    private static readonly Regex StylesheetRequest = new(@"/css/[^?#]*\.css(\?|#|$)");

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    [Theory]
    [InlineData(1440, 900, "/?sample=65")]
    [InlineData(1440, 900, "/?sample=400")]
    [InlineData(390, 800, "/?sample=65")]
    [InlineData(390, 800, "/?sample=400")]
    public async Task The_default_look_matches_the_released_stylesheets_pixel_for_pixel(int width, int height, string path)
    {
        await StartAsync(PrototypeOn);

        var current = await RenderAsync(Page, width, height, path);
        var legacy = await RenderWithFrozenStylesAsync(width, height, path, cabinetAddition: null);
        var difference = PixelComparison.Compare(current, legacy.Png);
        var name = $"default-look-{width}-{path.Replace("/?sample=", "sample", StringComparison.Ordinal)}";

        if (difference.DifferingPixels > 0)
        {
            await KeepAsync(name + "-current", current);
            await KeepAsync(name + "-released", legacy.Png);
            PixelComparison.WriteDiffImage(current, legacy.Png, name + "-diff");
        }

        difference.SecondSize.Should().Be(difference.FirstSize);
        difference.DifferingPixels.Should().Be(0);
        legacy.FrozenSheetsServed.Should().Be(2);
        legacy.LayerRequestsAnsweredEmpty.Should().Be(legacy.LayerSheetsLinked);
    }

    [Fact]
    public async Task A_one_step_colour_change_is_reported_as_differing_pixels()
    {
        await StartAsync(PrototypeOn);

        var current = await RenderAsync(Page, 1440, 900, "/?sample=65");
        var changed = await RenderWithFrozenStylesAsync(1440, 900, "/?sample=65", StepChange);
        var difference = PixelComparison.Compare(current, changed.Png);

        PixelComparison.WriteDiffImage(current, changed.Png, "default-look-teeth-diff");

        difference.DifferingPixels.Should().BeGreaterThan(0);
    }

    private async Task<FrozenRender> RenderWithFrozenStylesAsync(int width, int height, string path, string? cabinetAddition)
    {
        var cabinet = await File.ReadAllTextAsync(FrozenPath("cabinet.css"), TestContext.Current.CancellationToken);
        var site = await File.ReadAllTextAsync(FrozenPath("site.css"), TestContext.Current.CancellationToken);
        var emptied = 0;
        var frozen = 0;

        var page = await Page.Context.NewPageAsync();
        await page.RouteAsync(StylesheetRequest, route => AnswerAsync(route));

        try
        {
            var png = await RenderAsync(page, width, height, path);
            var linked = await page.EvaluateAsync<int>(LinkedLayerSheets);
            return new FrozenRender(png, Volatile.Read(ref frozen), Volatile.Read(ref emptied), linked);
        }
        finally
        {
            await page.CloseAsync();
        }

        Task AnswerAsync(IRoute route)
        {
            var stem = StylesheetStem(route.Request.Url);

            if (stem == "cabinet")
            {
                Interlocked.Increment(ref frozen);
                return FulfilAsync(route, cabinetAddition is null ? cabinet : cabinet + Environment.NewLine + cabinetAddition);
            }

            if (stem == "site")
            {
                Interlocked.Increment(ref frozen);
                return FulfilAsync(route, site);
            }

            if (stem.StartsWith("cabinet-", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref emptied);
                return FulfilAsync(route, string.Empty);
            }

            return route.ContinueAsync();
        }
    }

    private async Task<byte[]> RenderAsync(IPage page, int width, int height, string path)
    {
        await page.SetViewportSizeAsync(width, height);
        await page.GotoAsync($"http://127.0.0.1:{PublicPort}{path}");
        await page.WaitForSelectorAsync("#cabinet .section", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });

        var settled = 0;
        var lastHeight = -1;
        while (settled < 3)
        {
            var scrollHeight = await page.EvaluateAsync<int>("() => document.documentElement.scrollHeight");
            await page.SetViewportSizeAsync(width, scrollHeight);
            await page.WaitForFunctionAsync("() => [...document.images].every((image) => image.complete)");
            await page.EvaluateAsync("() => document.fonts.ready");
            await page.EvaluateAsync("() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
            settled = scrollHeight == lastHeight ? settled + 1 : 0;
            lastHeight = scrollHeight;
        }

        return await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true, Animations = ScreenshotAnimations.Disabled });
    }

    private static Task FulfilAsync(IRoute route, string body) =>
        route.FulfillAsync(new RouteFulfillOptions { Status = 200, ContentType = "text/css", Body = body });

    private static string StylesheetStem(string url)
    {
        var fileName = Path.GetFileName(new Uri(url).AbsolutePath);
        var firstDot = fileName.IndexOf('.', StringComparison.Ordinal);

        return firstDot < 0 ? fileName : fileName[..firstDot];
    }

    private static string FrozenPath(string file) => Path.Combine(AppContext.BaseDirectory, FrozenFolder, file);

    private static async Task KeepAsync(string name, byte[] png)
    {
        var directory = Environment.GetEnvironmentVariable("CABINET_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, name + ".png"), png, TestContext.Current.CancellationToken);
    }

    private sealed record FrozenRender(byte[] Png, int FrozenSheetsServed, int LayerRequestsAnsweredEmpty, int LayerSheetsLinked);
}

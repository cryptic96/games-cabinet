using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Owner;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;
using SkiaSharp;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser, with forced colours on, that nothing the visitor needs is lost to the system palette: the credit to
/// BoardGameGeek stays legible on every wall, the cabinet keeps the outline of its furniture and of each box, a box's focus ring still
/// wins over that outline, and the owner's look panel shows what is chosen and draws its pictures instead of leaving empty frames.
/// </summary>
[Trait("Category", "Browser")]
public sealed class LookForcedColoursTests : CabinetPageTest, IDisposable
{
    private const string OwnerSettingsPath = "/owner/settings";

    private const double LegibleShare = 0.02;

    private const double PlateShare = 0.3;

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private readonly TemporaryDirectory _storage = new();

    /// <summary>
    /// On a dark wall the white wordmark is drawn on a dark plate and on a light wall the dark wordmark on a light plate, so the
    /// letters differ from what is behind them on every wall.
    /// </summary>
    /// <param name="wood">The wood whose wall the page is drawn on.</param>
    /// <param name="lightWall">Whether that wall is light, so the dark wordmark is served.</param>
    [Theory]
    [InlineData("brown", false)]
    [InlineData("oak", false)]
    [InlineData("walnut", true)]
    [InlineData("birch", true)]
    public async Task The_credit_to_BoardGameGeek_stays_legible_on_its_own_plate_on_every_wall(string wood, bool lightWall)
    {
        await OpenAsync(new CabinetLook("classic", wood, "wood", "off", "shaded"), "/?sample=65", 1440);
        var logo = Page.Locator("footer .bgg-credit img");
        await Expect(logo).ToBeVisibleAsync();

        var style = await logo.EvaluateAsync<string[]>("element => { const style = getComputedStyle(element); return [style.forcedColorAdjust, style.backgroundColor]; }");
        style[0].Should().Be("none", wood);
        style[1].Should().Be(lightWall ? "rgb(251, 248, 240)" : "rgb(59, 36, 22)", wood);

        (await logo.GetAttributeAsync("src"))!.Contains("-dark", StringComparison.Ordinal).Should().Be(lightWall, wood);

        using var picture = SKBitmap.Decode(await logo.ScreenshotAsync()) ?? throw new InvalidOperationException("The logo picture is not readable.");
        var plate = ShareOfPixels(picture, lightWall ? Light : Dark);
        var letters = ShareOfPixels(picture, lightWall ? Dark : Light);

        plate.Should().BeGreaterThan(PlateShare, $"{wood}: most of the credit is its plate");
        letters.Should().BeGreaterThan(LegibleShare, $"{wood}: the wordmark differs from its plate");
    }

    /// <summary>
    /// The shelves, the side boards and every box keep an outline in the system text colour, and a box that has the keyboard focus
    /// wears its ring and not the thin outline.
    /// </summary>
    [Fact]
    public async Task The_cabinet_keeps_the_outline_of_its_furniture_and_its_boxes_and_a_focused_box_keeps_its_ring()
    {
        await OpenAsync(CabinetLook.Default, "/?sample=65", 1440);

        foreach (var part in new[] { ".section-top", ".section-trim", ".section-base" })
        {
            var outline = await Page.Locator($"#cabinet {part}").First.EvaluateAsync<string[]>("element => { const style = getComputedStyle(element); return [style.outlineStyle, style.outlineWidth]; }");
            outline.Should().Equal(["solid", "2px"], part);
        }

        var edge = await Page.Locator("#cabinet .section-trim").First.EvaluateAsync<double[]>("element => { const rect = element.getBoundingClientRect(); return [rect.left, rect.top + (rect.height / 2)]; }");
        using var strip = SKBitmap.Decode(await Page.ScreenshotAsync(new PageScreenshotOptions { Clip = new Clip { X = (float)edge[0] - 4, Y = (float)edge[1], Width = 8, Height = 1 } }))
            ?? throw new InvalidOperationException("The side board strip is not readable.");
        Enumerable.Range(0, strip.Width).Select(x => Luminance(strip.GetPixel(x, 0))).Min().Should().BeLessThan(70, "the side board is drawn as a dark line");

        var box = Page.Locator("#cabinet .placement").First;
        var outlined = await box.EvaluateAsync<string[]>("element => { const style = getComputedStyle(element); return [style.outlineStyle, style.outlineWidth]; }");
        outlined.Should().Equal("solid", "1px");

        var focused = Page.Locator("#cabinet .placement:focus-visible");

        for (var presses = 0; presses < 20 && await focused.CountAsync() == 0; presses++)
        {
            await Page.Keyboard.PressAsync("Tab");
        }

        await Expect(focused).ToHaveCountAsync(1);
        var ring = await focused.EvaluateAsync<string[]>("element => { const style = getComputedStyle(element); return [style.outlineStyle, style.outlineWidth]; }");
        ring.Should().Equal("solid", "2px");
        ConsoleErrors.Should().BeEmpty();
    }

    /// <summary>
    /// A chosen tile and a chosen pill carry an outline in the system text colour, an unchosen one carries none, a chosen item that has
    /// focus shows its radio's own ring as well, and the miniature cabinets and the preview draw as pictures.
    /// </summary>
    [Fact]
    public async Task The_look_panel_shows_what_is_chosen_with_an_outline_and_draws_its_pictures()
    {
        await OpenAsync(new CabinetLook("legs", "oak", "wood", "evenings", "deep"), OwnerSettingsPath, 1440, owner: true);

        var json = await Page.EvaluateAsync<string>(
            """
            () => {
              const outline = (element) => { const style = getComputedStyle(element); return style.outlineStyle + ' ' + style.outlineWidth; };
              const chosenTile = document.querySelector('.look-tile:has(input[name=design]:checked)');
              const otherTile = document.querySelector('.look-tile:has(input[name=design]:not(:checked))');
              const chosenPill = document.querySelector('.look-choice:has(input[name=lights]:checked)');
              const otherPill = document.querySelector('.look-choice:has(input[name=lights]:not(:checked))');
              const chosenDepth = document.querySelector('.look-choice:has(input[name=depth]:checked)');
              const swatch = getComputedStyle(document.querySelector('.look-swatch'));
              const preview = getComputedStyle(document.querySelector('.look-preview'));
              return JSON.stringify({
                chosenTile: outline(chosenTile),
                otherTile: outline(otherTile),
                chosenPill: outline(chosenPill),
                otherPill: outline(otherPill),
                chosenDepth: outline(chosenDepth),
                swatchAdjust: swatch.forcedColorAdjust,
                swatchBackground: swatch.backgroundColor,
                previewAdjust: preview.forcedColorAdjust,
                previewBackground: preview.backgroundColor,
              });
            }
            """);
        var read = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;

        read["chosenTile"].Should().Be("solid 3px");
        read["otherTile"].Should().StartWith("none");
        read["chosenPill"].Should().Be("solid 3px");
        read["otherPill"].Should().StartWith("none");
        read["chosenDepth"].Should().Be("solid 3px");
        read["swatchAdjust"].Should().Be("none");
        read["previewAdjust"].Should().Be("none");
        read["swatchBackground"].Should().NotBe("rgba(0, 0, 0, 0)").And.NotBe("rgb(255, 255, 255)");
        read["previewBackground"].Should().NotBe("rgba(0, 0, 0, 0)").And.NotBe("rgb(255, 255, 255)");

        await Page.Locator("input[name=lights][value=evenings]").FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowLeft");
        var focusRing = await Page.Locator("input[name=lights][value=evenings]").EvaluateAsync<string>("element => { const style = getComputedStyle(element); return style.outlineStyle + ' ' + style.outlineWidth; }");
        focusRing.Should().Be("solid 2px");

        await Page.Locator("input[name=lights][value=off]").CheckAsync();
        var switchedOff = await Page.Locator("#look-evening .look-choice:has(input:checked)").EvaluateAsync<string>("element => getComputedStyle(element).outlineStyle");
        switchedOff.Should().Be("none", "a pill of a group that is switched off carries no selection mark");
        ConsoleErrors.Should().BeEmpty();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private static bool Dark(SKColor colour) => Luminance(colour) < 70;

    private static bool Light(SKColor colour) => Luminance(colour) > 200;

    private static double Luminance(SKColor colour) => (0.2126 * colour.Red) + (0.7152 * colour.Green) + (0.0722 * colour.Blue);

    private static double ShareOfPixels(SKBitmap picture, Func<SKColor, bool> matches)
    {
        var hits = 0;

        for (var y = 0; y < picture.Height; y++)
        {
            for (var x = 0; x < picture.Width; x++)
            {
                if (matches(picture.GetPixel(x, y)))
                {
                    hits++;
                }
            }
        }

        return hits / (double)(picture.Width * picture.Height);
    }

    private async Task OpenAsync(CabinetLook look, string path, int width, bool owner = false)
    {
        OwnerDataSeed.Write(_storage.FullPath, OwnerData.Empty with { Revision = 1, Look = look });
        await StartAsync(new Dictionary<string, string?>(PrototypeOn) { ["Storage:Directory"] = _storage.FullPath });
        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ForcedColors = ForcedColors.Active });
        await Page.SetViewportSizeAsync(width, 1200);

        if (owner)
        {
            await GotoOwnerAsync(path);
            await Page.WaitForSelectorAsync("#look-preview .placement", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
        }
        else
        {
            await GotoCabinetAsync(path);
        }
    }
}

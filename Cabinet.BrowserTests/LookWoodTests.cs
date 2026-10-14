using Cabinet.BrowserTests.Infrastructure;
using System.Globalization;
using System.Text.RegularExpressions;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the wood the owner stored reaches every visitor from the first paint: the furniture is made of
/// that wood, the wall behind it is that wood's wall, the soft light from above is painted on the page itself rather than on a
/// layer of its own, and the page never scrolls sideways because of any of it.
/// </summary>
[Trait("Category", "Browser")]
public sealed partial class LookWoodTests : CabinetPageTest
{
    private const string OakWall = "rgb(39, 50, 59)";
    private const string OakBoard = "rgb(201, 164, 109)";
    private const string TodaysWall = "rgb(59, 36, 22)";
    private const string Ink = "rgb(42, 26, 16)";
    private const string Paper = "rgb(251, 248, 240)";
    private const string WhiteLogo = "powered-by-bgg.svg";
    private const string DarkLogo = "powered-by-bgg-dark.svg";
    private const double MinimumContrast = 4.5;

    private readonly TemporaryDirectory _storage = new();

    [Fact]
    public async Task A_stored_oak_look_shows_the_oak_cabinet_on_its_own_wall_from_the_first_paint()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithWoodAsync("oak");

        await GotoCabinetAsync("/?sample=65");

        (await Page.EvaluateAsync<string>("() => document.documentElement.dataset.wood")).Should().Be("oak");
        (await Computed("body", "backgroundColor")).Should().Be(OakWall);
        (await Computed(".section-body", "backgroundColor")).Should().Be(OakBoard);
        (await Computed("body", "backgroundImage")).Should().Contain("radial-gradient");
        (await Computed("body", "backgroundRepeat")).Should().Be("no-repeat");
        (await Computed("body", "backgroundAttachment")).Should().Be("scroll");
        (await Computed("html", "backgroundImage")).Should().Be("none");
        (await Page.EvaluateAsync<string>("() => getComputedStyle(document.documentElement, '::before').content")).Should().Be("none");
        (await Page.EvaluateAsync<string>("() => getComputedStyle(document.body).backgroundSize"))
            .Should().Be("100% " + await Page.EvaluateAsync<string>("() => window.innerHeight + 'px'"), "the falloff is exactly the large viewport tall");
        (await FixedFullViewportElementCount()).Should().Be(0);
        (await Overflow()).Should().BeLessThanOrEqualTo(0);
        await SaveScreenshotAsync("look-wood-oak");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_oak_floor_patch_lies_inside_its_section_box()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithWoodAsync("oak");

        await GotoCabinetAsync("/?sample=65");

        var outside = await Page.EvaluateAsync<int>(
            """
            () => [...document.querySelectorAll('.section')].filter((section) => {
              const box = section.getBoundingClientRect();
              const floor = section.querySelector(':scope > .section-floor').getBoundingClientRect();
              return floor.left < box.left - 0.5 || floor.right > box.right + 0.5 || floor.top < box.top - 0.5 || floor.bottom > box.bottom + 0.5;
            }).length
            """);
        var paints = await Page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('.section > .section-floor')).backgroundImage");

        outside.Should().Be(0);
        paints.Should().Contain("gradient");
    }

    [Theory]
    [InlineData("brown", "rgb(59, 36, 22)", WhiteLogo)]
    [InlineData("walnut", "rgb(210, 204, 192)", DarkLogo)]
    [InlineData("oak", "rgb(39, 50, 59)", WhiteLogo)]
    [InlineData("birch", "rgb(221, 214, 202)", DarkLogo)]
    [InlineData("mahogany", "rgb(28, 42, 35)", WhiteLogo)]
    public async Task Every_wood_stands_on_its_own_wall_with_the_logo_that_reads_on_it_and_the_owner_pages_never_change(string wood, string wall, string logo)
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithWoodAsync(wood);

        await GotoCabinetAsync("/?sample=65");

        (await Computed("body", "backgroundColor")).Should().Be(wall);
        (await Overflow()).Should().BeLessThanOrEqualTo(0);
        var credit = await ReadCreditAsync();
        credit.File.Should().Be(logo);
        credit.Width.Should().Be("342");
        credit.Height.Should().Be("76");
        credit.Alt.Should().Be("Powered by BGG");
        credit.Href.Should().Be("https://boardgamegeek.com/");
        await SaveScreenshotAsync($"look-wood-{wood}");

        foreach (var path in new[] { "/owner/settings", "/owner/pictures", "/owner/locations" })
        {
            await GotoOwnerAsync(path);

            (await Computed("body", "backgroundColor")).Should().Be(TodaysWall, path);
            var ownerCredit = await ReadCreditAsync();
            ownerCredit.File.Should().Be(WhiteLogo, path);
            ownerCredit.Alt.Should().Be("Powered by BGG", path);
            ownerCredit.Href.Should().Be("https://boardgamegeek.com/", path);
        }

        ConsoleErrors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("walnut")]
    [InlineData("birch")]
    public async Task The_game_card_keeps_its_paper_whatever_wall_stands_behind_it(string wood)
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithWoodAsync(wood);
        await GotoCabinetAsync("/?sample=65");

        await Page.Locator(".placement[data-kind=\"spine\"]").First.ClickAsync();

        await Expect(Page.Locator("dialog.card-dialog[open]")).ToBeVisibleAsync();
        (await Computed(".card", "backgroundColor")).Should().Be(Paper);
        (await Computed(".card", "color")).Should().Be(Ink);
    }

    [Theory]
    [InlineData("brown")]
    [InlineData("walnut")]
    [InlineData("oak")]
    [InlineData("birch")]
    [InlineData("mahogany")]
    public async Task The_load_error_keeps_its_button_readable_on_every_wall(string wood)
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithWoodAsync(wood);
        await Page.RouteAsync("**/cabinet/layout**", route => route.FulfillAsync(new RouteFulfillOptions { Status = 500, Body = "{}" }));

        await GotoCabinetAsync("/");

        await Expect(Page.Locator(".cabinet-retry")).ToBeVisibleAsync();
        var background = ParseColour(await Computed(".cabinet-retry", "backgroundColor"));
        var label = ParseColour(await Computed(".cabinet-retry", "color"));
        var wall = ParseColour(await Computed("body", "backgroundColor"));
        var message = ParseColour(await Computed(".cabinet-message", "color"));

        SpineColour.ContrastRatio(label, background).Should().BeGreaterThanOrEqualTo(MinimumContrast);
        SpineColour.ContrastRatio(message, wall).Should().BeGreaterThanOrEqualTo(MinimumContrast);

        if (CabinetLook.HasLightWall(wood))
        {
            (await Computed(".cabinet-retry", "backgroundColor")).Should().Be(Ink);
        }

        await SaveScreenshotAsync($"look-wood-error-{wood}");
    }

    [Theory]
    [InlineData("birch")]
    [InlineData("oak")]
    public async Task A_logo_that_fails_to_load_leaves_its_link_and_alt_text_in_the_wall_text_colour(string wood)
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithWoodAsync(wood);
        await Page.RouteAsync("**/img/powered-by-bgg*.svg*", route => route.AbortAsync());

        await GotoCabinetAsync("/?sample=65");

        var link = Page.Locator("footer .bgg-credit");
        await Expect(link).ToBeVisibleAsync();
        await Expect(link.Locator("img")).ToHaveAttributeAsync("alt", "Powered by BGG");
        (await Computed("footer .bgg-credit", "color")).Should().Be(await Computed("body", "color"));
        SpineColour.ContrastRatio(ParseColour(await Computed("footer .bgg-credit", "color")), ParseColour(await Computed("body", "backgroundColor")))
            .Should().BeGreaterThanOrEqualTo(MinimumContrast);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _storage.Dispose();
    }

    private Task StartWithWoodAsync(string wood)
    {
        OwnerDataSeed.Write(_storage.FullPath, OwnerData.Empty with { Revision = 1, Look = CabinetLook.Default with { Wood = wood } });

        return StartAsync(new Dictionary<string, string?> { ["Prototype:Enabled"] = "true", ["Storage:Directory"] = _storage.FullPath });
    }

    private async Task<Credit> ReadCreditAsync()
    {
        var link = Page.Locator("footer .bgg-credit");
        var image = link.Locator("img");
        var source = await image.GetAttributeAsync("src") ?? string.Empty;

        return new Credit(
            LogoFile().Replace(Path.GetFileName(new Uri(new Uri("http://127.0.0.1"), source).AbsolutePath), "$1.svg"),
            await image.GetAttributeAsync("width") ?? string.Empty,
            await image.GetAttributeAsync("height") ?? string.Empty,
            await image.GetAttributeAsync("alt") ?? string.Empty,
            await link.EvaluateAsync<string>("element => element.href"));
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

    [GeneratedRegex(@"^(powered-by-bgg(?:-dark)?)(?:\.[A-Za-z0-9]+)?\.svg$")]
    private static partial Regex LogoFile();

    [GeneratedRegex(@"^rgba?\((?<r>\d+), (?<g>\d+), (?<b>\d+)(, [0-9.]+)?\)$")]
    private static partial Regex ComputedColour();

    private Task<string> Computed(string selector, string property) =>
        Page.EvaluateAsync<string>($"() => getComputedStyle(document.querySelector('{selector}')).{property}");

    private Task<double> Overflow() =>
        Page.EvaluateAsync<double>("() => document.documentElement.scrollWidth - window.innerWidth");

    private Task<int> FixedFullViewportElementCount() =>
        Page.EvaluateAsync<int>(
            """
            () => [...document.querySelectorAll('body, html, body *')].filter((element) => {
              const style = getComputedStyle(element);
              const box = element.getBoundingClientRect();
              return style.position === 'fixed' && box.width >= window.innerWidth - 1 && box.height >= window.innerHeight - 1;
            }).length
            """);

    private sealed record Credit(string File, string Width, string Height, string Alt, string Href);
}

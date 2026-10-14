using Cabinet.BrowserTests.Infrastructure;
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
public sealed class LookWoodTests : CabinetPageTest
{
    private const string OakWall = "rgb(39, 50, 59)";
    private const string OakBoard = "rgb(201, 164, 109)";

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
}

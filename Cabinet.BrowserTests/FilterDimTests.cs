using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Owner;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that a box in shadow stays in shadow whatever the cubby lights do: with the lights off, always on and
/// lit in the evening, at every depth, the veil is fully there and the lamp never lies above it.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterDimTests : CabinetPageTest
{
    private const string ViewerZone = "Europe/Berlin";

    private const string EveningMoment = "2027-01-15T20:00:00+01:00";

    private const int FadeWaitMilliseconds = 6000;

    private const string LayerSelector = "#cabinet .section-light";

    private const string Dimmed = "#cabinet .placement[data-dim]";

    private const string ReadDimmedBox = """
        (selector) => {
          const box = document.querySelector(selector);
          const veil = box.querySelector(':scope > .placement-veil');
          const section = box.closest('.section');
          const light = section.querySelector(':scope > .section-light');
          const rect = box.getBoundingClientRect();
          const top = document.elementFromPoint(rect.left + rect.width / 2, rect.top + rect.height / 2);
          const level = (element) => Number.parseInt(getComputedStyle(element).zIndex, 10);
          return {
            veilOpacity: Number(getComputedStyle(veil).opacity),
            lightDisplayed: getComputedStyle(light).display !== 'none',
            topAtCentre: top !== null && top.closest('.section-light') !== null ? 'light' : 'box',
            topInsideBox: top !== null && box.contains(top),
            boxLevel: level(box),
            lightLevel: Number.isNaN(level(light)) ? 0 : level(light),
          };
        }
        """;

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
    [InlineData("off", "flat")]
    [InlineData("off", "shaded")]
    [InlineData("off", "deep")]
    [InlineData("on", "flat")]
    [InlineData("on", "shaded")]
    [InlineData("on", "deep")]
    [InlineData("evenings", "flat")]
    [InlineData("evenings", "shaded")]
    [InlineData("evenings", "deep")]
    public async Task The_shadow_stays_above_every_light(string lights, string depth)
    {
        var look = new CabinetLook("classic", "brown", "wood", lights, depth);
        OwnerDataSeed.Write(_storage.FullPath, OwnerData.Empty with { Revision = 1, Look = look });
        await StartAsync(new Dictionary<string, string?>(PrototypeOn) { ["Storage:Directory"] = _storage.FullPath });
        await Page.Clock.InstallAsync(new ClockInstallOptions { TimeString = EveningMoment });
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        if (lights != "off")
        {
            await Page.WaitForFunctionAsync(
                "(selector) => document.documentElement.hasAttribute('data-lit') && [...document.querySelectorAll(selector)].every((layer) => Number(getComputedStyle(layer).opacity) === 1)",
                LayerSelector,
                new PageWaitForFunctionOptions { Timeout = FadeWaitMilliseconds });
        }

        await Page.Locator("fieldset[data-filter=\"players\"] input[value=\"5\"]").ClickAsync();
        await Expect(Page.Locator(Dimmed).First).ToBeAttachedAsync();
        await Page.Locator(Dimmed).First.ScrollIntoViewIfNeededAsync();
        await Page.WaitForFunctionAsync(
            "(selector) => { const box = document.querySelector(selector); return Number(getComputedStyle(box.querySelector(':scope > .placement-veil')).opacity) === 1; }",
            Dimmed,
            new PageWaitForFunctionOptions { Timeout = FadeWaitMilliseconds });

        var reading = JsonSerializer.Deserialize<DimReading>((await Page.EvaluateAsync<JsonElement>(ReadDimmedBox, Dimmed)).GetRawText(), WebOptions)!;

        reading.VeilOpacity.Should().Be(1, "the veil is fully there under the lights");
        reading.LightDisplayed.Should().Be(lights != "off", "the light layer is drawn exactly when the lights are on");
        reading.TopAtCentre.Should().NotBe("light", "the lamp never lies above a box in shadow");
        reading.TopInsideBox.Should().BeTrue("what lies at the middle of the box is the box or the veil");
        reading.BoxLevel.Should().BeGreaterThan(reading.LightLevel, "the box, and so its veil, is above the light layer");
        ConsoleErrors.Should().BeEmpty();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record DimReading(
        double VeilOpacity,
        bool LightDisplayed,
        string TopAtCentre,
        bool TopInsideBox,
        int BoxLevel,
        int LightLevel);
}

using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Owner;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that each of the seven furniture designs reserves its own height before anything renders,
/// builds its members from the sizes it promises, and gives every section on a phone a complete piece of furniture of its
/// own. The look is stored the way the owner publishes it, so the page carries it from the first response.
/// </summary>
[Trait("Category", "Browser")]
public sealed class LookDesignTests : CabinetPageTest, IDisposable
{
    private const double Tolerance = 1.0;

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private static readonly (int Width, string Path)[] Views =
    [
        (1440, "/?sample=65"),
        (1440, "/?sample=400"),
        (390, "/?sample=65"),
        (390, "/?sample=400"),
    ];

    private readonly TemporaryDirectory _storage = new();

    [Theory]
    [InlineData("legs", 190)]
    public async Task Each_design_reserves_its_own_extra_height_and_every_section_is_a_whole_piece(string design, int extra)
    {
        await StartWithDesignAsync(design);

        foreach (var (width, path) in Views)
        {
            await OpenAsync(width, path);
            var measured = await MeasureAsync();
            var where = $"{design} at {width} on {path}";

            measured.Tokens.SideAllowance.Should().Be(32, where);
            (measured.Tokens.SideTrim + measured.Tokens.Overhang).Should().BeLessThanOrEqualTo(28, where);
            measured.Tokens.BaseOverhang.Should().BeLessThanOrEqualTo(14, where);
            (measured.Tokens.Top + measured.Tokens.Base + measured.Tokens.FloorMargin).Should().Be(extra, where);
            measured.Sections.Should().NotBeEmpty(where);

            foreach (var section in measured.Sections)
            {
                var units = section.Width / (section.SectionWidth + 2 * measured.Tokens.SideAllowance);
                var reserved = (measured.Tokens.Top + section.SectionHeight + measured.Tokens.Base + measured.Tokens.FloorMargin) * units;

                section.Height.Should().BeApproximately(reserved, Tolerance, where);
                section.Position.Should().BeNull($"{where}: phone sections are separate pieces");
                section.TopHeight.Should().BeApproximately(measured.Tokens.Top * units, Tolerance, where);
                section.BaseHeight.Should().BeApproximately(measured.Tokens.Base * units, Tolerance, where);
            }
        }

        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_on_legs_design_shows_an_apron_and_two_splayed_legs_cut_from_one_shape()
    {
        await StartWithDesignAsync("legs");

        foreach (var (width, path) in Views)
        {
            await OpenAsync(width, path);
            var shapes = await Page.EvaluateAsync<string[]>(
                "() => [...document.querySelectorAll('.section-base')].map((base) => getComputedStyle(base, '::before').clipPath)");

            shapes.Should().NotBeEmpty();
            shapes.Should().OnlyContain(shape => shape.StartsWith("polygon(", StringComparison.Ordinal), $"at {width} on {path}");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task StartWithDesignAsync(string design)
    {
        OwnerDataSeed.Write(
            _storage.FullPath,
            OwnerData.Empty with { Revision = 1, Look = new CabinetLook(design, "brown", "wood", "off", "shaded") });

        await StartAsync(new Dictionary<string, string?>(PrototypeOn) { ["Storage:Directory"] = _storage.FullPath });
    }

    private async Task OpenAsync(int width, string path)
    {
        await Page.SetViewportSizeAsync(width, 1600);
        await GotoCabinetAsync(path);
        await Page.WaitForSelectorAsync("#cabinet .section", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
    }

    private async Task<Measured> MeasureAsync()
    {
        var json = await Page.EvaluateAsync<string>(MeasureScript);

        return JsonSerializer.Deserialize<Measured>(json, ReadingOptions)!;
    }

    private static readonly JsonSerializerOptions ReadingOptions = new() { PropertyNameCaseInsensitive = true };

    private const string MeasureScript = """
        async () => {
          const frames = () => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
          const cabinet = document.querySelector('.cabinet');
          const style = getComputedStyle(cabinet);
          const number = (name) => parseFloat(style.getPropertyValue(name));
          const tokens = {
            sideAllowance: number('--side-allowance'),
            sideTrim: number('--side-trim'),
            overhang: number('--overhang'),
            top: number('--top-height'),
            base: number('--base-height'),
            baseOverhang: number('--base-overhang'),
            floorMargin: number('--floor-margin'),
          };
          const sections = [];
          for (const section of document.querySelectorAll('.section')) {
            section.scrollIntoView({ block: 'center' });
            await frames();
            const box = section.getBoundingClientRect();
            const own = getComputedStyle(section);
            sections.push({
              width: box.width,
              height: box.height,
              sectionWidth: parseFloat(own.getPropertyValue('--section-w')),
              sectionHeight: parseFloat(own.getPropertyValue('--section-h')),
              topHeight: section.querySelector('.section-top').getBoundingClientRect().height,
              baseHeight: section.querySelector('.section-base').getBoundingClientRect().height,
              position: section.dataset.position ?? null,
            });
          }
          return JSON.stringify({ tokens, sections });
        }
        """;

    private sealed record TokenReading(double SideAllowance, double SideTrim, double Overhang, double Top, double Base, double BaseOverhang, double FloorMargin);

    private sealed record SectionReading(double Width, double Height, double SectionWidth, double SectionHeight, double TopHeight, double BaseHeight, string? Position);

    private sealed record Measured(TokenReading Tokens, List<SectionReading> Sections);
}

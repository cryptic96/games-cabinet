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
    [InlineData("classic", 138)]
    [InlineData("modern", 112)]
    [InlineData("display", 154)]
    [InlineData("legs", 190)]
    [InlineData("library", 212)]
    [InlineData("plywood", 70)]
    [InlineData("plywood-legs", 172)]
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

    [Theory]
    [InlineData("classic")]
    [InlineData("modern")]
    [InlineData("display")]
    [InlineData("legs")]
    [InlineData("library")]
    [InlineData("plywood")]
    [InlineData("plywood-legs")]
    public async Task One_section_and_many_sections_reserve_the_same_extra_height(string design)
    {
        await StartWithDesignAsync(design);

        await OpenAsync(1440, "/?sample=12");
        var single = await MeasureAsync();
        await OpenAsync(1440, "/?sample=65");
        var many = await MeasureAsync();

        single.Sections.Should().HaveCount(1, design);
        many.Sections.Should().HaveCountGreaterThan(1, design);

        var reservedSingle = single.Sections[0].Height - single.Sections[0].SectionHeight * UnitsOf(single.Sections[0], single.Tokens);
        var reservedMany = many.Sections[0].Height - many.Sections[0].SectionHeight * UnitsOf(many.Sections[0], many.Tokens);

        reservedSingle.Should().BeApproximately(reservedMany, Tolerance, design);
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

    [Theory]
    [InlineData("classic")]
    [InlineData("modern")]
    [InlineData("display")]
    [InlineData("library")]
    [InlineData("plywood")]
    public async Task A_design_without_legs_has_no_leg_shape(string design)
    {
        await StartWithDesignAsync(design);
        await OpenAsync(1440, "/?sample=12");

        var shapes = await Page.EvaluateAsync<string[]>(
            "() => [...document.querySelectorAll('.section-base')].map((base) => getComputedStyle(base, '::before').clipPath)");

        shapes.Should().OnlyContain(shape => shape == "none");
    }

    [Fact]
    public async Task The_library_crown_and_plinth_tile_their_heights_exactly_and_the_brass_line_follows_the_shelf_front()
    {
        await StartWithDesignAsync("library");

        foreach (var (width, path) in new[] { (1440, "/?sample=65"), (390, "/?sample=65") })
        {
            await OpenAsync(width, path);
            var json = await Page.EvaluateAsync<string>(LibraryScript);
            var library = JsonSerializer.Deserialize<LibraryReading>(json, ReadingOptions)!;
            var where = $"at {width}";

            library.TopHeight.Should().BeApproximately(84, 0.1, where);
            library.Crown.Select(layer => layer.Height).Should().BeEquivalentTo([16d, 14d, 36d, 14d, 18d], options => options.Using<double>(context => context.Subject.Should().BeApproximately(context.Expectation, 0.1)).WhenTypeIs<double>().WithStrictOrdering(), where);
            library.Crown.Select(layer => layer.Top).Should().BeEquivalentTo([0d, 38d, 16d, 52d, 66d], options => options.Using<double>(context => context.Subject.Should().BeApproximately(context.Expectation, 0.1)).WhenTypeIs<double>().WithStrictOrdering(), where);
            library.Crown.Max(layer => layer.Top + layer.Height).Should().BeApproximately(84, 0.1, where);
            library.DentilPeriod.Should().BeApproximately(12, 0.1, where);

            library.BaseHeight.Should().BeApproximately(104, 0.1, where);
            library.Plinth.Max(layer => layer.Top + layer.Height).Should().BeApproximately(104, 0.1, where);
            library.Plinth.Where(layer => layer.Top < 1).Select(layer => layer.Height).Should().OnlyContain(height => Math.Abs(height - 18) < 0.1);
            library.Plinth.Where(layer => layer.Top is > 17 and < 19).Select(layer => layer.Height).Should().OnlyContain(height => Math.Abs(height - 70) < 0.1);
            library.Plinth.Where(layer => layer.Top is > 87 and < 89).Select(layer => layer.Height).Should().OnlyContain(height => Math.Abs(height - 16) < 0.1);

            library.BrassLeading.Should().BeApproximately(library.BrassTrailing, 0.001, where);
            (library.BrassLeading + library.BrassTrailing).Should().BeApproximately(Math.Max(1, 2 * library.Unit), 0.01, where);
            library.BrassPresent.Should().BeTrue(where);
        }
    }

    [Theory]
    [InlineData("plywood")]
    [InlineData("plywood-legs")]
    public async Task The_ply_period_never_drops_under_three_pixels_and_the_glue_line_goes_away_on_a_phone(string design)
    {
        await StartWithDesignAsync(design);

        await OpenAsync(1440, "/?sample=65");
        var desktop = JsonSerializer.Deserialize<PlyReading>(await Page.EvaluateAsync<string>(PlyScript), ReadingOptions)!;

        desktop.Period.Should().BeApproximately(Math.Max(3, 4 * desktop.Unit), 0.05);
        desktop.Period.Should().BeGreaterThanOrEqualTo(3);
        desktop.Glue.Should().BeGreaterThanOrEqualTo(1 - 0.01);
        desktop.GlueIsOwnColour.Should().BeTrue();

        await OpenAsync(390, "/?sample=65");
        var phone = JsonSerializer.Deserialize<PlyReading>(await Page.EvaluateAsync<string>(PlyScript), ReadingOptions)!;

        phone.Period.Should().BeApproximately(Math.Max(3, 4 * phone.Unit), 0.05);
        phone.Period.Should().BeGreaterThanOrEqualTo(3);
        phone.GlueIsOwnColour.Should().BeFalse("under 400 px the glue line and the second ply are one tone");
    }

    [Theory]
    [InlineData("plywood")]
    [InlineData("plywood-legs")]
    public async Task At_tile_size_the_ply_edge_is_two_tones_with_no_glue_line_and_a_period_of_at_least_four_pixels(string design)
    {
        await StartWithDesignAsync(design);
        await OpenAsync(1440, "/?sample=12");
        await Page.EvaluateAsync("() => document.querySelector('.section').style.setProperty('flex-basis', '180px')");

        var tile = JsonSerializer.Deserialize<PlyReading>(await Page.EvaluateAsync<string>(PlyScript), ReadingOptions)!;

        tile.Period.Should().BeApproximately(Math.Max(4, 4 * tile.Unit), 0.05);
        tile.Glue.Should().BeApproximately(0, 0.01);
        tile.GlueIsOwnColour.Should().BeFalse();
        tile.SecondPlyIsOwnColour.Should().BeTrue();
    }

    [Fact]
    public async Task The_default_design_is_the_classic_one_when_nothing_is_stored()
    {
        await StartAsync(PrototypeOn);
        await OpenAsync(1440, "/?sample=65");

        var measured = await MeasureAsync();

        (measured.Tokens.Top + measured.Tokens.Base + measured.Tokens.FloorMargin).Should().Be(138);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private static double UnitsOf(SectionReading section, TokenReading tokens) =>
        section.Width / (section.SectionWidth + 2 * tokens.SideAllowance);

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

    private const string LibraryScript = """
        async () => {
          const frames = () => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
          const split = (text) => {
            const parts = [];
            let depth = 0;
            let start = 0;
            for (let index = 0; index < text.length; index += 1) {
              if (text[index] === '(') depth += 1;
              else if (text[index] === ')') depth -= 1;
              else if (text[index] === ',' && depth === 0) { parts.push(text.slice(start, index).trim()); start = index + 1; }
            }
            parts.push(text.slice(start).trim());
            return parts;
          };
          const section = document.querySelector('.section');
          section.scrollIntoView({ block: 'center' });
          await frames();
          const box = section.getBoundingClientRect();
          const unit = box.width / (parseFloat(getComputedStyle(section).getPropertyValue('--section-w')) + 64);
          const last = (token) => parseFloat(token.trim().split(/\s+/).pop());
          const rows = (element) => {
            const style = getComputedStyle(element);
            const sizes = split(style.backgroundSize);
            const positions = split(style.backgroundPosition);
            return sizes.map((size, index) => ({
              height: last(size) / unit,
              top: last(positions[index]) / unit,
            }));
          };
          const top = section.querySelector('.section-top');
          const base = section.querySelector('.section-base');
          const crownImages = split(getComputedStyle(top).backgroundImage);
          const dentil = crownImages.find((layer) => layer.startsWith('repeating-linear-gradient'));
          const dentilStops = [...dentil.matchAll(/([\d.]+)px/g)].map((match) => parseFloat(match[1]));
          const cubby = section.querySelector('.cubby');
          const shelfImage = getComputedStyle(cubby, '::before').backgroundImage;
          const leading = shelfImage.match(/calc\(50% - ([\d.]+)px\)/);
          const trailing = shelfImage.match(/calc\(50% \+ ([\d.]+)px\)/);
          const inlay = getComputedStyle(document.documentElement).getPropertyValue('--inlay').trim();
          return JSON.stringify({
            unit,
            topHeight: top.getBoundingClientRect().height / unit,
            baseHeight: base.getBoundingClientRect().height / unit,
            crown: rows(top),
            plinth: rows(base),
            dentilPeriod: Math.max(...dentilStops) / unit,
            brassLeading: leading ? parseFloat(leading[1]) : -1,
            brassTrailing: trailing ? parseFloat(trailing[1]) : -2,
            brassPresent: shelfImage.includes('rgb(196, 161, 90)') && inlay.toLowerCase() === '#c4a15a',
          });
        }
        """;

    private const string PlyScript = """
        async () => {
          const frames = () => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
          const split = (text) => {
            const parts = [];
            let depth = 0;
            let start = 0;
            for (let index = 0; index < text.length; index += 1) {
              if (text[index] === '(') depth += 1;
              else if (text[index] === ')') depth -= 1;
              else if (text[index] === ',' && depth === 0) { parts.push(text.slice(start, index).trim()); start = index + 1; }
            }
            parts.push(text.slice(start).trim());
            return parts;
          };
          const section = document.querySelector('.section');
          section.scrollIntoView({ block: 'center' });
          await frames();
          const unit = section.getBoundingClientRect().width / (parseFloat(getComputedStyle(section).getPropertyValue('--section-w')) + 64);
          const cubby = section.querySelector('.cubby');
          const layers = split(getComputedStyle(cubby, '::before').backgroundImage);
          const ply = layers.find((layer) => layer.startsWith('repeating-linear-gradient'));
          const stops = [...ply.matchAll(/(-?[\d.]+)px/g)].map((match) => parseFloat(match[1]));
          const custom = (name) => getComputedStyle(cubby).getPropertyValue(name).trim();
          return JSON.stringify({
            unit,
            period: stops[stops.length - 1],
            glue: stops[stops.length - 1] - stops[stops.length - 2],
            glueIsOwnColour: custom('--pg') !== custom('--pa') && custom('--pg') !== custom('--pb'),
            secondPlyIsOwnColour: custom('--pb') !== custom('--pa'),
          });
        }
        """;

    private sealed record TokenReading(double SideAllowance, double SideTrim, double Overhang, double Top, double Base, double BaseOverhang, double FloorMargin);

    private sealed record SectionReading(double Width, double Height, double SectionWidth, double SectionHeight, double TopHeight, double BaseHeight, string? Position);

    private sealed record Measured(TokenReading Tokens, List<SectionReading> Sections);

    private sealed record LayerReading(double Height, double Top);

    private sealed record LibraryReading(
        double Unit,
        double TopHeight,
        double BaseHeight,
        List<LayerReading> Crown,
        List<LayerReading> Plinth,
        double DentilPeriod,
        double BrassLeading,
        double BrassTrailing,
        bool BrassPresent);

    private sealed record PlyReading(double Unit, double Period, double Glue, bool GlueIsOwnColour, bool SecondPlyIsOwnColour);
}

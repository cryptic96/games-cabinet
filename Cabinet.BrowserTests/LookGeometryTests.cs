using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Owner;
using FluentAssertions;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves that only the design may change the size of anything in the cabinet. For a design, every other choice (the wood, the
/// kind of back, the depth and every state of the lights, including the evening marker and the lit marker the page's script adds)
/// is written onto the page in turn, and the box of every section, every cubby and the page itself is read after each change.
/// Every combination must read exactly what the first one read, so no choice can shift, resize or clip the furniture.
/// </summary>
[Trait("Category", "Browser")]
public sealed class LookGeometryTests : CabinetPageTest
{
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    /// <summary>The design the case that proves the measurement has teeth runs.</summary>
    private const string LeadingDesign = "legs";

    private const string MeasureEveryCombination = """
        async ({ woods, backs, depths, lightStates }) => {
          const frames = () => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
          const root = document.documentElement;
          const read = () => [
            'page ' + document.documentElement.scrollHeight + ',' + document.documentElement.scrollWidth,
            ...[...document.querySelectorAll('#cabinet .section, #cabinet .cubby')].map((element) => {
              const rect = element.getBoundingClientRect();
              return element.className.split(' ')[0] + ' ' + [rect.x + scrollX, rect.y + scrollY, rect.width, rect.height].map((value) => value.toFixed(4)).join(',');
            }),
          ];
          const readings = new Map();
          let first = null;
          let combinations = 0;
          let widestOverflow = -1000;
          const sections = document.querySelectorAll('#cabinet .section').length;
          for (const wood of woods) {
            for (const back of backs) {
              for (const depth of depths) {
                for (const state of lightStates) {
                  root.dataset.wood = wood;
                  root.dataset.backs = back;
                  root.dataset.depth = depth;
                  root.dataset.lights = state.lights;
                  for (const marker of ['data-lit', 'data-evening']) {
                    root.toggleAttribute(marker, state.markers.includes(marker));
                  }
                  const reading = read();
                  widestOverflow = Math.max(widestOverflow, document.documentElement.scrollWidth - window.innerWidth);
                  first ??= reading;
                  combinations += 1;
                  const index = reading.findIndex((line, at) => line !== first[at]);
                  if (index >= 0 || reading.length !== first.length) {
                    readings.set([wood, back, depth, state.name].join(' '), 'box ' + index + ' reads ' + reading[index] + ' where the first read ' + first[index]);
                  }
                }
              }
            }
          }
          await frames();
          return JSON.stringify({
            combinations,
            sections,
            widestOverflow,
            differing: [...readings.entries()].slice(0, 3),
            differingCount: readings.size,
          });
        }
        """;

    private static readonly string[] Woods = ["brown", "walnut", "oak", "birch", "mahogany"];

    private static readonly string[] Backs = ["wood", "painted"];

    private static readonly string[] Depths = ["flat", "shaded", "deep"];

    private static readonly object[] LightStates =
    [
        new { name = "off", lights = "off", markers = Array.Empty<string>() },
        new { name = "on-unlit", lights = "on", markers = Array.Empty<string>() },
        new { name = "on-lit", lights = "on", markers = new[] { "data-lit" } },
        new { name = "evenings-by-day", lights = "evenings", markers = Array.Empty<string>() },
        new { name = "evenings-at-night", lights = "evenings", markers = new[] { "data-lit", "data-evening" } },
    ];

    /// <summary>Every design at both widths on both samples.</summary>
    public static TheoryData<string, int, string> DesignsAndViews { get; } = BuildDesignsAndViews();

    /// <summary>
    /// With one design set, every combination of wood, back, depth and lights leaves every box exactly where the first combination
    /// left it, and the page neither grows nor scrolls sideways under any of them.
    /// </summary>
    [Theory]
    [MemberData(nameof(DesignsAndViews))]
    public async Task One_design_keeps_every_box_where_it_is_across_every_other_choice(string design, int width, string path)
    {
        await StartAsync(PrototypeOn);

        var summary = await MeasureAsync(design, width, path);

        summary.DifferingCount.Should().Be(0, string.Join(" | ", summary.Differing.Select(pair => $"{pair[0]}: {pair[1]}")));
        summary.Combinations.Should().Be(Woods.Length * Backs.Length * Depths.Length * LightStates.Length);
        summary.Sections.Should().BeGreaterThan(1);
        summary.WidestOverflow.Should().BeLessThanOrEqualTo(0, $"{design} at {width} px on {path} scrolls sideways");
        ConsoleErrors.Should().BeEmpty();
    }

    /// <summary>
    /// The measurement has teeth: a rule that grows the cabinet for one wood alone is reported as differing combinations, and only
    /// the combinations of that wood differ.
    /// </summary>
    [Fact]
    public async Task A_rule_that_resizes_one_wood_is_reported_as_differing_combinations()
    {
        await StartAsync(PrototypeOn);

        var summary = await MeasureAsync(LeadingDesign, 1440, "/?sample=65", "[data-wood=\"oak\"] .section { margin-top: 3px; }");

        summary.DifferingCount.Should().Be(Backs.Length * Depths.Length * LightStates.Length);
    }

    private static TheoryData<string, int, string> BuildDesignsAndViews()
    {
        var data = new TheoryData<string, int, string>();

        foreach (var design in CabinetLook.Designs)
        {
            foreach (var width in new[] { 1440, 390 })
            {
                foreach (var path in new[] { "/?sample=65", "/?sample=400" })
                {
                    data.Add(design, width, path);
                }
            }
        }

        return data;
    }

    private async Task<Summary> MeasureAsync(string design, int width, string path, string? extraRule = null)
    {
        await Page.SetViewportSizeAsync(width, 900);
        await GotoCabinetAsync(path);

        if (extraRule is not null)
        {
            await Page.EvaluateAsync(
                "(css) => { const sheet = new CSSStyleSheet(); sheet.replaceSync(css); document.adoptedStyleSheets = [...document.adoptedStyleSheets, sheet]; }",
                extraRule);
        }

        await Page.EvaluateAsync("(value) => { document.documentElement.dataset.design = value; }", design);
        await FitViewportToPageAsync();

        var json = await Page.EvaluateAsync<string>(
            MeasureEveryCombination,
            new { woods = Woods, backs = Backs, depths = Depths, lightStates = LightStates });

        return JsonSerializer.Deserialize<Summary>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    /// <summary>
    /// Makes the viewport as tall as the page, at its current width, until the height stops changing, so every section is laid out
    /// and a look change that makes the page taller never leaves a section skipped below the fold.
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

    private sealed record Summary(int Combinations, int Sections, int WidestOverflow, List<List<string>> Differing, int DifferingCount);
}

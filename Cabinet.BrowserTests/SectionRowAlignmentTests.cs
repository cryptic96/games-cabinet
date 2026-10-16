using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the sections of a desktop row stand on one line: when one section is a shelf row shorter
/// than its neighbours, every piece in the row still ends on the same line, so the feet share one floor and the shorter
/// piece does not hang from the top of the row.
/// </summary>
[Trait("Category", "Browser")]
public sealed class SectionRowAlignmentTests : CabinetPageTest
{
    private const double Tolerance = 0.5;

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private static readonly JsonSerializerOptions ReadingOptions = new() { PropertyNameCaseInsensitive = true };

    [Theory]
    [InlineData(1440, 2)]
    [InlineData(1920, 3)]
    public async Task Every_section_of_a_row_ends_on_one_line_even_when_one_is_a_shelf_row_shorter(int width, int columns)
    {
        await Page.SetViewportSizeAsync(width, 900);
        await StartAsync(PrototypeOn);
        await GotoCabinetAsync("/?sample=400");

        var before = await ReadRowsAsync(columns);
        before.Should().NotBeEmpty();
        before.Should().OnlyContain(row => row.Bottoms.Max() - row.Bottoms.Min() < Tolerance);

        await Page.EvaluateAsync(ShortenOneSectionPerRow, columns);
        var after = await ReadRowsAsync(columns);

        after.Should().NotBeEmpty();

        foreach (var row in after)
        {
            row.Heights.Max().Should().BeGreaterThan(row.Heights.Min() + 20, "one section of the row was made a shelf row shorter");
            (row.Bottoms.Max() - row.Bottoms.Min()).Should().BeLessThan(Tolerance, "the feet of every piece in a row share one line");
            (row.Tops.Max() - row.Tops.Min()).Should().BeGreaterThan(20, "the shorter piece stands lower, it does not hang from the top of the row");
        }

        ConsoleErrors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("1")]
    [InlineData("5")]
    public async Task A_sparse_last_section_on_a_phone_has_no_empty_shelf_above_its_boxes(string sample)
    {
        await Page.SetViewportSizeAsync(390, 800);
        await StartAsync(PrototypeOn);
        await GotoCabinetAsync($"/?sample={sample}");

        await Page.Locator("#cabinet .section").Last.ScrollIntoViewIfNeededAsync();
        var json = await Page.EvaluateAsync<string>(LastSectionShelvesScript);
        var shelves = JsonSerializer.Deserialize<List<ShelfReading>>(json, ReadingOptions)!;
        var topBoxShelf = shelves.FindIndex(shelf => shelf.Boxes > 0);

        shelves.Should().NotBeEmpty();
        shelves.Count.Should().BeLessThan(7, "a sparse last section keeps only the shelf rows that hold a game");
        shelves[^1].Boxes.Should().BeGreaterThan(0, "the boxes stand on the bottom shelf");
        topBoxShelf.Should().BeLessThanOrEqualTo(1, "at most the one shelf that makes the two-shelf minimum is empty above the boxes");

        if (topBoxShelf == 1)
        {
            shelves.Should().HaveCount(2);
        }

        shelves.Skip(topBoxShelf).Should().OnlyContain(shelf => shelf.Boxes > 0, "no shelf between the boxes is empty");
        shelves.Should().OnlyContain(shelf => shelf.Overhang < Tolerance, "every box ends on a shelf of its section");
        ConsoleErrors.Should().BeEmpty();
    }

    private async Task<List<RowReading>> ReadRowsAsync(int columns)
    {
        var json = await Page.EvaluateAsync<string>(RowsScript, columns);

        return JsonSerializer.Deserialize<List<RowReading>>(json, ReadingOptions)!;
    }

    private const string ShortenOneSectionPerRow = """
        (columns) => {
          const sections = [...document.querySelectorAll('#cabinet .section')];
          for (let index = 1; index < sections.length; index += columns) {
            const height = parseFloat(getComputedStyle(sections[index]).getPropertyValue('--section-h'));
            sections[index].style.setProperty('--section-h', String(height * 0.8));
          }
        }
        """;

    private const string LastSectionShelvesScript = """
        async () => {
          await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
          const sections = [...document.querySelectorAll('#cabinet .section')];
          const cubbies = [...sections[sections.length - 1].querySelectorAll('.cubby')];
          const tops = [...new Set(cubbies.map((cubby) => Math.round(cubby.getBoundingClientRect().top)))].sort((a, b) => a - b);
          return JSON.stringify(tops.map((top) => {
            const row = cubbies.filter((cubby) => Math.round(cubby.getBoundingClientRect().top) === top);
            const boxes = row.flatMap((cubby) => [...cubby.querySelectorAll('.placement')].map((box) => ({ box, cubby })));
            const overhang = Math.max(0, ...boxes.map((entry) =>
              entry.box.getBoundingClientRect().bottom - entry.cubby.getBoundingClientRect().bottom));
            return { boxes: boxes.length, overhang };
          }));
        }
        """;

    private const string RowsScript = """
        async (columns) => {
          await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
          const sections = [...document.querySelectorAll('#cabinet .section')];
          const rows = [];
          for (let start = 0; start < sections.length; start += columns) {
            const boxes = sections.slice(start, start + columns).map((section) => section.getBoundingClientRect());
            rows.push({
              bottoms: boxes.map((box) => box.bottom + scrollY),
              heights: boxes.map((box) => box.height),
              tops: boxes.map((box) => box.top + scrollY),
            });
          }
          return JSON.stringify(rows);
        }
        """;

    private sealed record RowReading(double[] Bottoms, double[] Heights, double[] Tops);

    private sealed record ShelfReading(int Boxes, double Overhang);
}

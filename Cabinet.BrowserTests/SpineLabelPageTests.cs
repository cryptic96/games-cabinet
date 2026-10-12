using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser, on the sample collections at a desktop and a phone width, that no spine or flat box shows a scrap of
/// its title: a shortened label holds at least five characters before its ellipsis, and a box whose label is empty still carries
/// its full title in its accessible name and tooltip.
/// </summary>
[Trait("Category", "Browser")]
public sealed class SpineLabelPageTests : CabinetPageTest
{
    private const int MinVisibleCharacters = 5;

    private const string LabelsEndingInEllipsis = """
        () => {
          const segmenter = new Intl.Segmenter(undefined, { granularity: 'grapheme' });
          return [...document.querySelectorAll('#cabinet .placement-label')]
            .map((label) => label.textContent ?? '')
            .filter((text) => text.endsWith('…'))
            .map((text) => [...segmenter.segment(text.slice(0, -1))].length);
        }
        """;

    private const string EmptyLabelPlacementsWithoutAName = """
        () => [...document.querySelectorAll('#cabinet .placement')]
          .filter((placement) => (placement.querySelector('.placement-label')?.textContent ?? '').trim() === '')
          .filter((placement) => (placement.getAttribute('aria-label') ?? '').trim() === '' || (placement.title ?? '').trim() === '')
          .length
        """;

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    [Theory]
    [InlineData("/?sample=65", 1440, 900)]
    [InlineData("/?sample=65", 390, 800)]
    [InlineData("/?sample=400", 1440, 900)]
    [InlineData("/?sample=400", 390, 800)]
    public async Task No_shortened_label_shows_fewer_than_five_characters_before_its_ellipsis(string path, int width, int height)
    {
        await OpenAsync(path, width, height);

        var shortened = await Page.EvaluateAsync<int[]>(LabelsEndingInEllipsis);

        shortened.Should().NotBeEmpty("the samples hold titles too long for their boxes");
        shortened.Should().OnlyContain(shown => shown >= MinVisibleCharacters, "no label may be a scrap such as 'Ex…'");
        ConsoleErrors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("/?sample=65", 1440, 900)]
    [InlineData("/?sample=65", 390, 800)]
    [InlineData("/?sample=400", 1440, 900)]
    [InlineData("/?sample=400", 390, 800)]
    public async Task A_box_with_an_empty_label_keeps_its_full_title_in_its_name_and_tooltip(string path, int width, int height)
    {
        await OpenAsync(path, width, height);

        var withoutAName = await Page.EvaluateAsync<int>(EmptyLabelPlacementsWithoutAName);

        withoutAName.Should().Be(0);
        await SaveScreenshotAsync(width < 600 ? "thin-spines-phone" : "thin-spines-desktop");
    }

    private async Task OpenAsync(string path, int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await GotoCabinetAsync(path);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}

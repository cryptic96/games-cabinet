using Cabinet.BrowserTests.Infrastructure;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that a picture that cannot be loaded is marked in the page but looks like a game without a
/// picture, so visitors see nothing wrong while the owner can find it.
/// </summary>
[Trait("Category", "Browser")]
public sealed class CabinetPolishTests : CabinetPageTest
{
    private const int CollectionSize = 20;

    private const string FailedEntryIds = """
        () => [...document.querySelectorAll('#cabinet .placement[data-art="failed"]')]
          .map((placement) => Number(placement.dataset.entryId))
        """;

    private const string PictureCount = """
        () => document.querySelectorAll('#cabinet .placement[data-art="true"]').length
        """;

    private const string UprightTitleSizes = """
        () => [...new Set([...document.querySelectorAll(
          '#cabinet .placement[data-kind="spine"] .placement-label, #cabinet .placement[data-kind="expansionSpine"][data-lines="1"] .placement-label')]
          .filter((label) => (label.textContent ?? '').trim() !== '')
          .map((label) => parseFloat(getComputedStyle(label).fontSize)))]
        """;

    private const string FlatTitleSizes = """
        () => [...new Set([...document.querySelectorAll(
          '#cabinet .placement:is([data-kind="flatBox"], [data-kind="expansionLayer"]) .placement-label, #cabinet .placement[data-kind="orphanExpansion"][data-lines="1"] .placement-label')]
          .filter((label) => (label.textContent ?? '').trim() !== '')
          .map((label) => parseFloat(getComputedStyle(label).fontSize)))]
        """;

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    [Theory]
    [InlineData("/?sample=65", 1440, 900, "polish-desktop-65")]
    [InlineData("/?sample=65", 390, 800, "polish-phone-65")]
    [InlineData("/?sample=400", 1440, 900, "polish-desktop-400")]
    [InlineData("/?sample=400", 390, 800, "polish-phone-400")]
    public async Task One_line_titles_use_two_sizes_and_the_page_never_scrolls_sideways(string path, int width, int height, string screenshot)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await GotoCabinetAsync(path);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var upright = await Page.EvaluateAsync<double[]>(UprightTitleSizes);
        var flat = await Page.EvaluateAsync<double[]>(FlatTitleSizes);
        var scrollWidth = await Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth");

        upright.Should().NotBeEmpty();
        upright.Should().BeSubsetOf([12d, 16d]);
        flat.Should().BeSubsetOf([12d, 14d]);
        scrollWidth.Should().BeLessThanOrEqualTo(width);
        await SaveScreenshotAsync(screenshot);
    }

    [Fact]
    public async Task The_plinth_lip_is_visible_on_a_phone()
    {
        await Page.SetViewportSizeAsync(390, 800);
        await StartAsync(PrototypeOn);
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var plinth = Page.Locator("#cabinet .section-base").First;
        await plinth.ScrollIntoViewIfNeededAsync();
        var litness = await plinth.EvaluateAsync<string>("(element) => getComputedStyle(element, '::before').getPropertyValue('--arch-lit').trim()");

        litness.Should().Be("0.22");
        await SaveScreenshotAsync("plinth-lip-390");
    }

    [Fact]
    public async Task A_picture_that_cannot_load_is_marked_failed_and_shows_the_generated_cover()
    {
        await using var collection = await SyntheticArtCollection.StartAsync(CollectionSize);
        var factory = await StartAsync(collection.Settings, collection.ConfigureServices);
        using var client = factory.CreatePublicClient();
        await collection.SyncAsync(client);
        var placements = await SyntheticArtCollection.ArtPlacements(client);
        var missing = placements[0];
        var sharingTheFile = placements.Where(placement => placement.Url == missing.Url).Select(placement => placement.EntryId).ToList();
        collection.DeleteStoredPicture(missing.Url);
        await Page.SetViewportSizeAsync(1440, 900);

        await GotoCabinetAsync("/");
        var box = Page.Locator($"#cabinet .placement[data-entry-id='{missing.EntryId}']");
        await box.ScrollIntoViewIfNeededAsync();

        await Expect(box).ToHaveAttributeAsync("data-art", "failed");
        await Expect(box.Locator("img")).ToHaveCountAsync(0);
        await Expect(box.Locator(".cover-plate .placement-label")).ToHaveCountAsync(1);
        (await Page.EvaluateAsync<long[]>(FailedEntryIds)).Should().BeSubsetOf(sharingTheFile);
        (await Page.EvaluateAsync<int>(PictureCount)).Should().BeGreaterThan(0);
        ConsoleErrors.Should().OnlyContain(error => error.Contains("404", StringComparison.Ordinal) && !error.Contains("Content Security Policy", StringComparison.OrdinalIgnoreCase));
        await SaveScreenshotAsync("broken-art-desktop");
    }
}

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

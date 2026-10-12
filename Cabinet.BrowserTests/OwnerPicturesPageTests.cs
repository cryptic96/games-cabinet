using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the owner's pictures page lists every game with its four choices at a size a thumb can hit and
/// fits a phone without sideways scrolling.
/// </summary>
[Trait("Category", "Browser")]
public sealed class OwnerPicturesPageTests : CabinetPageTest
{
    private const string ImageHost = "https://example.org/images/";
    private const int MinimumTarget = 44;

    private static readonly byte[] FlatCover = SyntheticArt.Encode(SyntheticArtKind.FlatCover);
    private static readonly byte[] ColouredCover = SyntheticArt.Encode(SyntheticArtKind.CoverColouredField);

    private readonly TemporaryDirectory _storage = new();
    private HttpClient? _publicClient;

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(390, 844)]
    public async Task Every_game_shows_four_choices_at_thumb_size_and_the_page_never_scrolls_sideways(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartHostAsync();

        await GotoOwnerAsync("/owner/pictures");

        await Expect(Page.Locator("h2")).ToHaveTextAsync("Pictures");
        await Expect(Page.Locator("nav.owner-bar a[aria-current=\"page\"]")).ToHaveTextAsync("Pictures");
        var rows = await Page.Locator(".pic-row").CountAsync();
        rows.Should().BeGreaterThan(1);
        await Expect(Page.Locator(".pic-row input[type=radio]")).ToHaveCountAsync(rows * 4);
        await Expect(Page.Locator(".pic-row input[type=radio][value=Automatic]:checked")).ToHaveCountAsync(rows);
        var targets = await Page.Locator(".pic-tile:not([data-state='missing']):not([data-state='none'])").EvaluateAllAsync<double[]>(
            "elements => elements.map(element => element.getBoundingClientRect().height)");
        targets.Should().OnlyContain(target => target >= MinimumTarget);
        var game = Page.Locator(".pic-row", new PageLocatorOptions { Has = Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Example Game 1", Exact = true }) });
        await Expect(game.Locator("input[value=OwnedEdition]")).ToBeEnabledAsync();
        await Expect(game.Locator("input[value=MainPicture]")).ToBeEnabledAsync();
        await Expect(game.Locator("input[value=Link]")).ToBeDisabledAsync();
        var borders = await Page.Locator(".pic-tile").EvaluateAllAsync<string[]>(
            "labels => labels.map(label => `${label.querySelector('input').disabled}:${getComputedStyle(label).borderTopStyle}`)");
        borders.Should().OnlyContain(border => border == "false:solid" || border == "true:dashed");
        var overflow = await Page.EvaluateAsync<double>("() => document.documentElement.scrollWidth - window.innerWidth");
        overflow.Should().BeLessThanOrEqualTo(0);

        await SaveScreenshotAsync($"owner-pictures-{width}");
        ConsoleErrors.Should().BeEmpty();
        RequestHosts.Should().OnlyContain(host => host == $"127.0.0.1:{OwnerPort}");
    }

    private async Task StartHostAsync()
    {
        var items = SyntheticBggCollection.Create(5);
        var images = new ScriptedImageHandler();

        foreach (var item in items.Where(candidate => candidate.Owned && !candidate.IsExpansion && candidate.Version is not null))
        {
            images.Serve($"{ImageHost}version-{BggXml.VersionId(item)}.jpg", FlatCover);
            images.Serve($"{ImageHost}{item.ObjectId}.jpg", ColouredCover);
        }

        var clock = SyncHarness.NewClock();
        var factory = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(items),
            clock,
            new Dictionary<string, string?>
            {
                ["Images:AllowedHosts"] = "example.org",
                ["Images:MaxDownloadsPerRun"] = "1000",
                ["Storage:Directory"] = _storage.FullPath,
            },
            images);

        await StartAsync(factory);
        _publicClient = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(_publicClient, clock, advance: false);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        _publicClient?.Dispose();
        await base.DisposeAsync();
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }
}

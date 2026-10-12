using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that every game on the owner's pictures page shows its candidates as four mounted pictures in their
/// true proportions, that a tap on a tile saves the choice once with an Undo, and that the public card follows the choice.
/// </summary>
[Trait("Category", "Browser")]
public sealed class OwnerPictureTilesTests : CabinetPageTest
{
    private const string ImageHost = "https://example.org/images/";
    private const string CardsPath = "/cabinet/cards?profile=desktop";
    private const string ChoosePathPart = "handler=Choose";
    private const string RowTitle = "Example Game 1";

    private static readonly byte[] FlatCover = SyntheticArt.Encode(SyntheticArtKind.FlatCover);
    private static readonly byte[] ColouredCover = SyntheticArt.Encode(SyntheticArtKind.CoverColouredField);

    private readonly TemporaryDirectory _storage = new();
    private HttpClient? _publicClient;

    [Fact]
    public async Task A_tap_on_the_main_picture_tile_saves_it_once_with_an_undo_and_the_public_card_follows()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();
        var before = await CoverOfAsync(RowTitle);
        var posts = 0;
        Page.Request += (_, request) =>
        {
            if (request.Method == "POST" && request.Url.Contains(ChoosePathPart, StringComparison.Ordinal))
            {
                Interlocked.Increment(ref posts);
            }
        };

        await GotoOwnerAsync("/owner/pictures");
        var row = RowOf(RowTitle);
        await Expect(row.Locator("fieldset.pic-choices label.pic-tile")).ToHaveCountAsync(4);
        (await row.Locator("label.pic-tile .pic-label").AllInnerTextsAsync()).Should().Equal("Automatic", "Owned edition", "Main picture", "Your link");
        await Expect(row.Locator("fieldset legend")).ToHaveTextAsync($"Picture for {RowTitle}");
        await Expect(row.Locator("label.pic-tile img[src^='/art/']")).ToHaveCountAsync(3);
        await Expect(row.Locator("img[src^='/art/']").First).ToHaveJSPropertyAsync("complete", true);

        await TileOf(row, "Main picture").ClickAsync();

        await Expect(row.Locator("input[value=MainPicture]")).ToBeCheckedAsync();
        await Expect(row.Locator(".row-status")).ToContainTextAsync("Saved");
        await Expect(row.Locator(".row-status button")).ToHaveTextAsync("Undo");
        posts.Should().Be(1);
        (await CoverOfAsync(RowTitle)).Should().NotBe(before);
        (await TileOf(row, "Main picture").EvaluateAsync<string>("tile => getComputedStyle(tile).boxShadow")).Should().Contain("inset");

        await row.Locator(".row-status button").ClickAsync();

        await Expect(row.Locator("input[value=Automatic]")).ToBeCheckedAsync();
        await Expect(row.Locator(".row-status")).ToContainTextAsync("Undone");
        (await CoverOfAsync(RowTitle)).Should().Be(before);
        await SaveScreenshotAsync("owner-pictures-tiles-1440");
        ConsoleErrors.Should().BeEmpty();
        RequestHosts.Should().OnlyContain(host => host == $"127.0.0.1:{OwnerPort}");
    }

    private ILocator RowOf(string title) =>
        Page.Locator(".pic-row", new PageLocatorOptions { Has = Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = title, Exact = true }) });

    private static ILocator TileOf(ILocator row, string label) =>
        row.Locator("label.pic-tile", new LocatorLocatorOptions { Has = row.Page.Locator(".pic-label", new PageLocatorOptions { HasTextString = label }) });

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

    private async Task<string> CoverOfAsync(string title)
    {
        var json = await _publicClient!.GetStringAsync(CardsPath, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);

        return document.RootElement.GetProperty("cards").EnumerateArray()
            .First(card => card.GetProperty("title").GetString() == title)
            .GetProperty("cover").GetProperty("url").GetString()!;
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

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
        var posts = CountChoosePosts();

        await GotoOwnerAsync("/owner/pictures?view=all");
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
        posts().Should().Be(1);
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

    [Fact]
    public async Task The_four_tiles_are_equal_in_height_and_each_picture_keeps_its_proportions_and_the_states_read_clearly()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();

        await GotoOwnerAsync("/owner/pictures?view=all");
        await Expect(Page.Locator(".pic-art > img").First).ToHaveJSPropertyAsync("complete", true);

        var heights = await Page.Locator(".pic-row").EvaluateAllAsync<double[][]>(
            "rows => rows.map(row => Array.from(row.querySelectorAll('.pic-tile')).map(tile => Math.round(tile.getBoundingClientRect().height)))");
        heights.Should().OnlyContain(row => row.Distinct().Count() == 1);
        var shapes = await Page.Locator(".pic-art > img").EvaluateAllAsync<double[][]>(
            "images => images.filter(image => image.naturalWidth > 0).map(image => [image.getBoundingClientRect().width, image.getBoundingClientRect().height, image.naturalWidth, image.naturalHeight])");
        shapes.Should().NotBeEmpty();
        shapes.Should().OnlyContain(shape => Math.Abs(shape[0] - (shape[1] * shape[2] / shape[3])) <= 1);

        var extras = RowOf("Example Game 1: Lantern Extras");
        await Expect(TileOf(extras, "Automatic").Locator(".pic-art[data-art='cover'] .card-plate-label")).ToHaveTextAsync("Example Game 1: Lantern Extras");
        await Expect(TileOf(extras, "Automatic").Locator(".pic-sub")).ToHaveTextAsync("Uses a generated cover");
        await Expect(extras.Locator(".pic-tile .pic-tag")).ToHaveCountAsync(0);

        var ribbon = RowOf("Ribbon Orchard");
        await Expect(ribbon.Locator("label.pic-tile")).ToHaveCountAsync(4);
        await Expect(TileOf(ribbon, "Owned edition").Locator(".pic-sub")).ToHaveTextAsync("BGG has none");
        await Expect(ribbon.Locator("input[value=OwnedEdition]")).ToBeDisabledAsync();
        await Expect(ribbon.Locator("input[value=OwnedEdition]")).ToHaveAttributeAsync("aria-disabled", "true");
        (await TileOf(ribbon, "Owned edition").EvaluateAsync<string>("tile => getComputedStyle(tile).borderTopStyle")).Should().Be("dashed");
        await Expect(TileOf(ribbon, "Main picture").Locator(".pic-art > img")).ToHaveCountAsync(1);
        await Expect(TileOf(ribbon, "Automatic").Locator(".pic-sub")).ToHaveTextAsync("Same as Main picture");
        await Expect(TileOf(ribbon, "Main picture").Locator(".pic-tag")).ToHaveTextAsync("Automatic's pick");
        await Expect(TileOf(ribbon, "Your link").Locator(".pic-sub")).ToHaveTextAsync("None yet");
        await Expect(ribbon.Locator("input[value=Link]")).ToBeDisabledAsync();
        await Expect(RowOf(RowTitle).Locator(".pic-verdict").First).ToHaveTextAsync("Reads as a flat cover");

        var checkedShadow = await RowOf(RowTitle).Locator("input[value=Automatic]").EvaluateAsync<string>(
            "input => getComputedStyle(input.closest('.pic-tile')).boxShadow");
        checkedShadow.Should().Contain("inset");
        await SaveScreenshotAsync("owner-pictures-states-1440");
        ConsoleErrors.Should().BeEmpty();
        RequestHosts.Should().OnlyContain(host => host == $"127.0.0.1:{OwnerPort}");
    }

    [Fact]
    public async Task Arrowing_through_the_radios_and_waiting_posts_exactly_once()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();
        var posts = CountChoosePosts();

        await GotoOwnerAsync("/owner/pictures?view=all");
        var row = RowOf(RowTitle);
        await row.Locator("input[value=Automatic]").FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(row.Locator("input[value=MainPicture]")).ToBeCheckedAsync();
        await Task.Delay(900, TestContext.Current.CancellationToken);

        posts().Should().Be(1);
        await Expect(row.Locator(".row-status")).ToContainTextAsync("Saved");
        await GotoOwnerAsync("/owner/pictures?view=all");
        await Expect(RowOf(RowTitle).Locator("input[value=MainPicture]")).ToBeCheckedAsync();
    }

    [Fact]
    public async Task A_failed_save_says_so_without_an_undo_and_puts_the_radio_back()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();
        await GotoOwnerAsync("/owner/pictures?view=all");
        await Page.RouteAsync("**/owner/pictures?handler=Choose", route => route.FulfillAsync(new RouteFulfillOptions { Status = 500, Body = string.Empty }));

        var row = RowOf(RowTitle);
        await TileOf(row, "Main picture").ClickAsync();

        await Expect(row.Locator(".row-status")).ToContainTextAsync("Couldn't save that. Nothing changed. Try again.");
        await Expect(row.Locator(".row-status button")).ToHaveCountAsync(0);
        await Expect(row.Locator("input[value=Automatic]")).ToBeCheckedAsync();
        await Expect(row.Locator("input[value=MainPicture]")).Not.ToBeCheckedAsync();
    }

    [Theory]
    [InlineData(390)]
    [InlineData(320)]
    public async Task On_a_phone_the_tiles_go_two_to_a_row_and_nothing_scrolls_sideways(int width)
    {
        await Page.SetViewportSizeAsync(width, 844);
        await StartHostAsync();

        await GotoOwnerAsync("/owner/pictures?view=all");
        await Expect(Page.Locator(".pic-art > img").First).ToHaveJSPropertyAsync("complete", true);

        var tops = await RowOf(RowTitle).Locator(".pic-tile").EvaluateAllAsync<double[]>("tiles => tiles.map(tile => Math.round(tile.getBoundingClientRect().top))");
        tops.Distinct().Count().Should().Be(2);
        tops[0].Should().Be(tops[1]);
        tops[2].Should().Be(tops[3]);
        var heights = await Page.Locator(".pic-tile").EvaluateAllAsync<double[]>("tiles => tiles.map(tile => tile.getBoundingClientRect().height)");
        heights.Should().OnlyContain(height => height >= 44);
        var captionTops = await Page.Locator(".pic-caption").EvaluateAllAsync<double[][]>(
            "captions => captions.map(caption => [caption.querySelector('input').getBoundingClientRect().top, caption.querySelector('.pic-label').getBoundingClientRect().top])");
        captionTops.Should().NotBeEmpty();
        captionTops.Should().OnlyContain(pair => Math.Abs(pair[0] - pair[1]) <= 4, "each radio and its label share a top edge, so the captions line up");
        var emptyArt = await Page.Locator(".pic-tile[data-state='none'] > .pic-art").First.EvaluateAsync<string>("art => getComputedStyle(art).borderTopColor");
        emptyArt.Should().Be("rgba(0, 0, 0, 0)", "the empty link tile has one dashed frame, its own");
        var overflow = await Page.EvaluateAsync<double>("() => document.documentElement.scrollWidth - window.innerWidth");
        overflow.Should().BeLessThanOrEqualTo(0);
        await SaveScreenshotAsync($"owner-pictures-states-{width}");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Without_script_save_picture_posts_the_row_and_returns_to_it_and_the_public_card_follows()
    {
        await StartHostAsync();
        var before = await CoverOfAsync(RowTitle);
        var plain = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            JavaScriptEnabled = false,
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
            ExtraHTTPHeaders = new Dictionary<string, string> { ["Accept-Encoding"] = "identity" },
        });
        var page = await plain.NewPageAsync();
        await page.GotoAsync($"http://127.0.0.1:{OwnerPort}/owner/pictures?view=all");

        var row = page.Locator(".pic-row", new PageLocatorOptions { Has = page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = RowTitle, Exact = true }) });
        await row.Locator("label.pic-tile", new LocatorLocatorOptions { HasText = "Main picture" }).ClickAsync();
        await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Save picture" }).ClickAsync();

        await Expect(page.Locator("#owner-notice")).ToHaveTextAsync("Saved.");
        page.Url.Should().MatchRegex(@"/owner/pictures\?done=saved&view=all#game-\d+$");
        await Expect(row.Locator("input[value=MainPicture]")).ToBeCheckedAsync();
        (await CoverOfAsync(RowTitle)).Should().NotBe(before);
        await plain.CloseAsync();
    }

    [Fact]
    public async Task With_no_games_synced_the_page_says_so_in_two_parts()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await StartHostAsync(sync: false);

        await GotoOwnerAsync("/owner/pictures?view=all");

        await Expect(Page.Locator(".pic-empty h3")).ToHaveTextAsync("No games yet.");
        await Expect(Page.Locator(".pic-empty p")).ToHaveTextAsync("Your games show up here after the first sync.");
        await Expect(Page.Locator(".pic-row")).ToHaveCountAsync(0);
        ConsoleErrors.Should().BeEmpty();
    }

    private Func<int> CountChoosePosts()
    {
        var posts = 0;
        Page.Request += (_, request) =>
        {
            if (request.Method == "POST" && request.Url.Contains(ChoosePathPart, StringComparison.Ordinal))
            {
                Interlocked.Increment(ref posts);
            }
        };

        return () => Volatile.Read(ref posts);
    }

    private ILocator RowOf(string title) =>
        Page.Locator(".pic-row", new PageLocatorOptions { Has = Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = title, Exact = true }) });

    private static ILocator TileOf(ILocator row, string label) =>
        row.Locator("label.pic-tile", new LocatorLocatorOptions { Has = row.Page.Locator(".pic-label", new PageLocatorOptions { HasTextString = label }) });

    private async Task StartHostAsync(bool sync = true)
    {
        var items = SyntheticBggCollection.Create(5).Append(
            new FakeBggItem(SyntheticBggCollection.FirstObjectId + 90, SyntheticBggCollection.FirstCollId + 90, "Ribbon Orchard", false, true, 1987, null, null)).ToList();
        var images = new ScriptedImageHandler();

        foreach (var item in items.Where(candidate => candidate.Owned && !candidate.IsExpansion))
        {
            if (item.Version is not null)
            {
                images.Serve($"{ImageHost}version-{BggXml.VersionId(item)}.jpg", FlatCover);
            }

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

        if (sync)
        {
            await SyncRounds.PressAndWait(_publicClient, clock, advance: false);
        }
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

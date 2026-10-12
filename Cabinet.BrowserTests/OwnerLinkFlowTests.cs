using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the owner can paste the address of a gallery picture, see it on a small shelf as the face-out
/// box and the spine at cabinet scale, and use it: the button says it is checking while the server works, the preview is served
/// by the site itself, using it fills the Your link tile, marks the row, offers Undo and shows on every visitor's card, an address
/// that cannot be used is answered inline, a preview can be discarded, the whole flow works without script, and the preview
/// fits a 320 pixel screen.
/// </summary>
[Trait("Category", "Browser")]
public sealed class OwnerLinkFlowTests : CabinetPageTest
{
    private const string ImageHost = "https://example.org/images/";
    private const string LinkUrl = "https://example.org/owner/linked-picture.png";
    private const string OtherHostUrl = "https://img.example.net/owner/linked-picture.png";
    private const string CardsPath = "/cabinet/cards?profile=desktop";
    private const string CheckFragment = "handler=CheckLink";
    private const string RowTitle = "Example Game 1";
    private const double PixelsPerMillimetre = 0.5;

    private static readonly byte[] FlatCover = SyntheticArt.Encode(SyntheticArtKind.FlatCover);
    private static readonly byte[] LinkedCover = SyntheticArt.Encode(SyntheticArtKind.CoverColouredField);

    private readonly TemporaryDirectory _storage = new();
    private HttpClient? _publicClient;
    private CabinetWebApplicationFactory? _factory;

    [Fact]
    public async Task Pasting_a_link_shows_the_box_and_the_spine_on_a_small_shelf_and_using_it_fills_the_tile_and_every_card_shows_it()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();
        var before = await CoverOfAsync(RowTitle);
        await GotoPicturesAsync();
        var row = RowOf(RowTitle);
        var revision = Owner.Revision;
        var gate = new TaskCompletionSource();
        await Page.RouteAsync(new Regex(CheckFragment, RegexOptions.IgnoreCase), async route =>
        {
            await gate.Task;
            await route.ContinueAsync();
        });

        await row.Locator("summary").ClickAsync();
        var field = row.GetByLabel("Picture link");
        await field.FillAsync(LinkUrl);
        await row.GetByRole(AriaRole.Button, new() { Name = "Check link" }).ClickAsync();

        var checking = row.Locator("button[data-check-link]");
        await Expect(checking).ToHaveTextAsync("Checking...");
        await Expect(checking).ToHaveAttributeAsync("aria-disabled", "true");
        await Expect(field).ToHaveJSPropertyAsync("readOnly", true);
        gate.SetResult();

        var preview = row.Locator(".pic-preview");
        await Expect(preview).ToBeVisibleAsync();
        await Expect(preview.GetByRole(AriaRole.Heading, new() { Name = "Preview" })).ToBeFocusedAsync();
        await Expect(checking).ToHaveTextAsync("Check link");
        await Expect(field).ToHaveJSPropertyAsync("readOnly", false);
        await Expect(preview.Locator(".pic-preview-sub")).ToHaveTextAsync("How this picture will look in the cabinet.");
        await Expect(preview.Locator(".preview-caption[data-slot=box]")).ToHaveTextAsync("Box front");
        await Expect(preview.Locator(".preview-caption[data-slot=spine]")).ToHaveTextAsync("Spine");
        await Expect(preview.Locator("[data-preview-verdict]")).Not.ToBeEmptyAsync();
        await Expect(preview.Locator("[data-preview-shape]")).ToContainTextAsync("Box shape:");
        await Expect(preview.Locator("[data-preview-disclaimer]")).ToHaveTextAsync("The site can't check that this picture belongs to this game.");
        var image = preview.Locator(".placement[data-kind=cover] img");
        await Expect(image).ToHaveAttributeAsync("src", new Regex("^/owner/preview/[0-9a-f]{32}$"));
        await Expect(image).ToHaveJSPropertyAsync("complete", true);
        (await image.EvaluateAsync<double>("element => element.naturalWidth")).Should().BeGreaterThan(0);
        Owner.Revision.Should().Be(revision);

        await AssertMiniShelfAsync(preview);
        await SaveScreenshotAsync("owner-link-preview-1440");

        var posts = CountPosts();
        await preview.GetByRole(AriaRole.Button, new() { Name = "Use this" }).ClickAsync();

        var status = row.Locator(":scope > .row-status");
        await Expect(status).ToContainTextAsync("Saved");
        await Expect(status.GetByRole(AriaRole.Button, new() { Name = "Undo" })).ToBeVisibleAsync();
        await Expect(row.Locator(".pic-preview")).ToHaveCountAsync(0);
        (await row.Locator("details.pic-linkbox").EvaluateAsync<bool>("element => element.open")).Should().BeFalse();
        var linkTile = row.Locator("label.pic-tile[data-kind=Link]");
        await Expect(linkTile.Locator("input")).ToBeCheckedAsync();
        await Expect(linkTile.Locator("input")).ToBeFocusedAsync();
        await Expect(linkTile.Locator("img")).ToHaveAttributeAsync("src", new Regex("^/art/[0-9a-f]{16}-[0-9]+\\.webp$"));
        await Expect(linkTile.Locator(".pic-address")).ToHaveTextAsync("example.org/.../linked-picture.png");
        await Expect(row.Locator("[data-mark=yours]")).ToHaveTextAsync("Yours");
        posts().Should().Be(1);
        var saved = Owner.State is OwnerDataState.Ready ready ? ready.Data.Games.Single().Picture : null;
        saved!.Choice.Should().Be(PictureChoice.Link);
        saved.Link!.Url.Should().Be(LinkUrl);
        (await CoverOfAsync(RowTitle)).Should().NotBe(before);
        await SaveScreenshotAsync("owner-link-used-1440");

        await status.GetByRole(AriaRole.Button, new() { Name = "Undo" }).ClickAsync();

        await Expect(status).ToContainTextAsync("Undone.");
        await Expect(row.Locator("[data-mark=yours]")).ToHaveCountAsync(0);
        await Expect(row.Locator("label.pic-tile[data-kind=Link] input")).Not.ToBeCheckedAsync();
        await Expect(row.Locator("input[value=Automatic]")).ToBeCheckedAsync();
        (await CoverOfAsync(RowTitle)).Should().Be(before);
        AssertCleanConsole();
    }

    [Fact]
    public async Task Discarding_a_preview_removes_it_returns_focus_to_the_field_and_changes_nothing()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();
        await GotoPicturesAsync();
        var row = RowOf(RowTitle);
        var revision = Owner.Revision;
        await row.Locator("summary").ClickAsync();
        await row.GetByLabel("Picture link").FillAsync(LinkUrl);
        await row.GetByRole(AriaRole.Button, new() { Name = "Check link" }).ClickAsync();
        var token = await row.Locator(".pic-preview").GetAttributeAsync("data-token");

        await row.GetByRole(AriaRole.Button, new() { Name = "Discard preview" }).ClickAsync();

        await Expect(row.Locator(".pic-preview")).ToHaveCountAsync(0);
        await Expect(row.GetByLabel("Picture link")).ToBeFocusedAsync();
        Owner.Revision.Should().Be(revision);
        using var owner = _factory!.CreateOwnerClient();
        await Expect(Page).ToHaveURLAsync(new Regex("/owner/pictures"));
        using var served = await owner.GetAsync($"/owner/preview/{token}", TestContext.Current.CancellationToken);
        served.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        AssertCleanConsole();
    }

    [Fact]
    public async Task An_address_that_cannot_be_used_is_answered_inline_with_the_field_marked_and_focused_and_nothing_stored()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();
        await GotoPicturesAsync();
        var row = RowOf(RowTitle);
        var revision = Owner.Revision;
        await row.Locator("summary").ClickAsync();
        var field = row.GetByLabel("Picture link");

        await field.FillAsync(OtherHostUrl);
        await row.GetByRole(AriaRole.Button, new() { Name = "Check link" }).ClickAsync();

        var alert = row.Locator(".pic-link-error");
        await Expect(alert).ToBeVisibleAsync();
        await Expect(alert).ToHaveAttributeAsync("role", "alert");
        await Expect(alert).ToHaveTextAsync("Couldn't use that link. Check it and try again.");
        await Expect(field).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(field).ToBeFocusedAsync();
        await Expect(row.Locator(".pic-preview")).ToHaveCountAsync(0);
        Owner.Revision.Should().Be(revision);

        await field.FillAsync(LinkUrl);

        await Expect(alert).ToBeHiddenAsync();
        await Expect(field).Not.ToHaveAttributeAsync("aria-invalid", "true");
        ConsoleErrors.Where(error => !error.Contains("Failed to load resource", StringComparison.Ordinal)).Should().BeEmpty();
    }

    [Theory]
    [InlineData(390, 844)]
    [InlineData(320, 640)]
    public async Task The_preview_fits_the_screen_and_the_shelf_never_scrolls_sideways_on_a_phone(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartHostAsync();
        await GotoPicturesAsync();
        var row = RowOf(RowTitle);
        await row.Locator("summary").ClickAsync();
        await row.GetByLabel("Picture link").FillAsync(LinkUrl);
        await row.GetByRole(AriaRole.Button, new() { Name = "Check link" }).ClickAsync();
        var preview = row.Locator(".pic-preview");
        await Expect(preview).ToBeVisibleAsync();
        await Expect(preview.Locator(".placement[data-kind=cover] img")).ToHaveJSPropertyAsync("complete", true);
        await SaveScreenshotAsync($"owner-link-preview-{width}");

        var overflow = await Page.EvaluateAsync<double>("() => document.documentElement.scrollWidth - window.innerWidth");
        var shelf = await preview.Locator(".preview-shelf").EvaluateAsync<double[]>("element => { const box = element.getBoundingClientRect(); const outer = element.parentElement.getBoundingClientRect(); return [box.right, outer.right, element.scrollWidth - element.clientWidth]; }");
        var targets = await preview.Locator("button").EvaluateAllAsync<double[][]>("elements => elements.map(element => [element.getBoundingClientRect().width, element.getBoundingClientRect().height])");

        overflow.Should().BeLessThanOrEqualTo(0);
        shelf[0].Should().BeLessThanOrEqualTo(shelf[1] + 0.5);
        shelf[2].Should().BeLessThanOrEqualTo(0);
        targets.Should().NotBeEmpty().And.OnlyContain(size => size[1] >= 44 && size[0] >= 44);
        await AssertMiniShelfAsync(preview, exactScale: width >= 390);
        AssertCleanConsole();
    }

    [Fact]
    public async Task Without_script_checking_a_link_shows_the_preview_on_the_games_own_page_and_Use_this_stores_it()
    {
        await StartHostAsync();
        var plain = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            JavaScriptEnabled = false,
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
            ExtraHTTPHeaders = new Dictionary<string, string> { ["Accept-Encoding"] = "identity" },
        });
        var page = await plain.NewPageAsync();
        var gameId = (await CardsAsync()).First(card => card.Title == RowTitle).GameId;
        await page.GotoAsync($"http://127.0.0.1:{OwnerPort}/owner/pictures?game={gameId}");

        await page.Locator("summary").ClickAsync();
        await page.GetByLabel("Picture link").FillAsync(LinkUrl);
        await page.GetByRole(AriaRole.Button, new() { Name = "Check link" }).ClickAsync();

        await Expect(page).ToHaveURLAsync(new Regex($@"/owner/pictures\?game={gameId}&preview=[0-9a-f]{{32}}#game-{gameId}$"));
        var preview = page.Locator(".pic-preview");
        await Expect(preview.GetByRole(AriaRole.Heading, new() { Name = "Preview" })).ToBeVisibleAsync();
        await Expect(preview.Locator("svg")).ToHaveCountAsync(2);
        await Expect(preview.Locator("[data-preview-disclaimer]")).ToBeVisibleAsync();
        await Expect(preview.Locator("[data-preview-verdict]")).Not.ToBeEmptyAsync();
        var boxImage = preview.Locator("svg image");
        await Expect(boxImage).ToHaveAttributeAsync("href", new Regex("^/owner/preview/[0-9a-f]{32}$"));
        (await page.Locator("details.pic-linkbox").EvaluateAsync<bool>("element => element.open")).Should().BeTrue();
        await preview.GetByRole(AriaRole.Button, new() { Name = "Use this" }).ClickAsync();

        await Expect(page).ToHaveURLAsync(new Regex($@"/owner/pictures\?done=saved&game={gameId}#game-{gameId}$"));
        await Expect(page.Locator("#owner-notice")).ToHaveTextAsync("Saved.");
        await Expect(page.Locator("input[value=Link]")).ToBeCheckedAsync();
        await Expect(page.Locator(".pic-preview")).ToHaveCountAsync(0);
        await Expect(page.Locator("label.pic-tile[data-kind=Link] img")).ToHaveAttributeAsync("src", new Regex("^/art/"));
        await plain.CloseAsync();
    }

    [Fact]
    public async Task A_preview_that_ran_out_before_it_was_used_says_so_and_stores_nothing()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();
        await GotoPicturesAsync();
        var row = RowOf(RowTitle);
        var revision = Owner.Revision;
        await row.Locator("summary").ClickAsync();
        await row.GetByLabel("Picture link").FillAsync(LinkUrl);
        await row.GetByRole(AriaRole.Button, new() { Name = "Check link" }).ClickAsync();
        await Expect(row.Locator(".pic-preview")).ToBeVisibleAsync();
        _factory!.ServingServices.GetRequiredService<StagedItems<StagedLink>>().Remove(await row.Locator(".pic-preview").GetAttributeAsync("data-token"));

        await row.GetByRole(AriaRole.Button, new() { Name = "Use this" }).ClickAsync();

        await Expect(row.Locator(".pic-link-error")).ToHaveTextAsync("This preview expired. Check the link again.");
        await Expect(row.Locator(".pic-preview")).ToHaveCountAsync(0);
        await Expect(row.GetByLabel("Picture link")).ToBeFocusedAsync();
        Owner.Revision.Should().Be(revision);
        ConsoleErrors.Where(error => !error.Contains("Failed to load resource", StringComparison.Ordinal)).Should().BeEmpty();
    }

    private OwnerDataService Owner => _factory!.ServingServices.GetRequiredService<OwnerDataService>();

    private ILocator RowOf(string title) =>
        Page.Locator(".pic-row", new PageLocatorOptions { Has = Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = title, Exact = true }) });

    private async Task GotoPicturesAsync()
    {
        await GotoOwnerAsync("/owner/pictures?view=all");
        await Page.WaitForSelectorAsync("#pic-list[data-enhanced]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
    }

    private Func<int> CountPosts()
    {
        var posts = 0;
        Page.Request += (_, request) =>
        {
            if (request.Method == "POST" && request.Url.Contains("handler=UseLink", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref posts);
            }
        };

        return () => Volatile.Read(ref posts);
    }

    private async Task AssertMiniShelfAsync(ILocator preview, bool exactScale = true)
    {
        var shapes = await preview.EvaluateAsync<double[]>(
            """
            element => {
              const rect = (selector) => element.querySelector(selector).getBoundingClientRect();
              const box = rect('.placement[data-kind=cover]');
              const spine = rect('.placement[data-kind=spine]');
              const boxSlot = element.querySelector('.preview-slot[data-slot=box]');
              const spineSlot = element.querySelector('.preview-slot[data-slot=spine]');
              const mm = (slot, name) => Number.parseFloat(slot.style.getPropertyValue(name));
              return [box.width, box.height, spine.width, spine.height, box.bottom, spine.bottom, spine.left - box.right,
                mm(boxSlot, '--w'), mm(boxSlot, '--h'), mm(spineSlot, '--w'), mm(spineSlot, '--h')];
            }
            """);

        shapes[4].Should().BeApproximately(shapes[5], 1.0, "the box and the spine stand on the same board");
        shapes[6].Should().BeGreaterThanOrEqualTo(15.5, "the two are at least 16 pixels apart");
        (shapes[0] / shapes[1]).Should().BeApproximately(shapes[7] / shapes[8], 0.02, "the box is drawn in the proportions of the cabinet's box");
        (shapes[2] / shapes[3]).Should().BeApproximately(shapes[9] / shapes[10], 0.03, "the spine is drawn in the proportions of the cabinet's spine");

        if (exactScale)
        {
            shapes[0].Should().BeApproximately(shapes[7] * PixelsPerMillimetre, 1.0, "the box is drawn at half a pixel to the millimetre");
            shapes[3].Should().BeApproximately(shapes[10] * PixelsPerMillimetre, 1.0);
        }

        var label = await preview.Locator(".placement[data-kind=spine] .placement-label").InnerTextAsync();
        var background = await preview.Locator(".placement[data-kind=spine]").EvaluateAsync<string>("element => getComputedStyle(element).backgroundColor");
        background.Should().MatchRegex("^rgb\\(");
        label.Should().NotBeNull();
    }

    private void AssertCleanConsole()
    {
        ConsoleErrors.Should().BeEmpty();
        RequestHosts.Should().OnlyContain(host => host == $"127.0.0.1:{OwnerPort}");
    }

    private async Task StartHostAsync()
    {
        var items = SyntheticBggCollection.Create(5);
        var images = new ScriptedImageHandler().Serve(LinkUrl, LinkedCover);

        foreach (var item in items.Where(candidate => candidate.Owned && !candidate.IsExpansion))
        {
            if (item.Version is not null)
            {
                images.Serve($"{ImageHost}version-{BggXml.VersionId(item)}.jpg", FlatCover);
            }

            images.Serve($"{ImageHost}{item.ObjectId}.jpg", FlatCover);
        }

        var clock = SyncHarness.NewClock();
        _factory = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(items),
            clock,
            new Dictionary<string, string?>
            {
                ["Images:AllowedHosts"] = "example.org",
                ["Images:MaxDownloadsPerRun"] = "1000",
                ["Storage:Directory"] = _storage.FullPath,
            },
            images);

        await StartAsync(_factory);
        _publicClient = _factory.CreatePublicClient();
        await SyncRounds.PressAndWait(_publicClient, clock, advance: false);
    }

    private async Task<IReadOnlyList<CardReading>> CardsAsync()
    {
        var json = await _publicClient!.GetStringAsync(CardsPath, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);

        return
        [
            .. document.RootElement.GetProperty("cards").EnumerateArray().Select(card => new CardReading(
                card.GetProperty("gameId").GetInt32(),
                card.GetProperty("title").GetString()!,
                card.TryGetProperty("cover", out var cover) && cover.ValueKind == JsonValueKind.Object ? cover.GetProperty("url").GetString() : null)),
        ];
    }

    private async Task<string?> CoverOfAsync(string title) => (await CardsAsync()).First(card => card.Title == title).CoverUrl;

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        _publicClient?.Dispose();
        await base.DisposeAsync();
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record CardReading(int GameId, string Title, string? CoverUrl);
}

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
using Microsoft.Extensions.Time.Testing;
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
    private const string SecondLinkUrl = "https://example.org/owner/second-picture.png";
    private const string PictureUrl = "https://example.org/owner/shot.png";
    private const string SlowUrl = "https://example.org/owner/slow.png";
    private const string TooLargeUrl = "https://example.org/owner/too-large.png";
    private const string UnreadableUrl = "https://example.org/owner/not-a-picture.png";
    private const string HugeUrl = "https://example.org/owner/huge.png";
    private const string ThinUrl = "https://example.org/owner/thin.png";
    private const string NoSizeTitle = "Lantern & Harbour";
    private const string LongLinkUrl = "https://example.org/owner/a-very-long-picture-file-name-that-keeps-going-and-going-and-going.png";
    private const string CardsPath = "/cabinet/cards?profile=desktop";
    private const string CheckFragment = "handler=CheckLink";
    private const string RowTitle = "Example Game 1";
    private const double PixelsPerMillimetre = 0.5;

    private static readonly Dictionary<string, (string Address, string Message)> Refusals = new()
    {
        ["blank"] = (string.Empty, "Paste a picture link first."),
        ["not-a-link"] = ("not a link", "That isn't a web address. Paste the full picture link, starting with https://."),
        ["page-link"] = (
            "https://boardgamegeek.com/image/1234567/example-game",
            "That's a page link, not a picture link. In BGG's gallery, open the picture and choose \"Copy image address\", then paste that here."),
        ["other-host"] = (
            OtherHostUrl,
            "That link isn't from BGG's picture host. Only links from example.org work here. Copy the picture's address from BGG's gallery."),
        ["not-found"] = (
            "https://example.org/owner/missing.png",
            "That picture isn't there. Check that you copied the whole link."),
        ["timeout"] = (
            SlowUrl,
            "Couldn't fetch that picture. The link may be wrong or the picture host may be busy. Check the link and try again."),
        ["too-large"] = (
            TooLargeUrl,
            "That picture is too large to use (1.3 MB; the limit is 1 MB). Pick a smaller version from the gallery."),
        ["unreadable"] = (UnreadableUrl, "That isn't a picture the cabinet can read. Try another one."),
        ["too-many-pixels"] = (HugeUrl, "That picture has too many pixels to process. Pick a smaller version from the gallery."),
        ["extreme-shape"] = (ThinUrl, "That picture is very long and thin, so it can't be a box front. Try another one."),
    };

    private static readonly byte[] FlatCover = SyntheticArt.Encode(SyntheticArtKind.FlatCover);
    private static readonly byte[] LinkedCover = SyntheticArt.Encode(SyntheticArtKind.CoverColouredField);

    private readonly TemporaryDirectory _storage = new();
    private readonly ManualResetEventSlim _linkGate = new(true);
    private ScriptedImageHandler? _images;
    private FakeTimeProvider? _clock;
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

    [Theory]
    [InlineData("blank")]
    [InlineData("not-a-link")]
    [InlineData("page-link")]
    [InlineData("other-host")]
    [InlineData("not-found")]
    [InlineData("timeout")]
    [InlineData("too-large")]
    [InlineData("unreadable")]
    [InlineData("too-many-pixels")]
    [InlineData("extreme-shape")]
    public async Task Every_address_that_cannot_be_used_is_answered_inline_in_its_own_words_with_the_field_marked_and_focused_and_nothing_stored(string name)
    {
        var (address, message) = Refusals[name];
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync(
            script: images =>
            {
                images.ServeStalledBody(SlowUrl);
                images.Serve(TooLargeUrl, new byte[1_300_000]);
                images.Serve(UnreadableUrl, new byte[2_000]);
                images.ServePicture(HugeUrl, 1200, 1200);
                images.ServePicture(ThinUrl, 2000, 100);
            },
            extraSettings: new Dictionary<string, string?> { ["Images:MaxMegabytes"] = "1", ["Images:MaxMegapixels"] = "1" });
        await GotoPicturesAsync();
        var row = RowOf(RowTitle);
        var revision = Owner.Revision;
        await row.Locator("summary").ClickAsync();
        var field = row.GetByLabel("Picture link");
        await field.FillAsync(address);

        await row.GetByRole(AriaRole.Button, new() { Name = "Check link" }).ClickAsync();

        if (name == "timeout")
        {
            await _images!.StallReached.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            _clock!.Advance(OwnerLinkFetcher.CheckTimeout + TimeSpan.FromSeconds(1));
        }

        var alert = row.Locator(".pic-link-error");
        await Expect(alert).ToBeVisibleAsync();
        await Expect(alert).ToHaveAttributeAsync("role", "alert");
        await Expect(alert).ToHaveTextAsync(message);
        await Expect(alert.Locator("strong")).ToHaveCSSAsync("font-weight", "600");
        await Expect(field).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(field).ToHaveAttributeAsync("aria-describedby", new Regex($"pic-link-error-"));
        await Expect(field).ToBeFocusedAsync();
        await Expect(row.Locator(".pic-preview")).ToHaveCountAsync(0);
        await Expect(row.Locator("button[data-check-link]")).ToHaveTextAsync("Check link");
        Owner.Revision.Should().Be(revision);
        (await Page.EvaluateAsync<double>("() => document.documentElement.scrollWidth - window.innerWidth")).Should().BeLessThanOrEqualTo(0);
        await SaveScreenshotAsync($"owner-link-error-{name}");

        await field.FillAsync(LinkUrl);

        await Expect(alert).ToBeHiddenAsync();
        await Expect(field).Not.ToHaveAttributeAsync("aria-invalid", "true");
        ConsoleErrors.Where(error => !error.Contains("Failed to load resource", StringComparison.Ordinal)).Should().BeEmpty();
    }

    [Fact]
    public async Task Without_script_an_address_that_cannot_be_used_is_answered_on_the_page_in_its_own_words_including_the_size_of_a_picture_that_is_too_large()
    {
        await StartHostAsync(
            script: images => images.Serve(TooLargeUrl, new byte[1_300_000]),
            extraSettings: new Dictionary<string, string?> { ["Images:MaxMegabytes"] = "1" });
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

        await page.GetByLabel("Picture link").FillAsync("not a link");
        await page.GetByRole(AriaRole.Button, new() { Name = "Check link" }).ClickAsync();

        await Expect(page.Locator("#owner-notice")).ToHaveTextAsync(Refusals["not-a-link"].Message);
        await Expect(page.Locator("#owner-notice")).ToHaveAttributeAsync("role", "alert");

        await page.Locator("summary").ClickAsync();
        await page.GetByLabel("Picture link").FillAsync(TooLargeUrl);
        await page.GetByRole(AriaRole.Button, new() { Name = "Check link" }).ClickAsync();

        await Expect(page.Locator("#owner-notice")).ToHaveTextAsync(Refusals["too-large"].Message);
        await plain.CloseAsync();
    }

    [Theory]
    [InlineData(RowTitle, SyntheticArtKind.BoxOnWhite, "Reads as a 3D shot", "Box shape: from BGG's size", true)]
    [InlineData(NoSizeTitle, SyntheticArtKind.BoxOnWhite, "Reads as a 3D shot", "Box shape: estimated", true)]
    [InlineData(NoSizeTitle, SyntheticArtKind.FlatCover, "Reads as a flat cover", "Box shape: from this picture", false)]
    public async Task The_preview_says_what_the_picture_reads_as_where_the_boxs_shape_came_from_and_that_the_site_cannot_check_the_picture_belongs_to_the_game(
        string title,
        SyntheticArtKind kind,
        string verdict,
        string shape,
        bool hasNote)
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync(script: images => images.Serve(PictureUrl, SyntheticArt.Encode(kind)), lanternWithoutSize: true);
        await GotoPicturesAsync();
        var row = RowOf(title);
        var revision = Owner.Revision;
        await row.Locator("summary").ClickAsync();
        await row.GetByLabel("Picture link").FillAsync(PictureUrl);

        await row.GetByRole(AriaRole.Button, new() { Name = "Check link" }).ClickAsync();

        var preview = row.Locator(".pic-preview");
        await Expect(preview).ToBeVisibleAsync();
        await Expect(preview.Locator("[data-preview-verdict]")).ToHaveTextAsync(verdict);
        await Expect(preview.Locator("[data-preview-shape]")).ToHaveTextAsync(shape);
        await Expect(preview.Locator("[data-preview-disclaimer]")).ToHaveTextAsync("The site can't check that this picture belongs to this game.");

        if (hasNote)
        {
            await Expect(preview.Locator("[data-preview-note]")).ToHaveTextAsync(
                "This picture reads as a 3D shot. You can still use it. The box keeps its shape from BGG's size or an estimate.");
        }
        else
        {
            await Expect(preview.Locator("[data-preview-note]")).ToHaveCountAsync(0);
        }

        Owner.Revision.Should().Be(revision);
        await AssertMiniShelfAsync(preview);
        await SaveScreenshotAsync($"owner-link-preview-reads-{kind}");
    }

    [Fact]
    public async Task A_spine_in_the_preview_carries_the_shortened_title_the_cabinet_uses()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();
        await GotoPicturesAsync();
        var row = RowOf(RowTitle);
        await row.Locator("summary").ClickAsync();
        await row.GetByLabel("Picture link").FillAsync(LinkUrl);

        await row.GetByRole(AriaRole.Button, new() { Name = "Check link" }).ClickAsync();

        var label = row.Locator(".pic-preview .placement[data-kind=spine] .placement-label");
        await Expect(label).Not.ToBeEmptyAsync();
        (await label.InnerTextAsync()).Length.Should().BeLessThanOrEqualTo(RowTitle.Length);
        await Expect(row.Locator(".pic-preview h4")).ToHaveTextAsync("Preview");
    }

    [Fact]
    public async Task Switching_to_another_tile_keeps_the_Your_link_tile_and_its_copy_and_picking_it_again_saves_at_once_and_restores_Yours()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();
        await GotoPicturesAsync();
        var row = RowOf(RowTitle);
        await UseTheLinkAsync(row, LinkUrl);
        var status = row.Locator(":scope > .row-status");
        var linkTile = row.Locator("label.pic-tile[data-kind=Link]");

        await row.Locator("label.pic-tile[data-kind=Automatic]").ClickAsync();

        await Expect(status).ToContainTextAsync("Saved");
        await Expect(row.Locator("input[value=Automatic]")).ToBeCheckedAsync();
        await Expect(row.Locator("[data-mark=yours]")).ToHaveCountAsync(0);
        await Expect(linkTile.Locator("img")).ToHaveAttributeAsync("src", new Regex("^/art/"));
        await Expect(linkTile.Locator("input")).ToBeEnabledAsync();
        await Expect(linkTile.Locator(".pic-address")).ToHaveTextAsync("example.org/.../linked-picture.png");
        await Expect(linkTile.Locator(".pic-address")).ToHaveAttributeAsync("title", LinkUrl);
        await row.Locator("summary").ClickAsync();
        await Expect(row.Locator("[data-current-link]")).ToHaveTextAsync("Current link: example.org/.../linked-picture.png");
        await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Remove link" })).ToBeVisibleAsync();
        var kept = Owner.State is OwnerDataState.Ready ready ? ready.Data.Games.Single().Picture : null;
        kept!.Choice.Should().Be(PictureChoice.Automatic);
        kept.Link!.Url.Should().Be(LinkUrl);
        kept.Link.Record!.Files.Should().NotBeEmpty();
        await SaveScreenshotAsync("owner-link-kept-1440");

        await linkTile.ClickAsync();

        await Expect(status).ToContainTextAsync("Saved");
        await Expect(linkTile.Locator("input")).ToBeCheckedAsync();
        await Expect(row.Locator("[data-mark=yours]")).ToHaveTextAsync("Yours");
        ((Owner.State as OwnerDataState.Ready)!.Data.Games.Single().Picture!.Choice).Should().Be(PictureChoice.Link);
        ConsoleErrors.Where(error => !error.Contains("Failed to load resource", StringComparison.Ordinal)).Should().BeEmpty();
    }

    [Fact]
    public async Task Remove_link_removes_the_link_and_its_copy_falls_back_to_Automatic_logs_it_and_Undo_puts_it_back()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();
        var before = await CoverOfAsync(RowTitle);
        await GotoPicturesAsync();
        var row = RowOf(RowTitle);
        await UseTheLinkAsync(row, LinkUrl);
        var status = row.Locator(":scope > .row-status");
        var linkTile = row.Locator("label.pic-tile[data-kind=Link]");
        await row.Locator("summary").ClickAsync();
        var remove = row.GetByRole(AriaRole.Button, new() { Name = "Remove link" });
        (await remove.BoundingBoxAsync())!.Height.Should().BeGreaterThanOrEqualTo(44);

        await remove.ClickAsync();

        await Expect(status).ToContainTextAsync("Link removed");
        await Expect(status.GetByRole(AriaRole.Button, new() { Name = "Undo" })).ToBeVisibleAsync();
        await Expect(row.Locator("input[value=Automatic]")).ToBeCheckedAsync();
        await Expect(row.Locator("[data-mark=yours]")).ToHaveCountAsync(0);
        await Expect(linkTile.Locator(".pic-sub")).ToHaveTextAsync("None yet");
        await Expect(linkTile.Locator("img")).ToHaveCountAsync(0);
        await Expect(linkTile.Locator("input")).ToBeDisabledAsync();
        await Expect(row.Locator("[data-current-link]")).ToBeHiddenAsync();
        await Expect(remove).ToBeHiddenAsync();
        await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Check link" })).ToBeFocusedAsync();
        var data = (Owner.State as OwnerDataState.Ready)!.Data;
        data.Games.Should().BeEmpty();
        data.Changes[0].Text.Should().Be($"Removed the picture link of {RowTitle}");
        (await CoverOfAsync(RowTitle)).Should().Be(before);
        await SaveScreenshotAsync("owner-link-removed-1440");

        await status.GetByRole(AriaRole.Button, new() { Name = "Undo" }).ClickAsync();

        await Expect(status).ToContainTextAsync("Undone.");
        await Expect(linkTile.Locator("img")).ToHaveAttributeAsync("src", new Regex("^/art/"));
        await Expect(linkTile.Locator("input")).ToBeCheckedAsync();
        await Expect(row.Locator("[data-mark=yours]")).ToHaveTextAsync("Yours");
        await Expect(linkTile.Locator(".pic-address")).ToHaveTextAsync("example.org/.../linked-picture.png");
        await Expect(row.Locator("[data-current-link]")).ToBeVisibleAsync();
        await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Remove link" })).ToBeVisibleAsync();
        var restored = (Owner.State as OwnerDataState.Ready)!.Data.Games.Single().Picture!;
        restored.Choice.Should().Be(PictureChoice.Link);
        restored.Link!.Url.Should().Be(LinkUrl);
        ConsoleErrors.Where(error => !error.Contains("Failed to load resource", StringComparison.Ordinal)).Should().BeEmpty();
    }

    [Fact]
    public async Task Without_script_Remove_link_removes_the_link_and_says_so_with_Undo()
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
        await page.GetByRole(AriaRole.Button, new() { Name = "Use this" }).ClickAsync();
        await Expect(page.Locator("#owner-notice")).ToHaveTextAsync("Saved.");
        await page.Locator("summary").ClickAsync();
        await Expect(page.Locator("[data-current-link]")).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Remove link" }).ClickAsync();

        await Expect(page).ToHaveURLAsync(new Regex($@"/owner/pictures\?done=link-removed&game={gameId}#game-{gameId}$"));
        await Expect(page.Locator("#owner-notice")).ToHaveTextAsync("Link removed.");
        await Expect(page.Locator(".owner-notice").GetByRole(AriaRole.Button, new() { Name = "Undo" })).ToBeVisibleAsync();
        await Expect(page.Locator("input[value=Automatic]")).ToBeCheckedAsync();
        await Expect(page.Locator("label.pic-tile[data-kind=Link] .pic-sub")).ToHaveTextAsync("None yet");
        (Owner.State as OwnerDataState.Ready)!.Data.Games.Should().BeEmpty();

        await page.Locator(".owner-notice").GetByRole(AriaRole.Button, new() { Name = "Undo" }).ClickAsync();

        await Expect(page.Locator("input[value=Link]")).ToBeCheckedAsync();
        await plain.CloseAsync();
    }

    [Fact]
    public async Task Undo_of_a_removal_whose_copy_was_tidied_away_shows_Fetching_until_the_copy_is_stored_again()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync(background: true);
        await GotoPicturesAsync();
        var row = RowOf(RowTitle);
        await UseTheLinkAsync(row, LinkUrl);
        var status = row.Locator(":scope > .row-status");
        var linkTile = row.Locator("label.pic-tile[data-kind=Link]");
        var files = (Owner.State as OwnerDataState.Ready)!.Data.Games.Single().Picture!.Link!.Record!.Files!.Select(file => file.Name).ToList();
        await row.Locator("summary").ClickAsync();
        await row.GetByRole(AriaRole.Button, new() { Name = "Remove link" }).ClickAsync();
        await Expect(status).ToContainTextAsync("Link removed");
        foreach (var name in files)
        {
            File.Delete(Path.Combine(_storage.FullPath, "art", name));
        }

        _linkGate.Reset();
        await status.GetByRole(AriaRole.Button, new() { Name = "Undo" }).ClickAsync();

        await Expect(status).ToContainTextAsync("Undone.");
        await Expect(linkTile.Locator(".pic-sub")).ToHaveTextAsync("Fetching...");
        await Expect(linkTile.Locator("img")).ToHaveCountAsync(0);
        await Expect(row.Locator("[data-mark=fetching]")).ToHaveTextAsync("Fetching your link...");
        (await linkTile.EvaluateAsync<string>("element => getComputedStyle(element).borderTopStyle")).Should().Be("dashed");
        await Expect(linkTile.Locator(".pic-sub")).Not.ToHaveCSSAsync("animation-name", new Regex("spin"));
        await SaveScreenshotAsync("owner-link-fetching-1440");

        _linkGate.Set();
        await SyncHarness.WaitUntil(() => Task.FromResult(
            (Owner.State as OwnerDataState.Ready)!.Data.Games.Single().Picture!.Link!.Record is { Files: { Count: > 0 } stored }
            && stored.All(file => File.Exists(Path.Combine(_storage.FullPath, "art", file.Name)))));
        await GotoPicturesAsync();

        var again = RowOf(RowTitle).Locator("label.pic-tile[data-kind=Link]");
        await Expect(again.Locator("img")).ToHaveAttributeAsync("src", new Regex("^/art/"));
        await Expect(RowOf(RowTitle).Locator("[data-mark=fetching]")).ToHaveCountAsync(0);
        await Expect(again.Locator("input")).ToBeCheckedAsync();
    }

    [Theory]
    [InlineData(390, 844)]
    [InlineData(320, 640)]
    public async Task The_error_the_stored_link_line_and_Remove_link_fit_a_phone_and_a_long_address_is_cut_with_an_ellipsis(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartHostAsync(script: images => images.Serve(LongLinkUrl, LinkedCover));
        await GotoPicturesAsync();
        var row = RowOf(RowTitle);
        await UseTheLinkAsync(row, LongLinkUrl);
        await row.Locator("summary").ClickAsync();
        var field = row.GetByLabel("Picture link");
        await field.FillAsync("https://boardgamegeek.com/image/1234567/example-game");
        await row.GetByRole(AriaRole.Button, new() { Name = "Check link" }).ClickAsync();

        await Expect(row.Locator(".pic-link-error")).ToBeVisibleAsync();
        var line = row.Locator("[data-current-link]");
        await Expect(line).ToBeVisibleAsync();
        var sizes = await row.Locator(".pic-link-actions button").EvaluateAllAsync<double[][]>("elements => elements.map(element => [element.getBoundingClientRect().width, element.getBoundingClientRect().height])");
        var address = await row.Locator(".pic-current-address").EvaluateAsync<double[]>("element => [element.scrollWidth - element.clientWidth, element.getBoundingClientRect().right, element.parentElement.getBoundingClientRect().right, getComputedStyle(element).textOverflow === 'ellipsis' ? 1 : 0, getComputedStyle(element).whiteSpace === 'nowrap' ? 1 : 0]");

        sizes.Should().HaveCount(2).And.OnlyContain(size => size[0] >= 44 && size[1] >= 44);
        address[0].Should().BeGreaterThan(0, "the long address is cut, not wrapped");
        address[1].Should().BeLessThanOrEqualTo(address[2] + 0.5);
        address[3].Should().Be(1);
        address[4].Should().Be(1);
        (await Page.EvaluateAsync<double>("() => document.documentElement.scrollWidth - window.innerWidth")).Should().BeLessThanOrEqualTo(0);
        await SaveScreenshotAsync($"owner-link-form-{width}");
        await row.GetByRole(AriaRole.Button, new() { Name = "Remove link" }).ClickAsync();
        await Expect(row.Locator(":scope > .row-status")).ToContainTextAsync("Link removed");
        (await Page.EvaluateAsync<double>("() => document.documentElement.scrollWidth - window.innerWidth")).Should().BeLessThanOrEqualTo(0);
        await SaveScreenshotAsync($"owner-link-removed-{width}");
    }

    [Fact]
    public async Task Using_a_new_link_replaces_the_earlier_link_and_its_copy()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync(script: images => images.Serve(SecondLinkUrl, SyntheticArt.Encode(SyntheticArtKind.FlatWide)));
        await GotoPicturesAsync();
        var row = RowOf(RowTitle);
        await UseTheLinkAsync(row, LinkUrl);
        var earlier = (Owner.State as OwnerDataState.Ready)!.Data.Games.Single().Picture!.Link!.Record!.Files!.Select(file => file.Name).ToList();

        await UseTheLinkAsync(row, SecondLinkUrl);
        await SyncHarness.WaitUntil(() => Task.FromResult((Owner.State as OwnerDataState.Ready)!.Data.Games.Single().Picture!.Link!.Url == SecondLinkUrl));

        var link = (Owner.State as OwnerDataState.Ready)!.Data.Games.Single().Picture!.Link!;
        link.Url.Should().Be(SecondLinkUrl);
        link.Record!.Files!.Select(file => file.Name).Should().NotIntersectWith(earlier);
        Owner.ReferencedArtFiles()!.Should().NotIntersectWith(earlier);
        await Expect(row.Locator("label.pic-tile[data-kind=Link] .pic-address")).ToHaveTextAsync("example.org/.../second-picture.png");
        await row.Locator("summary").ClickAsync();
        await Expect(row.Locator("[data-current-link]")).ToHaveTextAsync("Current link: example.org/.../second-picture.png");
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

    private async Task UseTheLinkAsync(ILocator row, string address)
    {
        var details = row.Locator("details.pic-linkbox");

        if (!await details.EvaluateAsync<bool>("element => element.open"))
        {
            await row.Locator("summary").ClickAsync();
        }

        await row.GetByLabel("Picture link").FillAsync(address);
        await row.GetByRole(AriaRole.Button, new() { Name = "Check link" }).ClickAsync();
        await row.GetByRole(AriaRole.Button, new() { Name = "Use this" }).ClickAsync();
        await Expect(row.Locator(":scope > .row-status")).ToContainTextAsync("Saved");
        await Expect(row.Locator("label.pic-tile[data-kind=Link] input")).ToBeCheckedAsync();
    }

    private async Task StartHostAsync(
        Action<ScriptedImageHandler>? script = null,
        IReadOnlyDictionary<string, string?>? extraSettings = null,
        bool lanternWithoutSize = false,
        bool background = false)
    {
        var items = SyntheticBggCollection.Create(5);

        if (lanternWithoutSize)
        {
            items = [.. items.Select(item => item.Title == NoSizeTitle ? item with { Version = null } : item)];
        }

        var images = new ScriptedImageHandler().Serve(LinkUrl, LinkedCover);

        images.OnRequest = address =>
        {
            if (address.AbsoluteUri == LinkUrl)
            {
                _linkGate.Wait(TimeSpan.FromSeconds(20));
            }
        };
        script?.Invoke(images);
        _images = images;

        foreach (var item in items.Where(candidate => candidate.Owned && !candidate.IsExpansion))
        {
            if (item.Version is not null)
            {
                images.Serve($"{ImageHost}version-{BggXml.VersionId(item)}.jpg", FlatCover);
            }

            images.Serve($"{ImageHost}{item.ObjectId}.jpg", FlatCover);
        }

        var clock = SyncHarness.NewClock();
        _clock = clock;
        var settings = new Dictionary<string, string?>
        {
            ["Images:AllowedHosts"] = "example.org",
            ["Images:MaxDownloadsPerRun"] = "1000",
            ["Storage:Directory"] = _storage.FullPath,
        };

        foreach (var (key, value) in extraSettings ?? new Dictionary<string, string?>())
        {
            settings[key] = value;
        }

        if (background)
        {
            settings["Sync:BackgroundEnabled"] = "true";
        }

        _factory = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(items), clock, settings, images);

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
        _linkGate.Set();
        _publicClient?.Dispose();
        await base.DisposeAsync();
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record CardReading(int GameId, string Title, string? CoverUrl);
}

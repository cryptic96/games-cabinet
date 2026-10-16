using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Images;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the owner's pictures page opens on the games worth a look, that Looks right counts a game down
/// and keeps focus, that the row stays until the next load, and that every row says what needs attention and why Automatic chose
/// what it did.
/// </summary>
[Trait("Category", "Browser")]
public sealed partial class OwnerWorthALookTests : CabinetPageTest
{
    private const int CollectionSize = 65;
    private const string PicturesPath = "/owner/pictures";
    private const string OlderAddress = "https://example.org/images/older-picture.jpg";
    private const string StoredAddress = "https://example.org/images/stored-link.jpg";
    private const string StoredCopyName = "storedcopy0000001-240.webp";
    private const string ArtCacheDirectory = ArtCache.DirectoryName;

    private static readonly DateTimeOffset LongAgo = new(2029, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly byte[] StoredCopyBytes = SyntheticArt.Encode(SyntheticArtKind.FlatCover);

    private SyntheticArtCollection? _collection;

    [Fact]
    public async Task The_page_opens_on_the_worklist_and_Looks_right_counts_a_row_down_and_the_next_load_drops_it()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();

        await GotoOwnerAsync(PicturesPath);
        var shown = await Rows.CountAsync();
        shown.Should().BeGreaterThan(2);
        await Expect(Counter).ToHaveTextAsync($"{shown} of {shown} to check");
        await Expect(Page.Locator("[data-looks-right]")).ToHaveCountAsync(shown);
        await Expect(WorthLink).ToHaveAttributeAsync("aria-current", "true");
        await Expect(WorthLink).ToHaveTextAsync($"Worth a look ({shown})");
        await Expect(AllLink).Not.ToHaveAttributeAsync("aria-current", "true");
        var total = int.Parse(Regex.Match(await AllLink.InnerTextAsync(), @"\((\d+)\)").Groups[1].Value);
        total.Should().BeGreaterThan(shown);
        var first = Rows.First;
        var title = await first.Locator(".game-title").InnerTextAsync();
        var button = first.Locator("[data-looks-right]");

        await button.ClickAsync();

        await Expect(button).ToBeFocusedAsync();
        await Expect(button).ToHaveAttributeAsync("aria-disabled", "true");
        await Expect(button).ToHaveTextAsync("Checked");
        await Expect(first.Locator(".row-status")).ToContainTextAsync("Marked as checked");
        await Expect(first.Locator(".row-status button")).ToHaveTextAsync("Undo");
        await Expect(first.Locator("[data-mark='checked']")).ToBeVisibleAsync();
        await Expect(Counter).ToHaveTextAsync($"{shown - 1} of {shown} to check");
        await Expect(WorthLink).ToHaveTextAsync($"Worth a look ({shown - 1})");
        await Expect(Rows).ToHaveCountAsync(shown);

        await GotoOwnerAsync(PicturesPath);

        await Expect(Rows).ToHaveCountAsync(shown - 1);
        await Expect(Counter).ToHaveTextAsync($"{shown - 1} of {shown} to check");
        (await Rows.Locator(".game-title").AllTextContentsAsync()).Should().NotContain(title);

        await GotoOwnerAsync($"{PicturesPath}?view=all");

        await Expect(Rows).ToHaveCountAsync(total);
        await Expect(AllLink).ToHaveAttributeAsync("aria-current", "true");
        var inOrder = await Page.EvaluateAsync<bool>(
            """
            () => {
              const collator = new Intl.Collator('en', { numeric: true, sensitivity: 'base' });
              const titles = Array.from(document.querySelectorAll('.pic-row .game-title'), (title) => title.textContent);
              return titles.every((title, index) => index === 0 || collator.compare(titles[index - 1], title) <= 0);
            }
            """);
        inOrder.Should().BeTrue();
        await Expect(RowOf(title).Locator("[data-mark='checked']")).ToBeVisibleAsync();
        await Expect(RowOf(title).Locator("[data-looks-right]")).ToHaveCountAsync(0);
        ConsoleErrors.Should().BeEmpty();
        RequestHosts.Should().OnlyContain(host => host == $"127.0.0.1:{OwnerPort}");
    }

    [Fact]
    public async Task Undo_takes_Looks_right_back_and_the_button_and_counter_follow()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();
        await GotoOwnerAsync(PicturesPath);
        var shown = await Rows.CountAsync();
        var first = Rows.First;
        var button = first.Locator("[data-looks-right]");
        await button.ClickAsync();
        await Expect(Counter).ToHaveTextAsync($"{shown - 1} of {shown} to check");

        await first.Locator(".row-status button").ClickAsync();

        await Expect(first.Locator(".row-status")).ToContainTextAsync("Undone");
        await Expect(Counter).ToHaveTextAsync($"{shown} of {shown} to check");
        await Expect(button).ToHaveTextAsync("Looks right");
        await Expect(button).Not.ToHaveAttributeAsync("aria-disabled", "true");
        await Expect(button).ToBeFocusedAsync();
        await Expect(first.Locator("[data-mark='checked']")).ToBeHiddenAsync();
        await GotoOwnerAsync(PicturesPath);
        await Expect(Rows).ToHaveCountAsync(shown);
    }

    [Fact]
    public async Task Choosing_another_picture_resolves_the_row_in_the_same_way_and_the_counter_follows()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();
        await GotoOwnerAsync(PicturesPath);
        var shown = await Rows.CountAsync();
        var row = Rows.First;

        await row.Locator("label.pic-tile", new LocatorLocatorOptions { Has = Page.Locator("input[value=MainPicture]") }).ClickAsync();

        await Expect(row.Locator(".row-status")).ToContainTextAsync("Saved");
        await Expect(row.Locator("[data-mark='checked']")).ToBeVisibleAsync();
        await Expect(row.Locator("[data-looks-right]")).ToHaveTextAsync("Checked");
        await Expect(Counter).ToHaveTextAsync($"{shown - 1} of {shown} to check");

        await row.Locator(".row-status button").ClickAsync();

        await Expect(Counter).ToHaveTextAsync($"{shown} of {shown} to check");
        await Expect(row.Locator("[data-looks-right]")).ToHaveTextAsync("Looks right");
    }

    [Fact]
    public async Task Marking_every_game_checked_says_so_and_the_next_load_shows_the_empty_worklist()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();
        await GotoOwnerAsync(PicturesPath);
        var shown = await Rows.CountAsync();

        for (var index = 0; index < shown; index++)
        {
            var button = Rows.Nth(index).Locator("[data-looks-right]");
            await button.ClickAsync();
            await Expect(button).ToHaveAttributeAsync("aria-disabled", "true");
        }

        await Expect(Counter).ToHaveTextAsync($"You've checked all {shown} games.");
        await Expect(WorthLink).ToHaveTextAsync("Worth a look (0)");

        await GotoOwnerAsync(PicturesPath);

        await Expect(Page.Locator("#pic-nothing h3")).ToHaveTextAsync("Nothing to check.");
        await Expect(Page.Locator("#pic-nothing p")).ToHaveTextAsync("You've checked every game that needed a look. Use \"All games\" to change any picture.");
        await Expect(Rows).ToHaveCountAsync(0);
        await Expect(Counter).ToHaveTextAsync($"You've checked all {shown} games.");
        await SaveScreenshotAsync("owner-worth-a-look-empty-1440");
    }

    [Fact]
    public async Task The_worklist_puts_missing_and_broken_first_then_dead_links_then_changed_pictures_then_the_rest_a_to_z()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync(Seed);

        await GotoOwnerAsync(PicturesPath);

        var titles = await Rows.Locator(".game-title").AllTextContentsAsync();
        titles.Take(5).Should().Equal("Example Game 17", "Example Game 18", "Lantern & Harbour", "Example Game 1", "Example Game 1");
        var rest = await Page.EvaluateAsync<bool>(
            """
            () => {
              const collator = new Intl.Collator('en', { numeric: true, sensitivity: 'base' });
              const rows = Array.from(document.querySelectorAll('.pic-row')).slice(5);
              const titles = rows.map((row) => row.querySelector('.game-title').textContent);
              const quiet = rows.every((row) => row.querySelector('[data-mark="skips-owned"]') !== null);
              return quiet && titles.every((title, index) => index === 0 || collator.compare(titles[index - 1], title) <= 0);
            }
            """);
        rest.Should().BeTrue();
        await Expect(Counter).ToHaveTextAsync($"{titles.Count} of {titles.Count} to check");
    }

    [Fact]
    public async Task Marks_and_notes_say_what_needs_attention_and_the_why_line_says_why_Automatic_chose_what_it_did()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync(Seed);
        await GotoOwnerAsync(PicturesPath);

        var changed = RowOf("Example Game 1");
        await Expect(changed.Locator("[data-mark='changed']")).ToHaveTextAsync("Picture changed on BGG");
        await Expect(changed.Locator(".pic-note")).ToHaveTextAsync("BGG replaced the owned edition picture. Check that it still looks right.");
        await Expect(changed.Locator("[data-mark='checked']")).ToBeHiddenAsync();

        var dead = RowOf("Lantern & Harbour");
        await Expect(dead.Locator("[data-mark='link-dead']")).ToHaveTextAsync("Link no longer works");
        await Expect(dead.Locator("[data-mark='yours']")).ToHaveTextAsync("Yours");
        await Expect(dead.Locator(".pic-note")).ToHaveTextAsync("Your link no longer works. The cabinet keeps showing the copy it saved. Choose another picture, paste a new link, remove the link, or press \"Looks right\" to keep this copy.");
        var stored = dead.Locator("label.pic-tile", new LocatorLocatorOptions { Has = Page.Locator("input[value=Link]") }).Locator("img");
        await Expect(stored).ToHaveAttributeAsync("src", $"/art/{StoredCopyName}");
        await Expect(stored).ToHaveJSPropertyAsync("complete", true);
        (await stored.EvaluateAsync<int>("image => image.naturalWidth")).Should().BeGreaterThan(0);
        await Expect(dead.Locator("input[value=Link]")).ToBeCheckedAsync();

        var broken = RowOf("Example Game 17");
        await Expect(broken.Locator("[data-mark='wont-load']")).ToHaveTextAsync("Picture won't load");
        await Expect(broken.Locator(".pic-note")).ToHaveTextAsync("The picture can't be loaded right now. The cabinet shows a generated cover until it can.");
        await Expect(broken.Locator(".pic-why")).ToHaveTextAsync("Automatic has no picture to use, so the cabinet shows a generated cover.");
        await Expect(broken.Locator(".pic-why")).ToBeHiddenAsync();
        await Expect(changed.Locator(".pic-why")).ToBeHiddenAsync();
        await Expect(dead.Locator(".pic-why")).ToBeHiddenAsync();

        var skipped = RowOf("Example Game 21");
        await Expect(skipped.Locator("[data-mark='skips-owned']")).ToHaveTextAsync("Automatic skips the owned edition");
        await Expect(skipped.Locator(".pic-why")).ToHaveTextAsync("Automatic picks the main picture because the owned edition reads as a cut-out.");
        await Expect(skipped.Locator(".pic-why")).ToBeVisibleAsync();
        await Expect(RowOf("Example Game 27").Locator(".pic-why")).ToHaveTextAsync("Automatic picks the main picture because the owned edition reads as a 3D shot.");

        var whys = await Rows.Locator(".pic-why").AllTextContentsAsync();
        whys.Should().HaveCount(await Rows.CountAsync());
        whys.Should().OnlyContain(why => OneOfTheFourWhys().IsMatch(why));
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Every_mark_is_text_with_a_border_and_marks_wrap_instead_of_overflowing_on_a_phone()
    {
        await Page.SetViewportSizeAsync(320, 800);
        await StartHostAsync(Seed);
        await GotoOwnerAsync($"{PicturesPath}?view=all");

        var styles = await Page.Locator(".pic-marks li:not([hidden])").EvaluateAllAsync<string[]>(
            "marks => marks.map(mark => `${mark.dataset.mark}:${getComputedStyle(mark).borderTopStyle}:${getComputedStyle(mark).borderTopWidth}:${getComputedStyle(mark).fontWeight}`)");
        styles.Where(style => !style.StartsWith("expansion:", StringComparison.Ordinal)).Should().OnlyContain(style => style.Split(':')[2] == "1px");
        styles.Should().Contain(style => style.StartsWith("link-dead:solid:1px:600", StringComparison.Ordinal));
        styles.Should().Contain(style => style.StartsWith("fetching:dashed:", StringComparison.Ordinal));
        styles.Should().Contain(style => style.StartsWith("checked:solid:1px:400", StringComparison.Ordinal));
        (await RowOf("Example Game 1: Lantern Extras").Locator("[data-mark='expansion']").EvaluateAsync<string>("mark => getComputedStyle(mark).borderTopColor"))
            .Should().Be("rgba(0, 0, 0, 0)");
        (await Page.EvaluateAsync<double>("() => document.documentElement.scrollWidth - window.innerWidth")).Should().BeLessThanOrEqualTo(0);
        var withoutMarks = await Page.Locator(".pic-row").EvaluateAllAsync<bool[]>(
            "rows => rows.filter(row => !row.querySelector('.pic-marks li:not([hidden])')).map(row => getComputedStyle(row.querySelector('.pic-marks')).display === 'none')");
        withoutMarks.Should().NotBeEmpty().And.OnlyContain(hidden => hidden);
        await SaveScreenshotAsync("owner-worth-a-look-marks-320");
    }

    [Fact]
    public async Task The_all_view_marks_a_link_choice_a_pending_link_and_expansions_and_offers_no_button_where_nothing_needs_a_look()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync(Seed);

        await GotoOwnerAsync($"{PicturesPath}?view=all");

        var yours = RowOf("Ribbon Rally");
        await Expect(yours.Locator("[data-mark='yours']")).ToHaveTextAsync("Yours");
        await Expect(yours.Locator("[data-looks-right]")).ToHaveCountAsync(0);
        await Expect(yours.Locator("[data-mark='link-dead']")).ToHaveCountAsync(0);
        var pending = RowOf("Quiet Quarry");
        await Expect(pending.Locator("[data-mark='fetching']")).ToHaveTextAsync("Fetching your link...");
        var tile = pending.Locator("label.pic-tile", new LocatorLocatorOptions { Has = Page.Locator("input[value=Link]") });
        await Expect(tile.Locator(".pic-sub")).ToHaveTextAsync("Fetching...");
        (await tile.EvaluateAsync<string>("element => getComputedStyle(element).borderTopStyle")).Should().Be("dashed");
        await Expect(RowOf("Example Game 1: Lantern Extras").Locator("[data-mark='expansion']")).ToHaveTextAsync("Expansion");
        await Expect(RowOf("Example Game 22").Locator("[data-looks-right]")).ToHaveCountAsync(0);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Looks_right_hides_the_marks_it_clears_and_Undo_brings_them_back()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync(Seed);
        await GotoOwnerAsync(PicturesPath);
        var shown = await Rows.CountAsync();
        var row = RowOf("Lantern & Harbour");

        await row.Locator("[data-looks-right]").ClickAsync();

        await Expect(row.Locator("[data-mark='link-dead']")).ToBeHiddenAsync();
        await Expect(row.Locator(".pic-note")).ToBeHiddenAsync();
        await Expect(row.Locator(".pic-form .pic-why")).ToBeVisibleAsync();
        await Expect(row.Locator("[data-mark='checked']")).ToBeVisibleAsync();
        await Expect(row.Locator("[data-mark='yours']")).ToBeVisibleAsync();
        await Expect(Counter).ToHaveTextAsync($"{shown - 1} of {shown - 1} to check");

        await row.Locator(".row-status button").ClickAsync();

        await Expect(row.Locator("[data-mark='link-dead']")).ToBeVisibleAsync();
        await Expect(row.Locator(".pic-note")).ToBeVisibleAsync();
        await Expect(row.Locator(".pic-form .pic-why")).ToBeHiddenAsync();
        await Expect(row.Locator("[data-mark='checked']")).ToBeHiddenAsync();
        await Expect(Counter).ToHaveTextAsync($"{shown} of {shown} to check");
    }

    [Fact]
    public async Task A_tile_picture_that_fails_to_load_swaps_to_the_generated_cover_and_the_row_gets_Picture_wont_load()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync(Seed);
        await GotoOwnerAsync($"{PicturesPath}?view=all");
        var row = RowOf("Example Game 22");
        var source = await row.Locator("label.pic-tile", new LocatorLocatorOptions { Has = Page.Locator("input[value=OwnedEdition]") }).Locator("img").GetAttributeAsync("src");
        await Expect(row.Locator("[data-mark='wont-load']")).ToHaveCountAsync(0);
        _collection!.DeleteStoredPicture(source!);

        await GotoOwnerAsync($"{PicturesPath}?view=all");
        await RowOf("Example Game 22").ScrollIntoViewIfNeededAsync();

        await Expect(RowOf("Example Game 22").Locator("[data-mark='wont-load']")).ToHaveTextAsync("Picture won't load");
        await Expect(RowOf("Example Game 22").Locator(".pic-note")).ToHaveTextAsync("The picture can't be loaded right now. The cabinet shows a generated cover until it can.");
        await Expect(RowOf("Example Game 22").Locator("label.pic-tile", new LocatorLocatorOptions { Has = Page.Locator("input[value=OwnedEdition]") }).Locator(".pic-art[data-art='cover']")).ToHaveCountAsync(1);
        await Expect(RowOf("Example Game 22").Locator("[data-mark='wont-load']")).ToHaveCountAsync(1);
        ConsoleErrors.Should().OnlyContain(error => error.Contains("404", StringComparison.Ordinal) && !error.Contains("Content Security Policy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Find_narrows_the_rows_and_says_how_many_are_shown_or_that_none_match()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync(Seed);
        await GotoOwnerAsync($"{PicturesPath}?view=all");
        var total = await Rows.CountAsync();
        var find = Page.GetByLabel("Find a game");

        await find.FillAsync("Lantern");

        await Expect(Page.Locator(".pic-row:not([hidden])")).ToHaveCountAsync(2);
        await Expect(Page.Locator("#pic-count")).ToHaveTextAsync($"Showing 2 of {total} games");
        await Expect(Page.Locator("#owner-status")).ToHaveTextAsync($"Showing 2 of {total} games");

        await find.FillAsync("zzz");

        await Expect(Page.Locator("#pic-count")).ToHaveTextAsync("No games match \"zzz\".");
        await Expect(Page.Locator(".pic-row:not([hidden])")).ToHaveCountAsync(0);

        await find.FillAsync(string.Empty);

        await Expect(Page.Locator(".pic-row:not([hidden])")).ToHaveCountAsync(total);
        await Expect(Page.Locator("#pic-count")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task Looks_right_works_from_the_keyboard_and_keeps_focus_on_the_button()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartHostAsync();
        await GotoOwnerAsync(PicturesPath);
        var shown = await Rows.CountAsync();
        var button = Rows.First.Locator("[data-looks-right]");

        await button.FocusAsync();
        await Page.Keyboard.PressAsync("Enter");

        await Expect(button).ToHaveAttributeAsync("aria-disabled", "true");
        await Expect(button).ToBeFocusedAsync();
        await Expect(Counter).ToHaveTextAsync($"{shown - 1} of {shown} to check");
        await Page.Keyboard.PressAsync("Enter");
        await Task.Delay(300, TestContext.Current.CancellationToken);
        await Expect(Counter).ToHaveTextAsync($"{shown - 1} of {shown} to check");
    }

    [Fact]
    public async Task Without_script_Looks_right_posts_the_row_and_the_next_load_drops_it_with_an_undo()
    {
        await StartHostAsync();
        var plain = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            JavaScriptEnabled = false,
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
            ExtraHTTPHeaders = new Dictionary<string, string> { ["Accept-Encoding"] = "identity" },
        });
        var page = await plain.NewPageAsync();
        await page.GotoAsync($"http://127.0.0.1:{OwnerPort}{PicturesPath}");
        var shown = await page.Locator(".pic-row").CountAsync();
        await Expect(page.Locator("#pic-counter")).ToHaveTextAsync($"{shown} of {shown} to check");

        await page.Locator("[data-looks-right]").First.ClickAsync();

        await Expect(page.Locator("#owner-notice")).ToHaveTextAsync("Saved.");
        page.Url.Should().MatchRegex(@"/owner/pictures\?done=saved#entry-\d+$");
        await Expect(page.Locator(".pic-row")).ToHaveCountAsync(shown - 1);
        await Expect(page.Locator("#pic-counter")).ToHaveTextAsync($"{shown - 1} of {shown} to check");

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Undo" }).ClickAsync();

        await Expect(page.Locator(".pic-row")).ToHaveCountAsync(shown);
        await plain.CloseAsync();
    }

    [Theory]
    [InlineData(65, 1440, 900)]
    [InlineData(65, 390, 844)]
    [InlineData(400, 1440, 900)]
    [InlineData(400, 390, 844)]
    public async Task The_worklist_reads_as_paper_cards_with_equal_tiles_and_clear_marks(int size, int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartHostAsync(Seed, size);

        await GotoOwnerAsync(PicturesPath);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var heights = await Rows.EvaluateAllAsync<double[][]>(
            """
            rows => rows.flatMap((row) => {
              const tiles = Array.from(row.querySelectorAll('.pic-tile'));
              const tops = [...new Set(tiles.map((tile) => Math.round(tile.getBoundingClientRect().top)))];
              return tops.map((top) => tiles.filter((tile) => Math.round(tile.getBoundingClientRect().top) === top).map((tile) => Math.round(tile.getBoundingClientRect().height)));
            })
            """);
        heights.Should().OnlyContain(line => line.Distinct().Count() == 1);
        var buttons = await Page.Locator("[data-looks-right]").EvaluateAllAsync<double[]>("buttons => buttons.map(button => button.getBoundingClientRect().height)");
        buttons.Should().NotBeEmpty().And.OnlyContain(buttonHeight => buttonHeight >= 44);
        (await Page.EvaluateAsync<double>("() => document.documentElement.scrollWidth - window.innerWidth")).Should().BeLessThanOrEqualTo(0);
        var links = await Page.Locator("nav[aria-label='Games shown'] a").EvaluateAllAsync<double[]>("links => links.map(link => link.getBoundingClientRect().height)");
        links.Should().OnlyContain(linkHeight => linkHeight >= 44);
        await SaveScreenshotAsync($"owner-worth-a-look-{size}-{width}");
        ConsoleErrors.Should().BeEmpty();
    }

    private static void Seed(string storage)
    {
        var art = Path.Combine(storage, ArtCacheDirectory);

        Directory.CreateDirectory(art);
        File.WriteAllBytes(Path.Combine(art, StoredCopyName), StoredCopyBytes);

        var features = new ArtFeatures(0.0, 1.0, 0.0, 0.0, 4);
        var stored = new ImageRecord(StoredAddress, ImageStatus.Ok, LongAgo, [new ArtFile(240, 320, StoredCopyName)], features);
        var dead = new PictureLink(StoredAddress, stored, 3, LongAgo);
        var healthy = new PictureLink(StoredAddress, stored, 0, null);
        var pending = new PictureLink(StoredAddress, null, 0, null);
        var first = SyntheticBggCollection.FirstObjectId;
        var data = OwnerData.Empty with
        {
            Revision = 1,
            Games =
            [
                new OwnerGame(first, null, null, new PictureSetting(PictureChoice.Automatic, true, OlderAddress, null, null)),
                new OwnerGame(first + 1, null, null, new PictureSetting(PictureChoice.Link, true, null, null, dead)),
                new OwnerGame(first + 8, null, null, new PictureSetting(PictureChoice.Automatic, false, null, null, pending)),
                new OwnerGame(first + 9, null, null, new PictureSetting(PictureChoice.Link, true, null, null, healthy)),
            ],
        };

        OwnerDataSeed.Write(storage, data);
    }

    [GeneratedRegex(@"^Automatic (picks the owned edition\.|picks the main picture because the owned edition reads as (a 3D shot|a cut-out|a tight crop|an unsure shape)\.|picks the main picture because BGG has no owned edition picture\.|has no picture to use, so the cabinet shows a generated cover\.)$")]
    private static partial Regex OneOfTheFourWhys();

    private ILocator Rows => Page.Locator(".pic-row");

    private ILocator Counter => Page.Locator("#pic-counter");

    private ILocator WorthLink => Page.Locator("nav[aria-label='Games shown'] a").First;

    private ILocator AllLink => Page.Locator("nav[aria-label='Games shown'] a").Last;

    private ILocator RowOf(string title) =>
        Page.Locator(".pic-row", new PageLocatorOptions { Has = Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = title, Exact = true }) }).First;

    private async Task StartHostAsync(Action<string>? seed = null, int size = CollectionSize)
    {
        _collection = await SyntheticArtCollection.StartAsync(size);
        var storage = Path.GetDirectoryName(_collection.ArtDirectory)!;

        seed?.Invoke(storage);

        var factory = await StartAsync(_collection.Settings, _collection.ConfigureServices);
        using var client = factory.CreatePublicClient();
        await _collection.SyncAsync(client);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (_collection is not null)
        {
            await _collection.DisposeAsync();
        }

        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

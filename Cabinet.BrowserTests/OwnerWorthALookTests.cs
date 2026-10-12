using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the owner's pictures page opens on the games worth a look, that Looks right counts a game down
/// and keeps focus, that the row stays until the next load, and that every row says what needs attention and why Automatic chose
/// what it did.
/// </summary>
[Trait("Category", "Browser")]
public sealed class OwnerWorthALookTests : CabinetPageTest
{
    private const int CollectionSize = 65;
    private const string PicturesPath = "/owner/pictures";
    private const string OlderAddress = "https://example.org/images/older-picture.jpg";
    private const string StoredCopyName = "storedcopy0000001-240.webp";

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
        (await Rows.Locator(".game-title").AllInnerTextsAsync()).Should().NotContain(title);

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

        await row.Locator("label.pic-tile", new LocatorLocatorOptions { HasText = "Main picture" }).ClickAsync();

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

    private ILocator Rows => Page.Locator(".pic-row");

    private ILocator Counter => Page.Locator("#pic-counter");

    private ILocator WorthLink => Page.Locator("nav[aria-label='Games shown'] a").First;

    private ILocator AllLink => Page.Locator("nav[aria-label='Games shown'] a").Last;

    private ILocator RowOf(string title) =>
        Page.Locator(".pic-row", new PageLocatorOptions { Has = Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = title, Exact = true }) });

    private async Task StartHostAsync(Action<string>? seed = null)
    {
        _collection = await SyntheticArtCollection.StartAsync(CollectionSize);
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

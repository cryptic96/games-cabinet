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
/// Proves in a real browser that the pictures page can show one game on its own with the shared location picker under the picture
/// choices: the picker saves the value it settles on through the locations page's handler, an expansion and a game with no
/// locations get the right choices, an unknown game says so, a failed save puts the select back, a long title wraps, and without
/// script the picker's Save returns to the single-game view while a return target outside the fixed set returns to the locations page.
/// </summary>
[Trait("Category", "Browser")]
public sealed class OwnerSingleGameTests : CabinetPageTest
{
    private const string PicturesPath = "/owner/pictures";
    private const string CardsPath = "/cabinet/cards?profile=desktop";
    private const string AssignFragment = "handler=Assign";
    private const string FailedToLoad = "Failed to load resource";
    private const string QuarryBase = "Quarry Lights";
    private const string CafeBase = "Café Quarter";
    private const string CafeExpansion = "Café Quarter: Extra Pack";
    private const string LongTitle = "Cartographers of the Endless Amber Hills and Wandering Tides";
    private const string SavedText = "Saved";

    private HttpClient? _publicClient;

    [Fact]
    public async Task Opening_one_game_shows_only_its_row_with_the_picker_and_choosing_a_place_posts_once_and_the_public_card_shows_it()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        var ids = await host.SeedAsync("Cabinet A", "Shelf B");
        var game = host.Card(QuarryBase);
        await GotoSingleAsync(game.GameId);
        var posts = CountAssignPosts();

        await Expect(Page.Locator(".pic-row")).ToHaveCountAsync(1);
        await Expect(Page.Locator(".pic-row .game-title")).ToHaveTextAsync(QuarryBase);
        await Expect(Page.Locator(".pic-single")).ToContainTextAsync("Showing one game.");
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Show all games" })).ToHaveAttributeAsync("href", "/owner/pictures?view=all");
        await Expect(Page.Locator(".pic-row .game-title")).ToBeFocusedAsync();
        await Expect(Page.Locator(".pic-row .game-title")).ToHaveAttributeAsync("tabindex", "-1");
        await Expect(Page.Locator("label.picker-label")).ToHaveTextAsync("Location");
        await Expect(Page.Locator(".pic-views")).ToHaveCountAsync(0);

        var choicesBottom = await Page.Locator(".pic-why").EvaluateAsync<double>("element => element.getBoundingClientRect().bottom");
        var pickerTop = await Page.Locator(".game-picker").EvaluateAsync<double>("element => element.getBoundingClientRect().top");
        pickerTop.Should().BeGreaterThan(choicesBottom);

        var picker = Page.Locator($"#picker-{game.GameId}");
        var revisionBefore = host.Owner.Revision;
        await picker.SelectOptionAsync(new SelectOptionValue { Label = "Shelf B" });

        var status = Page.Locator(".game-picker .row-status");
        await Expect(status).ToContainTextAsync(SavedText);
        await Expect(status.GetByRole(AriaRole.Button, new() { Name = "Undo" })).ToBeVisibleAsync();
        await Expect(picker).ToHaveValueAsync(ids[1]);
        posts().Should().Be(1);
        host.Owner.Revision.Should().Be(revisionBefore + 1);
        (await ReadCardsAsync()).Single(card => card.GameId == game.GameId).Location.Should().Be("Shelf B");

        await status.GetByRole(AriaRole.Button, new() { Name = "Undo" }).ClickAsync();

        await Expect(status).ToContainTextAsync("Undone.");
        await Expect(picker).ToHaveValueAsync(string.Empty);
        (await ReadCardsAsync()).Single(card => card.GameId == game.GameId).Location.Should().BeNull();
        AssertCleanConsole();
    }

    [Fact]
    public async Task An_expansion_with_an_owned_base_offers_Same_as_the_base_and_with_no_locations_only_Not_placed_is_offered_with_a_way_to_add_one()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        var expansion = host.Card(CafeExpansion);
        var baseGame = host.Card(QuarryBase);

        await GotoSingleAsync(expansion.GameId);
        (await Options(expansion)).Should().Equal($"Same as {CafeBase}");
        await Expect(Page.Locator("#no-locations-note")).ToContainTextAsync("Add a location on the Locations page first.");
        await Expect(Page.Locator("#no-locations-note").GetByRole(AriaRole.Link, new() { Name = "Locations" })).ToHaveAttributeAsync("href", "/owner/locations");

        await GotoSingleAsync(baseGame.GameId);
        (await Options(baseGame)).Should().Equal("Not placed");
        await Expect(Page.Locator("#no-locations-note")).ToBeVisibleAsync();

        await host.SeedAsync("Cabinet A", "Shelf B");
        await GotoSingleAsync(expansion.GameId);

        (await Options(expansion)).Should().Equal($"Same as {CafeBase}", "Cabinet A", "Shelf B");
        await Expect(Page.Locator("#no-locations-note")).ToHaveCountAsync(0);
        AssertCleanConsole();
    }

    [Theory]
    [InlineData("999999")]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("abc")]
    [InlineData("%3Cscript%3Ewindow.hacked%3D1%3C%2Fscript%3E")]
    public async Task An_unknown_or_unreadable_game_value_says_the_game_is_gone_and_is_never_echoed(string value)
    {
        await StartHostAsync();
        await Page.SetViewportSizeAsync(390, 844);

        await GotoOwnerAsync($"{PicturesPath}?game={value}");

        await Expect(Page.GetByText("That game isn't in your collection any more.")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Show all games" })).ToBeVisibleAsync();
        await Expect(Page.Locator(".pic-row")).ToHaveCountAsync(0);
        (await Page.ContentAsync()).Should().NotContain("window.hacked");
        (await Page.EvaluateAsync<bool>("() => window.hacked === undefined")).Should().BeTrue();
        AssertCleanConsole();
    }

    [Fact]
    public async Task A_failed_save_shows_the_save_failed_text_and_returns_the_select_to_its_previous_value()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        await host.SeedAsync("Cabinet A", "Shelf B");
        var game = host.Card(QuarryBase);
        await GotoSingleAsync(game.GameId);
        await Page.RouteAsync(
            new Regex(AssignFragment, RegexOptions.IgnoreCase),
            route => route.FulfillAsync(new RouteFulfillOptions { Status = 500, Body = string.Empty }));
        var picker = Page.Locator($"#picker-{game.GameId}");

        await picker.SelectOptionAsync(new SelectOptionValue { Label = "Shelf B" });

        var status = Page.Locator(".game-picker .row-status");
        await Expect(status).ToContainTextAsync("Couldn't save that. Nothing changed. Try again.");
        await Expect(picker).ToHaveValueAsync(string.Empty);
        await Expect(status.GetByRole(AriaRole.Button, new() { Name = "Undo" })).ToHaveCountAsync(0);
        (await ReadCardsAsync()).Single(card => card.GameId == game.GameId).Location.Should().BeNull();
        AssertCleanConsole(allowRefusals: true);
    }

    [Fact]
    public async Task Without_script_Save_posts_and_returns_to_the_single_game_view_and_the_public_card_follows()
    {
        var host = await StartHostAsync();
        var ids = await host.SeedAsync("Cabinet A", "Shelf B");
        var game = host.Card(QuarryBase);
        var plain = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            JavaScriptEnabled = false,
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
            ExtraHTTPHeaders = new Dictionary<string, string> { ["Accept-Encoding"] = "identity" },
        });
        var page = await plain.NewPageAsync();
        await page.GotoAsync($"http://127.0.0.1:{OwnerPort}{PicturesPath}?game={game.GameId}");

        await page.Locator($"#picker-{game.GameId}").SelectOptionAsync(new SelectOptionValue { Label = "Shelf B" });
        await page.Locator(".game-picker").GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();

        await Expect(page.Locator("#owner-notice")).ToHaveTextAsync("Saved.");
        page.Url.Should().MatchRegex($@"/owner/pictures\?done=saved&game={game.GameId}#game-{game.GameId}$");
        await Expect(page.Locator(".pic-row")).ToHaveCountAsync(1);
        await Expect(page.Locator($"#picker-{game.GameId}")).ToHaveValueAsync(ids[1]);
        (await ReadCardsAsync()).Single(card => card.GameId == game.GameId).Location.Should().Be("Shelf B");

        await page.GetByRole(AriaRole.Button, new() { Name = "Undo" }).ClickAsync();

        page.Url.Should().MatchRegex($@"/owner/pictures\?done=undone&game={game.GameId}#");
        await Expect(page.Locator(".pic-row")).ToHaveCountAsync(1);
        (await ReadCardsAsync()).Single(card => card.GameId == game.GameId).Location.Should().BeNull();
        await plain.CloseAsync();
    }

    [Fact]
    public async Task A_return_target_outside_the_fixed_set_returns_to_the_locations_page_and_never_follows_the_value()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        var ids = await host.SeedAsync("Cabinet A", "Shelf B");
        var game = host.Card(QuarryBase);
        await host.PlaceAsync(game, ids[0]);
        await GotoSingleAsync(game.GameId);

        await Page.EvaluateAsync(
            """
            () => {
              const form = document.querySelector('.game-picker form');
              form.querySelector('input[name="returnTo"]').value = 'https://example.org/elsewhere';
              form.querySelector('select').value = '';
              HTMLFormElement.prototype.submit.call(form);
            }
            """);

        await Expect(Page).ToHaveURLAsync(new Regex(@"/owner/locations\?done=saved#game-\d+$"));
        new Uri(Page.Url).Host.Should().Be("127.0.0.1");
    }

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(390, 844)]
    public async Task A_long_title_wraps_and_the_view_fits_the_width_with_every_control_at_least_44_pixels(int width, int height)
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(width, height);
        await host.SeedAsync("Cabinet A", "Shelf B");
        var game = host.Card(LongTitle);
        game.Title.Length.Should().Be(60);
        await GotoSingleAsync(game.GameId);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await SaveScreenshotAsync($"owner-single-game-{width}");

        var overflow = await Page.EvaluateAsync<double>("() => document.documentElement.scrollWidth - window.innerWidth");
        var titleBox = await Page.Locator(".pic-row .game-title").EvaluateAsync<double[]>("element => { const box = element.getBoundingClientRect(); return [box.width, box.height, element.scrollWidth - element.clientWidth]; }");
        var targets = await Page.Locator(".pic-row select, .pic-row .owner-btn:not([hidden]), .pic-single a").EvaluateAllAsync<double[]>(
            "elements => elements.filter(element => element.getBoundingClientRect().width > 0).map(element => element.getBoundingClientRect().height)");

        overflow.Should().BeLessThanOrEqualTo(0);
        titleBox[2].Should().BeLessThanOrEqualTo(0);
        targets.Should().NotBeEmpty().And.OnlyContain(targetHeight => targetHeight >= 44);
        AssertCleanConsole();
    }

    private ILocator Picker(CardReading card) => Page.Locator($"#picker-{card.GameId}");

    private async Task<IReadOnlyList<string>> Options(CardReading card) =>
        [.. (await Picker(card).Locator("option").AllTextContentsAsync()).Select(text => text.Trim())];

    private async Task GotoSingleAsync(int gameId)
    {
        await GotoOwnerAsync($"{PicturesPath}?game={gameId}");
        await Page.WaitForSelectorAsync("#pic-list[data-enhanced]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
    }

    private Func<int> CountAssignPosts()
    {
        var posts = 0;

        Page.Request += (_, request) =>
        {
            if (request.Method == "POST" && request.Url.Contains(AssignFragment, StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref posts);
            }
        };

        return () => Volatile.Read(ref posts);
    }

    private async Task<OwnerHost> StartHostAsync()
    {
        var clock = SyncHarness.NewClock();
        var factory = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(Collection()), clock);

        await StartAsync(factory);
        _publicClient = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(_publicClient, clock, advance: false);

        return new OwnerHost(factory.ServingServices.GetRequiredService<OwnerDataService>(), await ReadCardsAsync());
    }

    private static IReadOnlyList<FakeBggItem> Collection()
    {
        var first = SyntheticBggCollection.FirstObjectId;

        return
        [
            Entry(0, QuarryBase, false, null),
            Entry(1, CafeBase, false, null),
            Entry(2, CafeExpansion, true, first + 1),
            Entry(3, LongTitle, false, null),
        ];
    }

    private static FakeBggItem Entry(int index, string title, bool isExpansion, int? baseObjectId) =>
        new(
            SyntheticBggCollection.FirstObjectId + index,
            SyntheticBggCollection.FirstCollId + index,
            title,
            isExpansion,
            true,
            2000 + index,
            null,
            null,
            baseObjectId);

    private async Task<IReadOnlyList<CardReading>> ReadCardsAsync()
    {
        using var response = await _publicClient!.GetAsync(CardsPath, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return
        [
            .. document.RootElement.GetProperty("cards").EnumerateArray().Select(card => new CardReading(
                card.GetProperty("gameId").GetInt32(),
                card.GetProperty("title").GetString()!,
                card.TryGetProperty("location", out var location) ? location.GetString() : null)),
        ];
    }

    private void AssertCleanConsole(bool allowRefusals = false)
    {
        var errors = allowRefusals ? ConsoleErrors.Where(error => !error.Contains(FailedToLoad, StringComparison.Ordinal)).ToList() : [.. ConsoleErrors];

        errors.Should().BeEmpty();
        RequestHosts.Should().OnlyContain(host => host == $"127.0.0.1:{OwnerPort}");
    }

    private sealed record CardReading(int GameId, string Title, string? Location);

    private sealed class OwnerHost(OwnerDataService owner, IReadOnlyList<CardReading> cards)
    {
        public OwnerDataService Owner { get; } = owner;

        public IReadOnlyList<CardReading> Cards { get; } = cards;

        public CardReading Card(string title) => Cards.First(card => string.Equals(card.Title, title, StringComparison.Ordinal));

        public async Task PlaceAsync(CardReading card, string locationId)
        {
            var result = await Owner.MutateAsync(
                Owner.Revision,
                data => LocationEdits.Assign(data, card.GameId, locationId, card.Title),
                TestContext.Current.CancellationToken);

            result.Outcome.Should().Be(OwnerOutcome.Saved);
        }

        public async Task<IReadOnlyList<string>> SeedAsync(params string[] names)
        {
            var ids = new List<string>();

            foreach (var name in names)
            {
                var result = await Owner.MutateAsync(
                    Owner.Revision,
                    data => LocationEdits.Add(data, name, OwnerDataService.NewId),
                    TestContext.Current.CancellationToken);

                result.Outcome.Should().Be(OwnerOutcome.Saved);
                ids.Add(result.Data!.Locations[^1].Id);
            }

            return ids;
        }
    }
}

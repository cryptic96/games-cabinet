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
/// Proves in a real browser that the owner's games list behaves as the page promises: a row's picker saves the value it settles
/// on and not every arrow key, the choices differ for a base game, an expansion with a base and an orphan expansion, the list
/// follows the collator, the search box and the not-placed switch narrow it with the right words, and Select all shown ticks
/// what is on screen.
/// </summary>
[Trait("Category", "Browser")]
public sealed class OwnerPickerTests : CabinetPageTest
{
    private const string LocationsPath = "/owner/locations";
    private const string CardsPath = "/cabinet/cards?profile=desktop";
    private const string FailedToLoad = "Failed to load resource";
    private const string AssignFragment = "handler=Assign";
    private const string SavedText = "Saved";
    private const string CafeBase = "Café Quarter";
    private const string CafeExpansion = "Café Quarter: Extra Pack";
    private const string TwoBase = "Game 2";
    private const string TenBase = "Game 10";
    private const string ZebraExpansion = "Game 2: Zebra Pack";
    private const string AppleExpansion = "Game 2: Apple Pack";
    private const string OrphanExpansion = "Orphan Pack";
    private const string QuarryBase = "Quarry Lights";
    private const int MissingBaseObjectId = 190001;

    private static readonly string[] CollatedTitles =
    [
        CafeBase,
        CafeExpansion,
        TwoBase,
        AppleExpansion,
        ZebraExpansion,
        TenBase,
        OrphanExpansion,
        QuarryBase,
    ];

    private HttpClient? _publicClient;

    [Fact]
    public async Task Arrowing_through_a_row_picker_makes_one_save_and_one_change_entry_and_the_public_card_shows_the_choice()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        var ids = await host.SeedAsync("Cabinet A", "Shelf B", "Shelf C");
        await GotoLocationsAsync();
        var game = host.Card(QuarryBase);
        var picker = Picker(game);
        var posts = CountAssignPosts();
        var revisionBefore = host.Owner.Revision;
        var changesBefore = ChangeCount(host);

        await picker.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Task.Delay(900, TestContext.Current.CancellationToken);

        var status = Status(game);
        await Expect(status).ToContainTextAsync(SavedText);
        await Expect(status.GetByRole(AriaRole.Button, new() { Name = "Undo" })).ToBeVisibleAsync();
        await Expect(picker).ToHaveValueAsync(ids[2]);
        posts().Should().Be(1);
        host.Owner.Revision.Should().Be(revisionBefore + 1);
        ChangeCount(host).Should().Be(changesBefore + 1);
        (await ReadCardsAsync()).Single(card => card.EntryId == game.EntryId).Location.Should().Be("Shelf C");
        await Expect(Row(game).Locator(".game-where")).ToHaveTextAsync("Shelf C");

        await status.GetByRole(AriaRole.Button, new() { Name = "Undo" }).ClickAsync();

        await Expect(status).ToContainTextAsync("Undone.");
        await Expect(picker).ToHaveValueAsync(string.Empty);
        ChangeCount(host).Should().Be(changesBefore);
        (await ReadCardsAsync()).Single(card => card.EntryId == game.EntryId).Location.Should().BeNull();
        AssertCleanConsole();
    }

    [Fact]
    public async Task A_pointer_choice_and_Tab_after_arrowing_save_at_once()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        var ids = await host.SeedAsync("Cabinet A", "Shelf B");
        await GotoLocationsAsync();
        var first = host.Card(QuarryBase);
        var second = host.Card(TenBase);
        var posts = CountAssignPosts();

        await Picker(first).SelectOptionAsync(new SelectOptionValue { Label = "Shelf B" });
        await Expect(Status(first)).ToContainTextAsync(SavedText, new() { Timeout = 500 });
        posts().Should().Be(1);

        await Picker(second).FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("Tab");

        await Expect(Status(second)).ToContainTextAsync(SavedText, new() { Timeout = 500 });
        await Expect(Picker(second)).ToHaveValueAsync(ids[1]);
        await Task.Delay(900, TestContext.Current.CancellationToken);
        posts().Should().Be(2);
        AssertCleanConsole();
    }

    [Fact]
    public async Task Stepping_back_to_where_it_started_saves_nothing()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        await host.SeedAsync("Cabinet A", "Shelf B");
        await GotoLocationsAsync();
        var game = host.Card(QuarryBase);
        var posts = CountAssignPosts();
        var changesBefore = ChangeCount(host);

        await Picker(game).FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Page.Keyboard.PressAsync("ArrowUp");
        await Page.Keyboard.PressAsync("Tab");
        await Task.Delay(900, TestContext.Current.CancellationToken);

        posts().Should().Be(0);
        ChangeCount(host).Should().Be(changesBefore);
        AssertCleanConsole();
    }

    [Fact]
    public async Task Each_kind_of_game_gets_its_own_choices_and_with_no_locations_every_picker_offers_only_Not_placed()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoLocationsAsync();

        await Expect(Page.Locator("#no-locations-note")).ToBeVisibleAsync();
        await Expect(Page.Locator("#no-locations-note")).ToHaveTextAsync("Add a location above before placing games.");
        (await Options(host.Card(QuarryBase))).Should().Equal("Not placed");
        (await Options(host.Card(OrphanExpansion))).Should().Equal("Not placed");
        (await Options(host.Card(CafeExpansion))).Should().Equal($"Same as {CafeBase}");

        await host.SeedAsync("Cabinet A", "Shelf B");
        await GotoLocationsAsync();

        await Expect(Page.Locator("#no-locations-note")).ToBeHiddenAsync();
        (await Options(host.Card(QuarryBase))).Should().Equal("Not placed", "Cabinet A", "Shelf B");
        (await Options(host.Card(OrphanExpansion))).Should().Equal("Not placed", "Cabinet A", "Shelf B");
        (await Options(host.Card(CafeExpansion))).Should().Equal($"Same as {CafeBase}", "Cabinet A", "Shelf B");
        (await Picker(host.Card(CafeExpansion)).EvaluateAsync<string>("select => select.options[select.selectedIndex].text")).Should().Be($"Same as {CafeBase}");
        await Expect(Picker(host.Card(QuarryBase))).ToHaveAttributeAsync("aria-label", $"Location for {QuarryBase}");
        AssertCleanConsole();
    }

    [Fact]
    public async Task An_expansion_takes_a_place_of_its_own_and_Same_as_its_base_goes_back_to_following()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        var ids = await host.SeedAsync("Cabinet A", "Shelf B");
        await host.PlaceAsync(host.Card(CafeBase), ids[0]);
        await GotoLocationsAsync();
        var expansion = host.Card(CafeExpansion);

        await Expect(Row(expansion).Locator(".game-where")).ToHaveTextAsync("Cabinet A");
        await Picker(expansion).SelectOptionAsync(new SelectOptionValue { Label = "Shelf B" });
        await Expect(Status(expansion)).ToContainTextAsync(SavedText);
        (await ReadCardsAsync()).Single(card => card.EntryId == expansion.EntryId).Location.Should().Be("Shelf B");
        (await ReadCardsAsync()).Single(card => card.Title == CafeBase).Location.Should().Be("Cabinet A");

        await Picker(expansion).SelectOptionAsync(new SelectOptionValue { Label = $"Same as {CafeBase}" });
        await Expect(Status(expansion).Locator("span").First).ToHaveTextAsync(SavedText);
        await Expect(Row(expansion).Locator(".game-where")).ToHaveTextAsync("Cabinet A");
        (await ReadCardsAsync()).Single(card => card.EntryId == expansion.EntryId).Location.Should().Be("Cabinet A");
        AssertCleanConsole();
    }

    [Fact]
    public async Task A_failed_save_returns_the_picker_to_its_previous_value_and_shows_the_save_failed_text()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        await host.SeedAsync("Cabinet A", "Shelf B");
        await GotoLocationsAsync();
        await Page.RouteAsync(
            new Regex(AssignFragment, RegexOptions.IgnoreCase),
            route => route.FulfillAsync(new RouteFulfillOptions { Status = 500, Body = string.Empty }));
        var game = host.Card(QuarryBase);
        var changesBefore = ChangeCount(host);

        await Picker(game).SelectOptionAsync(new SelectOptionValue { Label = "Shelf B" });

        await Expect(Status(game)).ToContainTextAsync("Couldn't save that. Nothing changed. Try again.");
        await Expect(Picker(game)).ToHaveValueAsync(string.Empty);
        await Expect(Status(game).GetByRole(AriaRole.Button, new() { Name = "Undo" })).ToHaveCountAsync(0);
        await Expect(Row(game).Locator(".game-where")).ToHaveTextAsync("Not placed");
        ChangeCount(host).Should().Be(changesBefore);
        AssertCleanConsole(allowRefusals: true);
    }

    [Fact]
    public async Task The_list_is_in_collator_order_with_expansions_under_their_base_game()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoLocationsAsync();

        var titles = await Page.Locator(".game-row .game-title").AllTextContentsAsync();
        var nested = await Page.Locator(".game-row").EvaluateAllAsync<string[]>("rows => rows.map(row => row.dataset.nested)");

        titles.Should().Equal(CollatedTitles);
        nested.Should().Equal("false", "true", "false", "true", "true", "false", "false", "false");
        host.Cards.Should().HaveCount(CollatedTitles.Length);
        AssertCleanConsole();
    }

    [Fact]
    public async Task Typing_a_query_finds_games_without_accents_keeps_a_base_as_context_and_says_what_it_shows()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoLocationsAsync();
        var find = Page.GetByLabel("Find a game");
        var status = Page.Locator("#owner-status");
        var visible = Page.Locator(".game-row:not([hidden])");

        await Expect(Page.Locator("#games-count")).ToHaveTextAsync("Showing 8 of 8 games");

        await find.FillAsync("cafe");
        await Expect(status).ToHaveTextAsync("Showing 2 of 8 games");
        (await visible.Locator(".game-title").AllTextContentsAsync()).Should().Equal(CafeBase, CafeExpansion);
        await Expect(Page.Locator("#games-count")).ToHaveTextAsync("Showing 2 of 8 games");

        await find.FillAsync("extra");
        await Expect(status).ToHaveTextAsync("Showing 2 of 8 games");
        await Expect(Row(host.Card(CafeBase))).ToHaveAttributeAsync("data-context", "true");
        await Expect(Row(host.Card(CafeBase))).ToBeVisibleAsync();
        await Expect(Row(host.Card(CafeExpansion))).Not.ToHaveAttributeAsync("data-context", "true");

        await find.FillAsync("zzz");
        await Expect(status).ToHaveTextAsync("No games match \"zzz\".");
        await Expect(Page.Locator("#games-empty")).ToHaveTextAsync("No games match.");
        await Expect(visible).ToHaveCountAsync(0);

        await find.FillAsync("<b>x</b>");
        await Expect(status).ToHaveTextAsync("No games match \"<b>x</b>\".");
        await Expect(Page.Locator("#owner-status b")).ToHaveCountAsync(0);

        await find.FillAsync(string.Empty);
        await Expect(status).ToHaveTextAsync("Showing 8 of 8 games");
        await Expect(visible).ToHaveCountAsync(8);
        await Expect(Page.Locator("#games-empty")).ToBeHiddenAsync();
        AssertCleanConsole();
    }

    [Fact]
    public async Task The_not_placed_switch_counts_filters_and_says_when_every_game_is_placed()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        var ids = await host.SeedAsync("Cabinet A", "Shelf B");
        await host.PlaceAsync(host.Card(TwoBase), ids[0]);
        await GotoLocationsAsync();
        var toggle = Page.GetByRole(AriaRole.Switch, new() { Name = "Not placed yet (5)" });
        var visible = Page.Locator(".game-row:not([hidden])");

        await Expect(toggle).Not.ToBeCheckedAsync();
        await Expect(visible).ToHaveCountAsync(8);

        await toggle.CheckAsync();

        await Expect(visible).ToHaveCountAsync(5);
        await Expect(Page.Locator("#owner-status")).ToHaveTextAsync("Showing 5 of 8 games");
        (await visible.Locator(".game-title").AllTextContentsAsync()).Should().Equal(CafeBase, CafeExpansion, TenBase, OrphanExpansion, QuarryBase);

        await Picker(host.Card(QuarryBase)).SelectOptionAsync(new SelectOptionValue { Label = "Shelf B" });
        await Expect(Status(host.Card(QuarryBase))).ToContainTextAsync(SavedText);
        await Expect(Page.GetByRole(AriaRole.Switch, new() { Name = "Not placed yet (4)" })).ToBeCheckedAsync();
        await Expect(Row(host.Card(QuarryBase))).ToBeVisibleAsync();

        foreach (var title in new[] { CafeBase, TenBase, OrphanExpansion })
        {
            await host.PlaceAsync(host.Card(title), ids[0]);
        }

        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync(".game-list[data-enhanced]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
        var everyPlaced = Page.GetByRole(AriaRole.Switch, new() { Name = "Not placed yet (0)" });
        await Expect(everyPlaced).ToBeVisibleAsync();
        await everyPlaced.SetCheckedAsync(false);
        await everyPlaced.CheckAsync();

        await Expect(Page.Locator("#games-empty")).ToHaveTextAsync("Every game is placed.");
        await Expect(Page.Locator("#owner-status")).ToHaveTextAsync("Every game is placed.");
        await Expect(visible).ToHaveCountAsync(0);
        AssertCleanConsole();
    }

    [Fact]
    public async Task The_filter_query_opens_with_the_switch_on_and_any_other_value_is_ignored()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        var ids = await host.SeedAsync("Cabinet A");
        await host.PlaceAsync(host.Card(TwoBase), ids[0]);
        await GotoLocationsAsync("?filter=unplaced");

        await Expect(Page.Locator("#unplaced-switch")).ToBeCheckedAsync();
        await Expect(Page.Locator(".game-row:not([hidden])")).ToHaveCountAsync(5);

        await GotoLocationsAsync("?filter=%3Cscript%3E");

        await Expect(Page.Locator("#unplaced-switch")).Not.ToBeCheckedAsync();
        await Expect(Page.Locator(".game-row:not([hidden])")).ToHaveCountAsync(8);
        AssertCleanConsole();
    }

    [Fact]
    public async Task Select_all_shown_ticks_what_is_on_screen_and_follows_the_ticks_of_the_rows()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        var ids = await host.SeedAsync("Cabinet A");
        await host.PlaceAsync(host.Card(TwoBase), ids[0]);
        await GotoLocationsAsync();
        var selectAll = Page.GetByLabel("Select all shown");

        await Page.GetByLabel("Find a game").FillAsync("cafe");
        await Expect(Page.Locator("#owner-status")).ToHaveTextAsync("Showing 2 of 8 games");
        await selectAll.CheckAsync();

        await Expect(Page.Locator("#dock-count")).ToHaveTextAsync("2 selected");
        await Expect(Page.Locator(".game-check:checked")).ToHaveCountAsync(2);
        await Expect(Row(host.Card(CafeBase)).Locator(".game-check")).ToBeCheckedAsync();
        await Expect(Row(host.Card(QuarryBase)).Locator(".game-check")).Not.ToBeCheckedAsync();

        await Row(host.Card(CafeExpansion)).Locator(".game-check").UncheckAsync();
        (await selectAll.EvaluateAsync<bool>("input => input.indeterminate")).Should().BeTrue();
        await Expect(Page.Locator("#dock-count")).ToHaveTextAsync("1 selected");

        await selectAll.CheckAsync();
        await Expect(selectAll).ToBeCheckedAsync();
        await selectAll.UncheckAsync();
        await Expect(Page.Locator(".game-check:checked")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#select-dock")).ToBeHiddenAsync();

        await Page.GetByLabel("Find a game").FillAsync(string.Empty);
        await Expect(Page.Locator("#owner-status")).ToHaveTextAsync("Showing 8 of 8 games");
        await selectAll.CheckAsync();
        await Expect(Page.Locator("#dock-count")).ToHaveTextAsync("8 selected");
        await Page.Locator("#dock-clear").ClickAsync();
        await Expect(selectAll).Not.ToBeCheckedAsync();
        AssertCleanConsole();
    }

    [Fact]
    public async Task With_no_games_the_page_says_so_and_offers_no_toolbar()
    {
        await StartHostAsync(SyntheticBggCollection.Create(0));
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoOwnerAsync(LocationsPath);

        await Expect(Page.GetByText("No games yet. Your games show up here after the first sync.")).ToBeVisibleAsync();
        await Expect(Page.Locator("#games-toolbar")).ToHaveCountAsync(0);
        AssertCleanConsole();
    }

    [Theory]
    [InlineData(1440)]
    [InlineData(390)]
    [InlineData(320)]
    public async Task The_toolbar_and_pickers_fit_every_width_and_every_control_is_at_least_44_pixels(int width)
    {
        var host = await StartHostAsync(items: null, size: 65);
        await Page.SetViewportSizeAsync(width, 780);
        await host.SeedAsync("Cabinet A", "Shelf B");
        await GotoLocationsAsync();
        await Page.GetByLabel("Find a game").FillAsync("example");
        await Expect(Page.Locator("#owner-status")).ToContainTextAsync("Showing");
        await SaveScreenshotAsync($"owner-picker-toolbar-{width}");

        var overflow = await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
        var targets = await Page.Locator(".owner-find input, .owner-switch, .game-form select").EvaluateAllAsync<double[][]>(
            "elements => elements.filter(element => element.getBoundingClientRect().width > 0).map(element => { const box = element.getBoundingClientRect(); return [box.width, box.height]; })");
        var rowOverflow = await Page.Locator(".game-row:not([hidden])").EvaluateAllAsync<bool[]>("rows => rows.map(row => row.scrollWidth > row.clientWidth + 1)");

        overflow.Should().BeFalse();
        rowOverflow.Should().OnlyContain(over => !over);
        targets.Should().OnlyContain(box => box[1] >= 44);
        AssertCleanConsole();
    }

    private static int ChangeCount(OwnerHost host) =>
        host.Owner.State is OwnerDataState.Ready ready ? ready.Data.Changes.Count : -1;

    private ILocator Row(CardReading card) => Page.Locator($"#game-{card.GameId}");

    private ILocator Picker(CardReading card) => Row(card).Locator("select");

    private ILocator Status(CardReading card) => Row(card).Locator(".row-status");

    private async Task<IReadOnlyList<string>> Options(CardReading card) =>
        [.. (await Picker(card).Locator("option").AllTextContentsAsync()).Select(text => text.Trim())];

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

    private async Task GotoLocationsAsync(string query = "")
    {
        await GotoOwnerAsync(LocationsPath + query);
        await Page.WaitForSelectorAsync("#your-locations[data-enhanced]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
        await Page.WaitForSelectorAsync(".game-list[data-enhanced]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
    }

    private Task<OwnerHost> StartHostAsync() => StartHostAsync(PickerCollection(), 0);

    private Task<OwnerHost> StartHostAsync(IReadOnlyList<FakeBggItem> items) => StartHostAsync(items, 0);

    private async Task<OwnerHost> StartHostAsync(IReadOnlyList<FakeBggItem>? items, int size)
    {
        var clock = SyncHarness.NewClock();
        var collection = items ?? SyntheticBggCollection.Create(size);
        var factory = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(collection), clock);

        await StartAsync(factory);
        _publicClient = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(_publicClient, clock, advance: false);

        return new OwnerHost(factory.ServingServices.GetRequiredService<OwnerDataService>(), await ReadCardsAsync());
    }

    private static IReadOnlyList<FakeBggItem> PickerCollection()
    {
        var firstGame = SyntheticBggCollection.FirstObjectId;

        return
        [
            Entry(0, TenBase, false, null),
            Entry(1, TwoBase, false, null),
            Entry(2, CafeBase, false, null),
            Entry(3, CafeExpansion, true, firstGame + 2),
            Entry(4, QuarryBase, false, null),
            Entry(5, OrphanExpansion, true, MissingBaseObjectId),
            Entry(6, ZebraExpansion, true, firstGame + 1),
            Entry(7, AppleExpansion, true, firstGame + 1),
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
                card.GetProperty("entryId").GetInt64(),
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

    private sealed record CardReading(long EntryId, int GameId, string Title, string? Location);

    private sealed class OwnerHost(OwnerDataService owner, IReadOnlyList<CardReading> cards)
    {
        public OwnerDataService Owner { get; } = owner;

        public IReadOnlyList<CardReading> Cards { get; } = cards;

        public CardReading Card(string title) => Cards.First(card => string.Equals(card.Title, title, StringComparison.Ordinal));

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

        public async Task PlaceAsync(CardReading card, string locationId)
        {
            var result = await Owner.MutateAsync(
                Owner.Revision,
                data => LocationEdits.Assign(data, card.GameId, locationId, card.Title),
                TestContext.Current.CancellationToken);

            result.Outcome.Should().Be(OwnerOutcome.Saved);
        }
    }
}

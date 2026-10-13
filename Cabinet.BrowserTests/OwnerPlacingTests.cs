using System.Globalization;
using System.Text.Json;
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
/// Proves in a real browser that the owner places games in bulk as the page promises: ticking rows, the dock that slides up, the
/// dialog of buttons that asks where the ticked games go, one change and one Undo per move, and the dock's states on a small phone.
/// </summary>
[Trait("Category", "Browser")]
public sealed class OwnerPlacingTests : CabinetPageTest
{
    private const string LocationsPath = "/owner/locations";
    private const string CardsPath = "/cabinet/cards?profile=desktop";
    private const string FailedToLoad = "Failed to load resource";
    private const string MoveDialog = "dialog[data-variant='move'][open]";
    private const string MoveToName = "Move to...";
    private const string BulkMoveFragment = "handler=BulkMove";

    private HttpClient? _publicClient;

    [Fact]
    public async Task Ticking_two_games_and_choosing_a_place_moves_both_in_one_change_and_Undo_takes_both_back()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        var ids = await host.SeedAsync("Cabinet A", "Shelf B");
        await GotoLocationsAsync();
        var first = host.BaseWithExpansion;
        var second = host.BaseWithoutExpansion;
        var expansion = host.Expansion;
        var posts = 0;
        Page.Request += (_, request) =>
        {
            if (request.Method == "POST" && request.Url.Contains(BulkMoveFragment, StringComparison.Ordinal))
            {
                Interlocked.Increment(ref posts);
            }
        };
        var revisionBefore = host.Owner.Revision;
        var changesBefore = ChangeCount(host);
        var dock = Page.Locator("#select-dock");

        await Expect(dock).ToBeHiddenAsync();
        await CheckAsync(first);
        await CheckAsync(second);

        await Expect(dock).ToBeVisibleAsync();
        await Expect(Page.Locator("#dock-count")).ToHaveTextAsync("2 selected");
        await Expect(Row(expansion).Locator(".game-check")).Not.ToBeCheckedAsync();
        await Expect(Row(expansion)).ToHaveAttributeAsync("data-nested", "true");

        await Page.GetByRole(AriaRole.Button, new() { Name = MoveToName }).ClickAsync();

        var dialog = Page.Locator(MoveDialog);
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.Locator("h3")).ToHaveTextAsync("Move 2 games to");
        await Expect(dialog.Locator(".move-option").First).ToHaveTextAsync("Cabinet A");
        await Expect(dialog.Locator(".move-option").First).ToBeFocusedAsync();
        await SaveScreenshotAsync("owner-placing-dialog-1440");

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Shelf B" }).ClickAsync();

        var status = Page.Locator("#dock-status");
        await Expect(dialog).ToHaveCountAsync(0, new() { Timeout = 2000 });
        await Expect(status).ToContainTextAsync("Moved 2 games to Shelf B");
        await Expect(status.GetByRole(AriaRole.Button, new() { Name = "Undo" })).ToBeVisibleAsync();
        await Expect(status.GetByRole(AriaRole.Button, new() { Name = "Dismiss" })).ToBeVisibleAsync();
        await Expect(status).ToBeFocusedAsync();
        await Expect(status).ToHaveAttributeAsync("role", "status");
        await Expect(Row(first).Locator(".game-where")).ToHaveTextAsync("Shelf B");
        await Expect(Row(expansion).Locator(".game-where")).ToHaveTextAsync("Shelf B");
        await Expect(Page.Locator($"#loc-{ids[1]} .loc-count")).ToHaveTextAsync("3 games");
        await SaveScreenshotAsync("owner-placing-done-1440");
        posts.Should().Be(1);
        host.Owner.Revision.Should().Be(revisionBefore + 1);
        ChangeCount(host).Should().Be(changesBefore + 1);
        var cards = await ReadCardsAsync();
        cards.Single(card => card.EntryId == first.EntryId).Location.Should().Be("Shelf B");
        cards.Single(card => card.EntryId == second.EntryId).Location.Should().Be("Shelf B");

        await status.GetByRole(AriaRole.Button, new() { Name = "Undo" }).ClickAsync();

        await Expect(status).ToContainTextAsync("Undone.");
        await Expect(Row(first).Locator(".game-where")).ToHaveTextAsync("Not placed");
        await Expect(Page.Locator($"#loc-{ids[1]} .loc-count")).ToHaveTextAsync("No games");
        ChangeCount(host).Should().Be(changesBefore);
        cards = await ReadCardsAsync();
        cards.Single(card => card.EntryId == first.EntryId).Location.Should().BeNull();
        cards.Single(card => card.EntryId == second.EntryId).Location.Should().BeNull();
        AssertCleanConsole();
    }

    [Fact]
    public async Task Escape_a_backdrop_click_and_Keep_selection_move_nothing_and_return_focus_to_Move_to()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        await host.SeedAsync("Cabinet A", "Shelf B");
        await GotoLocationsAsync();
        var revisionBefore = host.Owner.Revision;
        await CheckAsync(host.BaseWithoutExpansion);
        var moveTo = Page.GetByRole(AriaRole.Button, new() { Name = MoveToName });
        var dialog = Page.Locator(MoveDialog);

        await moveTo.ClickAsync();
        await Expect(dialog).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(dialog).ToHaveCountAsync(0);
        await Expect(moveTo).ToBeFocusedAsync();

        await moveTo.ClickAsync();
        await Page.Mouse.ClickAsync(4, 4);
        await Expect(dialog).ToHaveCountAsync(0);
        await Expect(moveTo).ToBeFocusedAsync();

        await moveTo.ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Keep selection" }).ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0);
        await Expect(moveTo).ToBeFocusedAsync();
        await Expect(Page.Locator("#dock-count")).ToHaveTextAsync("1 selected");
        host.Owner.Revision.Should().Be(revisionBefore);
        AssertCleanConsole();
    }

    [Fact]
    public async Task Not_placed_and_Same_as_base_game_word_what_happened_and_Same_as_base_game_is_offered_only_for_an_expansion_with_a_base()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        var ids = await host.SeedAsync("Cabinet A", "Shelf B");
        await host.PlaceAsync(host.BaseWithoutExpansion, ids[0]);
        await host.PlaceAsync(host.Expansion, ids[1]);
        await GotoLocationsAsync();
        var moveTo = Page.GetByRole(AriaRole.Button, new() { Name = MoveToName });
        var dialog = Page.Locator(MoveDialog);
        var status = Page.Locator("#dock-status");

        await CheckAsync(host.BaseWithoutExpansion);
        await moveTo.ClickAsync();
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Same as base game" })).ToHaveCountAsync(0);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Not placed" }).ClickAsync();
        await Expect(status).ToContainTextAsync("1 game is now not placed");
        await Expect(Row(host.BaseWithoutExpansion).Locator(".game-where")).ToHaveTextAsync("Not placed");
        await status.GetByRole(AriaRole.Button, new() { Name = "Dismiss" }).ClickAsync();
        await Expect(Page.Locator("#select-dock")).ToBeHiddenAsync();
        await Expect(Page.Locator("#games-heading")).ToBeFocusedAsync();

        await CheckAsync(host.Expansion);
        await moveTo.ClickAsync();
        var sameAsBase = dialog.GetByRole(AriaRole.Button, new() { Name = "Same as base game" });
        await Expect(sameAsBase).ToBeVisibleAsync();
        await SaveScreenshotAsync("owner-placing-dialog-expansion-1440");
        await sameAsBase.ClickAsync();

        await Expect(status).ToContainTextAsync("1 expansion now follows its base game");
        await Expect(Row(host.Expansion).Locator(".game-follow")).ToBeVisibleAsync();
        host.Games.Any(game => game.GameId == host.Expansion.GameId && game.LocationId is not null).Should().BeFalse();
        AssertCleanConsole();
    }

    [Fact]
    public async Task The_dock_announces_the_count_after_a_pause_and_says_one_selected_for_one_row()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        await host.SeedAsync("Cabinet A");
        await GotoLocationsAsync();

        await CheckAsync(host.BaseWithoutExpansion);
        await Expect(Page.Locator("#owner-status")).ToHaveTextAsync("1 selected");
        await Expect(Page.Locator("#dock-count")).ToHaveTextAsync("1 selected");

        await CheckAsync(host.BaseWithExpansion);
        await CheckAsync(host.Expansion);
        await Expect(Page.Locator("#dock-count")).ToHaveTextAsync("3 selected");
        await Expect(Page.Locator("#owner-status")).ToHaveTextAsync("3 selected");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear" }).ClickAsync();
        await Expect(Page.Locator("#select-dock")).ToBeHiddenAsync();
        await Expect(Page.Locator(".game-check:checked")).ToHaveCountAsync(0);
        AssertCleanConsole();
    }

    [Fact]
    public async Task A_failed_move_shows_the_save_failed_text_in_the_dock_keeps_the_selection_and_offers_no_Undo()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        await host.SeedAsync("Cabinet A");
        await GotoLocationsAsync();
        await Page.RouteAsync("**/owner/locations?handler=BulkMove", route => route.FulfillAsync(new RouteFulfillOptions { Status = 500, Body = string.Empty }));
        var changesBefore = ChangeCount(host);

        await CheckAsync(host.BaseWithoutExpansion);
        await Page.GetByRole(AriaRole.Button, new() { Name = MoveToName }).ClickAsync();
        await Page.Locator(MoveDialog).GetByRole(AriaRole.Button, new() { Name = "Cabinet A" }).ClickAsync();

        var status = Page.Locator("#dock-status");
        await Expect(status).ToContainTextAsync("Couldn't save that. Nothing changed. Try again.");
        await Expect(status.GetByRole(AriaRole.Button, new() { Name = "Undo" })).ToHaveCountAsync(0);
        await Expect(Page.Locator("#select-dock")).ToHaveAttributeAsync("data-state", "error");
        await Expect(Page.Locator("#dock-count")).ToBeVisibleAsync();
        await Expect(Page.Locator("#dock-count")).ToHaveTextAsync("1 selected");
        await Expect(Row(host.BaseWithoutExpansion).Locator(".game-check")).ToBeCheckedAsync();
        ChangeCount(host).Should().Be(changesBefore);
        AssertCleanConsole(allowRefusals: true);
    }

    [Fact]
    public async Task A_long_location_name_wraps_in_the_dock_and_the_dialog_and_a_long_list_scrolls_inside_the_dialog()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(390, 844);
        var longName = new string('W', 60);
        var names = Enumerable.Range(1, 49).Select(number => string.Create(CultureInfo.InvariantCulture, $"Place {number}")).Append(longName).ToArray();
        await host.SeedAsync(names);
        await GotoLocationsAsync();

        await CheckAsync(host.BaseWithoutExpansion);
        await Page.GetByRole(AriaRole.Button, new() { Name = MoveToName }).ClickAsync();

        var dialog = Page.Locator(MoveDialog);
        await Expect(dialog).ToBeVisibleAsync();
        var heights = await dialog.Locator(".move-option").EvaluateAllAsync<double[]>("buttons => buttons.map(button => button.getBoundingClientRect().height)");
        var scrolling = await dialog.Locator(".move-options").EvaluateAsync<double[]>("list => [list.scrollHeight, list.clientHeight]");
        var longHeight = await dialog.GetByRole(AriaRole.Button, new() { Name = longName }).EvaluateAsync<double>("button => button.getBoundingClientRect().height");
        await dialog.GetByRole(AriaRole.Button, new() { Name = longName }).ScrollIntoViewIfNeededAsync();
        await SaveScreenshotAsync("owner-placing-dialog-long-390");

        heights.Should().OnlyContain(height => height >= 44);
        longHeight.Should().BeGreaterThan(44);
        scrolling[0].Should().BeGreaterThan(scrolling[1]);
        scrolling[1].Should().BeLessThanOrEqualTo(448 + 1);

        await dialog.GetByRole(AriaRole.Button, new() { Name = longName }).ClickAsync();

        var status = Page.Locator("#dock-status");
        await Expect(status).ToContainTextAsync("Moved 1 game to " + longName);
        var fits = await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= document.documentElement.clientWidth");
        var statusBox = await status.EvaluateAsync<double[]>("element => { const box = element.getBoundingClientRect(); return [box.left, box.right]; }");
        fits.Should().BeTrue();
        statusBox[1].Should().BeLessThanOrEqualTo(390 + 0.5);
        await SaveScreenshotAsync("owner-placing-done-long-390");
        AssertCleanConsole();
    }

    [Theory]
    [InlineData(390)]
    [InlineData(320)]
    public async Task On_a_phone_with_many_rows_the_last_row_clears_the_dock_and_nothing_scrolls_sideways(int width)
    {
        var host = await StartHostAsync(65);
        await Page.SetViewportSizeAsync(width, 780);
        await host.SeedAsync("Cabinet A", "Shelf B");
        await GotoLocationsAsync();

        await Page.Locator(".game-check").First.CheckAsync();
        await Expect(Page.Locator("#select-dock")).ToHaveAttributeAsync("data-state", "selecting");
        await ScrollToBottomAsync();
        await SaveScreenshotAsync($"owner-placing-bottom-{width}");

        var lastBottom = await Page.Locator(".game-row").Last.EvaluateAsync<double>("row => row.getBoundingClientRect().bottom");
        var dockBox = await Page.Locator("#select-dock").EvaluateAsync<double[]>("dock => { const box = dock.getBoundingClientRect(); return [box.top, box.height, box.right]; }");
        var overflow = await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
        var targets = await Page.Locator(".game-pick, .select-dock button").EvaluateAllAsync<double[][]>(
            "elements => elements.filter(element => element.getBoundingClientRect().width > 0).map(element => { const box = element.getBoundingClientRect(); return [box.width, box.height]; })");
        var rowOverflow = await Page.Locator(".game-row").EvaluateAllAsync<bool[]>("rows => rows.map(row => row.scrollWidth > row.clientWidth + 1)");

        lastBottom.Should().BeLessThanOrEqualTo(dockBox[0] + 0.5, "the dock never hides the last row");
        overflow.Should().BeFalse();
        rowOverflow.Should().OnlyContain(over => !over);
        targets.Should().OnlyContain(box => box[0] >= 44 && box[1] >= 44);
        dockBox[2].Should().BeLessThanOrEqualTo(width + 0.5);

        if (width == 320)
        {
            var layout = await Page.EvaluateAsync<double[]>(
                "() => { const count = document.getElementById('dock-count').getBoundingClientRect(); const move = document.getElementById('dock-move').getBoundingClientRect(); return [count.bottom, move.top]; }");

            dockBox[1].Should().BeGreaterThan(64, "the dock grows to fit its second line");
            layout[0].Should().BeLessThanOrEqualTo(layout[1] + 0.5, "the buttons take a second line");
        }

        AssertCleanConsole();
    }

    [Fact]
    public async Task The_dock_slides_with_a_transition_and_has_none_when_reduced_motion_is_asked_for()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoLocationsAsync();
        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.NoPreference });

        await CheckAsync(host.BaseWithoutExpansion);
        var normal = await Page.Locator("#select-dock").EvaluateAsync<string>("dock => getComputedStyle(dock).transitionDuration");
        await Page.Locator("#dock-clear").ClickAsync();
        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });
        await CheckAsync(host.BaseWithoutExpansion);
        var reduced = await Page.Locator("#select-dock").EvaluateAsync<string>("dock => getComputedStyle(dock).transitionDuration");

        normal.Should().Be("0.2s");
        reduced.Should().Be("0s");
        AssertCleanConsole();
    }

    [Fact]
    public async Task The_done_state_stays_without_a_timer_until_a_row_is_ticked()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        await host.SeedAsync("Cabinet A");
        await GotoLocationsAsync();
        await CheckAsync(host.BaseWithoutExpansion);
        await Page.GetByRole(AriaRole.Button, new() { Name = MoveToName }).ClickAsync();
        await Page.Locator(MoveDialog).GetByRole(AriaRole.Button, new() { Name = "Cabinet A" }).ClickAsync();
        var status = Page.Locator("#dock-status");
        await Expect(status).ToContainTextAsync("Moved 1 game to Cabinet A");

        await Task.Delay(TimeSpan.FromSeconds(10.5), TestContext.Current.CancellationToken);

        await Expect(status).ToContainTextAsync("Moved 1 game to Cabinet A");
        await Expect(status.GetByRole(AriaRole.Button, new() { Name = "Undo" })).ToBeVisibleAsync();

        await CheckAsync(host.BaseWithExpansion);

        await Expect(status).ToBeEmptyAsync();
        await Expect(Page.Locator("#dock-count")).ToHaveTextAsync("1 selected");
        await Expect(Page.Locator("#select-dock")).ToHaveAttributeAsync("data-state", "selecting");
        AssertCleanConsole();
    }

    [Theory]
    [InlineData(390)]
    [InlineData(320)]
    public async Task On_a_phone_the_done_line_is_the_message_then_Undo_and_Dismiss_side_by_side_below_it(int width)
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(width, 844);
        await host.SeedAsync("Cabinet A", "Shelf B");
        await GotoLocationsAsync();
        await CheckAsync(host.BaseWithoutExpansion);
        await CheckAsync(host.BaseWithExpansion);
        await Page.GetByRole(AriaRole.Button, new() { Name = MoveToName }).ClickAsync();
        await Page.Locator(MoveDialog).GetByRole(AriaRole.Button, new() { Name = "Shelf B" }).ClickAsync();
        var status = Page.Locator("#dock-status");
        await Expect(status).ToContainTextAsync("Moved 2 games to Shelf B");

        var message = (await status.Locator("span").First.BoundingBoxAsync())!;
        var undo = (await status.GetByRole(AriaRole.Button, new() { Name = "Undo" }).BoundingBoxAsync())!;
        var dismiss = (await status.GetByRole(AriaRole.Button, new() { Name = "Dismiss" }).BoundingBoxAsync())!;
        var dock = (await Page.Locator("#select-dock").BoundingBoxAsync())!;

        undo.Y.Should().BeGreaterThanOrEqualTo(message.Y + message.Height, "the buttons sit on the line below the message");
        Math.Abs(undo.Y - dismiss.Y).Should().BeLessThanOrEqualTo(1, "Undo and Dismiss share one line");
        dismiss.X.Should().BeGreaterThan(undo.X + undo.Width - 1, "Dismiss follows Undo on the same line");
        dock.Height.Should().BeLessThanOrEqualTo(110);
        (await Page.EvaluateAsync<double>("() => document.documentElement.scrollWidth - window.innerWidth")).Should().BeLessThanOrEqualTo(0);
        await SaveScreenshotAsync($"owner-placing-done-two-lines-{width}");
        AssertCleanConsole();
    }

    private async Task ScrollToBottomAsync()
    {
        var previous = -1.0;

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var reached = await Page.EvaluateAsync<double>(
                "() => new Promise(resolve => { window.scrollTo(0, document.documentElement.scrollHeight); requestAnimationFrame(() => requestAnimationFrame(() => resolve(document.documentElement.scrollHeight))); })");

            if (Math.Abs(reached - previous) < 0.5)
            {
                return;
            }

            previous = reached;
        }
    }

    private ILocator Row(CardReading card) => Page.Locator($"#game-{card.GameId}");

    private async Task CheckAsync(CardReading card)
    {
        await Row(card).Locator(".game-check").CheckAsync();
    }

    private Task WaitEnhancedAsync() =>
        Page.WaitForSelectorAsync("#your-locations[data-enhanced]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });

    private async Task GotoLocationsAsync()
    {
        await GotoOwnerAsync(LocationsPath);
        await WaitEnhancedAsync();
        await Page.WaitForSelectorAsync(".game-list[data-enhanced]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
    }

    private static int ChangeCount(OwnerHost host) =>
        host.Owner.State is OwnerDataState.Ready ready ? ready.Data.Changes.Count : -1;

    private async Task<OwnerHost> StartHostAsync(int size = 5)
    {
        var clock = SyncHarness.NewClock();
        var factory = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(size)), clock);

        await StartAsync(factory);
        _publicClient = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(_publicClient, clock, advance: false);

        return new OwnerHost(factory.ServingServices.GetRequiredService<OwnerDataService>(), await ReadCardsAsync());
    }

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
                card.GetProperty("isExpansion").GetBoolean(),
                card.TryGetProperty("location", out var location) ? location.GetString() : null)),
        ];
    }

    private void AssertCleanConsole(bool allowRefusals = false)
    {
        var errors = allowRefusals ? ConsoleErrors.Where(error => !error.Contains(FailedToLoad, StringComparison.Ordinal)).ToList() : [.. ConsoleErrors];

        errors.Should().BeEmpty();
        RequestHosts.Should().OnlyContain(host => host == $"127.0.0.1:{OwnerPort}");
    }

    private sealed record CardReading(long EntryId, int GameId, string Title, bool IsExpansion, string? Location);

    private sealed class OwnerHost(OwnerDataService owner, IReadOnlyList<CardReading> cards)
    {
        public OwnerDataService Owner { get; } = owner;

        public IReadOnlyList<CardReading> Cards { get; } = cards;

        public CardReading BaseWithExpansion => Cards.First(card => card.Title == "Example Game 1");

        public CardReading BaseWithoutExpansion => Cards.First(card => card.Title == "Lantern & Harbour");

        public CardReading Expansion => Cards.First(card => card.IsExpansion);

        public IReadOnlyList<OwnerGame> Games => Owner.State is OwnerDataState.Ready ready ? ready.Data.Games : [];

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

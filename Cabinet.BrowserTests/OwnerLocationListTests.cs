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
/// Proves in a real browser that the owner's list of locations works as the page promises: add, rename in place, move with focus
/// kept, delete with a question that names the count, Undo of the latest change, inline name errors, a closed panel once there are
/// locations, a phone layout that fits, and every action without script.
/// </summary>
[Trait("Category", "Browser")]
public sealed class OwnerLocationListTests : CabinetPageTest
{
    private const string LocationsPath = "/owner/locations";
    private const string CardsPath = "/cabinet/cards?profile=desktop";
    private const string AddButtonName = "Add location";
    private const string FailedToLoad = "Failed to load resource";

    private HttpClient? _publicClient;

    [Fact]
    public async Task A_location_is_added_renamed_and_deleted_with_the_status_and_Undo_of_each_and_the_card_follows_the_rename()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoLocationsAsync(open: false);
        var game = host.BaseWithoutExpansion;

        await Expect(Page.Locator("#your-locations[open]")).ToHaveCountAsync(1);
        await Expect(Page.Locator("#loc-empty")).ToBeVisibleAsync();
        await Page.Locator("#new-location").FillAsync("Cabinet A");
        await Page.GetByRole(AriaRole.Button, new() { Name = AddButtonName }).ClickAsync();

        var item = Page.Locator(".loc-item");
        await Expect(item).ToHaveCountAsync(1);
        await Expect(item.Locator(".loc-name")).ToHaveValueAsync("Cabinet A");
        await Expect(Page.Locator("#new-location")).ToHaveValueAsync(string.Empty);
        await Expect(Page.Locator("#new-location")).ToBeFocusedAsync();
        await Expect(Page.Locator("#new-location-status")).ToContainTextAsync("Added \"Cabinet A\"");
        await Expect(Page.Locator("#new-location-status button")).ToHaveTextAsync("Undo");
        await Expect(Page.Locator("#locations-heading")).ToHaveTextAsync("Your locations (1)");
        await Expect(Page.Locator("#loc-empty")).ToBeHiddenAsync();
        await Expect(Page.Locator("#your-locations[open]")).ToHaveCountAsync(1);

        await Page.Locator($"#game-{game.GameId} select").SelectOptionAsync(new SelectOptionValue { Label = "Cabinet A" });
        await Expect(Page.Locator($"#game-{game.GameId} .row-status")).ToContainTextAsync("Saved");
        (await ReadCardsAsync()).Single(card => card.EntryId == game.EntryId).Location.Should().Be("Cabinet A");

        var changesBefore = ChangeCount(host);
        var field = Page.Locator(".loc-item .loc-name");
        await field.FillAsync("Shelf B");
        await field.PressAsync("Enter");

        var status = Page.Locator(".loc-item .row-status");
        await Expect(status).ToContainTextAsync("Renamed to \"Shelf B\"");
        await Expect(status.Locator("button")).ToHaveTextAsync("Undo");
        await Expect(field).ToHaveValueAsync("Shelf B");
        await Expect(field).ToBeFocusedAsync();
        (await ReadCardsAsync()).Single(card => card.EntryId == game.EntryId).Location.Should().Be("Shelf B");
        ChangeCount(host).Should().Be(changesBefore + 1);
        await Expect(Page.Locator($"#game-{game.GameId} select option[value=\"{host.LocationIdOf("Shelf B")}\"]")).ToHaveTextAsync("Shelf B");

        await status.Locator("button").ClickAsync();

        await Expect(field).ToHaveValueAsync("Cabinet A");
        await Expect(status).ToContainTextAsync("Undone.");
        (await ReadCardsAsync()).Single(card => card.EntryId == game.EntryId).Location.Should().Be("Cabinet A");
        ChangeCount(host).Should().Be(changesBefore);

        await Page.Locator("#new-location").FillAsync("Crate C");
        await Page.Locator("#new-location").PressAsync("Enter");
        await Expect(Page.Locator(".loc-item")).ToHaveCountAsync(2);
        var crate = Page.Locator(".loc-item").Nth(1);
        await crate.GetByRole(AriaRole.Button, new() { Name = "Delete Crate C" }).ClickAsync();

        await Expect(Page.Locator(".loc-item")).ToHaveCountAsync(1);
        await Expect(Page.Locator("dialog[open]")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#new-location-status")).ToContainTextAsync("Deleted \"Crate C\"");
        await Expect(Page.Locator("#new-location-status button")).ToHaveTextAsync("Undo");
        await Expect(Page.Locator("#new-location")).ToBeFocusedAsync();

        await Page.Locator("#new-location-status button").ClickAsync();

        await Expect(Page.Locator(".loc-item")).ToHaveCountAsync(2);
        await Expect(Page.Locator(".loc-item").Nth(1).Locator(".loc-name")).ToHaveValueAsync("Crate C");
        await SaveScreenshotAsync("owner-locations-1440");
        AssertCleanConsole();
    }

    [Fact]
    public async Task Deleting_a_location_with_games_asks_first_with_the_count_and_Keep_it_has_focus()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        var shelf = await host.SeedAsync("Cabinet A", "Shelf B");
        await host.PlaceAsync(host.BaseWithExpansion, shelf[1]);
        await GotoLocationsAsync();

        var item = Page.Locator(".loc-item").Nth(1);
        var delete = item.GetByRole(AriaRole.Button, new() { Name = "Delete Shelf B" });
        await Expect(item.Locator(".loc-count")).ToHaveTextAsync("3 games");
        await delete.ClickAsync();

        var dialog = Page.Locator("dialog.owner-dialog[open]");
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.Locator("h3")).ToHaveTextAsync("Delete \"Shelf B\"?");
        await Expect(dialog.Locator(".warning-line")).ToHaveTextAsync("3 games will become not placed. They aren't moved anywhere else.");
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Keep it" })).ToBeFocusedAsync();
        await SaveScreenshotAsync("owner-locations-dialog-1440");

        await Page.Keyboard.PressAsync("Escape");

        await Expect(dialog).ToHaveCountAsync(0);
        await Expect(delete).ToBeFocusedAsync();
        await Expect(Page.Locator(".loc-item")).ToHaveCountAsync(2);

        await delete.ClickAsync();
        await Expect(dialog).ToBeVisibleAsync();
        await Page.Mouse.ClickAsync(4, 4);

        await Expect(dialog).ToHaveCountAsync(0);
        await Expect(delete).ToBeFocusedAsync();

        await delete.ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Keep it" }).ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0);
        await Expect(delete).ToBeFocusedAsync();
        var shown = (await ReadCardsAsync()).Count(card => card.Location == "Shelf B");
        shown.Should().BeGreaterThanOrEqualTo(2);

        await delete.ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Delete location" }).ClickAsync();

        await Expect(Page.Locator(".loc-item")).ToHaveCountAsync(1);
        await Expect(Page.Locator("#new-location-status")).ToContainTextAsync("Deleted \"Shelf B\". 3 games are now not placed.");
        await Expect(Page.Locator("#new-location-status button")).ToHaveTextAsync("Undo");
        await Expect(Page.Locator("#new-location")).ToBeFocusedAsync();
        (await ReadCardsAsync()).Where(card => card.Location == "Shelf B").Should().BeEmpty();
        host.Games.Should().BeEmpty();

        await Page.Locator("#new-location-status button").ClickAsync();

        await Expect(Page.Locator(".loc-item")).ToHaveCountAsync(2);
        await Expect(Page.Locator(".loc-item").Nth(1).Locator(".loc-name")).ToHaveValueAsync("Shelf B");
        (await ReadCardsAsync()).Count(card => card.Location == "Shelf B").Should().Be(shown);
        AssertCleanConsole();
    }

    [Fact]
    public async Task The_delete_question_for_one_game_uses_the_singular_wording()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        var shelf = await host.SeedAsync("Shelf B");
        await host.PlaceAsync(host.BaseWithoutExpansion, shelf[0]);
        await GotoLocationsAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Delete Shelf B" }).ClickAsync();

        await Expect(Page.Locator("dialog[open] .warning-line")).ToHaveTextAsync("1 game will become not placed. It isn't moved anywhere else.");
        await Page.Locator("dialog[open]").GetByRole(AriaRole.Button, new() { Name = "Delete location" }).ClickAsync();
        await Expect(Page.Locator("#new-location-status")).ToContainTextAsync("Deleted \"Shelf B\". 1 game is now not placed.");
        AssertCleanConsole();
    }

    [Fact]
    public async Task Moving_keeps_focus_on_the_same_button_announces_the_new_place_and_disables_the_ends_without_losing_focus()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        var ids = await host.SeedAsync("Cabinet A", "Shelf B", "Crate C");
        await GotoLocationsAsync();

        var first = Page.Locator("#loc-" + ids[0]);
        var down = first.GetByRole(AriaRole.Button, new() { Name = "Move Cabinet A down" });
        await Expect(first.GetByRole(AriaRole.Button, new() { Name = "Move Cabinet A up" })).ToHaveAttributeAsync("aria-disabled", "true");
        await Expect(down).ToContainTextAsync("Down");
        await down.FocusAsync();
        await down.ClickAsync();

        await Expect(first.Locator(".row-status")).ToContainTextAsync("Moved \"Cabinet A\" down. Now 2 of 3.");
        await Expect(down).ToBeFocusedAsync();
        await ExpectNamesAsync(Page, ["Shelf B", "Cabinet A", "Crate C"]);
        await Expect(Page.Locator("#owner-status")).ToHaveTextAsync("Moved \"Cabinet A\" down. Now 2 of 3.");

        await down.ClickAsync();

        await ExpectNamesAsync(Page, ["Shelf B", "Crate C", "Cabinet A"]);
        await Expect(down).ToHaveAttributeAsync("aria-disabled", "true");
        await Expect(down).ToBeFocusedAsync();
        await Expect(Page.Locator(".loc-item .loc-name").First).ToHaveAttributeAsync("aria-label", "Name of location 1");
        await Page.ReloadAsync();
        await WaitEnhancedAsync();
        await OpenPanelAsync();
        await ExpectNamesAsync(Page, ["Shelf B", "Crate C", "Cabinet A"]);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Move Cabinet A up" }).ClickAsync();
        await first.Locator(".row-status button").ClickAsync();
        await ExpectNamesAsync(Page, ["Shelf B", "Crate C", "Cabinet A"]);
        await Expect(Page.Locator("#owner-status")).ToHaveTextAsync("Undone.");
        AssertCleanConsole();
    }

    [Fact]
    public async Task A_location_added_by_the_script_carries_the_saved_revision_in_its_own_forms()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        await host.SeedAsync("Shelf B");
        await GotoLocationsAsync();

        await Page.Locator("#new-location").FillAsync("Crate C");
        await Page.Locator("#new-location").PressAsync("Enter");

        await Expect(Page.Locator(".loc-item")).ToHaveCountAsync(2);
        var revision = host.Owner.Revision.ToString(CultureInfo.InvariantCulture);
        await Expect(Page.Locator(".loc-item").Nth(1).Locator("input[name='revision']").First).ToHaveValueAsync(revision);
        var stale = await Page.Locator("input[name='revision'][data-page-revision]").EvaluateAllAsync<int>(
            "inputs => inputs.filter(input => input.value !== '" + revision + "').length");
        stale.Should().Be(0);
        AssertCleanConsole();
    }

    [Fact]
    public async Task A_duplicate_name_is_refused_inline_and_nothing_changes()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        await host.SeedAsync("Shelf B");
        await GotoLocationsAsync();
        var revisionBefore = host.Owner.Revision;

        await Page.Locator("#new-location").FillAsync("shelf b");
        await Page.GetByRole(AriaRole.Button, new() { Name = AddButtonName }).ClickAsync();

        var alert = Page.Locator("#new-location-error");
        await Expect(alert).ToHaveTextAsync("You already have a location called \"shelf b\".");
        await Expect(alert).ToHaveAttributeAsync("role", "alert");
        await Expect(Page.Locator("#new-location")).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(Page.Locator("#new-location")).ToHaveAttributeAsync("aria-describedby", "new-location-hint new-location-error");
        await Expect(Page.Locator(".loc-item")).ToHaveCountAsync(1);
        host.Owner.Revision.Should().Be(revisionBefore);

        await Page.Locator("#new-location").FillAsync("Crate C");
        await Page.Locator("#new-location").PressAsync("Enter");
        await Expect(alert).ToBeEmptyAsync();
        await Expect(Page.Locator("#new-location")).Not.ToHaveAttributeAsync("aria-invalid", "true");

        var field = Page.Locator(".loc-item").First.Locator(".loc-name");
        await field.FillAsync("crate c");
        await field.PressAsync("Enter");
        var itemAlert = Page.Locator(".loc-item").First.Locator(".field-error");
        await Expect(itemAlert).ToHaveTextAsync("You already have a location called \"crate c\".");
        await Expect(field).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(field).ToHaveAttributeAsync("aria-describedby", await itemAlert.GetAttributeAsync("id") ?? string.Empty);
        await field.PressAsync("Escape");
        await Expect(field).ToHaveValueAsync("Shelf B");
        await Expect(itemAlert).ToBeEmptyAsync();

        await Page.Locator("#new-location").FillAsync(string.Empty);
        await Page.Locator("#new-location").PressAsync("Enter");
        await Expect(Page.Locator("#new-location")).ToBeFocusedAsync();
        AssertCleanConsole(allowRefusals: true);
    }

    [Fact]
    public async Task Submitting_the_add_form_twice_quickly_adds_one_location_and_shows_no_error()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoLocationsAsync(open: false);
        var posts = 0;
        Page.Request += (_, request) =>
        {
            if (request.Method == "POST" && request.Url.Contains("handler=AddLocation", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref posts);
            }
        };
        await Page.RouteAsync("**/owner/locations?handler=AddLocation", async route =>
        {
            await Task.Delay(500);
            await route.ContinueAsync();
        });

        await Page.Locator("#new-location").FillAsync("Cabinet A");
        await Page.Locator("#new-location").PressAsync("Enter");
        await Page.Locator("#new-location").PressAsync("Enter");

        await Expect(Page.Locator("#new-location-status")).ToContainTextAsync("Added \"Cabinet A\"");
        await Expect(Page.Locator("#new-location-error")).ToBeEmptyAsync();
        await Expect(Page.Locator(".loc-item")).ToHaveCountAsync(1);
        Volatile.Read(ref posts).Should().Be(1);
        host.Locations.Should().ContainSingle();
    }

    [Fact]
    public async Task Pressing_Undo_twice_quickly_undoes_once_and_keeps_the_Undone_line()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        await host.SeedAsync("Cabinet A");
        await GotoLocationsAsync();
        var field = Page.Locator(".loc-item .loc-name");
        await field.FillAsync("Shelf B");
        await field.PressAsync("Enter");
        var status = Page.Locator(".loc-item .row-status");
        await Expect(status.Locator("button")).ToHaveTextAsync("Undo");
        var undos = 0;
        Page.Request += (_, request) =>
        {
            if (request.Method == "POST" && request.Url.Contains("handler=Undo", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref undos);
            }
        };
        await Page.RouteAsync("**/owner/locations?handler=Undo", async route =>
        {
            await Task.Delay(500);
            await route.ContinueAsync();
        });

        await status.Locator("button").ClickAsync(new LocatorClickOptions { ClickCount = 2 });

        await Expect(status).ToContainTextAsync("Undone.");
        await Expect(status).Not.ToHaveAttributeAsync("data-problem", "true");
        await Expect(field).ToHaveValueAsync("Cabinet A");
        Volatile.Read(ref undos).Should().Be(1);
        host.Locations.Select(location => location.Name).Should().Equal("Cabinet A");
    }

    [Fact]
    public async Task The_panel_opens_empty_closes_with_locations_and_stays_open_when_the_first_location_is_added()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(390, 844);
        await GotoLocationsAsync(open: false);

        await Expect(Page.Locator("#your-locations[open]")).ToHaveCountAsync(1);
        await Expect(Page.Locator("#locations-heading")).ToHaveTextAsync("Your locations");
        await Expect(Page.Locator("#loc-empty")).ToHaveTextAsync("No locations yet. Add one to start placing games.");
        await Expect(Page.Locator("#new-location-hint")).ToHaveTextAsync("Shown to every visitor. Avoid people's names.");
        await Page.Locator("#new-location").FillAsync("Cabinet A");
        await Page.Locator("#new-location").PressAsync("Enter");
        await Expect(Page.Locator(".loc-item")).ToHaveCountAsync(1);
        await Expect(Page.Locator("#your-locations[open]")).ToHaveCountAsync(1);

        await Page.ReloadAsync();
        await WaitEnhancedAsync();

        await Expect(Page.Locator("#your-locations[open]")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#locations-heading")).ToHaveTextAsync("Your locations (1)");
        await Expect(Page.Locator("#your-locations summary")).ToBeVisibleAsync();
        await Expect(Page.Locator(".loc-item")).ToBeHiddenAsync();
        var gamesTop = await Page.Locator("#games-heading").EvaluateAsync<double>("element => element.getBoundingClientRect().top");
        gamesTop.Should().BeLessThan(500, "the games come first once the list is closed");
        await Page.Locator("#your-locations summary").ClickAsync();
        await Expect(Page.Locator(".loc-item")).ToBeVisibleAsync();
        host.Owner.State.Should().BeOfType<OwnerDataState.Ready>();
        AssertCleanConsole();
    }

    [Theory]
    [InlineData(390)]
    [InlineData(320)]
    public async Task On_a_phone_each_item_is_two_lines_with_phone_sized_buttons_and_nothing_scrolls_sideways(int width)
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(width, 844);
        var longName = new string('W', 60);
        await host.SeedAsync("Cabinet A", longName, "Crate C");
        await host.PlaceAsync(host.BaseWithExpansion, host.LocationIdOf("Cabinet A"));
        await GotoLocationsAsync(query: "?item=" + host.LocationIdOf("Cabinet A"));

        await SaveScreenshotAsync($"owner-locations-{width}");
        var item = Page.Locator(".loc-item").First;
        var name = await item.Locator(".loc-name").EvaluateAsync<double[]>("element => { const box = element.getBoundingClientRect(); return [box.top, box.bottom, box.width]; }");
        var panelBox = await item.EvaluateAsync<double[]>("element => { const box = element.getBoundingClientRect(); return [box.left, box.right]; }");
        var controls = await item.Locator("button:not(.loc-save)").EvaluateAllAsync<double[][]>(
            "elements => elements.map(element => { const box = element.getBoundingClientRect(); return [box.top, box.bottom, box.left, box.right, box.width, box.height]; })");
        var buttons = await Page.Locator(".loc-item button:not(.loc-save), .owner-btn-primary").EvaluateAllAsync<double[][]>(
            "elements => elements.filter(element => element.getBoundingClientRect().width > 0).map(element => { const box = element.getBoundingClientRect(); return [box.width, box.height]; })");
        var overflow = await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
        var itemOverflow = await Page.Locator(".loc-item").EvaluateAllAsync<bool[]>("items => items.map(item => item.scrollWidth > item.clientWidth + 1)");

        var count = await item.Locator(".loc-count").EvaluateAsync<double[]>("element => { const box = element.getBoundingClientRect(); return [box.top, box.bottom, box.left, box.right]; }");

        name[1].Should().BeLessThanOrEqualTo(controls.Min(box => box[0]) + 1, "the name field is on its own line");
        count[0].Should().BeGreaterThanOrEqualTo(name[1] - 1, "the count is below the name field");
        count[3].Should().BeLessThanOrEqualTo(controls.Min(box => box[2]) + 1, "the count comes before the buttons on the same line");
        name[2].Should().BeGreaterThan((panelBox[1] - panelBox[0]) * 0.9, "the name field takes the full width");
        controls.Select(box => Math.Round(box[0])).Distinct().Should().HaveCount(1, "the three buttons share the second line");
        controls.Max(box => box[3]).Should().BeLessThanOrEqualTo(panelBox[1] + 0.5);
        buttons.Should().OnlyContain(box => box[0] >= 44 && box[1] >= 44);
        overflow.Should().BeFalse();
        itemOverflow.Should().OnlyContain(over => !over);
        AssertCleanConsole();
    }

    [Fact]
    public async Task A_page_that_is_out_of_date_or_has_an_expired_token_or_loses_its_save_says_so_and_changes_nothing()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        await host.SeedAsync("Cabinet A");
        await GotoLocationsAsync();

        await host.SeedAsync("Shelf B");
        var field = Page.Locator(".loc-item").First.Locator(".loc-name");
        await field.FillAsync("Cabinet Z");
        await field.PressAsync("Enter");

        var status = Page.Locator(".loc-item").First.Locator(".row-status");
        await Expect(status).ToContainTextAsync("This page is out of date. Reload it and try again.");
        await Expect(status.GetByRole(AriaRole.Button, new() { Name = "Reload" })).ToBeVisibleAsync();
        host.Locations.Select(location => location.Name).Should().Equal("Cabinet A", "Shelf B");

        await GotoLocationsAsync();
        await Page.Context.ClearCookiesAsync();
        await Page.Locator(".loc-item").First.Locator(".loc-name").FillAsync("Cabinet Y");
        await Page.Locator(".loc-item").First.Locator(".loc-name").PressAsync("Enter");
        await Expect(Page.Locator(".loc-item").First.Locator(".row-status")).ToContainTextAsync("This page has expired. Reload it and try again.");
        await Expect(Page.Locator(".loc-item").First.Locator(".row-status button")).ToHaveTextAsync("Reload");
        host.Locations.Select(location => location.Name).Should().Equal("Cabinet A", "Shelf B");

        await GotoLocationsAsync();
        await Page.RouteAsync("**/owner/locations?handler=Rename", route => route.AbortAsync());
        var again = Page.Locator(".loc-item").First.Locator(".loc-name");
        await again.FillAsync("Cabinet X");
        await again.PressAsync("Enter");
        await Expect(Page.Locator(".loc-item").First.Locator(".row-status")).ToContainTextAsync("Couldn't save that. Nothing changed. Try again.");
        await Expect(again).ToHaveValueAsync("Cabinet A");
        AssertCleanConsole(allowRefusals: true);
    }

    [Fact]
    public async Task Without_script_every_action_works_through_forms_and_returns_to_the_same_item()
    {
        var host = await StartHostAsync();
        var plain = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            JavaScriptEnabled = false,
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
            ExtraHTTPHeaders = new Dictionary<string, string> { ["Accept-Encoding"] = "identity" },
        });
        var page = await plain.NewPageAsync();
        await page.GotoAsync($"http://127.0.0.1:{OwnerPort}{LocationsPath}");

        await page.Locator("#new-location").FillAsync("Cabinet A");
        await page.GetByRole(AriaRole.Button, new() { Name = AddButtonName }).ClickAsync();
        await page.Locator("#new-location").FillAsync("Shelf B");
        await page.GetByRole(AriaRole.Button, new() { Name = AddButtonName }).ClickAsync();
        await ExpectNamesAsync(page, ["Cabinet A", "Shelf B"]);
        await Expect(page.Locator(".loc-item").Last.Locator(".row-status")).ToContainTextAsync("Saved.");

        var first = page.Locator(".loc-item").First;
        await first.Locator(".loc-name").FillAsync("Cabinet Z");
        await first.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await ExpectNamesAsync(page, ["Cabinet Z", "Shelf B"]);
        page.Url.Should().Contain("done=saved").And.Contain("#loc-");
        await Expect(page.Locator(".loc-item").First.Locator(".row-status")).ToContainTextAsync("Saved.");
        await Expect(page.Locator(".loc-item").First.Locator(".row-status button")).ToHaveTextAsync("Undo");

        await page.Locator(".loc-item").First.GetByRole(AriaRole.Button, new() { Name = "Move Cabinet Z down" }).ClickAsync();
        await ExpectNamesAsync(page, ["Shelf B", "Cabinet Z"]);

        var game = host.BaseWithoutExpansion;
        await host.PlaceAsync(game, host.LocationIdOf("Shelf B"));
        await page.GotoAsync($"http://127.0.0.1:{OwnerPort}{LocationsPath}");
        await page.Locator("#your-locations summary").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Delete Shelf B" }).ClickAsync();

        page.Url.Should().Contain("confirm=");
        await Expect(page.Locator(".loc-confirm .warning-line")).ToContainTextAsync("1 game will become not placed. It isn't moved anywhere else.");
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Keep it" })).ToBeVisibleAsync();
        (await ReadCardsAsync()).Single(card => card.EntryId == game.EntryId).Location.Should().Be("Shelf B");
        await page.GetByRole(AriaRole.Button, new() { Name = "Delete location" }).ClickAsync();

        await Expect(page.Locator(".loc-item")).ToHaveCountAsync(1);
        await Expect(page.Locator(".loc-item").First.Locator(".row-status")).ToContainTextAsync("Saved.");
        (await ReadCardsAsync()).Single(card => card.EntryId == game.EntryId).Location.Should().BeNull();

        await page.Locator("#new-location").FillAsync("cabinet z");
        await page.GetByRole(AriaRole.Button, new() { Name = AddButtonName }).ClickAsync();
        await Expect(page.Locator("#new-location-error")).ToHaveTextAsync("You already have a location with that name.");
        await Expect(page.Locator("#new-location-error")).ToHaveAttributeAsync("role", "alert");
        await Expect(page.Locator("#your-locations[open]")).ToHaveCountAsync(1);
        await plain.CloseAsync();
    }

    private static async Task ExpectNamesAsync(IPage page, string[] expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        string[] names;

        do
        {
            names = await page.Locator(".loc-item .loc-name").EvaluateAllAsync<string[]>("fields => fields.map(field => field.value)");

            if (names.SequenceEqual(expected))
            {
                return;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
        while (DateTime.UtcNow < deadline);

        names.Should().Equal(expected);
    }

    private Task WaitEnhancedAsync() =>
        Page.WaitForSelectorAsync("#your-locations[data-enhanced]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });

    private async Task GotoLocationsAsync(bool open = true, string query = "")
    {
        await GotoOwnerAsync(LocationsPath + query);
        await WaitEnhancedAsync();

        if (open)
        {
            await OpenPanelAsync();
        }
    }

    private async Task OpenPanelAsync()
    {
        if (await Page.Locator("#your-locations[open]").CountAsync() == 0)
        {
            await Page.Locator("#your-locations summary").ClickAsync();
        }

        await Expect(Page.Locator("#your-locations[open]")).ToHaveCountAsync(1);
    }

    private static int ChangeCount(OwnerHost host) =>
        host.Owner.State is OwnerDataState.Ready ready ? ready.Data.Changes.Count : -1;

    private async Task<OwnerHost> StartHostAsync()
    {
        var clock = SyncHarness.NewClock();
        var factory = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5)), clock);

        await StartAsync(factory);
        _publicClient = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(_publicClient, clock, advance: false);

        var host = new OwnerHost(factory.ServingServices.GetRequiredService<OwnerDataService>(), await ReadCardsAsync());

        return host;
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

        public IReadOnlyList<OwnerLocation> Locations => Owner.State is OwnerDataState.Ready ready ? ready.Data.Locations : [];

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

        public string LocationIdOf(string name) =>
            Locations.FirstOrDefault(location => location.Name == name)?.Id
            ?? throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"No location called {name}."));
    }
}

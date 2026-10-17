using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the cabinet at the owner address leads into the tools: every card ends with an Edit link to that
/// game's single-game view, and every box whose game has no place carries a small corner tag and a longer accessible name. It also
/// proves that none of it reaches the public address, that the tags re-appear after the cabinet is redrawn, and that the quiet
/// states (every game placed, the card data failing, forced colours, an open card) behave.
/// </summary>
[Trait("Category", "Browser")]
public sealed class OwnerCabinetTests : CabinetPageTest
{
    private const string PicturesPath = "/owner/pictures";
    private const string QuarryBase = "Quarry Lights";
    private const string CafeBase = "Café Quarter";
    private const string CafeExpansion = "Café Quarter: Extra Pack";
    private const string LongTitle = "Cartographers of the Endless Amber Hills and Wandering Tides";
    private const string OpenCard = "dialog.card-dialog[open]";
    private const string Boxes = "#cabinet .placement:not([data-kind=\"moreMarker\"])";
    private const string FailedToLoad = "Failed to load resource";
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task At_the_owner_address_an_unplaced_box_carries_the_tag_and_its_card_ends_with_an_Edit_link_to_its_pictures_view()
    {
        var host = await StartHostAsync();
        var ids = await host.SeedAsync("Cabinet A", "Shelf B");
        var placed = host.Card(QuarryBase);
        await host.PlaceAsync(placed, ids[0]);
        await Page.SetViewportSizeAsync(1440, 900);

        await GotoOwnerCabinetAsync();

        await Expect(Box(host.Card(CafeBase))).ToHaveAttributeAsync("data-unplaced", string.Empty);
        await Expect(Box(placed)).Not.ToHaveAttributeAsync("data-unplaced", string.Empty);
        foreach (var card in host.Cards.Where(card => card.GameId != placed.GameId))
        {
            var box = Box(card);
            await Expect(box).ToHaveAttributeAsync("data-unplaced", string.Empty);
            (await box.GetAttributeAsync("aria-label")).Should().EndWith(", not placed");
            (await box.GetAttributeAsync("title")).Should().NotContain("not placed");
        }

        (await Box(placed).GetAttributeAsync("aria-label")).Should().Be(QuarryBase);
        await SaveScreenshotAsync("owner-cabinet-1440");

        var target = host.Card(CafeBase);
        await Box(target).ClickAsync();

        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        var last = Page.Locator($"{OpenCard} .card-ruled > :last-child");
        await Expect(last).ToHaveClassAsync("card-edit");
        await Expect(Page.Locator($"{OpenCard} .card-link + .card-edit")).ToHaveCountAsync(1);
        await Expect(Page.Locator($"{OpenCard} .card-edit")).ToHaveCountAsync(1);
        var link = last.Locator("a");
        await Expect(link).ToHaveAttributeAsync("href", $"{PicturesPath}?game={target.GameId}");
        await Expect(link).ToHaveTextAsync("Edit");
        await Expect(link.Locator("svg use")).ToHaveAttributeAsync("href", new Regex("#i-pencil$"));
        (await last.EvaluateAsync<string>("line => getComputedStyle(line).paddingBottom")).Should().Be("28px", "the card ends on a blank ruled line below Edit");
        await Expect(Page.Locator("html[data-pull-kind]")).ToHaveCountAsync(0);
        await SaveScreenshotAsync("owner-cabinet-card-1440");

        await link.ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex($@"{PicturesPath}\?game={target.GameId}$"));
        await Expect(Page.Locator(".pic-row")).ToHaveCountAsync(1);
        await Expect(Page.Locator(".pic-row .game-title")).ToHaveTextAsync(CafeBase);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_bar_sits_below_the_strip_and_tags_stay_above_the_shadow()
    {
        var host = await StartHostAsync();
        var dimmed = host.Card(CafeBase);
        await Page.SetViewportSizeAsync(1440, 900);

        await GotoOwnerCabinetAsync();
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();

        var strip = await RectOf(".owner-bar[data-variant=\"strip\"]");
        var bar = await RectOf(".filter-bar");
        bar.Top.Should().BeGreaterThanOrEqualTo(strip.Bottom - 0.5, "the filter bar sits below the owner strip");

        await Page.Locator("#filter-q").FillAsync(QuarryBase);
        var box = Box(dimmed);
        await Expect(box).ToHaveAttributeAsync("data-dim", string.Empty);
        await Expect(box).ToHaveAttributeAsync("data-unplaced", string.Empty);
        await Page.WaitForFunctionAsync(
            "(id) => { const box = document.querySelector('#cabinet .placement[data-game-id=\"' + id + '\"]'); return Number(getComputedStyle(box.querySelector(':scope > .placement-veil')).opacity) === 1; }",
            dimmed.GameId.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var reading = JsonSerializer.Deserialize<TagReading>((await box.EvaluateAsync<JsonElement>(
            """
            (box) => {
              const tag = box.querySelector(':scope > .placement-tag');
              const veil = box.querySelector(':scope > .placement-veil');
              tag.style.setProperty('pointer-events', 'auto');
              veil.style.setProperty('pointer-events', 'auto');
              const rect = tag.getBoundingClientRect();
              const top = document.elementFromPoint(rect.left + rect.width / 2, rect.top + rect.height / 2);
              return {
                tagLevel: getComputedStyle(tag).zIndex,
                veilLevel: getComputedStyle(veil).zIndex,
                tagWidth: rect.width,
                topIsTag: top === tag,
                visibility: getComputedStyle(tag).visibility,
              };
            }
            """)).GetRawText(), WebOptions)!;

        reading.TagLevel.Should().Be("2");
        reading.VeilLevel.Should().Be("1");
        reading.TagWidth.Should().BeGreaterThan(0);
        reading.Visibility.Should().Be("visible");
        reading.TopIsTag.Should().BeTrue("the corner tag lies above the shadow of a dimmed box");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task An_expansion_card_links_to_the_expansion_and_follows_an_unplaced_base_with_the_tag()
    {
        var host = await StartHostAsync();
        var expansion = host.Card(CafeExpansion);
        await Page.SetViewportSizeAsync(1440, 900);

        await GotoOwnerCabinetAsync();

        await Expect(Box(expansion)).ToHaveAttributeAsync("data-unplaced", string.Empty);
        await Box(expansion).ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator($"{OpenCard} .card-edit")).ToHaveCountAsync(1);
        await Expect(Page.Locator($"{OpenCard} .card-edit a")).ToHaveAttributeAsync("href", $"{PicturesPath}?game={expansion.GameId}");
    }

    [Fact]
    public async Task With_the_Dutch_cookie_the_link_reads_Bewerken_and_the_name_ends_niet_geplaatst()
    {
        var host = await StartHostAsync();
        await Context.AddCookiesAsync([new Cookie { Name = "lang", Value = "nl", Url = $"http://127.0.0.1:{OwnerPort}" }]);
        await Page.SetViewportSizeAsync(390, 844);
        var target = host.Card(QuarryBase);

        await GotoOwnerCabinetAsync();

        await Expect(Box(target)).ToHaveAttributeAsync("data-unplaced", string.Empty);
        (await Box(target).GetAttributeAsync("aria-label")).Should().Be($"{QuarryBase}, niet geplaatst");
        await Box(target).ClickAsync();
        await Expect(Page.Locator($"{OpenCard} .card-edit a")).ToHaveTextAsync("Bewerken");
        await Expect(Page.Locator("html[data-pull-kind]")).ToHaveCountAsync(0);
        await SaveScreenshotAsync("owner-cabinet-card-nl-390");
    }

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(390, 844)]
    public async Task At_the_public_address_there_is_no_tag_no_Edit_link_no_owner_stylesheet_and_no_owner_script(int width, int height)
    {
        var host = await StartHostAsync();
        await host.SeedAsync("Cabinet A");
        await Page.SetViewportSizeAsync(width, height);
        var target = host.Card(QuarryBase);

        await GotoCabinetAsync("/");

        await Expect(Page.Locator("#cabinet .placement").First).ToBeAttachedAsync();
        await Expect(Page.Locator("[data-unplaced]")).ToHaveCountAsync(0);
        (await Page.Locator("dialog.card-dialog").GetAttributeAsync("data-edit-href")).Should().BeNull();
        await Expect(Page.Locator("[data-edit-href]")).ToHaveCountAsync(0);
        await Expect(Page.Locator("link[href*=\"owner.css\"]")).ToHaveCountAsync(0);
        await Expect(Page.Locator("script[src*=\"owner-cabinet\"]")).ToHaveCountAsync(0);
        (await Box(target).GetAttributeAsync("aria-label")).Should().Be(QuarryBase);
        await Box(target).ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator($"{OpenCard} .card-edit")).ToHaveCountAsync(0);
        await Expect(Page.Locator($"{OpenCard} .card-ruled > :last-child")).ToHaveClassAsync("card-link");
        await Expect(Page.Locator("html[data-pull-kind]")).ToHaveCountAsync(0);
        await SaveScreenshotAsync($"public-card-{width}");

        (await Page.ContentAsync()).Should().NotContain("/owner");
        foreach (var script in new[] { "cabinet.js", "card-view.js", "detail.js", "copy.js", "render.js", "card-flow.js" })
        {
            var source = await Page.EvaluateAsync<string>("name => fetch('/js/' + name).then(response => response.text())", script);
            source.Should().NotContain("/owner");
        }

        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_cabinet_where_every_game_is_placed_shows_no_tags_and_an_expansion_following_its_base_is_placed_too()
    {
        var host = await StartHostAsync();
        var ids = await host.SeedAsync("Cabinet A");
        await host.PlaceAsync(host.Card(QuarryBase), ids[0]);
        await host.PlaceAsync(host.Card(CafeBase), ids[0]);
        await host.PlaceAsync(host.Card(LongTitle), ids[0]);
        await Page.SetViewportSizeAsync(1440, 900);

        var requests = await FailCardDataRequestsAsync(0);

        await GotoOwnerCabinetAsync();
        await WaitForCardDataRequestsAsync(requests, 1);

        await Expect(Page.Locator(Boxes)).ToHaveCountAsync(host.Cards.Count);
        await Expect(Page.Locator("[data-unplaced]")).ToHaveCountAsync(0);
        foreach (var label in await Page.Locator(Boxes).EvaluateAllAsync<string[]>("boxes => boxes.map(box => box.getAttribute('aria-label'))"))
        {
            label.Should().NotContain("not placed");
        }

        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task When_the_card_data_fails_no_tags_show_and_the_cabinet_still_draws()
    {
        var host = await StartHostAsync();
        var requests = await FailCardDataRequestsAsync();
        await Page.SetViewportSizeAsync(1440, 900);

        await GotoOwnerCabinetAsync();
        await WaitForCardDataRequestsAsync(requests, 3);

        await Expect(Page.Locator(Boxes)).ToHaveCountAsync(host.Cards.Count);
        await Expect(Page.Locator("[data-unplaced]")).ToHaveCountAsync(0);
        (await Page.Locator(Boxes).First.GetAttributeAsync("aria-label")).Should().NotContain("not placed");
        ConsoleErrors.Where(error => !error.Contains(FailedToLoad, StringComparison.Ordinal)).Should().BeEmpty();
    }

    [Fact]
    public async Task Under_forced_colours_the_page_loads_cleanly_and_the_tag_is_a_two_pixel_corner_border()
    {
        var host = await StartHostAsync();
        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ForcedColors = ForcedColors.Active });
        await Page.SetViewportSizeAsync(1440, 900);

        await GotoOwnerCabinetAsync();

        var box = Box(host.Card(QuarryBase));
        await Expect(box).ToHaveAttributeAsync("data-unplaced", string.Empty);
        var border = await box.Locator(".placement-tag").EvaluateAsync<string[]>("element => { const style = getComputedStyle(element); return [style.borderTopWidth, style.borderLeftWidth, style.borderTopStyle]; }");
        border.Should().Equal("2px", "2px", "solid");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task An_open_card_hides_its_source_box_with_its_tag()
    {
        var host = await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        var target = host.Card(QuarryBase);

        await GotoOwnerCabinetAsync();
        await Expect(Box(target)).ToHaveAttributeAsync("data-unplaced", string.Empty);
        await Box(target).ClickAsync();

        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        var visibility = await Box(target).EvaluateAsync<string>("element => getComputedStyle(element).visibility");
        visibility.Should().Be("hidden");

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        (await Box(target).EvaluateAsync<string>("element => getComputedStyle(element).visibility")).Should().Be("visible");
        await Expect(Box(target)).ToHaveAttributeAsync("data-unplaced", string.Empty);
    }

    [Fact]
    public async Task After_a_sync_redraws_the_cabinet_the_tags_are_applied_again()
    {
        var bgg = new SyncRounds.SwitchableBgg(SyntheticBggCollection.Create(5));
        var clock = SyncHarness.NewClock();
        var factory = SyncHarness.CreateFactory(bgg.Handler, clock);
        await StartAsync(factory);
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoOwnerCabinetAsync();
        await Expect(Page.Locator("[data-unplaced]").First).ToBeAttachedAsync();
        var boxesBefore = await Page.Locator(Boxes).CountAsync();

        bgg.Serve([.. SyntheticBggCollection.Create(5).Take(3)]);
        await SyncRounds.PressAndWait(client, clock);

        await Expect(Page.Locator(Boxes)).Not.ToHaveCountAsync(boxesBefore);
        var boxesAfter = await Page.Locator(Boxes).CountAsync();
        boxesAfter.Should().BeLessThan(boxesBefore);
        await Expect(Page.Locator("[data-unplaced]")).ToHaveCountAsync(boxesAfter);
        foreach (var label in await Page.Locator(Boxes).EvaluateAllAsync<string[]>("boxes => boxes.map(box => box.getAttribute('aria-label'))"))
        {
            label.Should().EndWith(", not placed");
        }
    }

    [Theory]
    [InlineData("65", 1440, 900)]
    [InlineData("65", 390, 844)]
    [InlineData("400", 1440, 900)]
    [InlineData("400", 390, 844)]
    public async Task The_tag_never_sits_over_a_label_and_takes_no_pointer_input(string sample, int width, int height)
    {
        await StartAsync(PrototypeOn);
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync($"http://127.0.0.1:{OwnerPort}/?sample={sample}");
        await Page.WaitForSelectorAsync("#cabinet .placement", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
        await Expect(Page.Locator("[data-unplaced]").First).ToBeAttachedAsync();
        await SaveScreenshotAsync($"owner-cabinet-sample-{sample}-{width}");

        var overlaps = await Page.EvaluateAsync<string[]>(
            """
            () => [...document.querySelectorAll('#cabinet .placement[data-unplaced]')].flatMap(box => {
              const tag = box.querySelector('.placement-tag').getBoundingClientRect();
              return [...box.querySelectorAll('.placement-label, .placement-sub')].flatMap(label => {
                const range = document.createRange();
                range.selectNodeContents(label);
                return [...range.getClientRects()]
                  .filter(line => line.width > 0 && (line.left - tag.left) + (line.top - tag.top) < tag.width - 2 && line.left < tag.right && line.top < tag.bottom)
                  .map(line => box.dataset.kind + ' ' + box.dataset.entryId + ' tag ' + [tag.left, tag.top, tag.width].map(Math.round).join(',') + ' text ' + [line.left, line.top, line.right, line.bottom].map(Math.round).join(','));
              });
            })
            """);

        overlaps.Should().BeEmpty();
        var pointerEvents = await Page.Locator("#cabinet .placement[data-unplaced]").EvaluateAllAsync<string[]>(
            "boxes => boxes.map(box => getComputedStyle(box.querySelector('.placement-tag')).pointerEvents)");
        pointerEvents.Should().OnlyContain(events => events == "none");
    }

    [Fact]
    public async Task Above_forty_rem_the_plate_sits_before_the_toggle_in_one_cell_side_by_side()
    {
        await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);

        await GotoOwnerCabinetAsync();

        var plate = await RectOf("header .header-aside .owner-plate");
        var toggle = await RectOf("header .header-aside .lang-toggle");
        plate.Right.Should().BeLessThanOrEqualTo(toggle.Left);
        Math.Abs((plate.Top + (plate.Height / 2)) - (toggle.Top + (toggle.Height / 2))).Should().BeLessThan(2);
        await Expect(Page.Locator("header .header-aside > :first-child")).ToHaveClassAsync("owner-plate");
        await SaveScreenshotAsync("owner-header-1440");
    }

    [Theory]
    [InlineData(390, 844)]
    [InlineData(320, 640)]
    public async Task Below_forty_rem_the_plate_stacks_above_the_toggle_which_keeps_its_44_pixel_items(int width, int height)
    {
        await StartHostAsync();
        await Page.SetViewportSizeAsync(width, height);

        await GotoOwnerCabinetAsync();

        var plate = await RectOf("header .header-aside .owner-plate");
        var toggle = await RectOf("header .header-aside .lang-toggle");
        var title = await RectOf("header > h1");
        plate.Bottom.Should().BeLessThanOrEqualTo(toggle.Top + 1);
        plate.Right.Should().BeApproximately(toggle.Right, 1.5);
        title.Right.Should().BeLessThanOrEqualTo(Math.Min(plate.Left, toggle.Left) + 1);
        title.Bottom.Should().BeApproximately(toggle.Bottom, 1.5, "the title shares its line with the toggle and the plate sits above both");
        foreach (var item in await Page.Locator("header .lang-toggle a").EvaluateAllAsync<double[]>("items => items.map(item => item.getBoundingClientRect().height)"))
        {
            item.Should().BeGreaterThanOrEqualTo(44);
        }

        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= document.documentElement.clientWidth")).Should().BeTrue();
        await SaveScreenshotAsync($"owner-header-{width}");
    }

    [Fact]
    public async Task The_plate_is_not_in_the_tab_order_and_the_toggle_keeps_its_own_focus_order()
    {
        await StartHostAsync();
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoOwnerCabinetAsync();

        var focusable = await Page.EvaluateAsync<string[]>(
            "() => [...document.querySelectorAll('header a[href], header button, header [tabindex]')].filter(element => element.tabIndex >= 0).map(element => element.className || element.textContent.trim())");

        focusable.Should().NotContain("owner-plate");
        (await Page.Locator("header .owner-plate").GetAttributeAsync("tabindex")).Should().BeNull();
        await Page.Keyboard.PressAsync("Tab");
        await Page.Keyboard.PressAsync("Tab");
        var active = await Page.EvaluateAsync<string>("() => document.activeElement.textContent.trim()");
        active.Should().Be("EN");
    }

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(390, 844)]
    [InlineData(320, 640)]
    public async Task The_strip_names_the_owner_view_counts_the_games_to_place_and_keeps_its_links_44_pixels_tall(int width, int height)
    {
        await StartHostAsync();
        await Page.SetViewportSizeAsync(width, height);

        await GotoOwnerCabinetAsync();

        var strip = Page.Locator("header nav.owner-bar[data-variant=\"strip\"]");
        await Expect(strip).ToContainTextAsync("Owner view. Changes you make here show for every visitor.");
        await Expect(strip).ToContainTextAsync("4 games are not placed yet.");
        await Expect(strip.GetByRole(AriaRole.Link, new() { Name = "Place them" })).ToHaveAttributeAsync("href", "/owner/locations?filter=unplaced");
        await Expect(strip.GetByRole(AriaRole.Link, new() { Name = "Cabinet", Exact = true })).ToHaveCountAsync(0);
        await Expect(strip.Locator("[aria-live], [role=status], [role=alert]")).ToHaveCountAsync(0);
        foreach (var height44 in await strip.Locator("a").EvaluateAllAsync<double[]>("links => links.map(link => link.getBoundingClientRect().height)"))
        {
            height44.Should().BeGreaterThanOrEqualTo(44);
        }

        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= document.documentElement.clientWidth")).Should().BeTrue();

        if (width <= 640)
        {
            var box = (await strip.BoundingBoxAsync())!;
            var count = await RectOf("header nav.owner-bar .owner-strip-note:nth-of-type(2)");
            var place = (await strip.GetByRole(AriaRole.Link, new() { Name = "Place them" }).BoundingBoxAsync())!;
            var tops = await strip.Locator(".owner-strip-links a").EvaluateAllAsync<double[]>("links => links.map(link => Math.round(link.getBoundingClientRect().top))");

            var sentence = await RectOf("header nav.owner-bar .owner-strip-note:nth-of-type(1)");
            var links = (await strip.Locator(".owner-strip-links").BoundingBoxAsync())!;
            var lineHeight = await strip.Locator(".owner-strip-note").First.EvaluateAsync<double>("note => parseFloat(getComputedStyle(note).lineHeight)");
            var sameLine = Math.Abs((count.Top + (count.Height / 2)) - (place.Y + (place.Height / 2))) < 2;
            var justBelow = place.Y >= count.Bottom - 1 && place.Y - count.Bottom <= 8;
            var content = (links.Y + links.Height) - sentence.Top;

            Math.Round(sentence.Height / lineHeight).Should().BeLessThanOrEqualTo(3, "the owner-view sentence stays short even in a wide font");
            (sameLine || justBelow).Should().BeTrue("Place them follows the count on its line, or wraps straight under it when a wide font leaves no room");
            ((double)box.Height).Should().BeLessThanOrEqualTo(content + 19, "the strip adds only its 8px padding and border around its rows");
            ((double)box.Height).Should().BeLessThanOrEqualTo(200, "the strip stays quiet so the cabinet is the first read");
            tops.Distinct().Should().HaveCount(1, "the three links share one row");
        }

        await SaveScreenshotAsync($"owner-strip-{width}");
    }

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(390, 844)]
    public async Task At_the_public_address_the_header_holds_the_toggle_alone_with_no_plate_and_no_strip(int width, int height)
    {
        await StartHostAsync();
        await Page.SetViewportSizeAsync(width, height);

        await GotoCabinetAsync("/");

        await Expect(Page.Locator("header .lang-toggle")).ToBeVisibleAsync();
        await Expect(Page.Locator(".owner-plate, .header-aside, .owner-bar")).ToHaveCountAsync(0);
        (await Page.Locator("header").InnerTextAsync()).Should().NotContain("Owner");
        await SaveScreenshotAsync($"public-header-{width}");
    }

    [Theory]
    [InlineData(65, 1440, 900)]
    [InlineData(65, 390, 844)]
    [InlineData(400, 1440, 900)]
    [InlineData(400, 390, 844)]
    public async Task With_a_synced_collection_and_no_owner_data_no_tag_is_painted_over_its_boxs_label_text(int games, int width, int height)
    {
        var clock = SyncHarness.NewClock();
        var factory = SyncHarness.CreateFactory(ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(games)), clock);
        await StartAsync(factory);
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        await Page.SetViewportSizeAsync(width, height);

        await GotoOwnerCabinetAsync();

        var boxes = await Page.Locator(Boxes).CountAsync();
        boxes.Should().BeGreaterThan(games / 2);
        await Expect(Page.Locator("[data-unplaced]")).ToHaveCountAsync(boxes);
        var overlaps = await Page.EvaluateAsync<string[]>(
            """
            () => [...document.querySelectorAll('#cabinet .placement[data-unplaced]')].flatMap(box => {
              const tag = box.querySelector('.placement-tag').getBoundingClientRect();
              const label = box.querySelector('.placement-label');
              if (!label) {
                return [];
              }
              const range = document.createRange();
              range.selectNodeContents(label);
              return [...range.getClientRects()]
                .filter(line => line.width > 0 && line.height > 0 && line.right > tag.left && line.bottom > tag.top
                  && Math.max(0, line.left - tag.left) + Math.max(0, line.top - tag.top) < tag.width - 1)
                .map(line => box.dataset.kind + ' ' + box.dataset.entryId + ' tag ' + [tag.left, tag.top, tag.width].map(Math.round).join(',') + ' text ' + [line.left, line.top, line.right, line.bottom].map(Math.round).join(','));
            })
            """);

        overlaps.Should().BeEmpty();
        var sizes = await Page.Locator("[data-unplaced] .placement-tag").EvaluateAllAsync<double[]>("tags => tags.map(tag => tag.getBoundingClientRect().width)");
        sizes.Should().OnlyContain(size => size >= 8 && size <= 12);
        await SaveScreenshotAsync($"owner-cabinet-synced-{games}-{width}");
    }

    private async Task<RectReading> RectOf(string selector)
    {
        var edges = await Page.Locator(selector).EvaluateAsync<double[]>(
            "element => { const rect = element.getBoundingClientRect(); return [rect.left, rect.top, rect.right, rect.bottom]; }");

        return new RectReading(edges[0], edges[1], edges[2], edges[3]);
    }

    private sealed record TagReading(string TagLevel, string VeilLevel, double TagWidth, bool TopIsTag, string Visibility);

    private sealed record RectReading(double Left, double Top, double Right, double Bottom)
    {
        public double Width => Right - Left;

        public double Height => Bottom - Top;
    }

    private ILocator Box(CardReading card) => Page.Locator($"{Boxes}[data-game-id=\"{card.GameId}\"]").First;

    private async Task GotoOwnerCabinetAsync()
    {
        await Page.GotoAsync($"http://127.0.0.1:{OwnerPort}/");
        await Page.WaitForSelectorAsync("#cabinet .placement", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
    }

    private Task<OwnerHost> StartHostAsync() => OwnerHost.SyncAsync(factory => StartAsync(factory), Collection());

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
}

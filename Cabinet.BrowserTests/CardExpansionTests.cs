using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that a base game's card lists every owned expansion as a tappable row, that a row swaps the card in
/// place without a second pull-out or a new history step, that an expansion's card links back to its owned base games, and that
/// the marker counting hidden expansions opens its base game's card at the expansions.
/// </summary>
[Trait("Category", "Browser")]
public sealed class CardExpansionTests : CabinetPageTest
{
    private const string OpenCard = "dialog.card-dialog[open]";
    private const string BlankPage = "about:blank";
    private const string MoreMarker = ".placement[data-kind=\"moreMarker\"]";
    private const int HeadingTopTolerance = 32;
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task A_base_game_lists_every_owned_expansion_in_collection_order_and_a_game_without_any_has_no_group()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        var cards = await LoadCardsAsync("/?sample=65", "desktop");
        var family = cards.Where(card => !card.IsExpansion).MaxBy(card => card.Expansions.Count)!;

        await OpenCardAsync(family);
        await SaveScreenshotAsync("card-expansions-desktop");

        await Expect(Page.Locator(".card-expansions-title")).ToHaveTextAsync("Owned expansions");
        var rows = Page.Locator(".card-expansions .exp-row");
        await Expect(rows).ToHaveCountAsync(family.Expansions.Count);
        (await Page.Locator(".card-expansions .exp-title").AllInnerTextsAsync()).Should().Equal(family.Expansions.Select(link => link.Title));
        (await rows.EvaluateAllAsync<string[]>("rows => rows.map(row => row.dataset.entryId)")).Should().Equal(family.Expansions.Select(link => link.EntryId.ToString()));
        await Expect(rows.First.Locator(".exp-chip")).ToHaveCountAsync(1);
        await Expect(rows.First.Locator("svg")).ToHaveCountAsync(1);
        (await Page.Locator(".card-ruled").EvaluateAsync<string[]>("ruled => Array.from(ruled.children).map(child => child.className)"))
            .Should().ContainInOrder("card-owned", "card-link");
        await CloseCardAsync();

        await OpenCardAsync(cards.First(card => !card.IsExpansion && card.Expansions.Count == 0));
        await Expect(Page.Locator(".card-owned")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".card-expansions-title")).ToHaveCountAsync(0);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Pressing_an_expansion_row_swaps_the_card_in_place_and_Back_still_closes_the_whole_card()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        await Page.GotoAsync(BlankPage);
        var cards = await LoadCardsAsync("/?sample=65", "desktop");
        var family = cards.First(card => !card.IsExpansion && card.Expansions.Count > 1);
        var target = family.Expansions[^1];
        var address = Page.Url;

        await OpenCardAsync(family);
        await Page.Locator($".card-expansions .exp-row[data-entry-id=\"{target.EntryId}\"]").ClickAsync();

        await Expect(Page.Locator("#card-title")).ToHaveTextAsync(target.Title);
        await Expect(Page.Locator("#card-title")).ToBeFocusedAsync();
        await Expect(Page.Locator(".card[data-swap]")).ToHaveCountAsync(0);
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(1);
        await Expect(Page.Locator("dialog")).ToHaveCountAsync(1);
        (await Page.EvaluateAsync<double>("document.querySelector('.card').scrollTop")).Should().Be(0);
        Page.Url.Should().Be(address);

        await Page.GoBackAsync();
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        Page.Url.Should().Be(address);

        await Page.GoBackAsync();
        Page.Url.Should().Be(BlankPage);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task An_expansion_of_two_owned_bases_lists_both_in_game_id_order_and_appears_on_each_base_card()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        var cards = await LoadCardsAsync("/?sample=65", "desktop");
        var expansion = cards.First(card => card.IsExpansion && card.Bases.Count == 2 && card.Bases.All(link => link.EntryId is not null));

        expansion.Bases.Select(link => link.GameId).Should().BeInAscendingOrder();

        await OpenCardAsync(expansion);
        await SaveScreenshotAsync("card-expansion-two-bases-desktop");

        await Expect(Page.Locator(".card-bases-title")).ToHaveTextAsync("Expansion for");
        var rows = Page.Locator(".card-bases .exp-row.base-row");
        await Expect(rows).ToHaveCountAsync(2);
        (await Page.Locator(".card-bases .exp-title").AllInnerTextsAsync()).Should().Equal(expansion.Bases.Select(link => link.Title));
        (await Page.Locator(".card-ruled").EvaluateAsync<string>("ruled => ruled.firstElementChild.className")).Should().Be("card-bases");
        await CloseCardAsync();

        foreach (var link in expansion.Bases)
        {
            await OpenCardAsync(cards.Single(card => card.EntryId == link.EntryId));
            await Expect(Page.Locator($".card-expansions .exp-row[data-entry-id=\"{expansion.EntryId}\"]")).ToHaveCountAsync(1);
            await CloseCardAsync();
        }
    }

    [Fact]
    public async Task An_expansion_of_one_owned_base_has_one_row_back_and_pressing_it_swaps_to_the_base()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        var cards = await LoadCardsAsync("/?sample=65", "desktop");
        var expansion = cards.First(card => card.IsExpansion && card.Bases.Count == 1 && card.Bases[0].EntryId is not null);
        var baseLink = expansion.Bases[0];

        await OpenCardAsync(expansion);

        var row = Page.Locator(".card-bases button.exp-row.base-row");
        await Expect(row).ToHaveCountAsync(1);
        await Expect(row.Locator(".exp-title")).ToHaveTextAsync($"Expansion for {baseLink.Title}");
        await Expect(Page.Locator(".card-bases-title")).ToHaveCountAsync(0);

        await row.ClickAsync();

        await Expect(Page.Locator("#card-title")).ToHaveTextAsync(baseLink.Title);
        await Expect(Page.Locator("#card-title")).ToBeFocusedAsync();
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task An_expansion_whose_base_game_is_not_owned_shows_a_plain_line_and_no_button()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        var cards = await LoadCardsAsync("/?sample=65", "desktop");
        var expansion = cards.First(card => card.IsExpansion && card.Bases.Count > 0 && card.Bases.All(link => link.EntryId is null));

        await OpenCardAsync(expansion);

        await Expect(Page.Locator(".card-bases .card-expansion-of").First).ToHaveTextAsync($"Expansion for {expansion.Bases[0].Title}");
        await Expect(Page.Locator(".card-bases button")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".exp-row")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task The_marker_that_counts_hidden_expansions_opens_the_base_card_at_the_expansions_with_the_heading_focused()
    {
        await Page.SetViewportSizeAsync(1440, 520);
        await StartAsync(PrototypeOn);
        await LoadCardsAsync("/?sample=400", "desktop");

        var marker = Page.Locator(MoreMarker).First;
        var entryId = await marker.GetAttributeAsync("data-entry-id");

        await marker.ScrollIntoViewIfNeededAsync();
        await marker.ClickAsync();

        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator(".card-expansions-title")).ToBeFocusedAsync();
        await Expect(Page.Locator(".card-expansions-title")).ToHaveTextAsync("Owned expansions");
        await Expect(Page.Locator("#card-title")).Not.ToHaveTextAsync("Owned expansions");

        var offset = await Page.EvaluateAsync<double>(
            "document.querySelector('.card-expansions-title').getBoundingClientRect().top - document.querySelector('.card').getBoundingClientRect().top");
        offset.Should().BeInRange(-1, HeadingTopTolerance);
        (await Page.Locator(".card-expansions .exp-row").CountAsync()).Should().BeGreaterThan(0);
        entryId.Should().NotBeNullOrEmpty();
    }

    private async Task<IReadOnlyList<CardData>> LoadCardsAsync(string pathAndQuery, string profile)
    {
        var sample = Regex.Match(pathAndQuery, "sample=([a-z0-9]+)", RegexOptions.CultureInvariant).Groups[1].Value;
        var cardsResponse = Page.WaitForResponseAsync("**/cabinet/cards**");

        await GotoCabinetAsync(pathAndQuery);
        await (await cardsResponse).FinishedAsync();

        var json = await Page.EvaluateAsync<JsonElement>(
            "async address => (await (await fetch(address)).json()).cards",
            $"/cabinet/cards?profile={profile}&sample={sample}");

        return JsonSerializer.Deserialize<List<CardData>>(json.GetRawText(), WebOptions)!;
    }

    private async Task OpenCardAsync(CardData record)
    {
        var box = Page.Locator($".placement[data-entry-id=\"{record.EntryId}\"]:not([data-kind=\"moreMarker\"])").First;

        await box.ScrollIntoViewIfNeededAsync();
        await box.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator("#card-title")).ToHaveTextAsync(record.Title);
    }

    private async Task CloseCardAsync()
    {
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
    }

    private sealed record CardLink(long? EntryId, int GameId, string Title, string? Chip);

    private sealed record CardData(long EntryId, string Title, bool IsExpansion, List<CardLink> Expansions, List<CardLink> Bases);
}

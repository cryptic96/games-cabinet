using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the card shows exactly what is known about a game, in the order and formats of the page language:
/// the strip of players, time, weight and age, the location, the rating, designers and mechanics, and the link last. What is
/// missing is left out, never shown as zero or unknown, and BGG text keeps its own direction and language marks.
/// </summary>
[Trait("Category", "Browser")]
public sealed class CardContentTests : CabinetPageTest
{
    private const string OpenCard = "dialog.card-dialog[open]";
    private const string EnDash = "–";
    private const int OpenedCardsToScan = 10;
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex EnglishRating = new(@"^\d\.\d / 10$", RegexOptions.CultureInvariant);
    private static readonly Regex DutchRating = new(@"^\d,\d / 10$", RegexOptions.CultureInvariant);
    private static readonly Regex ZeroMinutes = new(@"\b0 min\b|\b0 players\b|\b0 spelers\b|unknown|onbekend", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    [Fact]
    public async Task A_game_with_every_detail_shows_the_strip_then_the_location_then_rating_designers_and_mechanics_then_the_link()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        var cards = await LoadCardsAsync("/?sample=65", "desktop");
        var record = cards.First(card => card.IsFull);

        await OpenCardAsync(record);
        await SaveScreenshotAsync("card-full-desktop");

        var facts = Page.Locator(".card-ruled .facts .fact");
        await Expect(facts).ToHaveCountAsync(4);
        await Expect(facts.Nth(0).Locator(".v")).ToHaveTextAsync($"{record.MinPlayers}{EnDash}{record.MaxPlayers}");
        await Expect(facts.Nth(0).Locator(".l")).ToHaveTextAsync("players");
        await Expect(facts.Nth(1).Locator(".v")).ToHaveTextAsync($"{record.MinPlayTime}{EnDash}{record.MaxPlayTime} min");
        await Expect(facts.Nth(1).Locator(".l")).ToHaveTextAsync("play time");
        await Expect(facts.Nth(2).Locator(".v")).ToHaveTextAsync(record.EnglishWeightWord);
        await Expect(facts.Nth(2).Locator(".l")).ToHaveTextAsync($"weight {record.Weight!.Value.ToString("F1", CultureInfo.InvariantCulture)} / 5");
        await Expect(facts.Nth(3).Locator(".v")).ToHaveTextAsync($"{record.MinAge}+");
        await Expect(facts.Nth(3).Locator(".l")).ToHaveTextAsync("min. age");
        await Expect(facts.Locator("svg[aria-hidden=\"true\"]")).ToHaveCountAsync(4);

        await Expect(Page.Locator(".card-where dt")).ToHaveTextAsync("Stored in");
        await Expect(Page.Locator(".card-where dd")).ToHaveTextAsync(record.Location!);
        await Expect(Page.Locator(".card-where dd")).ToHaveAttributeAsync("dir", "auto");

        var rows = Page.Locator(".card-meta dt");
        await Expect(rows.Nth(0)).ToHaveTextAsync("BGG rating");
        (await Page.Locator(".card-meta dd").Nth(0).InnerTextAsync()).Should().MatchRegex(EnglishRating.ToString());
        await Expect(Page.Locator(".card-meta dd[dir=\"auto\"]")).ToHaveTextAsync(string.Join(", ", record.Designers!));
        await Expect(Page.Locator(".card-meta ul.mech[lang=\"en\"] > li")).ToHaveCountAsync(record.Mechanics!.Count);
        (await Page.Locator(".card-meta ul.mech").GetAttributeAsync("role")).Should().Be("list");

        var order = await Page.Locator(".card-ruled").EvaluateAsync<string[]>("ruled => Array.from(ruled.children).map(child => child.className)");
        order.Where(name => name is not ("card-owned" or "card-hole")).Should().Equal("facts", "card-where", "card-meta", "card-link");
        (await Page.EvaluateAsync<string>("getComputedStyle(document.querySelector('.card-ruled .fact')).lineHeight")).Should().Be("28px");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_game_without_any_detail_shows_the_quiet_note_instead_of_the_strip_and_the_rows()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        var cards = await LoadCardsAsync("/?sample=65", "desktop");
        var record = cards.First(card => !card.IsExpansion && !card.HasAnyDetail);

        await OpenCardAsync(record);
        await SaveScreenshotAsync("card-nodetails-desktop");

        await Expect(Page.Locator(".card-ruled .card-note")).ToHaveTextAsync("More details arrive after the next sync.");
        await Expect(Page.Locator(".card-ruled .facts")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".card-ruled .card-meta")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".card-ruled .card-link a")).ToHaveCountAsync(1);
        (await Page.Locator(".card").InnerTextAsync()).Should().NotMatchRegex(ZeroMinutes.ToString());
    }

    [Fact]
    public async Task A_game_without_a_location_has_no_stored_in_group_and_one_with_a_location_has_it()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        var cards = await LoadCardsAsync("/?sample=65", "desktop");

        await OpenCardAsync(cards.First(card => !card.IsExpansion && card.Location is null));
        await Expect(Page.Locator(".card-where")).ToHaveCountAsync(0);
        await CloseCardAsync();

        var located = cards.First(card => !card.IsExpansion && card.Location is not null);
        await OpenCardAsync(located);
        await Expect(Page.Locator(".card-where dd")).ToHaveTextAsync(located.Location!);
    }

    [Fact]
    public async Task One_designer_and_one_mechanic_use_the_singular_labels_and_several_use_the_plural()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        var cards = await LoadCardsAsync("/?sample=65", "desktop");

        await OpenCardAsync(cards.First(card => !card.IsExpansion && card.Designers?.Count == 1));
        (await Page.Locator(".card-meta dt").AllInnerTextsAsync()).Should().Contain("Designer").And.NotContain("Designers");
        await CloseCardAsync();

        await OpenCardAsync(cards.First(card => !card.IsExpansion && card.Mechanics?.Count == 1));
        (await Page.Locator(".card-meta dt").AllInnerTextsAsync()).Should().Contain("Mechanic").And.NotContain("Mechanics");
        await CloseCardAsync();

        await OpenCardAsync(cards.First(card => !card.IsExpansion && card.Designers?.Count > 1 && card.Mechanics?.Count > 1));
        (await Page.Locator(".card-meta dt").AllInnerTextsAsync()).Should().Contain("Designers").And.Contain("Mechanics");
    }

    [Fact]
    public async Task The_Dutch_card_uses_the_Dutch_words_and_decimal_comma_and_the_longest_label_fits_its_column()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        await UseDutchAsync();
        var cards = await LoadCardsAsync("/?sample=65", "desktop");
        var record = cards.First(card => card.IsFull && card.Mechanics!.Count > 1);

        await OpenCardAsync(record);

        var facts = Page.Locator(".card-ruled .facts .fact");
        await Expect(facts.Nth(0).Locator(".l")).ToHaveTextAsync("spelers");
        await Expect(facts.Nth(1).Locator(".l")).ToHaveTextAsync("speelduur");
        await Expect(facts.Nth(2).Locator(".v")).ToHaveTextAsync(record.DutchWeightWord);
        await Expect(facts.Nth(2).Locator(".l")).ToHaveTextAsync($"zwaarte {record.Weight!.Value.ToString("F1", CultureInfo.GetCultureInfo("nl-NL"))} / 5");
        await Expect(facts.Nth(3).Locator(".l")).ToHaveTextAsync("min. leeftijd");
        await Expect(Page.Locator(".card-where dt")).ToHaveTextAsync("Staat in");
        (await Page.Locator(".card-meta dd").Nth(0).InnerTextAsync()).Should().MatchRegex(DutchRating.ToString());

        var mechanicsLabel = Page.Locator(".card-meta dt", new PageLocatorOptions { HasText = "Mechanismen" });
        await Expect(mechanicsLabel).ToHaveCountAsync(1);
        (await mechanicsLabel.EvaluateAsync<bool>("label => label.scrollWidth === label.clientWidth")).Should().BeTrue();
        (await mechanicsLabel.BoundingBoxAsync())!.Height.Should().BeLessThanOrEqualTo(28.5f);
    }

    [Fact]
    public async Task The_Dutch_phone_card_keeps_one_entry_per_line_without_sideways_scrolling()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await StartAsync(PrototypeOn);
        await UseDutchAsync();
        var cards = await LoadCardsAsync("/?sample=65", "phone");
        var record = cards.First(card => card.IsFull);

        await OpenCardAsync(record);
        await SaveScreenshotAsync("card-full-phone-nl");

        var tops = await Page.Locator(".card-ruled .fact").EvaluateAllAsync<double[]>("items => items.map(item => item.getBoundingClientRect().top)");
        tops.Should().OnlyHaveUniqueItems();
        (await Page.EvaluateAsync<bool>("(() => { const c = document.querySelector('.card'); return c.scrollWidth <= c.clientWidth; })()")).Should().BeTrue();
    }

    [Fact]
    public async Task Mixed_script_cards_never_scroll_sideways_and_give_names_their_own_direction()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await StartAsync(PrototypeOn);
        var cards = await LoadCardsAsync("/?sample=edge", "phone");

        foreach (var record in cards.Where(card => !card.IsExpansion).Take(OpenedCardsToScan + 2))
        {
            await OpenCardAsync(record);

            (await Page.EvaluateAsync<bool>("(() => { const c = document.querySelector('.card'); return c.scrollWidth <= c.clientWidth; })()")).Should().BeTrue(record.Title);
            await Expect(Page.Locator(".card-meta dd[dir=\"auto\"]").First).ToBeAttachedAsync();
            await Expect(Page.Locator(".card-where dd[dir=\"auto\"]")).ToBeAttachedAsync();
            (await Page.Locator(".card-where dd").GetAttributeAsync("lang")).Should().BeNull();
            await CloseCardAsync();
        }
    }

    [Fact]
    public async Task No_card_of_the_sample_says_zero_minutes_or_unknown_in_either_language()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        var cards = await LoadCardsAsync("/?sample=65", "desktop");

        foreach (var record in cards.Take(OpenedCardsToScan))
        {
            await OpenCardAsync(record);
            (await Page.Locator(".card").InnerTextAsync()).Should().NotMatchRegex(ZeroMinutes.ToString(), record.Title);
            await CloseCardAsync();
        }
    }

    private async Task UseDutchAsync()
    {
        await Context.AddCookiesAsync([new Cookie { Name = "lang", Value = "nl", Url = $"http://127.0.0.1:{PublicPort}" }]);
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

        if (!string.IsNullOrWhiteSpace(record.Title))
        {
            await Expect(Page.Locator("#card-title")).ToHaveTextAsync(record.Title);
        }
    }

    private async Task CloseCardAsync()
    {
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
    }

    private sealed record CardData(
        long EntryId,
        string Title,
        bool IsExpansion,
        int? MinPlayers,
        int? MaxPlayers,
        int? PlayTime,
        int? MinPlayTime,
        int? MaxPlayTime,
        int? MinAge,
        double? Weight,
        double? Rating,
        string? Location,
        List<string>? Designers,
        List<string>? Mechanics)
    {
        private static readonly string[] EnglishWords = ["Light", "Medium-light", "Medium", "Medium-heavy", "Heavy"];
        private static readonly string[] DutchWords = ["Licht", "Vrij licht", "Gemiddeld", "Vrij zwaar", "Zwaar"];

        public bool IsFull =>
            !IsExpansion
            && MinPlayers is > 0 && MaxPlayers is > 0 && MinPlayers != MaxPlayers
            && MinPlayTime is > 0 && MaxPlayTime is > 0 && MinPlayTime != MaxPlayTime
            && MinAge is > 0 && Weight is > 0 && Rating is > 0
            && Location is not null
            && Designers?.Count > 0 && Mechanics?.Count > 0;

        public bool HasAnyDetail =>
            MinPlayers is > 0 || MaxPlayers is > 0 || PlayTime is > 0 || MinPlayTime is > 0 || MaxPlayTime is > 0 || MinAge is > 0
            || Weight is > 0 || Rating is > 0 || Designers?.Count > 0 || Mechanics?.Count > 0;

        public string EnglishWeightWord => EnglishWords[Band];

        public string DutchWeightWord => DutchWords[Band];

        private int Band
        {
            get
            {
                var shown = Math.Round(Weight!.Value, 1, MidpointRounding.AwayFromZero);

                return new[] { 1.5, 2.5, 3.5, 4.5 }.Count(start => shown >= start);
            }
        }
    }
}

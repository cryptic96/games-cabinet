using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// The scenarios that prove the card looks like a ruled index card in a real browser: every block of the ruled area is a whole
/// number of rulings tall whatever script the text is in, pressable rows are one or two rulings tall and never overlap, the focus
/// ring on paper is ink, and the punched hole is decoration only. A concrete class below runs them at one viewport.
/// </summary>
[Trait("Category", "Browser")]
public abstract class CardLookScenarios(int width, int height, bool touch) : CabinetPageTest
{
    private const string OpenCard = "dialog.card-dialog[open]";
    private const int Ruling = 28;
    private const double RulingTolerance = 0.05;
    private const int CardsPerSample = 12;
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The smallest height a pressable row must have at this viewport: one ruling with a mouse, two on touch or a phone.</summary>
    protected int MinimumRowHeight => touch || width <= 640 ? 2 * Ruling : Ruling;

    /// <inheritdoc />
    public override BrowserNewContextOptions ContextOptions() => new()
    {
        ViewportSize = new ViewportSize { Width = width, Height = height },
        HasTouch = touch,
    };

    [Fact]
    public async Task Every_block_of_the_ruled_area_is_a_whole_number_of_rulings_over_mixed_scripts()
    {
        await StartAsync(PrototypeOn);
        var cards = await LoadCardsAsync("/?sample=edge", "edge");

        foreach (var record in cards.Where(card => !card.IsExpansion).Take(CardsPerSample))
        {
            await OpenCardAsync(record);
            await AssertOnTheRulingAsync(record.Title);
            await CloseCardAsync();
        }

        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_longest_title_and_every_card_of_the_review_sample_stay_on_the_ruling_with_expansions_and_bases()
    {
        await StartAsync(PrototypeOn);
        var cards = await LoadCardsAsync("/?sample=65", "review");
        var longest = cards.MaxBy(card => card.Title.Length)!;
        var family = cards.Where(card => !card.IsExpansion).MaxBy(card => card.Expansions.Count)!;
        var twoBases = cards.First(card => card.IsExpansion && card.Bases.Count == 2 && card.Bases.All(link => link.EntryId is not null));
        var notOwned = cards.First(card => card.IsExpansion && card.Bases.Count > 0 && card.Bases.All(link => link.EntryId is null));

        foreach (var record in new[] { longest, family, twoBases, notOwned })
        {
            await OpenCardAsync(record);
            await AssertOnTheRulingAsync(record.Title);
            (await Page.EvaluateAsync<string>("getComputedStyle(document.querySelector('#card-title')).lineHeight")).Should().Be("28px");
            await CloseCardAsync();
        }

        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_Dutch_card_with_its_longer_words_stays_on_the_ruling()
    {
        await StartAsync(PrototypeOn);
        await UseDutchAsync();
        var cards = await LoadCardsAsync("/?sample=65", "review");
        var family = cards.Where(card => !card.IsExpansion).MaxBy(card => card.Expansions.Count)!;

        await OpenCardAsync(family);
        await Expect(Page.Locator(".card-expansions-title")).ToHaveTextAsync("Uitbreidingen in de kast");
        await AssertOnTheRulingAsync(family.Title);
    }

    [Fact]
    public async Task Pressable_rows_are_whole_rulings_tall_at_least_the_minimum_and_never_overlap()
    {
        await StartAsync(PrototypeOn);
        var cards = await LoadCardsAsync("/?sample=65", "review");
        var family = cards.Where(card => !card.IsExpansion).MaxBy(card => card.Expansions.Count)!;

        await OpenCardAsync(family);

        var heights = await Page.EvaluateAsync<double[]>("Array.from(document.querySelectorAll('.card .exp-row, .card .card-link')).map(row => row.getBoundingClientRect().height)");
        heights.Should().HaveCountGreaterThan(family.Expansions.Count);
        heights.Should().OnlyContain(height => height >= MinimumRowHeight - RulingTolerance);
        heights.Should().OnlyContain(height => IsWholeRulings(height));

        var edges = await Page.EvaluateAsync<double[][]>("Array.from(document.querySelectorAll('.card .exp-row')).map(row => { const box = row.getBoundingClientRect(); return [box.top, box.bottom]; })");

        for (var index = 1; index < edges.Length; index++)
        {
            edges[index][0].Should().BeGreaterThanOrEqualTo(edges[index - 1][1] - RulingTolerance, $"row {index} starts where the row above ends");
        }
    }

    [Fact]
    public async Task A_long_expansion_title_wraps_over_whole_rulings_and_the_whole_row_stays_one_tap_area()
    {
        await StartAsync(PrototypeOn);
        var cards = await LoadCardsAsync("/?sample=65", "review");
        var family = cards.Where(card => !card.IsExpansion).MaxBy(card => card.Expansions.Count)!;

        await OpenCardAsync(family);

        var longest = family.Expansions.MaxBy(link => link.Title.Length)!;
        var row = Page.Locator($".card-expansions .exp-row[data-entry-id=\"{longest.EntryId}\"]");
        var box = (await row.BoundingBoxAsync())!;

        IsWholeRulings(box.Height).Should().BeTrue();
        await row.ClickAsync(new LocatorClickOptions { Position = new Position { X = 4, Y = 2 } });
        await Expect(Page.Locator("#card-title")).ToHaveTextAsync(longest.Title);
    }

    [Fact]
    public async Task A_row_reached_with_the_keyboard_shows_a_two_pixel_ink_ring_and_the_hole_is_hidden_decoration()
    {
        await StartAsync(PrototypeOn);
        var cards = await LoadCardsAsync("/?sample=65", "review");
        var family = cards.Where(card => !card.IsExpansion).MaxBy(card => card.Expansions.Count)!;

        await OpenCardAsync(family);
        await Page.Keyboard.PressAsync("Tab");

        var row = Page.Locator(".card .exp-row").First;
        await Expect(row).ToBeFocusedAsync();

        var ring = await row.EvaluateAsync<string[]>("row => { const style = getComputedStyle(row); return [style.outlineColor, style.outlineStyle, style.outlineWidth, style.outlineOffset]; }");
        ring.Should().Equal("rgb(42, 26, 16)", "solid", "2px", "2px");

        var hole = Page.Locator(".card-hole");
        await Expect(hole).ToHaveCountAsync(1);
        await Expect(hole).ToHaveAttributeAsync("aria-hidden", "true");
        (await hole.EvaluateAsync<int>("hole => hole.querySelectorAll('a, button, [tabindex]').length")).Should().Be(0);
        (await Page.Locator(".card-ruled").EvaluateAsync<string>("ruled => ruled.lastElementChild.className")).Should().Be("card-hole");
    }

    /// <summary>Sets the language cookie to Dutch for the pages opened afterwards.</summary>
    protected async Task UseDutchAsync()
    {
        await Context.AddCookiesAsync([new Cookie { Name = "lang", Value = "nl", Url = $"http://127.0.0.1:{PublicPort}" }]);
    }

    /// <summary>Loads a sample and returns the card records the page's own request for card data returns.</summary>
    /// <param name="pathAndQuery">The path and query of the cabinet page.</param>
    /// <param name="sampleLabel">A label only used for the error message when no card data arrives.</param>
    protected async Task<IReadOnlyList<CardData>> LoadCardsAsync(string pathAndQuery, string sampleLabel)
    {
        var sample = Regex.Match(pathAndQuery, "sample=([a-z0-9]+)", RegexOptions.CultureInvariant).Groups[1].Value;
        var profile = width <= 640 ? "phone" : "desktop";
        var cardsResponse = Page.WaitForResponseAsync("**/cabinet/cards**");

        await GotoCabinetAsync(pathAndQuery);
        await (await cardsResponse).FinishedAsync();

        var json = await Page.EvaluateAsync<JsonElement>(
            "async address => (await (await fetch(address)).json()).cards",
            $"/cabinet/cards?profile={profile}&sample={sample}");

        var cards = JsonSerializer.Deserialize<List<CardData>>(json.GetRawText(), WebOptions)!;
        cards.Should().NotBeEmpty(sampleLabel);

        return cards;
    }

    /// <summary>Opens the card of a record by pressing its box and waits for its title.</summary>
    /// <param name="record">The record whose box is pressed.</param>
    protected async Task OpenCardAsync(CardData record)
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

    /// <summary>Closes the open card with the keyboard.</summary>
    protected async Task CloseCardAsync()
    {
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
    }

    private static bool IsWholeRulings(double height)
    {
        var rulings = height / Ruling;

        return Math.Abs(rulings - Math.Round(rulings)) <= RulingTolerance / Ruling;
    }

    private async Task AssertOnTheRulingAsync(string label)
    {
        var offenders = await Page.EvaluateAsync<string[]>(
            """
            () => {
              const ruling = 28;
              const out = [];
              const ruled = document.querySelector('.card-ruled');
              const check = (element, name) => {
                const height = element.getBoundingClientRect().height;
                const rulings = height / ruling;
                if (Math.abs(rulings - Math.round(rulings)) > 0.05 / ruling) {
                  out.push(name + ' is ' + height + 'px');
                }
              };
              check(ruled, 'the ruled area');
              for (const child of ruled.children) {
                check(child, 'child ' + child.className);
              }
              for (const element of ruled.querySelectorAll('p, h3, dt, dd, ul, dl, li, button, a')) {
                if (getComputedStyle(element).display !== 'inline') {
                  check(element, element.tagName.toLowerCase() + '.' + element.className);
                }
              }
              return out;
            }
            """);

        offenders.Should().BeEmpty($"every block of the card of '{label}' sits on the ruling");
    }

    /// <summary>One link of a card list.</summary>
    protected sealed record CardLink(long? EntryId, int GameId, string Title, string? Chip);

    /// <summary>The part of a card record these scenarios read.</summary>
    protected sealed record CardData(long EntryId, string Title, bool IsExpansion, List<CardLink> Expansions, List<CardLink> Bases);
}

/// <summary>Runs the card look scenarios on a wide screen with a mouse, where a pressable row is one ruling tall.</summary>
[Trait("Category", "Browser")]
public sealed class CardLookTests() : CardLookScenarios(1440, 900, false)
{
    [Fact]
    public async Task The_desktop_card_in_English_is_saved_for_review()
    {
        await StartAsync(new Dictionary<string, string?> { ["Prototype:Enabled"] = "true" });
        var cards = await LoadCardsAsync("/?sample=65", "review");

        await OpenCardAsync(cards.Where(card => !card.IsExpansion).MaxBy(card => card.Expansions.Count)!);
        await SaveScreenshotAsync("card-look-desktop-en");

        MinimumRowHeight.Should().Be(28);
    }
}

/// <summary>Runs the card look scenarios on a phone with touch, where a pressable row is two rulings tall.</summary>
[Trait("Category", "Browser")]
public sealed class PhoneCardLookTests() : CardLookScenarios(390, 800, true)
{
    [Fact]
    public async Task The_phone_card_in_Dutch_and_the_mixed_script_card_are_saved_for_review()
    {
        await StartAsync(new Dictionary<string, string?> { ["Prototype:Enabled"] = "true" });
        await UseDutchAsync();
        var cards = await LoadCardsAsync("/?sample=65", "review");

        await OpenCardAsync(cards.Where(card => !card.IsExpansion).MaxBy(card => card.Expansions.Count)!);
        await SaveScreenshotAsync("card-look-phone-nl");
        await CloseCardAsync();

        var edge = await LoadCardsAsync("/?sample=edge", "edge");

        await OpenCardAsync(edge.First(card => !card.IsExpansion));
        await SaveScreenshotAsync("card-look-edge-phone");

        MinimumRowHeight.Should().Be(56);
    }
}

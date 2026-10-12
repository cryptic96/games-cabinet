using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that a visitor who prefers less motion never gets a view transition: the card and the dim fade in, the
/// box stays in its slot with a plain outline, the close is the same fade, and the card says exactly what it says with motion.
/// </summary>
[Trait("Category", "Browser")]
public sealed class ReducedMotionTests : CabinetPageTest
{
    private const string OpenCard = "dialog.card-dialog[open]";
    private const string MoreMarker = ".placement[data-kind=\"moreMarker\"]";
    private const string WallTextColour = "rgb(239, 227, 194)";
    private const int HeadingTopTolerance = 32;
    private const string TransitionSpy = """
        (() => {
          window.__pullSpy = { calls: 0 };
          const original = document.startViewTransition.bind(document);
          document.startViewTransition = (callback) => {
            window.__pullSpy.calls += 1;
            return original(callback);
          };
        })();
        """;
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Opening_and_closing_boxes_and_swapping_never_start_a_view_transition_and_the_box_is_outlined_in_place()
    {
        await StartCabinetAsync(1440, 900, "/?sample=65");
        await SaveBoxOpenedAsync(".placement[data-kind=\"spine\"]", "reduced-motion-open");
        await SaveBoxOpenedAsync(".placement[data-kind=\"cover\"]", null);

        var cards = await LoadCardsAsync();
        var family = cards.First(card => !card.IsExpansion && card.Expansions.Count > 0);
        var familyBox = Page.Locator($".placement[data-entry-id=\"{family.EntryId}\"]:not([data-kind=\"moreMarker\"])").First;
        await familyBox.ScrollIntoViewIfNeededAsync();
        await familyBox.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();

        var target = family.Expansions[0];
        await Page.Locator($".card-expansions .exp-row[data-entry-id=\"{target.EntryId}\"]").ClickAsync();
        await Expect(Page.Locator("#card-title")).ToHaveTextAsync(target.Title);
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await Expect(Page.Locator("[data-open], [data-out]")).ToHaveCountAsync(0);

        (await TransitionCallsAsync()).Should().Be(0);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_marker_opens_its_base_card_at_the_expansions_heading_without_a_view_transition_or_a_smooth_scroll()
    {
        await StartCabinetAsync(1440, 520, "/?sample=400");

        var marker = Page.Locator(MoreMarker).First;
        var entryId = await marker.GetAttributeAsync("data-entry-id");
        await marker.ScrollIntoViewIfNeededAsync();
        await marker.ClickAsync();

        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator(".card-expansions-title")).ToBeFocusedAsync();
        var offset = await Page.EvaluateAsync<double>(
            "() => document.querySelector('.card-expansions-title').getBoundingClientRect().top - document.querySelector('.card').getBoundingClientRect().top");
        offset.Should().BeInRange(-1, HeadingTopTolerance);
        await Expect(Page.Locator($".placement[data-entry-id=\"{entryId}\"]:not([data-kind=\"moreMarker\"])")).ToHaveAttributeAsync("data-open", string.Empty);
        await Expect(Page.Locator("[data-out]")).ToHaveCountAsync(0);
        (await TransitionCallsAsync()).Should().Be(0);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_card_says_the_same_with_reduced_motion_as_without()
    {
        await StartCabinetAsync(1440, 900, "/?sample=65");
        var box = Page.Locator(".placement[data-kind=\"spine\"]").First;
        await box.ScrollIntoViewIfNeededAsync();

        await box.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        var reducedText = await Page.Locator(".card").InnerTextAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);

        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.NoPreference });
        await box.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator("html[data-pull-kind]")).ToHaveCountAsync(0);
        var fullText = await Page.Locator(".card").InnerTextAsync();

        reducedText.Should().NotBeNullOrWhiteSpace();
        fullText.Should().Be(reducedText);
        (await TransitionCallsAsync()).Should().Be(1);
    }

    private async Task StartCabinetAsync(int width, int height, string pathAndQuery)
    {
        await Page.AddInitScriptAsync(TransitionSpy);
        await Page.SetViewportSizeAsync(width, height);
        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });
        await StartAsync(PrototypeOn);
        await GotoCabinetAsync(pathAndQuery);
    }

    private async Task SaveBoxOpenedAsync(string selector, string? screenshot)
    {
        var box = Page.Locator(selector).First;
        var entryId = await box.GetAttributeAsync("data-entry-id");
        var kind = await box.GetAttributeAsync("data-kind");
        var same = Page.Locator($".placement[data-entry-id=\"{entryId}\"][data-kind=\"{kind}\"]");
        await box.ScrollIntoViewIfNeededAsync();
        await box.ClickAsync();

        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(same).ToHaveAttributeAsync("data-open", string.Empty);
        await Expect(same).ToHaveCountAsync(1);
        await Expect(Page.Locator("[data-out]")).ToHaveCountAsync(0);
        await Expect(same).ToHaveCSSAsync("visibility", "visible");
        await Expect(same).ToHaveCSSAsync("outline-color", WallTextColour);
        await Expect(Page.Locator("#card-title")).ToBeFocusedAsync();

        if (screenshot is not null)
        {
            await Expect(Page.Locator("dialog.card-dialog[data-fade]")).ToHaveCountAsync(0);
            await SaveScreenshotAsync(screenshot);
        }

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await Expect(same).ToBeFocusedAsync();
        await Expect(Page.Locator("[data-open]")).ToHaveCountAsync(0);
    }

    private async Task<IReadOnlyList<CardData>> LoadCardsAsync()
    {
        var json = await Page.EvaluateAsync<JsonElement>(
            "async address => (await (await fetch(address)).json()).cards",
            "/cabinet/cards?profile=desktop&sample=65");

        return JsonSerializer.Deserialize<List<CardData>>(json.GetRawText(), WebOptions)!;
    }

    private async Task<int> TransitionCallsAsync()
    {
        return await Page.EvaluateAsync<int>("() => window.__pullSpy.calls");
    }

    private sealed record CardLink(long? EntryId, int GameId, string Title);

    private sealed record CardData(long EntryId, string Title, bool IsExpansion, List<CardLink> Expansions);
}

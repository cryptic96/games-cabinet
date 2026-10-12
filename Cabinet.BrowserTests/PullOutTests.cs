using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that opening a card pulls the tapped box off the shelf with a view transition, that the box's slot stays
/// empty while the card is open and that the box slides back into it on close, and that every case without a flight falls back to a
/// plain fade. A script added before the page loads counts every call to the transition API and records the names of the
/// animations running once the transition is ready.
/// </summary>
[Trait("Category", "Browser")]
public sealed class PullOutTests : CabinetPageTest
{
    private const string OpenCard = "dialog.card-dialog[open]";
    private const string MoreMarker = ".placement[data-kind=\"moreMarker\"]";
    private const int HeadingTopTolerance = 32;
    private const string TransitionSpy = """
        (() => {
          window.__pullSpy = { calls: 0, names: [] };
          const original = document.startViewTransition.bind(document);
          document.startViewTransition = (callback) => {
            window.__pullSpy.calls += 1;
            const transition = original(callback);
            transition.ready.then(() => {
              for (const animation of document.documentElement.getAnimations({ subtree: true })) {
                if (animation.animationName) {
                  window.__pullSpy.names.push(animation.animationName);
                }
              }
            }).catch(() => {});
            return transition;
          };
        })();
        """;
    private const string WithoutTransitionApi = """
        window.__pullSpy = { calls: 0, names: [] };
        delete Document.prototype.startViewTransition;
        """;
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task An_upright_spine_lifts_turns_to_its_cover_and_leaves_its_slot_empty_until_it_slides_back()
    {
        await StartCabinetAsync(1440, 900);

        var spine = Page.Locator(".placement[data-kind=\"spine\"]").First;
        var entryId = await spine.GetAttributeAsync("data-entry-id");
        await spine.ScrollIntoViewIfNeededAsync();
        await spine.ClickAsync();

        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await WaitForTransitionToEndAsync();

        (await TransitionCallsAsync()).Should().Be(1);
        (await AnimationNamesAsync()).Should().Contain(["pull-lift", "pull-away-y", "pull-in-y"]);
        var box = Page.Locator($".placement[data-entry-id=\"{entryId}\"][data-kind=\"spine\"]");
        await Expect(box).ToHaveCSSAsync("visibility", "hidden");
        var slot = await box.EvaluateAsync<double[]>("box => { const rect = box.parentElement.getBoundingClientRect(); return [rect.width, rect.height]; }");
        slot.Should().OnlyContain(size => size > 0);
        await Expect(Page.Locator("#card-title")).ToBeFocusedAsync();
        await SaveScreenshotAsync("pullout-open-desktop");

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await WaitForTransitionToEndAsync();
        (await TransitionCallsAsync()).Should().Be(2);
        await Expect(box).ToHaveCSSAsync("visibility", "visible");
        await Expect(box).ToBeFocusedAsync();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_lying_box_tips_to_its_cover_and_a_cover_only_lifts()
    {
        await StartCabinetAsync(1440, 900);

        var flat = Page.Locator(".placement[data-kind=\"flatBox\"]").First;
        await flat.ScrollIntoViewIfNeededAsync();
        await flat.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await WaitForTransitionToEndAsync();

        (await AnimationNamesAsync()).Should().Contain(["pull-lift", "pull-away-x", "pull-in-x"]);
        await SaveScreenshotAsync("pullout-flatbox-desktop");

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await WaitForTransitionToEndAsync();
        await ClearAnimationNamesAsync();

        var cover = Page.Locator(".placement[data-kind=\"cover\"]").First;
        await cover.ScrollIntoViewIfNeededAsync();
        await cover.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await WaitForTransitionToEndAsync();

        var names = await AnimationNamesAsync();
        names.Should().Contain("pull-lift");
        names.Should().NotContain(name => name.StartsWith("pull-away", StringComparison.Ordinal) || name.StartsWith("pull-in", StringComparison.Ordinal));
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Without_the_transition_api_the_card_fades_open_and_the_box_is_out_until_the_close_ends()
    {
        await StartCabinetAsync(1440, 900, WithoutTransitionApi);

        (await Page.EvaluateAsync<string>("() => typeof document.startViewTransition")).Should().Be("undefined");
        var spine = Page.Locator(".placement[data-kind=\"spine\"]").First;
        var entryId = await spine.GetAttributeAsync("data-entry-id");
        var box = Page.Locator($".placement[data-entry-id=\"{entryId}\"][data-kind=\"spine\"]");
        await spine.ScrollIntoViewIfNeededAsync();
        await spine.ClickAsync();

        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(box).ToHaveAttributeAsync("data-out", string.Empty);
        await Expect(Page.Locator("#card-title")).ToBeFocusedAsync();
        (await TransitionCallsAsync()).Should().Be(0);

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await Expect(Page.Locator($".placement[data-entry-id=\"{entryId}\"][data-out]")).ToHaveCountAsync(0);
        await Expect(box).ToBeFocusedAsync();
        (await TransitionCallsAsync()).Should().Be(0);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_card_opened_from_the_games_list_for_a_box_that_is_off_screen_fades_without_scrolling_the_page()
    {
        await StartCabinetAsync(1440, 300);

        var lastBox = Page.Locator(".placement:not([data-kind=\"moreMarker\"])").Last;
        var entryId = await lastBox.GetAttributeAsync("data-entry-id");
        await Page.EvaluateAsync("() => window.scrollTo(0, 0)");
        var top = await lastBox.EvaluateAsync<double>("box => box.getBoundingClientRect().top");
        top.Should().BeGreaterThan(300);

        await Page.Locator($".games-list-items button[data-entry-id=\"{entryId}\"]").First.DispatchEventAsync("click");

        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator($".placement[data-entry-id=\"{entryId}\"][data-out]")).ToHaveCountAsync(1);
        (await TransitionCallsAsync()).Should().Be(0);
        (await Page.EvaluateAsync<double>("() => window.scrollY")).Should().Be(0);

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await Expect(Page.Locator($".placement[data-entry-id=\"{entryId}\"][data-out]")).ToHaveCountAsync(0);
        (await TransitionCallsAsync()).Should().Be(0);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_card_that_was_swapped_to_another_game_closes_with_a_plain_fade_and_the_source_box_comes_back()
    {
        await StartCabinetAsync(1440, 900);
        var cards = await LoadCardsAsync();
        var family = cards.First(card => !card.IsExpansion && card.Expansions.Count > 0);
        var box = Page.Locator($".placement[data-entry-id=\"{family.EntryId}\"]:not([data-kind=\"moreMarker\"])").First;

        await box.ScrollIntoViewIfNeededAsync();
        await box.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await WaitForTransitionToEndAsync();
        (await TransitionCallsAsync()).Should().Be(1);

        var target = family.Expansions[0];
        await Page.Locator($".card-expansions .exp-row[data-entry-id=\"{target.EntryId}\"]").ClickAsync();
        await Expect(Page.Locator("#card-title")).ToHaveTextAsync(target.Title);
        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await Expect(Page.Locator($".placement[data-entry-id=\"{family.EntryId}\"][data-out]")).ToHaveCountAsync(0);
        await Expect(box).ToHaveCSSAsync("visibility", "visible");
        (await TransitionCallsAsync()).Should().Be(1);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_second_tap_on_another_box_while_the_first_pull_runs_opens_only_the_first_card()
    {
        await StartCabinetAsync(1440, 900);

        var boxes = Page.Locator(".placement:not([data-kind=\"moreMarker\"])");
        var first = boxes.Nth(0);
        var second = boxes.Nth(1);
        var firstTitle = await first.GetAttributeAsync("title");
        var secondBounds = (await second.BoundingBoxAsync())!;

        await first.ClickAsync();
        await Page.Mouse.ClickAsync(secondBounds.X + (secondBounds.Width / 2), secondBounds.Y + (secondBounds.Height / 2));

        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await WaitForTransitionToEndAsync();
        await Expect(Page.Locator("#card-title")).ToHaveTextAsync(firstTitle!);
        await Expect(Page.Locator("dialog.card-dialog")).ToHaveCountAsync(1);
        (await TransitionCallsAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_more_marker_pulls_out_its_base_box_and_ends_with_the_expansions_heading_focused_at_the_top_of_the_card()
    {
        await Page.AddInitScriptAsync(TransitionSpy);
        await Page.SetViewportSizeAsync(1440, 520);
        await StartAsync(PrototypeOn);
        await GotoCabinetAsync("/?sample=400");

        var marker = Page.Locator(MoreMarker).First;
        var entryId = await marker.GetAttributeAsync("data-entry-id");
        await marker.ScrollIntoViewIfNeededAsync();
        await marker.ClickAsync();

        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator($".placement[data-entry-id=\"{entryId}\"]:not([data-kind=\"moreMarker\"])")).ToHaveAttributeAsync("data-out", string.Empty);
        await Expect(Page.Locator(MoreMarker + "[data-out]")).ToHaveCountAsync(0);
        await WaitForTransitionToEndAsync();
        await Expect(Page.Locator(".card-expansions-title")).ToBeFocusedAsync();
        await WaitForHeadingAtTopAsync();
    }

    [Fact]
    public async Task On_a_phone_the_sheet_rises_with_the_flight_of_the_cover()
    {
        await StartCabinetAsync(390, 800);

        var cover = Page.Locator(".placement[data-kind=\"cover\"]").First;
        await cover.ScrollIntoViewIfNeededAsync();
        await cover.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await WaitForTransitionToEndAsync();

        (await AnimationNamesAsync()).Should().Contain("card-rise");
        await SaveScreenshotAsync("pullout-phone");

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await WaitForTransitionToEndAsync();
        (await AnimationNamesAsync()).Should().Contain("card-sink");
        ConsoleErrors.Should().BeEmpty();
    }

    private async Task StartCabinetAsync(int width, int height, string? script = null)
    {
        await Page.AddInitScriptAsync(script ?? TransitionSpy);
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await GotoCabinetAsync("/?sample=65");
    }

    private async Task WaitForTransitionToEndAsync()
    {
        await Expect(Page.Locator("html[data-pull-kind]")).ToHaveCountAsync(0);
    }

    private async Task WaitForHeadingAtTopAsync()
    {
        var offset = double.NaN;

        for (var attempt = 0; attempt < 100; attempt++)
        {
            offset = await Page.EvaluateAsync<double>(
                "() => document.querySelector('.card-expansions-title').getBoundingClientRect().top - document.querySelector('.card').getBoundingClientRect().top");

            if (offset is >= -1 and <= HeadingTopTolerance)
            {
                return;
            }

            await Task.Delay(50);
        }

        offset.Should().BeInRange(-1, HeadingTopTolerance);
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

    private async Task<string[]> AnimationNamesAsync()
    {
        return await Page.EvaluateAsync<string[]>("() => window.__pullSpy.names");
    }

    private async Task ClearAnimationNamesAsync()
    {
        await Page.EvaluateAsync("() => { window.__pullSpy.names.length = 0; }");
    }

    private sealed record CardLink(long? EntryId, int GameId, string Title);

    private sealed record CardData(long EntryId, string Title, bool IsExpansion, List<CardLink> Expansions);
}

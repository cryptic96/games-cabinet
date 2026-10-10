using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that opening a card pulls the tapped box off the shelf with a view transition, that the box's slot stays
/// empty while the card is open and that the box slides back into it on close. A script added before the page loads counts every
/// call to the transition API and records the names of the animations running once the transition is ready.
/// </summary>
[Trait("Category", "Browser")]
public sealed class PullOutTests : CabinetPageTest
{
    private const string OpenCard = "dialog.card-dialog[open]";
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
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

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

    private async Task StartCabinetAsync(int width, int height)
    {
        await Page.AddInitScriptAsync(TransitionSpy);
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await GotoCabinetAsync("/?sample=65");
    }

    private async Task WaitForTransitionToEndAsync()
    {
        await Expect(Page.Locator("html[data-pull-kind]")).ToHaveCountAsync(0);
    }

    private async Task<int> TransitionCallsAsync()
    {
        return await Page.EvaluateAsync<int>("() => window.__pullSpy.calls");
    }

    private async Task<string[]> AnimationNamesAsync()
    {
        return await Page.EvaluateAsync<string[]>("() => window.__pullSpy.names");
    }
}

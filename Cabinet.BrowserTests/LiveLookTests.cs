using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that a look the owner publishes reaches a public page that is already open: the page follows within a few
/// seconds through the live channel, cross-fades where the browser can, leaves an open card open, and swaps the footer logo with the
/// wall.
/// </summary>
[Trait("Category", "Browser")]
public sealed class LiveLookTests : CabinetPageTest
{
    private const string SettingsPath = "/owner/settings";
    private const string OpenCard = "dialog.card-dialog[open]";
    private const string FirstBox = "#cabinet .placement:not([data-kind=\"moreMarker\"])";
    private const string SavedText = "Saved for everyone. Open pages follow within a few seconds.";
    private const float FollowTimeoutMs = 5000;
    private const string TransitionSpy = """
        (() => {
          window.__lookSpy = { calls: 0 };
          const original = document.startViewTransition?.bind(document);
          if (original === undefined) {
            return;
          }
          document.startViewTransition = (callback) => {
            window.__lookSpy.calls += 1;
            return original(callback);
          };
        })();
        """;

    private const string LampSpy = """
        (() => {
          window.__lamp = {};
          new MutationObserver(() => {
            const layer = document.querySelector('#cabinet .section-light');
            if (document.documentElement.hasAttribute('data-lit') && layer !== null && window.__lamp.opacityAtLit === undefined) {
              window.__lamp.opacityAtLit = Number(getComputedStyle(layer).opacity);
              window.__lamp.eveningAtLit = typeof window.cabinetEvening;
            }
          }).observe(document, { attributes: true, subtree: true, attributeFilter: ['data-lit'] });
        })();
        """;

    private static readonly Regex WhiteLogo = new(@"/img/powered-by-bgg\.[^/]*svg", RegexOptions.None, TimeSpan.FromSeconds(1));
    private static readonly Regex DarkLogo = new(@"/img/powered-by-bgg-dark\.[^/]*svg", RegexOptions.None, TimeSpan.FromSeconds(1));
    private readonly TemporaryDirectory _storage = new();
    private readonly FakeTimeProvider _clock =SyncHarness.NewClock();

    [Fact]
    public async Task A_look_published_on_the_owner_page_reaches_an_open_public_page_with_one_cross_fade_and_the_card_stays_open()
    {
        await StartCabinetAsync();
        await Page.Locator(FirstBox).First.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator("html[data-pull-kind]")).ToHaveCountAsync(0);
        await Page.EvaluateAsync("() => { window.__lookSpy.calls = 0; }");

        await PublishAsync(("design", "modern"), ("wood", "oak"));

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-wood", "oak", new() { Timeout = FollowTimeoutMs });
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-design", "modern");
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator("html[data-look-switch]")).ToHaveCountAsync(0);
        (await Page.EvaluateAsync<int>("() => window.__lookSpy.calls")).Should().Be(1);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Turning_the_lights_on_loads_the_lamp_script_once_applies_the_look_after_it_ran_and_fades_the_light_in_without_a_cross_fade()
    {
        var eveningRequests = 0;
        await Page.AddInitScriptAsync(LampSpy);
        Page.Request += (_, request) =>
        {
            if (request.Url.Contains("/js/evening.js", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref eveningRequests);
            }
        };
        await StartCabinetAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-lights", "off");
        Volatile.Read(ref eveningRequests).Should().Be(0, "a page opened with the lights off has no reason to load the lamp script");
        await Page.EvaluateAsync("() => { window.__lookSpy.calls = 0; }");

        await PublishAsync(("lights", "on"));

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-lights", "on", new() { Timeout = FollowTimeoutMs });
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-lit", string.Empty);
        await Expect(Page.Locator("#cabinet .section-light").First).ToHaveCSSAsync("opacity", "1");
        Volatile.Read(ref eveningRequests).Should().Be(1);
        (await Page.EvaluateAsync<string>("() => window.__lamp.eveningAtLit")).Should().Be("object");
        (await Page.EvaluateAsync<double>("() => window.__lamp.opacityAtLit")).Should().BeLessThan(0.5, "the lamp fades on instead of appearing");
        (await Page.EvaluateAsync<int>("() => window.__lookSpy.calls")).Should().Be(0);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_footer_logo_follows_the_wall_to_the_dark_file_on_a_light_wall_and_back()
    {
        await StartCabinetAsync();
        var logo = Page.Locator("footer .bgg-credit img");
        await Expect(logo).ToHaveAttributeAsync("src", WhiteLogo);
        var alt = await logo.GetAttributeAsync("alt");
        var size = (await logo.GetAttributeAsync("width"), await logo.GetAttributeAsync("height"));

        await PublishAsync(("wood", "birch"));

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-wood", "birch", new() { Timeout = FollowTimeoutMs });
        await Expect(logo).ToHaveAttributeAsync("src", DarkLogo);
        await Expect(logo).ToHaveAttributeAsync("alt", alt!);

        await PublishAsync(("wood", "mahogany"));

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-wood", "mahogany", new() { Timeout = FollowTimeoutMs });
        await Expect(logo).ToHaveAttributeAsync("src", WhiteLogo);
        (await logo.GetAttributeAsync("width"), await logo.GetAttributeAsync("height")).Should().Be(size);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_visitor_who_prefers_less_motion_gets_a_design_change_with_no_view_transition()
    {
        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });
        await StartCabinetAsync();
        await Page.EvaluateAsync("() => { window.__lookSpy.calls = 0; }");

        await PublishAsync(("design", "library"));

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-design", "library", new() { Timeout = FollowTimeoutMs });
        (await Page.EvaluateAsync<int>("() => window.__lookSpy.calls")).Should().Be(0);
        await Expect(Page.Locator("html[data-look-switch]")).ToHaveCountAsync(0);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task With_the_live_channel_blocked_the_page_keeps_its_look_and_follows_on_the_next_catch_up_or_load()
    {
        await Page.RouteAsync("**/cabinet/live**", route => route.AbortAsync());
        await StartCabinetAsync();

        await PublishAsync(("wood", "oak"));
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-wood", "brown");
        await Expect(Page.Locator("#cabinet .placement").First).ToBeVisibleAsync();

        await Page.EvaluateAsync("() => document.dispatchEvent(new Event('visibilitychange'))");

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-wood", "oak", new() { Timeout = FollowTimeoutMs });

        await PublishAsync(("wood", "mahogany"));
        await Page.ReloadAsync();

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-wood", "mahogany");
    }

    private async Task StartCabinetAsync()
    {
        var bgg = new SyncRounds.SwitchableBgg(SyntheticBggCollection.Create(5));
        var factory = SyncHarness.CreateFactory(
            bgg.Handler,
            _clock,
            new Dictionary<string, string?> { ["Storage:Directory"] = _storage.FullPath });
        await StartAsync(factory);
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, _clock, advance: false);
        await Page.SetViewportSizeAsync(1440, 900);
        await Page.AddInitScriptAsync(TransitionSpy);
        await GotoCabinetAsync("/");
    }

    private async Task PublishAsync(params (string Field, string Value)[] choices)
    {
        var owner = await Page.Context.NewPageAsync();

        try
        {
            await owner.GotoAsync($"http://127.0.0.1:{OwnerPort}{SettingsPath}");
            await owner.WaitForSelectorAsync("#owner-main", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });

            foreach (var (field, value) in choices)
            {
                await owner.Locator($"input[name={field}][value={value}]").CheckAsync();
            }

            await owner.GetByRole(AriaRole.Button, new() { Name = "Use for everyone" }).ClickAsync();
            await Assertions.Expect(owner.Locator("#finish-status")).ToContainTextAsync(SavedText);
            _clock.Advance(OwnerChangeNotifier.QuietPeriod);
        }
        finally
        {
            await owner.CloseAsync();
        }
    }
}

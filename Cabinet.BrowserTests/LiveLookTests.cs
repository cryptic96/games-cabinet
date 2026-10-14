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
    internal const string SettingsPath = "/owner/settings";
    private const string OpenCard = "dialog.card-dialog[open]";
    private const string FirstBox = "#cabinet .placement:not([data-kind=\"moreMarker\"])";
    internal const string SavedText = "Saved for everyone. Open pages follow within a few seconds.";
    private const float FollowTimeoutMs = 5000;
    internal const int ManyGames = 400;
    private const string ViewerZone = "Europe/Berlin";
    private const string EveningMoment = "2027-01-15T20:00:00+01:00";
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

    private const string PullGate = """
        (() => {
          const original = document.startViewTransition?.bind(document);
          const gate = { open: false, release: () => {}, releasedAt: null, changes: [] };
          window.__pullGate = gate;
          new MutationObserver(() => gate.changes.push(performance.now())).observe(document, { attributes: true, subtree: true, attributeFilter: ['data-wood'] });
          if (original === undefined) {
            return;
          }
          document.startViewTransition = (callback) => {
            const transition = original(callback);
            if (!gate.open || !document.documentElement.hasAttribute('data-pull-kind')) {
              return transition;
            }
            const held = new Promise((resolve) => {
              gate.release = () => {
                gate.releasedAt = performance.now();
                resolve();
              };
            });
            return {
              ready: transition.ready,
              updateCallbackDone: transition.updateCallbackDone,
              finished: transition.finished.then(() => held),
              skipTransition: () => transition.skipTransition(),
            };
          };
        })();
        """;

    internal const string AnchorSpy = """
        (() => {
          window.__anchor = [];
          const set = CSSStyleDeclaration.prototype.setProperty;
          const remove = CSSStyleDeclaration.prototype.removeProperty;
          CSSStyleDeclaration.prototype.setProperty = function (name, value, priority) {
            if (name === 'overflow-anchor') {
              window.__anchor.push(value);
            }
            return set.call(this, name, value, priority);
          };
          CSSStyleDeclaration.prototype.removeProperty = function (name) {
            if (name === 'overflow-anchor') {
              window.__anchor.push('removed');
            }
            return remove.call(this, name);
          };
        })();
        """;

    internal const string PlaceScript = """
        () => {
          const centre = innerHeight / 2;
          const sections = [...document.querySelectorAll('#cabinet .section')];
          const index = sections.findIndex((section) => {
            const rect = section.getBoundingClientRect();
            return rect.top <= centre && centre < rect.bottom;
          });
          return [index, sections[index].getBoundingClientRect().top, scrollY];
        }
        """;

    internal const string SectionTopScript = "(index) => document.querySelectorAll('#cabinet .section')[index].getBoundingClientRect().top";

    internal const string ScrollToThirdSectionScript = """
        () => {
          const rect = document.querySelectorAll('#cabinet .section')[2].getBoundingClientRect();
          window.scrollTo(0, rect.top + scrollY + rect.height / 2 - innerHeight / 2);
        }
        """;

    private static readonly Regex WhiteLogo = new(@"/img/powered-by-bgg\.[^/]*svg", RegexOptions.None, TimeSpan.FromSeconds(1));
    private static readonly Regex DarkLogo = new(@"/img/powered-by-bgg-dark\.[^/]*svg", RegexOptions.None, TimeSpan.FromSeconds(1));
    private readonly TemporaryDirectory _storage = new();
    private readonly FakeTimeProvider _clock =SyncHarness.NewClock();

    /// <summary>Makes the browser read the clock in a Central European zone, which has summer time.</summary>
    public override BrowserNewContextOptions ContextOptions()
    {
        var options = base.ContextOptions();
        options.TimezoneId = ViewerZone;

        return options;
    }

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

    [Fact]
    public async Task A_design_change_keeps_the_section_in_the_middle_of_the_screen_where_it_was_and_a_wood_change_moves_nothing()
    {
        await Page.AddInitScriptAsync(AnchorSpy);
        await StartCabinetAsync(gameCount: ManyGames);
        await Expect(Page.Locator("#cabinet .section").Nth(2)).ToBeVisibleAsync();
        await Page.EvaluateAsync(ScrollToThirdSectionScript);
        var before = await Page.EvaluateAsync<double[]>(PlaceScript);
        var index = (int)before[0];
        before[2].Should().BeGreaterThan(0, "the page is scrolled down to the third section");

        await PublishAsync(("design", "library"));

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-design", "library", new() { Timeout = FollowTimeoutMs });
        await Expect(Page.Locator("html[data-look-switch]")).ToHaveCountAsync(0);
        var after = await Page.EvaluateAsync<double>(SectionTopScript, index);
        after.Should().BeApproximately(before[1], 2, "the section that was in the middle of the screen is where it was");
        (await Page.EvaluateAsync<double[]>(PlaceScript))[2].Should().NotBe(before[2], "the sections grew, so the page had to scroll to keep the place");
        (await Page.EvaluateAsync<string[]>("() => window.__anchor")).Should().Equal(["none", "removed"], "native scroll anchoring is off only while the design is applied");

        var settled = await Page.EvaluateAsync<double[]>(PlaceScript);

        await PublishAsync(("wood", "oak"));

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-wood", "oak", new() { Timeout = FollowTimeoutMs });
        await Expect(Page.Locator("html[data-look-switch]")).ToHaveCountAsync(0);
        (await Page.EvaluateAsync<double[]>(PlaceScript))[2].Should().Be(settled[2], "a change of the wood does not scroll");
        (await Page.EvaluateAsync<string[]>("() => window.__anchor")).Should().HaveCount(2, "a change of the wood leaves the anchoring alone");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_look_that_arrives_while_a_box_is_being_pulled_out_waits_until_the_pull_out_is_over_and_the_card_stays_open()
    {
        await Page.AddInitScriptAsync(PullGate);
        await StartCabinetAsync();
        await Page.EvaluateAsync("() => { window.__pullGate.open = true; }");

        await Page.Locator(FirstBox).First.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator("html[data-pull-kind]")).ToHaveCountAsync(1);

        await PublishAsync(("wood", "oak"));
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-wood", "brown");
        await Expect(Page.Locator("html[data-pull-kind]")).ToHaveCountAsync(1);

        await Page.EvaluateAsync("() => window.__pullGate.release()");

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-wood", "oak", new() { Timeout = FollowTimeoutMs });
        await Expect(Page.Locator("html[data-pull-kind]")).ToHaveCountAsync(0);
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        var timing = await Page.EvaluateAsync<double[]>("() => [window.__pullGate.releasedAt, ...window.__pullGate.changes]");
        timing[1].Should().BeGreaterThanOrEqualTo(timing[0], "the look is applied only after the pull-out has ended");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Turning_the_lights_from_off_to_evenings_only_after_dusk_loads_the_lamp_script_marks_the_evening_and_fades_the_lights_on()
    {
        var eveningRequests = 0;
        Page.Request += (_, request) =>
        {
            if (request.Url.Contains("/js/evening.js", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref eveningRequests);
            }
        };
        await StartCabinetAsync(browserTime: EveningMoment);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-lights", "off");
        Volatile.Read(ref eveningRequests).Should().Be(0, "a page opened with the lights off has no reason to load the lamp script");

        await PublishAsync(("lights", "evenings"));

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-lights", "evenings", new() { Timeout = FollowTimeoutMs });
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-evening", string.Empty);
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-lit", string.Empty);
        await Expect(Page.Locator("#cabinet .section-light").First).ToHaveCSSAsync("opacity", "1");
        Volatile.Read(ref eveningRequests).Should().Be(1);
        ConsoleErrors.Should().BeEmpty();
    }

    private async Task StartCabinetAsync(int gameCount = 5, string? browserTime = null)
    {
        var bgg = new SyncRounds.SwitchableBgg(SyntheticBggCollection.Create(gameCount));
        var factory = SyncHarness.CreateFactory(
            bgg.Handler,
            _clock,
            new Dictionary<string, string?> { ["Storage:Directory"] = _storage.FullPath });
        await StartAsync(factory);
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, _clock, advance: false);
        await Page.SetViewportSizeAsync(1440, 900);
        await Page.AddInitScriptAsync(TransitionSpy);

        if (browserTime is not null)
        {
            await Page.Clock.InstallAsync(new ClockInstallOptions { TimeString = browserTime });
        }

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

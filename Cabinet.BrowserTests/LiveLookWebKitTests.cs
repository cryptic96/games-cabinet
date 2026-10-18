using Cabinet.FakeBgg;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Live;
using Cabinet.Service.Owner;
using Cabinet.Service.Sync;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in WebKit, the engine behind Safari, that a live design change keeps the section in the middle of the screen where it was.
/// WebKit does not anchor the scroll position by itself, so here the page's own place keeping does all the work. The browser is
/// launched explicitly rather than chosen by the runner, and the test only runs where <c>CABINET_WEBKIT=1</c> is set, because the
/// engine is installed by a separate step.
/// </summary>
/// <remarks>
/// Every wait is for a condition, never for the time a fast machine needs. A page that draws four hundred games keeps its main
/// thread busy for a long time on a slow machine, and its live connection can drop and come back in that time; a server that still
/// counts the connection does not prove the page can hear it. So before the design is published, the server sends the page the
/// status it already shows, again until the page has received it, which proves a push gets through from end to end right then.
/// The quiet period after the owner's save is then let pass on the host's clock, again if the design has not arrived after a while,
/// a bounded number of times, and the end state is read with retrying assertions.
/// </remarks>
[Trait("Category", "Browser")]
[Trait("Engine", "WebKit")]
public sealed class LiveLookWebKitTests
{
    private const string EnableVariable = "CABINET_WEBKIT";
    private const string SkipReason = "WebKit runs only where CABINET_WEBKIT=1";
    private const double PlaceToleranceInPixels = 2;

    /// <summary>How long the page may take to hear one push before the same status is sent again.</summary>
    private const float HearingStepTimeoutMs = 5000;

    /// <summary>How many times the status the page shows is sent before the page must have heard one.</summary>
    private const int MostHearingRounds = 8;

    /// <summary>How long the design may take to arrive after one quiet period has passed on the host's clock.</summary>
    private const float FollowStepTimeoutMs = 5000;

    /// <summary>How many times the quiet period is let pass before the design must have arrived.</summary>
    private const int MostQuietPeriods = 4;

    /// <summary>How long the cross-fade of the design change may take to end once the design is on the page.</summary>
    private const float SwitchEndTimeoutMs = 15000;

    /// <summary>
    /// Counts, in the page, the statuses the live channel delivers. It wraps the browser client of the live channel as the page loads
    /// it, adds one more listener to every connection the page builds, and changes nothing the page does.
    /// </summary>
    private const string LiveSpy = """
        (() => {
          const rec = { built: 0, heard: 0 };
          window.__live = rec;
          let client;
          Object.defineProperty(window, 'signalR', {
            configurable: true,
            get() {
              return client;
            },
            set(value) {
              client = value;
              const build = value.HubConnectionBuilder.prototype.build;
              value.HubConnectionBuilder.prototype.build = function () {
                const connection = build.call(this);
                rec.built += 1;
                connection.on('statusChanged', () => {
                  rec.heard += 1;
                });
                return connection;
              };
            },
          });
        })();
        """;

    [Fact]
    public async Task A_design_change_keeps_the_section_in_the_middle_of_the_screen_where_it_was()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable(EnableVariable) == "1", SkipReason);

        using var storage = new TemporaryDirectory();
        var clock = SyncHarness.NewClock();
        var bgg = new SyncRounds.SwitchableBgg(SyntheticBggCollection.Create(LiveLookTests.ManyGames));
        await using var factory = SyncHarness.CreateFactory(
            bgg.Handler,
            clock,
            new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using (var client = factory.CreatePublicClient())
        {
            await SyncRounds.PressAndWait(client, clock, advance: false);
        }

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Webkit.LaunchAsync();
        var visitor = await NewContextAsync(browser);
        var owner = await NewContextAsync(browser);
        var page = await visitor.NewPageAsync();
        await page.AddInitScriptAsync(LiveSpy);
        await page.AddInitScriptAsync(LiveLookTests.AnchorSpy);
        await page.GotoAsync($"http://127.0.0.1:{factory.PublicPort}/");
        await Assertions.Expect(page.Locator("#cabinet .section").Nth(2)).ToBeVisibleAsync(new() { Timeout = LiveLookTests.FirstDrawTimeoutMs });
        var limiter = factory.ServingServices.GetRequiredService<LiveConnectionLimiter>();
        await SyncHarness.WaitUntil(() => Task.FromResult(limiter.Count == 1));
        await page.EvaluateAsync(LiveLookTests.ScrollToThirdSectionScript);
        var before = await page.EvaluateAsync<double[]>(LiveLookTests.PlaceScript);
        var index = (int)before[0];
        before[2].Should().BeGreaterThan(0, "the page is scrolled down to the third section");
        await WaitUntilThePageHearsAPushAsync(page, factory.ServingServices);

        await SaveForEveryoneAsync(owner, factory.OwnerPort, "design", "library");
        await LetTheQuietPeriodPassUntilAsync(page, clock, "() => document.documentElement.dataset.design === 'library'");

        await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("data-design", "library");
        await Assertions.Expect(page.Locator("html[data-look-switch]")).ToHaveCountAsync(0, new() { Timeout = SwitchEndTimeoutMs });
        var after = await page.EvaluateAsync<double>(LiveLookTests.SectionTopScript, index);
        after.Should().BeApproximately(before[1], PlaceToleranceInPixels, "the section that was in the middle of the screen is where it was");
        (await page.EvaluateAsync<double[]>(LiveLookTests.PlaceScript))[2].Should().NotBe(before[2], "the sections grew, so the page had to scroll to keep the place");
        (await page.EvaluateAsync<string[]>("() => window.__anchor")).Should().Equal(["none", "removed"], "the page's own rule alone moves the page, with native anchoring off only while the design is applied");
    }

    private static Task<IBrowserContext> NewContextAsync(IBrowser browser) =>
        browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
            ExtraHTTPHeaders = new Dictionary<string, string> { ["Accept-Encoding"] = "identity" },
        });

    /// <summary>
    /// Sends the page the status it already shows, again each time the page has not heard it after a while, a bounded number of times,
    /// until the page has received one. The page shows nothing new for it: the look and the collection are the ones on screen.
    /// </summary>
    /// <param name="page">The visitor's page, with the live spy installed.</param>
    /// <param name="services">The services of the running host.</param>
    private static async Task WaitUntilThePageHearsAPushAsync(IPage page, IServiceProvider services)
    {
        var live = services.GetRequiredService<ILiveNotifier>();
        var status = services.GetRequiredService<SyncStatusService>();
        var heard = await page.EvaluateAsync<int>("() => window.__live.heard");

        for (var round = 1; round <= MostHearingRounds; round++)
        {
            await live.PublishAsync(status.Current(), TestContext.Current.CancellationToken);

            try
            {
                await page.WaitForFunctionAsync("(heard) => window.__live.heard > heard", heard, new PageWaitForFunctionOptions { Timeout = HearingStepTimeoutMs });

                return;
            }
            catch (TimeoutException) when (round < MostHearingRounds)
            {
            }
        }
    }

    /// <summary>
    /// Lets the quiet period after the owner's save pass on the host's clock until the condition holds on the page, a bounded number of
    /// times, so a host that has not yet scheduled its notice when the clock first moves still sends it.
    /// </summary>
    /// <param name="page">The visitor's page.</param>
    /// <param name="clock">The host's clock.</param>
    /// <param name="condition">A script that tells whether the change has arrived.</param>
    private static async Task LetTheQuietPeriodPassUntilAsync(IPage page, FakeTimeProvider clock, string condition)
    {
        for (var round = 1; round <= MostQuietPeriods; round++)
        {
            clock.Advance(OwnerChangeNotifier.QuietPeriod);

            try
            {
                await page.WaitForFunctionAsync(condition, null, new PageWaitForFunctionOptions { Timeout = FollowStepTimeoutMs });

                return;
            }
            catch (TimeoutException) when (round < MostQuietPeriods)
            {
            }
        }
    }

    private static async Task SaveForEveryoneAsync(IBrowserContext ownerContext, int ownerPort, string field, string value)
    {
        var page = await ownerContext.NewPageAsync();

        await page.GotoAsync($"http://127.0.0.1:{ownerPort}{LiveLookTests.SettingsPath}");
        await page.WaitForSelectorAsync("#owner-main", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
        await page.Locator($"input[name={field}][value={value}]").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Use for everyone" }).ClickAsync();
        await Assertions.Expect(page.Locator("#finish-status")).ToContainTextAsync(LiveLookTests.SavedText);
        await page.CloseAsync();
    }
}

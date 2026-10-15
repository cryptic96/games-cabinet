using Cabinet.FakeBgg;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Live;
using Cabinet.Service.Owner;
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
[Trait("Category", "Browser")]
[Trait("Engine", "WebKit")]
public sealed class LiveLookWebKitTests
{
    private const string EnableVariable = "CABINET_WEBKIT";
    private const string SkipReason = "WebKit runs only where CABINET_WEBKIT=1";
    private const float FollowTimeoutMs = 5000;
    private const double PlaceToleranceInPixels = 2;

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
        await page.AddInitScriptAsync(LiveLookTests.AnchorSpy);
        await page.GotoAsync($"http://127.0.0.1:{factory.PublicPort}/");
        await Assertions.Expect(page.Locator("#cabinet .section").Nth(2)).ToBeVisibleAsync();
        var limiter = factory.ServingServices.GetRequiredService<LiveConnectionLimiter>();
        await SyncHarness.WaitUntil(() => Task.FromResult(limiter.Count == 1));
        await page.EvaluateAsync(LiveLookTests.ScrollToThirdSectionScript);
        var before = await page.EvaluateAsync<double[]>(LiveLookTests.PlaceScript);
        var index = (int)before[0];
        before[2].Should().BeGreaterThan(0, "the page is scrolled down to the third section");

        await PublishAsync(owner, factory.OwnerPort, clock, "design", "library");

        await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("data-design", "library", new() { Timeout = FollowTimeoutMs });
        await Assertions.Expect(page.Locator("html[data-look-switch]")).ToHaveCountAsync(0);
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

    private static async Task PublishAsync(IBrowserContext ownerContext, int ownerPort, FakeTimeProvider clock, string field, string value)
    {
        var page = await ownerContext.NewPageAsync();

        await page.GotoAsync($"http://127.0.0.1:{ownerPort}{LiveLookTests.SettingsPath}");
        await page.WaitForSelectorAsync("#owner-main", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
        await page.Locator($"input[name={field}][value={value}]").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Use for everyone" }).ClickAsync();
        await Assertions.Expect(page.Locator("#finish-status")).ToContainTextAsync(LiveLookTests.SavedText);
        clock.Advance(OwnerChangeNotifier.QuietPeriod);
        await page.CloseAsync();
    }
}

using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Live;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves a visitor turned away by a live limit still gets the whole cabinet: the page draws, shows no message and raises no
/// script error, and follows the live channel again on a reload once there is room.
/// </summary>
[Trait("Category", "Browser")]
public sealed class LiveRefusalTests : CabinetPageTest
{
    private const string CabinetBoxes = "#cabinet .placement";
    private const string StatusNote = "#sync-note";
    private const string SyncPanel = ".sync";

    private readonly TemporaryDirectory _storage = new();
    private readonly FakeTimeProvider _clock = SyncHarness.NewClock();

    [Fact]
    public async Task A_page_refused_a_live_place_still_draws_the_cabinet_stays_quiet_and_connects_on_reload_when_there_is_room()
    {
        var refusals = new LiveRefusalWatch();
        var factory = await StartHostAsync(refusals);
        var limiter = factory.ServingServices.GetRequiredService<LiveConnectionLimiter>();
        await GotoCabinetAsync("/");
        await SyncHarness.WaitUntil(() => Task.FromResult(limiter.Count == 1));
        var firstNote = await Page.Locator(StatusNote).InnerTextAsync();
        var firstRunning = await Page.Locator(SyncPanel).GetAttributeAsync("data-running");
        var refusalsBefore = refusals.Refusals;
        var pageErrors = new List<string>();
        var second = await OpenSecondPageAsync(pageErrors);

        await second.GotoAsync($"http://127.0.0.1:{PublicPort}/");
        await second.WaitForSelectorAsync(CabinetBoxes, new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
        await SyncHarness.WaitUntil(() => Task.FromResult(refusals.Refusals > refusalsBefore));

        await Assertions.Expect(second.Locator(CabinetBoxes).First).ToBeVisibleAsync();
        (await second.Locator(StatusNote).InnerTextAsync()).Should().Be(firstNote, "a refused page shows nothing a connected page would not");
        (await second.Locator(SyncPanel).GetAttributeAsync("data-running")).Should().Be(firstRunning);
        pageErrors.Should().BeEmpty("a refusal is not a script error");
        limiter.Count.Should().Be(1, "the refused page took no place and the first kept its own");

        await Page.GotoAsync("about:blank");
        await second.ReloadAsync();
        await second.WaitForSelectorAsync(CabinetBoxes, new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });

        await SyncHarness.WaitUntil(() => Task.FromResult(limiter.Count == 1), TimeSpan.FromSeconds(30));
        pageErrors.Should().BeEmpty();
        await Assertions.Expect(second.Locator(CabinetBoxes).First).ToBeVisibleAsync();
    }

    private async Task<CabinetWebApplicationFactory> StartHostAsync(LiveRefusalWatch refusals)
    {
        var bgg = new SyncRounds.SwitchableBgg(SyntheticBggCollection.Create(5));
        var factory = SyncHarness.CreateFactory(
            bgg.Handler,
            _clock,
            new Dictionary<string, string?>
            {
                ["Storage:Directory"] = _storage.FullPath,
                ["ClientLimits:LiveConnectionsPerClient"] = "1",
            },
            configureServices: services => refusals.Register(services));
        await StartAsync(factory);
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, _clock, advance: false);
        await Page.SetViewportSizeAsync(1440, 900);

        return factory;
    }

    private async Task<IPage> OpenSecondPageAsync(List<string> pageErrors)
    {
        var context = await Browser.NewContextAsync(ContextOptions());
        await context.SetExtraHTTPHeadersAsync(new Dictionary<string, string> { ["Accept-Encoding"] = "identity" });
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        page.PageError += (_, error) =>
        {
            lock (pageErrors)
            {
                pageErrors.Add(error);
            }
        };

        return page;
    }
}

using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Owner;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that work which meets a stuck page runs again on a fresh page. A stuck page is made by keeping the main
/// thread of its renderer busy for far longer than the budget, which leaves every script, animation frame and screenshot asked of
/// that page unanswered, the way a renderer that is starved of processor time can stay unresponsive. The page is then closed, a fresh
/// page of the same browser context takes its place with the same recording of what it logs, and a page that is stuck on every
/// attempt still ends the test with the timeout. A timeout on a page that still answers is not a stall: it ends the test at once, and
/// so does a failed check inside the work, each after exactly one run.
/// </summary>
[Trait("Category", "Browser")]
public sealed class StalledPageTests : CabinetPageTest, IDisposable
{
    private const int RecoveringBudgetMs = 8000;

    private const int HopelessBudgetMs = 3000;

    private const int ResponsiveWaitMs = 1000;

    private const int StuckForMs = 60000;

    private const string BusyScript = "(milliseconds) => { const end = performance.now() + milliseconds; while (performance.now() < end) { } }";

    private readonly TemporaryDirectory _storage = new();

    [Fact]
    public async Task A_page_that_stalls_is_replaced_and_the_work_runs_again_on_the_fresh_page()
    {
        await StartCabinetAsync();
        var stalled = Page;
        var pages = new List<IPage>();

        await RunOnFreshPageWhenStalledAsync(
            async page =>
            {
                pages.Add(page);
                await GotoCabinetAsync(page, "/?sample=65");

                if (pages.Count == 1)
                {
                    GetStuck(page);
                }

                await page.Locator("#cabinet .section").First.WaitForAsync();
                await page.EvaluateAsync("() => console.error('seen from the fresh page')");
            },
            TimeSpan.FromMilliseconds(RecoveringBudgetMs),
            attempts: 3);

        pages.Should().HaveCount(2);
        pages[0].Should().BeSameAs(stalled);
        pages[1].Should().NotBeSameAs(stalled);
        stalled.IsClosed.Should().BeTrue();
        ActivePage.Should().BeSameAs(pages[1]);
        (await pages[1].Locator("#cabinet .placement").CountAsync()).Should().BeGreaterThan(0);
        ConsoleErrors.Should().Equal("seen from the fresh page");
    }

    [Fact]
    public async Task A_page_that_is_stuck_on_every_attempt_ends_the_test_with_the_timeout()
    {
        await StartCabinetAsync();
        var runs = 0;

        var stuckEveryTime = async () => await RunOnFreshPageWhenStalledAsync(
            async page =>
            {
                runs++;
                await GotoCabinetAsync(page, "/?sample=65");
                GetStuck(page);
                await page.Locator("#cabinet .section").First.WaitForAsync();
            },
            TimeSpan.FromMilliseconds(HopelessBudgetMs),
            attempts: 2);

        await stuckEveryTime.Should().ThrowAsync<TimeoutException>();
        runs.Should().Be(2);
    }

    [Fact]
    public async Task A_timeout_on_a_page_that_still_answers_ends_the_test_after_one_run()
    {
        await StartCabinetAsync();
        var runs = 0;

        var waitsForNothing = async () => await RunOnFreshPageWhenStalledAsync(
            async page =>
            {
                runs++;
                await GotoCabinetAsync(page, "/?sample=65");
                await page.Locator("#no-such-element").WaitForAsync(new LocatorWaitForOptions { Timeout = ResponsiveWaitMs });
            },
            TimeSpan.FromMilliseconds(RecoveringBudgetMs),
            attempts: 3);

        await waitsForNothing.Should().ThrowAsync<TimeoutException>();
        runs.Should().Be(1);
        Page.IsClosed.Should().BeFalse("a page that answers is kept");
    }

    [Fact]
    public async Task A_run_that_outlasts_its_budget_on_a_page_that_still_answers_ends_the_test_after_one_run()
    {
        await StartCabinetAsync();
        var runs = 0;

        var slowButAlive = async () => await RunOnFreshPageWhenStalledAsync(
            async page =>
            {
                runs++;
                await GotoCabinetAsync(page, "/?sample=65");
                await Task.Delay(Timeout.Infinite, TestContext.Current.CancellationToken);
            },
            TimeSpan.FromMilliseconds(HopelessBudgetMs),
            attempts: 3);

        await slowButAlive.Should().ThrowAsync<TimeoutException>();
        runs.Should().Be(1);
    }

    [Fact]
    public async Task A_failed_check_inside_the_work_runs_once_and_propagates()
    {
        await StartCabinetAsync();
        var runs = 0;

        var failing = async () => await RunOnFreshPageWhenStalledAsync(
            async page =>
            {
                runs++;
                await GotoCabinetAsync(page, "/?sample=65");
                (await page.Locator("#cabinet .placement").CountAsync()).Should().Be(-1, "a failed check is a real failure");
            },
            TimeSpan.FromMilliseconds(RecoveringBudgetMs),
            attempts: 3);

        var thrown = await Record.ExceptionAsync(failing);

        thrown.Should().NotBeNull().And.NotBeOfType<TimeoutException>();
        runs.Should().Be(1);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void GetStuck(IPage page)
    {
        var busy = page.EvaluateAsync(BusyScript, StuckForMs);
        _ = busy.ContinueWith(static ended => _ = ended.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    private async Task StartCabinetAsync()
    {
        OwnerDataSeed.Write(
            _storage.FullPath,
            OwnerData.Empty with { Revision = 1, Look = new CabinetLook("classic", "brown", "wood", "off", "shaded") });
        await StartAsync(new Dictionary<string, string?> { ["Prototype:Enabled"] = "true", ["Storage:Directory"] = _storage.FullPath });
        await Page.SetViewportSizeAsync(1440, 900);
    }
}

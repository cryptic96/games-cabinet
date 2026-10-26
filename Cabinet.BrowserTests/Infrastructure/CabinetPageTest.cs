using Cabinet.IntegrationTests.Infrastructure;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;

namespace Cabinet.BrowserTests.Infrastructure;

/// <summary>
/// Base class for tests that drive a real browser against the cabinet host running in this process. It starts the host
/// on a loopback port, records what the page logs and which hosts it talks to, and can keep screenshots for review.
/// </summary>
public abstract class CabinetPageTest : PageTest
{
    private const string ScreenshotDirectoryVariable = "CABINET_SCREENSHOT_DIR";

    private const string CabinetReadySelector = "#cabinet .placement, .cabinet-message:not(.cabinet-loading)";

    private const string OpenCardSelector = "dialog.card-dialog[open]";

    private const string PullOutRunningSelector = "html[data-pull-kind]";

    /// <summary>
    /// How long a retrying assertion waits for its end state, in milliseconds. A passing test never waits this long, so the value only
    /// matters on a slow runner, where the default of five seconds is too short for a card to settle or a history step to be undone.
    /// </summary>
    private const float DefaultExpectTimeoutMs = 15000;

    /// <summary>The file in the screenshot directory that names every stalled page a test gave up on, one line each.</summary>
    private const string StallRecordFileName = "stalled-pages.txt";

    /// <summary>How long closing a page that stalled may take before it is left to the end of the test, in milliseconds.</summary>
    private const int StalledPageCloseTimeoutMs = 5000;

    /// <summary>How long a page may take to answer a trivial script before it is taken to be stuck, in milliseconds.</summary>
    private const int ResponsivenessProbeTimeoutMs = 3000;

    private static readonly object StallRecordLock = new();

    private readonly object _recordLock = new();
    private readonly List<string> _consoleErrors = [];
    private readonly HashSet<string> _requestHosts = new(StringComparer.Ordinal);
    private CabinetWebApplicationFactory? _factory;
    private IPage? _replacement;

    /// <summary>Sets the wait of every retrying assertion once, before the first test of the process creates its page.</summary>
    static CabinetPageTest() => Assertions.SetDefaultExpectTimeout(DefaultExpectTimeoutMs);

    /// <summary>The console errors and uncaught page errors seen since the host was started.</summary>
    protected IReadOnlyList<string> ConsoleErrors
    {
        get
        {
            lock (_recordLock)
            {
                return [.. _consoleErrors];
            }
        }
    }

    /// <summary>The <c>host:port</c> of every http or https request the page made since the host was started.</summary>
    protected IReadOnlyCollection<string> RequestHosts
    {
        get
        {
            lock (_recordLock)
            {
                return [.. _requestHosts];
            }
        }
    }

    /// <summary>The loopback port the public listener of the running host uses.</summary>
    protected int PublicPort => (_factory ?? throw new InvalidOperationException("Call StartAsync before using the host.")).PublicPort;

    /// <summary>The loopback port the owner listener of the running host uses.</summary>
    protected int OwnerPort => (_factory ?? throw new InvalidOperationException("Call StartAsync before using the host.")).OwnerPort;

    /// <summary>The page the test is driving now: the page the test base made, or the fresh page that took its place after it stalled.</summary>
    protected IPage ActivePage => _replacement ?? Page;

    /// <summary>
    /// Starts the host with the given settings and prepares the page to talk to it. Module scripts are only served with a
    /// JavaScript content type when the browser asks for no content encoding, because the test host does not run against
    /// published output, so the page context asks for the identity encoding.
    /// </summary>
    /// <param name="settings">Synthetic configuration values, such as the switch that enables the sample collections.</param>
    /// <param name="configureServices">Replaces services before the host starts, such as the source of the collection; null leaves them as they are.</param>
    protected Task<CabinetWebApplicationFactory> StartAsync(
        IReadOnlyDictionary<string, string?> settings,
        Action<Microsoft.Extensions.DependencyInjection.IServiceCollection>? configureServices = null) =>
        StartAsync(new CabinetWebApplicationFactory(settings, configureServices));

    /// <summary>
    /// Uses a host the test built itself, such as one whose BGG answers are scripted, and prepares the page to talk to it exactly
    /// as the overload that takes settings does. The test base disposes the host with the page.
    /// </summary>
    /// <param name="factory">The running host.</param>
    protected async Task<CabinetWebApplicationFactory> StartAsync(CabinetWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;

        await Page.Context.SetExtraHTTPHeadersAsync(new Dictionary<string, string> { ["Accept-Encoding"] = "identity" });
        RecordPage(Page);

        return factory;
    }

    /// <summary>
    /// Runs a piece of work against the page and, when it does not end within the budget or one of its waits times out and the page
    /// then proves unresponsive, closes that page, opens a fresh one in the same browser context and runs the work again, up to the
    /// given number of attempts. A browser that is short of processor time can, rarely, leave the renderer of one page unresponsive:
    /// a script it was asked to run, an animation frame or a screenshot is never answered, and the page does not come back, while a
    /// new page in the same browser works at once. The work must therefore start from nothing, as a view that loads its own address
    /// does. A timeout alone is not a stall, because Playwright reports a wait that ran out, such as a box that never drew, as a
    /// timeout too, and that is a real failure. So after a timeout the page is asked to evaluate a trivial script within a short
    /// budget; only a page that does not answer is replaced, and a page that answers ends the test with the original timeout. A failed
    /// check inside the work ends the test as it always did. What the fresh page logs is recorded the same way as what the first page
    /// logged, and each page given up on is written to the test output and named in <c>stalled-pages.txt</c> in the screenshot directory.
    /// </summary>
    /// <param name="work">What to do with the page. It is given the page of the attempt, the page the test started with at first.</param>
    /// <param name="budget">How long one attempt may take in all before its page is checked for being stuck.</param>
    /// <param name="attempts">How many attempts at most; the timeout of the last one ends the test.</param>
    /// <returns>A task that ends when an attempt has finished its work.</returns>
    /// <exception cref="TimeoutException">An attempt timed out on a page that still answers, or every attempt timed out on a stuck page.</exception>
    protected async Task RunOnFreshPageWhenStalledAsync(Func<IPage, Task> work, TimeSpan budget, int attempts)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);

        for (var attempt = 1; ; attempt++)
        {
            var page = ActivePage;
            var running = work(page);

            try
            {
                await running.WaitAsync(budget, TestContext.Current.CancellationToken);

                return;
            }
            catch (TimeoutException)
            {
                var stalled = attempt < attempts && !await AnswersAsync(page);

                if (!stalled)
                {
                    throw;
                }

                _ = running.ContinueWith(static ended => _ = ended.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                RecordStalledPage(attempt, page);
                await GiveUpOnAsync(page);
            }
        }
    }

    /// <summary>
    /// Waits until the opening pull-out of the card has ended. While it runs the page marks its root element, and a key press sent
    /// before the marker is gone can land on a card that is not yet ready to close, which leaves the dialog open on a slow runner.
    /// </summary>
    protected async Task WaitUntilTheCardHasSettledAsync()
    {
        await Assertions.Expect(Page.Locator(PullOutRunningSelector)).ToHaveCountAsync(0);
    }

    /// <summary>
    /// Closes the open card with the keyboard: waits until the opening pull-out has settled, presses Escape, waits until no card is
    /// open and then until the card's history step is gone, so a later navigation or Back step does not race the close.
    /// </summary>
    protected async Task CloseCardAsync()
    {
        await WaitUntilTheCardHasSettledAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Page.Locator(OpenCardSelector)).ToHaveCountAsync(0);
        await WaitUntilTheCardStepIsGoneAsync();
    }

    /// <summary>
    /// Waits until the history step a card adds while it is open is gone again. Closing a card removes that step with a history
    /// traversal that finishes a moment after the dialog closes, and a navigation or Back started before then races it: the
    /// browser aborts the new navigation or the Back lands on the card's own step.
    /// </summary>
    protected async Task WaitUntilTheCardStepIsGoneAsync()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (await Page.EvaluateAsync<bool>("window.history.state === null"))
            {
                return;
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("The card's history step was not removed.");
    }

    /// <summary>
    /// Waits until the view switch is offered: present in the filter panel on a desktop, or, below the desktop width, in the Filters
    /// sheet, while the owner has locations. The switch is hidden by the page while the owner has no location, wherever it lives.
    /// </summary>
    protected async Task ExpectViewSwitchAvailableAsync()
    {
        await Page.WaitForFunctionAsync("() => { const toggle = document.querySelector('.filter-view'); return toggle !== null && !toggle.hidden; }");
    }

    /// <summary>
    /// Picks a segment of the view switch, 0 for all together and 1 for by location. On a desktop the switch lives in the filter
    /// panel, so a folded panel is opened for the tap and folded again afterwards. Below the desktop width the switch lives in the
    /// Filters sheet, so the sheet is opened for the tap and closed again with its primary button.
    /// </summary>
    /// <param name="segment">The segment to tap.</param>
    protected async Task ChooseViewAsync(int segment)
    {
        if (await IsDockWidthAsync())
        {
            var folded = await DockStateAsync() == "rail";

            if (folded)
            {
                await OpenFilterPanelAsync();
            }

            await Page.Locator(".filter-view-segment").Nth(segment).ClickAsync();

            if (folded)
            {
                await FoldFilterPanelAsync();
            }

            return;
        }

        var opener = Page.Locator(".filter-bar .filter-open");
        var inSheet = await opener.IsVisibleAsync();

        if (inSheet)
        {
            await opener.ClickAsync();
            await Assertions.Expect(Page.Locator("dialog.filter-dialog[open]")).ToBeVisibleAsync();
        }

        await Page.Locator(".filter-view-segment").Nth(segment).ClickAsync();

        if (inSheet)
        {
            await Page.Locator(".filter-done").ClickAsync();
            await Assertions.Expect(Page.Locator("dialog.filter-dialog[open]")).ToHaveCountAsync(0);
        }
    }

    /// <summary>
    /// Opens the desktop filter panel with the rail's three-line button when the dock is folded, and waits until the panel is open
    /// beside or over the cabinet and visible. An open panel is left as it is.
    /// </summary>
    /// <exception cref="InvalidOperationException">The viewport is narrower than the desktop width, where there is no dock.</exception>
    protected async Task OpenFilterPanelAsync()
    {
        if (!await IsDockWidthAsync())
        {
            throw new InvalidOperationException("The filter panel only exists from the desktop width.");
        }

        if (await DockStateAsync() == "rail")
        {
            await Page.Locator(".filter-rail .filter-menu").ClickAsync();
        }

        await Page.WaitForFunctionAsync("() => ['push', 'over'].includes(document.documentElement.dataset.filterDock)");
        await Assertions.Expect(Page.Locator("#filter-panel")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Folds the desktop filter panel with its head's three-line button and waits until the dock is the rail again.
    /// </summary>
    protected async Task FoldFilterPanelAsync()
    {
        await Page.Locator("#filter-panel .filter-menu").ClickAsync();
        await Page.WaitForFunctionAsync("() => document.documentElement.dataset.filterDock === 'rail'");
    }

    /// <summary>Tells whether the viewport is at the desktop width, where the filters stand in the dock at the left edge.</summary>
    /// <returns>True from 64rem.</returns>
    protected Task<bool> IsDockWidthAsync() => Page.EvaluateAsync<bool>("() => window.matchMedia('(min-width: 64rem)').matches");

    /// <summary>Reads the state of the desktop filter dock from the root element.</summary>
    /// <returns>The rail, push or over state, or null while the page has no dock.</returns>
    protected Task<string?> DockStateAsync() => Page.EvaluateAsync<string?>("() => document.documentElement.dataset.filterDock ?? null");

    /// <summary>
    /// Makes the page's requests for the card data fail with a network error, the given number of times, and lets every later one
    /// through to the host. Call it before opening the cabinet page.
    /// </summary>
    /// <param name="failures">How many requests fail before the rest succeed; the default fails them all.</param>
    /// <returns>A function that tells how many card data requests the page has made so far.</returns>
    protected async Task<Func<int>> FailCardDataRequestsAsync(int failures = int.MaxValue)
    {
        var seen = 0;

        await Page.RouteAsync("**/cabinet/cards**", async route =>
        {
            if (Interlocked.Increment(ref seen) <= failures)
            {
                await route.AbortAsync();
            }
            else
            {
                await route.ContinueAsync();
            }
        });

        return () => Volatile.Read(ref seen);
    }

    /// <summary>
    /// Waits until the page has made the given number of card data requests and has had a moment to take in the answer to the last
    /// one, so what the page knows about the card data no longer changes.
    /// </summary>
    /// <param name="requestCount">Tells how many card data requests the page has made so far.</param>
    /// <param name="expected">How many requests the page makes in all.</param>
    protected async Task WaitForCardDataRequestsAsync(Func<int> requestCount, int expected)
    {
        ArgumentNullException.ThrowIfNull(requestCount);

        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (requestCount() < expected)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"The page made {requestCount()} of {expected} card data requests.");
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        await Task.Delay(300, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Opens a page of the running host and waits until the cabinet has drawn its first box or shown its own message.
    /// </summary>
    /// <param name="pathAndQuery">The path and query to open, such as <c>/?sample=65</c>.</param>
    /// <returns>The absolute address that was opened.</returns>
    protected Task<string> GotoCabinetAsync(string pathAndQuery) => GotoCabinetAsync(Page, pathAndQuery);

    /// <summary>
    /// Opens a page of the running host in the given page and waits until the cabinet has drawn its first box or shown its own message.
    /// </summary>
    /// <param name="page">The page to open it in, such as the fresh page of a repeated attempt.</param>
    /// <param name="pathAndQuery">The path and query to open, such as <c>/?sample=65</c>.</param>
    /// <returns>The absolute address that was opened.</returns>
    protected async Task<string> GotoCabinetAsync(IPage page, string pathAndQuery)
    {
        ArgumentNullException.ThrowIfNull(page);

        var address = $"http://127.0.0.1:{PublicPort}{pathAndQuery}";

        await page.GotoAsync(address);
        await page.WaitForSelectorAsync(CabinetReadySelector, new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });

        return address;
    }

    /// <summary>
    /// Opens a page of the running host on the owner port and waits until the owner page's main area exists.
    /// </summary>
    /// <param name="pathAndQuery">The path and query to open, such as <c>/owner/settings</c>.</param>
    /// <returns>The absolute address that was opened.</returns>
    protected async Task<string> GotoOwnerAsync(string pathAndQuery)
    {
        var address = $"http://127.0.0.1:{OwnerPort}{pathAndQuery}";

        await Page.GotoAsync(address);
        await Page.WaitForSelectorAsync("#owner-main", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });

        return address;
    }

    /// <summary>
    /// Saves a screenshot as <c>{name}.png</c> in the directory named by the <c>CABINET_SCREENSHOT_DIR</c> environment
    /// variable, and does nothing when the variable is not set. The shot covers the whole page, except while a dialog is open:
    /// a full-page capture resizes the viewport, which moves the open card and drops focus, so the visible window is saved instead.
    /// </summary>
    /// <param name="name">The file name without extension.</param>
    protected Task SaveScreenshotAsync(string name) => SaveScreenshotAsync(Page, name);

    /// <summary>
    /// Saves a screenshot of the given page the way <see cref="SaveScreenshotAsync(string)"/> does.
    /// </summary>
    /// <param name="page">The page to take the picture of, such as the fresh page of a repeated attempt.</param>
    /// <param name="name">The file name without extension.</param>
    protected async Task SaveScreenshotAsync(IPage page, string name)
    {
        ArgumentNullException.ThrowIfNull(page);

        var directory = Environment.GetEnvironmentVariable(ScreenshotDirectoryVariable);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var dialogOpen = await page.Locator("dialog[open]").CountAsync() > 0;
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(directory, $"{name}.png"), FullPage = !dialogOpen });
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
            _factory = null;
        }

        GC.SuppressFinalize(this);
    }

    private void RecordPage(IPage page)
    {
        page.Console += (_, message) => RecordConsoleMessage(message);
        page.PageError += (_, error) => RecordConsoleError(error);
        page.Request += (_, request) => RecordRequest(request);
    }

    /// <summary>
    /// Closes a page that stalled and makes a fresh page of the same browser context the active one, recorded like the first. A page
    /// that is stuck may not close at once, so the wait is bounded; the browser context ends it with the test in any case.
    /// </summary>
    /// <param name="stalled">The page to give up on.</param>
    private async Task GiveUpOnAsync(IPage stalled)
    {
        try
        {
            await stalled.CloseAsync(new PageCloseOptions { RunBeforeUnload = false }).WaitAsync(TimeSpan.FromMilliseconds(StalledPageCloseTimeoutMs), TestContext.Current.CancellationToken);
        }
        catch (Exception exception) when (exception is TimeoutException or PlaywrightException)
        {
            _ = exception;
        }

        var fresh = await stalled.Context.NewPageAsync();
        RecordPage(fresh);
        _replacement = fresh;
    }

    /// <summary>
    /// Asks the page to evaluate a trivial script and tells whether it answered within the probe budget. A page whose renderer is
    /// stuck never answers, and a page that is closed or crashed fails the call, so both count as not answering.
    /// </summary>
    /// <param name="page">The page to ask.</param>
    /// <returns>True when the page answered in time.</returns>
    private static async Task<bool> AnswersAsync(IPage page)
    {
        var probe = page.EvaluateAsync<int>("() => 1");

        try
        {
            await probe.WaitAsync(TimeSpan.FromMilliseconds(ResponsivenessProbeTimeoutMs), TestContext.Current.CancellationToken);

            return true;
        }
        catch (Exception exception) when (exception is TimeoutException or PlaywrightException)
        {
            _ = probe.ContinueWith(static ended => _ = ended.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

            return false;
        }
    }

    /// <summary>
    /// Writes a line naming the running test to the test output, so a stall the test rode out is visible in the run itself, and
    /// appends the same line to <c>stalled-pages.txt</c> in the screenshot directory when one is set.
    /// </summary>
    /// <param name="attempt">The attempt whose page stalled.</param>
    /// <param name="page">The page that stalled.</param>
    private static void RecordStalledPage(int attempt, IPage page)
    {
        var test = TestContext.Current.Test?.TestDisplayName ?? "a test that is not running";
        var line = $"{DateTimeOffset.UtcNow:O} attempt {attempt} stalled on {page.Url}: {test}";

        TestContext.Current.TestOutputHelper?.WriteLine($"Stalled page replaced: {line}");
        Console.Error.WriteLine($"Stalled page replaced: {line}");

        var directory = Environment.GetEnvironmentVariable(ScreenshotDirectoryVariable);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        lock (StallRecordLock)
        {
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, StallRecordFileName), line + Environment.NewLine);
        }
    }

    private void RecordConsoleMessage(IConsoleMessage message)
    {
        if (string.Equals(message.Type, "error", StringComparison.Ordinal))
        {
            RecordConsoleError(message.Text);
        }
    }

    private void RecordConsoleError(string text)
    {
        lock (_recordLock)
        {
            _consoleErrors.Add(text);
        }
    }

    private void RecordRequest(IRequest request)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var address) || address.Scheme is not ("http" or "https"))
        {
            return;
        }

        lock (_recordLock)
        {
            _requestHosts.Add(address.Authority);
        }
    }
}

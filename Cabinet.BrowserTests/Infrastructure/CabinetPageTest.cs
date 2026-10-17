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

    private readonly object _recordLock = new();
    private readonly List<string> _consoleErrors = [];
    private readonly HashSet<string> _requestHosts = new(StringComparer.Ordinal);
    private CabinetWebApplicationFactory? _factory;

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
        Page.Console += (_, message) => RecordConsoleMessage(message);
        Page.PageError += (_, error) => RecordConsoleError(error);
        Page.Request += (_, request) => RecordRequest(request);

        return factory;
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
    /// Waits until the view switch is offered: shown in the bar at a full-bar width, or, below it, present in the Filters sheet while
    /// the owner has locations. The switch is hidden by the page while the owner has no location, whichever bar is shown.
    /// </summary>
    protected async Task ExpectViewSwitchAvailableAsync()
    {
        await Page.WaitForFunctionAsync("() => { const toggle = document.querySelector('.filter-view'); return toggle !== null && !toggle.hidden; }");
    }

    /// <summary>
    /// Picks a segment of the view switch, 0 for all together and 1 for by location. Below the full bar the switch lives in the
    /// Filters sheet, so the sheet is opened for the tap and closed again with its primary button.
    /// </summary>
    /// <param name="segment">The segment to tap.</param>
    protected async Task ChooseViewAsync(int segment)
    {
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
    protected async Task<string> GotoCabinetAsync(string pathAndQuery)
    {
        var address = $"http://127.0.0.1:{PublicPort}{pathAndQuery}";

        await Page.GotoAsync(address);
        await Page.WaitForSelectorAsync(CabinetReadySelector, new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });

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
    protected async Task SaveScreenshotAsync(string name)
    {
        var directory = Environment.GetEnvironmentVariable(ScreenshotDirectoryVariable);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var dialogOpen = await Page.Locator("dialog[open]").CountAsync() > 0;
        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(directory, $"{name}.png"), FullPage = !dialogOpen });
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

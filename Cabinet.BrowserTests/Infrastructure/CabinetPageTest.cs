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

    /// <summary>
    /// Starts the host with the given settings and prepares the page to talk to it. Module scripts are only served with a
    /// JavaScript content type when the browser asks for no content encoding, because the test host does not run against
    /// published output, so the page context asks for the identity encoding.
    /// </summary>
    /// <param name="settings">Synthetic configuration values, such as the switch that enables the sample collections.</param>
    /// <param name="configureServices">Replaces services before the host starts, such as the source of the collection; null leaves them as they are.</param>
    protected async Task<CabinetWebApplicationFactory> StartAsync(
        IReadOnlyDictionary<string, string?> settings,
        Action<Microsoft.Extensions.DependencyInjection.IServiceCollection>? configureServices = null)
    {
        var factory = new CabinetWebApplicationFactory(settings, configureServices);
        _factory = factory;

        await Page.Context.SetExtraHTTPHeadersAsync(new Dictionary<string, string> { ["Accept-Encoding"] = "identity" });
        Page.Console += (_, message) => RecordConsoleMessage(message);
        Page.PageError += (_, error) => RecordConsoleError(error);
        Page.Request += (_, request) => RecordRequest(request);

        return factory;
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
    /// Saves a full-page screenshot as <c>{name}.png</c> in the directory named by the <c>CABINET_SCREENSHOT_DIR</c>
    /// environment variable, and does nothing when the variable is not set.
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
        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(directory, $"{name}.png"), FullPage = true });
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

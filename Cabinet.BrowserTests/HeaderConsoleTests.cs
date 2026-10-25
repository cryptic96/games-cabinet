using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in Chromium that the cabinet and an owner page load fully under the whole security-header set without a single
/// console message of any kind and without a content security policy violation. A permissions policy feature the browser
/// does not know shows up here as a console message, so the list in the header can never carry a name that only some
/// browsers understand. The browser's own request for a site icon is the one answer ignored, see <see cref="ConsoleWatch"/>.
/// </summary>
[Trait("Category", "Browser")]
public sealed class HeaderConsoleTests : CabinetPageTest
{
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    [Fact]
    public async Task The_public_cabinet_loads_without_a_console_message_or_a_policy_violation()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        var watch = await ConsoleWatch.AttachAsync(Page);

        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator("#cabinet .placement").First).ToBeVisibleAsync();

        watch.Messages.Should().BeEmpty();
        (await watch.ViolationsAsync(Page)).Should().BeEmpty();
    }

    [Fact]
    public async Task An_owner_page_loads_without_a_console_message_or_a_policy_violation()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(8));
        var watch = await ConsoleWatch.AttachAsync(Page);

        await GotoOwnerAsync("/owner/pictures");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator("#owner-main")).ToBeVisibleAsync();

        watch.Messages.Should().BeEmpty();
        (await watch.ViolationsAsync(Page)).Should().BeEmpty();
    }
}

/// <summary>
/// Records every console message and uncaught page error of a page, of any type, and the content security policy violations the
/// page reports. The site has no icon and no icon link by design, so a browser's own request for the icon answers 404 and
/// Chromium logs it; that one message, recognised by a location naming the icon's address, is ignored, because serving an
/// icon would be a visual change and an inline data icon would break the policy.
/// </summary>
internal sealed class ConsoleWatch
{
    private const string IconAddressEnd = "/favicon.ico";

    private const string ViolationRecorder = """
        (() => {
          window.__violations = [];
          document.addEventListener('securitypolicyviolation', (event) => {
            window.__violations.push(event.violatedDirective + ' ' + event.blockedURI);
          });
        })();
        """;

    private readonly object _lock = new();
    private readonly List<string> _messages = [];

    private ConsoleWatch()
    {
    }

    /// <summary>The console messages and page errors seen so far, each as its type and text.</summary>
    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (_lock)
            {
                return [.. _messages];
            }
        }
    }

    /// <summary>Starts recording on a page that has not navigated yet.</summary>
    /// <param name="page">The page to watch.</param>
    public static async Task<ConsoleWatch> AttachAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var watch = new ConsoleWatch();
        await page.AddInitScriptAsync(ViolationRecorder);
        page.Console += (_, message) => watch.Record(message);
        page.PageError += (_, error) => watch.Add($"pageerror: {error}");

        return watch;
    }

    /// <summary>Reads the policy violations the current document reported.</summary>
    /// <param name="page">The watched page, after it has loaded.</param>
    public async Task<IReadOnlyList<string>> ViolationsAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        return await page.EvaluateAsync<string[]>("() => window.__violations ?? []");
    }

    private void Record(IConsoleMessage message)
    {
        if (message.Location is not null && message.Location.Contains(IconAddressEnd, StringComparison.Ordinal))
        {
            return;
        }

        Add($"{message.Type}: {message.Text}");
    }

    private void Add(string text)
    {
        lock (_lock)
        {
            _messages.Add(text);
        }
    }
}

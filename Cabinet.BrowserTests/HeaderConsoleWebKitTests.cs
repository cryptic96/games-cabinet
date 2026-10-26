using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Hosting;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in WebKit, the engine behind Safari, that the cabinet and an owner page load fully under the whole security-header set
/// without a single console message and without a content security policy violation. The browser is launched explicitly rather
/// than chosen by the runner, and the test only runs where <c>CABINET_WEBKIT=1</c> is set, because the engine is installed by a
/// separate step. Two answers are ignored: the browser's own request for a site icon, see <see cref="ConsoleWatch"/>, and the
/// notice newer WebKit builds raise for each <c>select</c> element, see <see cref="EngineSelectNotice"/>. The second test shows
/// that the allowance for the select notice does not hide an inline style the page itself carries.
/// </summary>
[Trait("Category", "Browser")]
[Trait("Engine", "WebKit")]
public sealed class HeaderConsoleWebKitTests
{
    private const string EnableVariable = "CABINET_WEBKIT";
    private const string SkipReason = "WebKit runs only where CABINET_WEBKIT=1";

    [Fact]
    public async Task The_public_cabinet_and_an_owner_page_load_without_a_console_message_or_a_policy_violation()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable(EnableVariable) == "1", SkipReason);

        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(8)),
            clock,
            new Dictionary<string, string?> { ["Prototype:Enabled"] = "true" });
        using (var client = factory.CreatePublicClient())
        {
            await SyncRounds.PressAndWait(client, clock, advance: false);
        }

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Webkit.LaunchAsync();
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
            ExtraHTTPHeaders = new Dictionary<string, string> { ["Accept-Encoding"] = "identity" },
        });
        var selectNotice = await EngineSelectNotice.MeasureAsync(context);

        var publicPage = await context.NewPageAsync();
        var publicWatch = await ConsoleWatch.AttachAsync(publicPage);
        await publicPage.GotoAsync($"http://127.0.0.1:{factory.PublicPort}/?sample=65");
        await Assertions.Expect(publicPage.Locator("#cabinet .placement").First).ToBeVisibleAsync();
        await publicPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var publicSelects = await publicPage.Locator("select").CountAsync();

        var ownerPage = await context.NewPageAsync();
        var ownerWatch = await ConsoleWatch.AttachAsync(ownerPage);
        await ownerPage.GotoAsync($"http://127.0.0.1:{factory.OwnerPort}/owner/pictures");
        await Assertions.Expect(ownerPage.Locator("#owner-main")).ToBeVisibleAsync();
        await ownerPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var ownerSelects = await ownerPage.Locator("select").CountAsync();

        selectNotice.WithoutNotices(publicWatch.Messages, publicSelects).Should().BeEmpty();
        selectNotice.WithoutViolations(await publicWatch.ViolationsAsync(publicPage), publicSelects).Should().BeEmpty();
        selectNotice.WithoutNotices(ownerWatch.Messages, ownerSelects).Should().BeEmpty();
        selectNotice.WithoutViolations(await ownerWatch.ViolationsAsync(ownerPage), ownerSelects).Should().BeEmpty();
    }

    [Fact]
    public async Task An_inline_style_beside_a_select_is_still_reported()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable(EnableVariable) == "1", SkipReason);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Webkit.LaunchAsync();
        var context = await browser.NewContextAsync();
        var selectNotice = await EngineSelectNotice.MeasureAsync(context);

        var (page, watch) = await EngineSelectNotice.OpenUnderSitePolicyAsync(
            context,
            "<select><option>a</option></select><div style=\"color: red\">styled</div>");

        selectNotice.WithoutNotices(watch.Messages, selects: 1).Should().ContainSingle();
        selectNotice.WithoutViolations(await watch.ViolationsAsync(page), selects: 1).Should().ContainSingle();
    }
}

/// <summary>
/// What WebKit itself reports for each <c>select</c> element under the site's content security policy. Newer WebKit builds
/// (first seen in 27.2) apply an inline style of their own to the internals of a select and hold it against the policy, so
/// every select makes the page log one console error and fire one <c>style-src-attr</c> violation, even though the page has no
/// inline style, the select carries no style attribute and it renders exactly as it does without the policy. Older builds
/// raise nothing, and Chromium never does. The policy stays as strict as it is, because allowing inline styles would open the
/// door the policy exists to close.
/// The count is measured on a page that holds nothing but one select, served under the real policy, instead of being taken
/// for granted. The allowance is therefore exactly zero in an engine that does not behave this way, and it never covers more
/// than that per-select count for the selects in the page, so an inline style the page itself carries still counts against it.
/// </summary>
internal sealed class EngineSelectNotice
{
    private const string Message = "error: Refused to apply a stylesheet because its hash, its nonce, or 'unsafe-inline' appears in neither the style-src directive nor the default-src directive of the Content Security Policy.";
    private const string Violation = "style-src-attr inline";
    private const string ProbeOrigin = "http://example.org/";

    private readonly int _messagesPerSelect;
    private readonly int _violationsPerSelect;

    private EngineSelectNotice(int messagesPerSelect, int violationsPerSelect)
    {
        _messagesPerSelect = messagesPerSelect;
        _violationsPerSelect = violationsPerSelect;
    }

    /// <summary>Loads a page of nothing but one select under the site's policy and counts the notices the engine raises for it.</summary>
    /// <param name="context">The browser context to open the probe page in.</param>
    public static async Task<EngineSelectNotice> MeasureAsync(IBrowserContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (page, watch) = await OpenUnderSitePolicyAsync(context, "<select><option>a</option></select>");
        var violations = await watch.ViolationsAsync(page);
        await page.CloseAsync();

        return new EngineSelectNotice(
            watch.Messages.Count(message => message == Message),
            violations.Count(violation => violation == Violation));
    }

    /// <summary>
    /// Opens a page whose body is the given markup, answered from the test itself with the site's content security policy and no
    /// network request, and starts recording its console and policy violations before it loads.
    /// </summary>
    /// <param name="context">The browser context to open the page in.</param>
    /// <param name="bodyMarkup">The markup placed in the body of the page.</param>
    public static async Task<(IPage Page, ConsoleWatch Watch)> OpenUnderSitePolicyAsync(IBrowserContext context, string bodyMarkup)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(bodyMarkup);

        var page = await context.NewPageAsync();
        var watch = await ConsoleWatch.AttachAsync(page);
        await page.RouteAsync(
            $"{ProbeOrigin}**",
            route => route.FulfillAsync(new RouteFulfillOptions
            {
                Status = route.Request.Url == ProbeOrigin ? 200 : 404,
                ContentType = "text/html; charset=utf-8",
                Headers = new Dictionary<string, string> { ["Content-Security-Policy"] = SecurityHeaders.ContentSecurityPolicy },
                Body = $"<!doctype html><html><head><meta charset=\"utf-8\"></head><body>{bodyMarkup}</body></html>",
            }));
        await page.GotoAsync(ProbeOrigin);

        return (page, watch);
    }

    /// <summary>Drops from the reported console messages the notices the engine raises for the page's selects, and no others.</summary>
    /// <param name="reported">The console messages a page reported.</param>
    /// <param name="selects">How many select elements the page holds.</param>
    public IReadOnlyList<string> WithoutNotices(IReadOnlyList<string> reported, int selects)
    {
        return Without(reported, Message, _messagesPerSelect * selects);
    }

    /// <summary>Drops from the reported policy violations the ones the engine raises for the page's selects, and no others.</summary>
    /// <param name="reported">The policy violations a page reported.</param>
    /// <param name="selects">How many select elements the page holds.</param>
    public IReadOnlyList<string> WithoutViolations(IReadOnlyList<string> reported, int selects)
    {
        return Without(reported, Violation, _violationsPerSelect * selects);
    }

    private static List<string> Without(IReadOnlyList<string> reported, string known, int allowance)
    {
        var remaining = new List<string>();
        foreach (var entry in reported)
        {
            if (entry == known && allowance > 0)
            {
                allowance--;
                continue;
            }

            remaining.Add(entry);
        }

        return remaining;
    }
}

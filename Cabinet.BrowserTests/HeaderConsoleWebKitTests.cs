using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in WebKit, the engine behind Safari, that the cabinet and an owner page load fully under the whole security-header set
/// without a single console message and without a content security policy violation. The browser is launched explicitly rather
/// than chosen by the runner, and the test only runs where <c>CABINET_WEBKIT=1</c> is set, because the engine is installed by a
/// separate step. The one answer ignored is the browser's own request for a site icon, see <see cref="ConsoleWatch"/>.
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

        var publicPage = await context.NewPageAsync();
        var publicWatch = await ConsoleWatch.AttachAsync(publicPage);
        await publicPage.GotoAsync($"http://127.0.0.1:{factory.PublicPort}/?sample=65");
        await Assertions.Expect(publicPage.Locator("#cabinet .placement").First).ToBeVisibleAsync();
        await publicPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var ownerPage = await context.NewPageAsync();
        var ownerWatch = await ConsoleWatch.AttachAsync(ownerPage);
        await ownerPage.GotoAsync($"http://127.0.0.1:{factory.OwnerPort}/owner/pictures");
        await Assertions.Expect(ownerPage.Locator("#owner-main")).ToBeVisibleAsync();
        await ownerPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        publicWatch.Messages.Should().BeEmpty();
        (await publicWatch.ViolationsAsync(publicPage)).Should().BeEmpty();
        ownerWatch.Messages.Should().BeEmpty();
        (await ownerWatch.ViolationsAsync(ownerPage)).Should().BeEmpty();
    }
}

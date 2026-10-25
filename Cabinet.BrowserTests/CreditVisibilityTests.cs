using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves the linked BoardGameGeek credit is on screen in every state the cabinet page can be in: drawn, still filling before
/// the first sync, an empty collection and a layout that failed to load, at a desktop and at a phone width. The credit must be
/// visible, point at BoardGameGeek and sit wholly inside the page width once scrolled to.
/// </summary>
[Trait("Category", "Browser")]
public sealed class CreditVisibilityTests : CabinetPageTest
{
    private const string CreditAddress = "https://boardgamegeek.com";

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(390, 800)]
    public async Task The_credit_shows_on_a_drawn_cabinet(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);

        await GotoCabinetAsync("/?sample=65");

        await AssertCreditAsync();
    }

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(390, 800)]
    public async Task The_credit_shows_on_the_filling_message_before_the_first_sync(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(new Dictionary<string, string?>());

        await GotoCabinetAsync("/");

        await Expect(Page.Locator(".cabinet-filling")).ToBeVisibleAsync();
        await AssertCreditAsync();
    }

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(390, 800)]
    public async Task The_credit_shows_on_an_empty_collection(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(0));

        await Page.GotoAsync($"http://127.0.0.1:{PublicPort}/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.Locator(".cabinet-room")).ToBeVisibleAsync();
        await Expect(Page.Locator(".cabinet-filling")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#cabinet .placement")).ToHaveCountAsync(0);
        await AssertCreditAsync();
    }

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(390, 800)]
    public async Task The_credit_shows_after_a_failed_layout_load(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await Page.RouteAsync("**/cabinet/layout**", route => route.FulfillAsync(new RouteFulfillOptions { Status = 500 }));

        await GotoCabinetAsync("/");

        await Expect(Page.Locator(".cabinet-retry")).ToBeVisibleAsync();
        await AssertCreditAsync();
    }

    private async Task AssertCreditAsync()
    {
        var credit = Page.Locator("footer a.bgg-credit");

        await credit.ScrollIntoViewIfNeededAsync();
        await Expect(credit).ToBeVisibleAsync();
        await Expect(credit).ToHaveAttributeAsync("href", CreditAddress);
        await Expect(credit.Locator("img")).ToBeVisibleAsync();

        var box = await credit.BoundingBoxAsync();
        var pageWidth = await Page.EvaluateAsync<double>("() => document.documentElement.clientWidth");

        box.Should().NotBeNull();
        box!.Width.Should().BeGreaterThan(0);
        box.X.Should().BeGreaterThanOrEqualTo(0, "the credit is not clipped at the left edge");
        ((double)box.X + box.Width).Should().BeLessThanOrEqualTo(pageWidth + 1, "the credit is not clipped at the right edge");
    }
}

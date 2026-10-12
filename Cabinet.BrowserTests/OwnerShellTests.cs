using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that every owner page is English whatever the language cookie says, carries the plate, the bar,
/// the status region and the credited footer in the fixed order, keeps its pointers phone-sized, and makes no request that
/// leaves the host or breaks the content security policy.
/// </summary>
[Trait("Category", "Browser")]
public sealed class OwnerShellTests : CabinetPageTest
{
    private static readonly Dictionary<string, string?> NoSettings = [];

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(390, 844)]
    public async Task The_settings_page_wears_the_full_English_owner_shell_even_with_a_Dutch_language_cookie(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(NoSettings);
        await Page.Context.AddCookiesAsync([new Cookie { Name = "lang", Value = "nl", Url = $"http://127.0.0.1:{OwnerPort}" }]);

        var address = await GotoOwnerAsync("/owner/settings");
        var response = await Page.APIRequest.GetAsync(address);

        response.Headers["content-language"].Should().Be("en");
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "en");
        await Expect(Page).ToHaveTitleAsync("Settings - Owner tools");
        await Expect(Page.Locator("header .owner-plate")).ToHaveTextAsync("Owner tools");
        await Expect(Page.Locator(".lang-toggle")).ToHaveCountAsync(0);

        var items = Page.Locator("nav.owner-bar[aria-label=\"Owner tools\"] a");
        await Expect(items).ToHaveTextAsync(["Pictures", "Locations", "Settings", "Cabinet"]);
        var hrefs = await items.EvaluateAllAsync<string[]>("links => links.map(link => link.getAttribute('href'))");
        hrefs.Should().Equal("/owner/pictures", "/owner/locations", "/owner/settings", "/");
        await Expect(items.Nth(2)).ToHaveAttributeAsync("aria-current", "page");
        await Expect(Page.Locator("nav.owner-bar a[aria-current]")).ToHaveCountAsync(1);

        await Expect(Page.Locator("#owner-status")).ToHaveAttributeAsync("role", "status");
        await Expect(Page.Locator("meta[name=\"request-verification-token\"]")).ToHaveCountAsync(1);
        await Expect(Page.Locator("footer .bgg-credit")).ToHaveAttributeAsync("href", "https://boardgamegeek.com");
        await Expect(Page.Locator("footer .version")).ToBeVisibleAsync();
        await Expect(Page.Locator("#owner-main")).ToHaveAttributeAsync("data-icons", new Regex(@"/img/icons\.svg"));

        await SaveScreenshotAsync($"owner-shell-{width}");
        ConsoleErrors.Should().BeEmpty();
        RequestHosts.Should().OnlyContain(host => host == $"127.0.0.1:{OwnerPort}");
    }

    [Fact]
    public async Task The_first_Tab_lands_on_the_skip_link_that_leads_to_the_page_content()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(NoSettings);
        await GotoOwnerAsync("/owner/settings");

        await Page.Keyboard.PressAsync("Tab");

        var skip = Page.Locator("a.skip-link");
        await Expect(skip).ToBeFocusedAsync();
        await Expect(skip).ToHaveTextAsync("Skip to the page content");
        await Expect(skip).ToHaveAttributeAsync("href", "#owner-main");
    }

    [Fact]
    public async Task The_shell_is_in_the_fixed_order_and_the_plate_is_not_in_the_tab_order()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(NoSettings);
        await GotoOwnerAsync("/owner/settings");

        var order = await Page.EvaluateAsync<string[]>(
            "() => ['a.skip-link', 'header h1', 'header .owner-plate', 'nav.owner-bar', '#owner-main', 'footer']" +
            ".map(selector => document.querySelector(selector))" +
            ".map((element, index, all) => index === 0 || (all[index - 1].compareDocumentPosition(element) & Node.DOCUMENT_POSITION_FOLLOWING) ? 'ok' : 'out of order')");
        var focusable = await Page.Locator(".owner-plate").EvaluateAsync<bool>("element => element.tabIndex >= 0");

        order.Should().OnlyContain(step => step == "ok");
        focusable.Should().BeFalse();
    }

    [Fact]
    public async Task The_public_port_has_no_settings_page_and_the_public_page_keeps_its_footer_and_credit()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(NoSettings);

        var response = await Page.GotoAsync($"http://127.0.0.1:{PublicPort}/owner/settings");
        response!.Status.Should().Be(404);

        await GotoCabinetAsync("/");
        await Expect(Page.Locator("footer .version")).ToBeVisibleAsync();
        await Expect(Page.Locator("footer .bgg-credit")).ToHaveAttributeAsync("href", "https://boardgamegeek.com");
        await Expect(Page.Locator(".owner-plate")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Every_bar_item_is_a_phone_sized_target_and_the_bar_wraps_at_320_pixels_without_sideways_scrolling()
    {
        await Page.SetViewportSizeAsync(320, 640);
        await StartAsync(NoSettings);
        await GotoOwnerAsync("/owner/settings");

        var boxes = await Page.Locator("nav.owner-bar a").EvaluateAllAsync<double[][]>(
            "links => links.map(link => { const box = link.getBoundingClientRect(); return [box.width, box.height, box.top]; })");
        var overflow = await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
        var plateTop = await Page.Locator(".owner-plate").EvaluateAsync<double>("element => element.getBoundingClientRect().top");
        var titleBottom = await Page.Locator("header h1").EvaluateAsync<double>("element => element.getBoundingClientRect().bottom");

        boxes.Should().OnlyContain(box => box[0] >= 44 && box[1] >= 44);
        boxes.Select(box => box[2]).Distinct().Count().Should().BeGreaterThan(1, "four items cannot share one line at 320 pixels");
        overflow.Should().BeFalse();
        plateTop.Should().BeLessThan(titleBottom, "the plate stays on the title's row");
    }
}

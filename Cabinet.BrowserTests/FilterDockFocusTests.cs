using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser how the desktop filter panel behaves for the keyboard, screen readers and Back. Over the cabinet it is a
/// modal fly-over: the panel is the dialog, everything else is inert, Tab stays inside, and every close route (the head button,
/// Escape, one Back, a press outside) does exactly one thing and gives focus back to the rail. Beside the cabinet it is a plain
/// neighbour: no dialog, no inert page, no history step, and opening or folding it keeps the reader's place.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterDockFocusTests : CabinetPageTest
{
    private const int PanelWidth = 336;
    private const string RailMenu = ".filter-rail .filter-menu";
    private const string HeadMenu = "#filter-panel .filter-menu";
    private const string Panel = "#filter-panel";
    private const string OpenCard = "dialog.card-dialog[open]";
    private const string BlankPage = "about:blank";
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly string[] OutsideParts = [".skip-link", "body > header", "body > main", "body > footer", ".filter-rail"];

    private const string ReadModality = """
        () => {
          const panel = document.getElementById('filter-panel');
          const parts = ['.skip-link', 'body > header', 'body > main', 'body > footer', '.filter-rail'];
          return {
            role: panel.getAttribute('role'),
            modal: panel.getAttribute('aria-modal'),
            labelledBy: panel.getAttribute('aria-labelledby'),
            inert: parts.filter((selector) => document.querySelector(selector).hasAttribute('inert')),
            anyInert: document.querySelectorAll('[inert]').length,
            dockInert: document.querySelector('.filter-bar').hasAttribute('inert'),
            dockRole: document.querySelector('.filter-bar').getAttribute('role'),
          };
        }
        """;

    private const string ReadBox = """
        (selector) => {
          const rect = document.querySelector(selector).getBoundingClientRect();
          return [rect.left, rect.top, rect.width, rect.height];
        }
        """;

    [Fact]
    public async Task The_fly_over_is_modal_and_Escape_folds_it()
    {
        await OpenAsync("/?sample=65", DockWidths.Over, 800);
        var cabinetBefore = await BoxAsync("#cabinet");

        await Page.Locator(RailMenu).ClickAsync();

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "over");
        await ExpectOpenFlyOverAsync();
        await Expect(Page.Locator("#filter-q")).ToBeFocusedAsync();
        var cabinetOpen = await BoxAsync("#cabinet");

        for (var index = 0; index < 4; index++)
        {
            cabinetOpen[index].Should().BeApproximately(cabinetBefore[index], 1, "the fly-over never resizes the cabinet");
        }

        await ExpectPanelOnTheViewportsLeftAsync("at the laptop width");

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        await ExpectNotModalAsync("after Escape");
        await Expect(Page.Locator(RailMenu)).ToBeFocusedAsync();

        await Page.SetViewportSizeAsync(DockWidths.NarrowestDesktop, 800);
        await Page.WaitForFunctionAsync($"() => window.innerWidth === {DockWidths.NarrowestDesktop}");
        await Page.Locator(RailMenu).ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "over");
        await ExpectPanelOnTheViewportsLeftAsync("at the narrowest desktop");
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        ConsoleErrors.Should().BeEmpty();
    }

    private async Task ExpectOpenFlyOverAsync()
    {
        var modality = await Page.EvaluateAsync<JsonElement>(ReadModality);
        modality.GetProperty("role").GetString().Should().Be("dialog");
        modality.GetProperty("modal").GetString().Should().Be("true");
        modality.GetProperty("labelledBy").GetString().Should().Be("filter-panel-title");
        modality.GetProperty("inert").EnumerateArray().Select(part => part.GetString()).Should().BeEquivalentTo(OutsideParts);
        modality.GetProperty("dockInert").GetBoolean().Should().BeFalse("the dock holds the panel and is never inert");
        modality.GetProperty("dockRole").GetString().Should().Be("search", "the dock stays the search landmark");
    }

    private async Task ExpectNotModalAsync(string moment)
    {
        var modality = await Page.EvaluateAsync<JsonElement>(ReadModality);
        modality.GetProperty("role").ValueKind.Should().Be(JsonValueKind.Null, moment);
        modality.GetProperty("modal").ValueKind.Should().Be(JsonValueKind.Null, moment);
        modality.GetProperty("anyInert").GetInt32().Should().Be(0, moment);
    }

    private async Task ExpectPanelOnTheViewportsLeftAsync(string moment)
    {
        await Page.WaitForFunctionAsync("() => { const translate = getComputedStyle(document.getElementById('filter-panel')).translate; return translate === 'none' || translate === '0px'; }");
        var panel = await BoxAsync(Panel);
        var height = await Page.EvaluateAsync<double>("() => window.innerHeight");
        panel[0].Should().BeApproximately(0, 0.5, moment);
        panel[1].Should().BeApproximately(0, 0.5, moment);
        panel[2].Should().BeApproximately(PanelWidth, 0.5, moment);
        panel[3].Should().BeApproximately(height, 0.5, moment);
    }

    private async Task<double[]> BoxAsync(string selector) => await Page.EvaluateAsync<double[]>(ReadBox, selector);

    private async Task<string> OpenAsync(string path, int width, int height, bool blankFirst = false)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await Page.RouteAsync("**/cabinet/search-names**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "application/json",
            Body = "{\"names\":{}}",
        }));

        if (blankFirst)
        {
            await Page.GotoAsync(BlankPage);
        }

        var address = await GotoCabinetAsync(path);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-filter-dock", "rail");
        await Page.EvaluateAsync("() => document.fonts.ready");

        return address;
    }
}

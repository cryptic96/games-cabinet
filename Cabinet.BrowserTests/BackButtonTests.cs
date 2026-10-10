using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that one Back closes an open card, that every other way of closing leaves no dead history step behind,
/// that the address never changes, and that a press inside the card dragged outside it does not close it.
/// </summary>
[Trait("Category", "Browser")]
public sealed class BackButtonTests : CabinetPageTest
{
    private const string OpenCard = "dialog.card-dialog[open]";
    private const string CoverWithoutSecondLine = ".placement[data-kind=\"cover\"]:not(:has(.placement-sub))";
    private const string BlankPage = "about:blank";
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    [Fact]
    public async Task One_Back_closes_the_card_without_changing_the_address_and_a_second_Back_leaves_the_site()
    {
        var address = await OpenCardAfterBlankPageAsync();

        await Page.GoBackAsync();

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        Page.Url.Should().Be(address);

        await Page.GoBackAsync();

        Page.Url.Should().Be(BlankPage);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Escape_leaves_no_dead_step_so_one_Back_leaves_the_site()
    {
        var address = await OpenCardAfterBlankPageAsync();

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await WaitUntilTheCardStepIsGoneAsync();
        Page.Url.Should().Be(address);

        await Page.GoBackAsync();

        Page.Url.Should().Be(BlankPage);
    }

    [Fact]
    public async Task The_close_button_leaves_no_dead_step_so_one_Back_leaves_the_site()
    {
        var address = await OpenCardAfterBlankPageAsync();

        await Page.Locator(".card-close").ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await WaitUntilTheCardStepIsGoneAsync();
        Page.Url.Should().Be(address);

        await Page.GoBackAsync();

        Page.Url.Should().Be(BlankPage);
    }

    [Fact]
    public async Task A_click_on_the_frame_outside_the_card_closes_it_and_leaves_no_dead_step()
    {
        var address = await OpenCardAfterBlankPageAsync();

        await Page.Mouse.ClickAsync(5, 5);
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await WaitUntilTheCardStepIsGoneAsync();
        Page.Url.Should().Be(address);

        await Page.GoBackAsync();

        Page.Url.Should().Be(BlankPage);
    }

    [Fact]
    public async Task A_press_inside_the_card_released_on_the_frame_leaves_the_card_open()
    {
        await OpenCardAfterBlankPageAsync();
        var title = (await Page.Locator("#card-title").BoundingBoxAsync())!;

        await Page.Mouse.MoveAsync(title.X + 4, title.Y + (title.Height / 2));
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(5, 5, new MouseMoveOptions { Steps = 5 });
        await Page.Mouse.UpAsync();

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task A_reload_with_a_card_open_shows_the_page_without_a_card_and_clears_the_card_step()
    {
        var address = await OpenCardAfterBlankPageAsync();

        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync("#cabinet .placement");

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        Page.Url.Should().Be(address);
        (await Page.EvaluateAsync<bool>("window.history.state === null")).Should().BeTrue();

        await Page.GoBackAsync();
        await Page.WaitForSelectorAsync("#cabinet .placement");

        Page.Url.Should().Be(address);
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);

        await Page.GoBackAsync();

        Page.Url.Should().Be(BlankPage);
    }

    [Fact]
    public async Task Opening_a_card_again_right_after_closing_it_still_takes_one_step_only()
    {
        var address = await OpenCardAfterBlankPageAsync();

        await Page.Keyboard.PressAsync("Escape");
        await WaitUntilTheCardStepIsGoneAsync();
        await Page.Locator(CoverWithoutSecondLine).First.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(1);
        await Page.GoBackAsync();
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        Page.Url.Should().Be(address);

        await Page.GoBackAsync();

        Page.Url.Should().Be(BlankPage);
    }

    private async Task WaitUntilTheCardStepIsGoneAsync()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (await Page.EvaluateAsync<bool>("window.history.state === null"))
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("The card's history step was not removed.");
    }

    private async Task<string> OpenCardAfterBlankPageAsync()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        await Page.GotoAsync(BlankPage);
        var address = await GotoCabinetAsync("/?sample=65");

        await Page.Locator(CoverWithoutSecondLine).First.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        Page.Url.Should().Be(address);

        return address;
    }
}

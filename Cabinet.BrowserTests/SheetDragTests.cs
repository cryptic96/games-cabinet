using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that on a phone the card sheet closes with a downward drag from its grip strip or header and springs back
/// from a short one, that reading and scrolling its content never closes it, and that a viewport change across the phone breakpoint
/// closes an open card with the plain fade before the cabinet is drawn again.
/// </summary>
[Trait("Category", "Browser")]
public sealed partial class SheetDragTests : CabinetPageTest
{
    private const string OpenCard = "dialog.card-dialog[open]";
    private const string CoverBox = ".placement[data-kind=\"cover\"]";
    private const string BlankPage = "about:blank";
    private const int PhoneWidth = 390;
    private const int PhoneHeight = 800;
    private const int SpringWaitMilliseconds = 400;
    private const string TransitionSpy = """
        window.__pullSpy = { calls: 0 };
        (() => {
          const original = document.startViewTransition.bind(document);
          document.startViewTransition = (callback) => {
            window.__pullSpy.calls += 1;
            return original(callback);
          };
        })();
        """;
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    /// <inheritdoc />
    public override BrowserNewContextOptions ContextOptions() => new()
    {
        ViewportSize = new ViewportSize { Width = PhoneWidth, Height = PhoneHeight },
        HasTouch = true,
    };

    [Fact]
    public async Task A_long_drag_from_the_grip_closes_the_card_returns_focus_to_the_box_and_leaves_no_dead_step()
    {
        var address = await OpenCardAsync();
        var card = await BoxOfAsync(".card");
        var grip = await BoxOfAsync(".card-grip");

        await Page.Mouse.MoveAsync(grip.X + (grip.Width / 2), grip.Y + (grip.Height / 2));
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(grip.X + (grip.Width / 2), grip.Y + (grip.Height / 2) + (card.Height * 0.4f), new MouseMoveOptions { Steps = 10 });
        await Expect(Page.Locator(".card[data-moving]")).ToHaveCountAsync(1);
        await SaveScreenshotAsync("sheet-mid-drag");
        await Page.Mouse.UpAsync();

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await Expect(Page.Locator(".placement:focus")).ToHaveCountAsync(1);
        await WaitUntilTheCardStepIsGoneAsync();
        Page.Url.Should().Be(address);

        await Page.GoBackAsync();

        Page.Url.Should().Be(BlankPage);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_short_drag_springs_back_and_leaves_the_card_open()
    {
        await OpenCardAsync();
        var card = await BoxOfAsync(".card");
        var grip = await BoxOfAsync(".card-grip");
        var x = grip.X + (grip.Width / 2);
        var y = grip.Y + (grip.Height / 2);

        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(x, y + (card.Height * 0.1f), new MouseMoveOptions { Steps = 6 });
        await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        (await Page.Locator(".card").EvaluateAsync<string>("card => getComputedStyle(card).translate")).Should().NotMatchRegex(AtRest());
        await Page.Mouse.UpAsync();

        await Expect(Page.Locator(".card")).ToHaveCSSAsync("translate", AtRest(), new LocatorAssertionsToHaveCSSOptions { Timeout = SpringWaitMilliseconds });
        await Expect(Page.Locator(".card[data-moving]")).ToHaveCountAsync(0);
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task A_fast_flick_from_the_header_closes_the_card_even_though_it_is_short()
    {
        await OpenCardAsync();
        var head = await BoxOfAsync(".card-head");
        var x = head.X + 6;
        var y = head.Y + (head.Height / 2);

        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(x, y + 80, new MouseMoveOptions { Steps = 4 });
        await Page.Mouse.UpAsync();

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Scrolling_the_content_or_dragging_inside_it_never_closes_the_sheet()
    {
        await Page.SetViewportSizeAsync(PhoneWidth, 420);
        await OpenCardAsync();
        var ruled = await BoxOfAsync(".card-ruled");
        var x = ruled.X + (ruled.Width / 2);
        var y = ruled.Y + 40;

        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.WheelAsync(0, 200);
        await Expect(Page.Locator(".card")).Not.ToHaveJSPropertyAsync("scrollTop", 0);

        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(x, y + 300, new MouseMoveOptions { Steps = 8 });
        await Page.Mouse.UpAsync();

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(1);
        await Expect(Page.Locator(".card")).ToHaveCSSAsync("translate", AtRest());
        await Expect(Page.Locator(".card[data-moving]")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task The_close_button_and_links_never_start_a_drag()
    {
        await OpenCardAsync();
        var close = await BoxOfAsync(".card-close");
        var x = close.X + (close.Width / 2);
        var y = close.Y + (close.Height / 2);

        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(x, y + 200, new MouseMoveOptions { Steps = 6 });
        await Expect(Page.Locator(".card[data-moving]")).ToHaveCountAsync(0);
        await Page.Mouse.UpAsync();

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task Scrolling_over_the_dimmed_strip_does_not_move_the_page_behind_the_card()
    {
        await OpenCardAsync();
        var before = await Page.EvaluateAsync<double>("window.scrollY");

        await Page.Mouse.MoveAsync(PhoneWidth / 2, 12);
        await Page.Mouse.WheelAsync(0, 300);
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);

        (await Page.EvaluateAsync<double>("window.scrollY")).Should().Be(before);
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task Crossing_the_phone_breakpoint_with_a_card_open_closes_it_with_the_plain_fade_and_draws_the_phone_cabinet()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        var address = await OpenCardAsync();
        (await TransitionCallsAsync()).Should().Be(1);
        var layoutRequests = new List<string>();
        Page.Request += (_, request) =>
        {
            if (request.Url.Contains("/cabinet/layout", StringComparison.Ordinal))
            {
                lock (layoutRequests)
                {
                    layoutRequests.Add(request.Url);
                }
            }
        };

        await Page.SetViewportSizeAsync(PhoneWidth, PhoneHeight);

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await Expect(Page.Locator("#cabinet .placement").First).ToBeVisibleAsync();
        lock (layoutRequests)
        {
            layoutRequests.Should().ContainSingle().Which.Should().Contain("profile=phone");
        }

        (await TransitionCallsAsync()).Should().Be(1);
        await WaitUntilTheCardStepIsGoneAsync();
        Page.Url.Should().Be(address);

        await Page.GoBackAsync();

        Page.Url.Should().Be(BlankPage);
        ConsoleErrors.Should().BeEmpty();
    }

    [GeneratedRegex("^(none|0px( 0px)?)$")]
    private static partial Regex AtRest();

    private async Task<string> OpenCardAsync()
    {
        await StartAsync(PrototypeOn);
        await Page.AddInitScriptAsync(TransitionSpy);
        await Page.GotoAsync(BlankPage);
        var address = await GotoCabinetAsync("/?sample=65");

        await Page.Locator(CoverBox).First.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator("html[data-pull-kind]")).ToHaveCountAsync(0);

        return address;
    }

    private async Task<LocatorBoundingBoxResult> BoxOfAsync(string selector) =>
        (await Page.Locator(selector).First.BoundingBoxAsync())!;

    private Task<int> TransitionCallsAsync() => Page.EvaluateAsync<int>("window.__pullSpy.calls");

    private async Task WaitUntilTheCardStepIsGoneAsync()
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
}

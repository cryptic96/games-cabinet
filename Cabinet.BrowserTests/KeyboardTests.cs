using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the cabinet is one tab stop, that the arrow keys, Home and End move between the boxes by where they
/// sit, and that Enter and Space open a card and give focus back to the box on close.
/// </summary>
[Trait("Category", "Browser")]
public sealed class KeyboardTests : CabinetPageTest
{
    private const string Boxes = "#cabinet .placement";
    private const string LastLinkBeforeCabinet = ".sample-switcher a >> nth=-1";
    private const string FocusedBoxScript = "(() => { const e = document.activeElement; const r = e.getBoundingClientRect(); return { isBox: e.classList.contains('placement'), shelf: e.dataset.shelf ?? null, entryId: e.dataset.entryId ?? null, kind: e.dataset.kind ?? null, tabIndex: e.getAttribute('tabindex'), left: r.left, top: r.top, right: r.right, bottom: r.bottom }; })()";
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private sealed class FocusedBox
    {
        public bool IsBox { get; set; }

        public string? Shelf { get; set; }

        public string? EntryId { get; set; }

        public string? Kind { get; set; }

        public string? TabIndex { get; set; }

        public double Left { get; set; }

        public double Top { get; set; }

        public double Right { get; set; }

        public double Bottom { get; set; }
    }

    [Fact]
    public async Task Exactly_one_box_is_a_tab_stop_and_it_is_the_first_one_on_the_first_visit()
    {
        await OpenAsync("/?sample=65", 1440, 900);

        await Expect(Page.Locator(Boxes + "[tabindex=\"0\"]")).ToHaveCountAsync(1);
        var total = await Page.Locator(Boxes).CountAsync();
        total.Should().BeGreaterThan(10);
        await Expect(Page.Locator(Boxes + "[tabindex=\"-1\"]")).ToHaveCountAsync(total - 1);
        (await Page.Locator(Boxes).First.GetAttributeAsync("tabindex")).Should().Be("0");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task One_Tab_enters_the_cabinet_on_the_tab_stop_and_the_next_Tab_leaves_it()
    {
        await OpenAsync("/?sample=65", 1440, 900);

        await Page.Locator(LastLinkBeforeCabinet).FocusAsync();
        await Page.Keyboard.PressAsync("Tab");

        await Expect(Page.Locator(Boxes).First).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("Tab");

        await Expect(Page.Locator(".bgg-credit")).ToBeFocusedAsync();
        (await FocusedAsync()).IsBox.Should().BeFalse();
    }

    [Fact]
    public async Task Shift_Tab_from_the_footer_comes_back_to_the_box_that_last_had_focus()
    {
        await OpenAsync("/?sample=65", 1440, 900);

        await Page.Locator(Boxes).First.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        var moved = await FocusedAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Page.Keyboard.PressAsync("Shift+Tab");

        var back = await FocusedAsync();
        back.EntryId.Should().Be(moved.EntryId);
        back.Kind.Should().Be(moved.Kind);
    }

    [Fact]
    public async Task Right_stays_on_the_shelf_and_moves_to_the_right_and_Down_moves_below_the_box()
    {
        await OpenAsync("/?sample=65", 1440, 900);

        var start = Page.Locator(Boxes + "[data-shelf]").First;
        await start.FocusAsync();
        var before = await FocusedAsync();

        await Page.Keyboard.PressAsync("ArrowRight");
        var right = await FocusedAsync();

        right.Shelf.Should().Be(before.Shelf);
        right.Left.Should().BeGreaterThan(before.Left);

        await Page.Locator(Boxes).Nth(await IndexOfBoxWithBoxBelowAsync()).FocusAsync();
        var above = await FocusedAsync();

        await Page.Keyboard.PressAsync("ArrowDown");
        var below = await FocusedAsync();

        below.IsBox.Should().BeTrue();
        below.Top.Should().BeGreaterThanOrEqualTo(above.Bottom - 2);
    }

    [Fact]
    public async Task Home_focuses_the_first_box_in_document_order_and_End_the_last()
    {
        await OpenAsync("/?sample=65", 1440, 900);

        await Page.Locator(Boxes).Nth(3).FocusAsync();
        await Page.Keyboard.PressAsync("Home");
        await Expect(Page.Locator(Boxes).First).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("End");
        await Expect(Page.Locator(Boxes).Last).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Enter_opens_the_card_with_focus_on_its_title_and_Escape_returns_focus_to_the_same_box_which_keeps_the_tab_stop()
    {
        await OpenAsync("/?sample=65", 1440, 900);

        await Page.Locator(LastLinkBeforeCabinet).FocusAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Page.Keyboard.PressAsync("ArrowRight");
        var box = await FocusedAsync();

        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("dialog.card-dialog[open]")).ToBeVisibleAsync();
        await Expect(Page.Locator("#card-title")).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator("dialog.card-dialog[open]")).ToHaveCountAsync(0);
        var returned = await FocusedAsync();
        returned.EntryId.Should().Be(box.EntryId);
        returned.Kind.Should().Be(box.Kind);
        returned.TabIndex.Should().Be("0");
        await Expect(Page.Locator(Boxes + "[tabindex=\"0\"]")).ToHaveCountAsync(1);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Space_also_opens_a_card()
    {
        await OpenAsync("/?sample=65", 1440, 900);

        await Page.Locator(Boxes).First.FocusAsync();
        await Page.Keyboard.PressAsync("Space");

        await Expect(Page.Locator("dialog.card-dialog[open]")).ToBeVisibleAsync();
        await Expect(Page.Locator("#card-title")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task A_handled_key_does_not_scroll_the_page_when_the_target_is_already_in_view()
    {
        await OpenAsync("/?sample=65", 1440, 900);

        await Page.Locator(Boxes).First.FocusAsync();
        var before = await Page.EvaluateAsync<double>("window.scrollY");

        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Page.Keyboard.PressAsync("Space");
        await Page.Keyboard.PressAsync("Escape");

        (await Page.EvaluateAsync<double>("window.scrollY")).Should().Be(before);
    }

    [Fact]
    public async Task The_cabinet_is_a_group_named_Cabinet_with_a_hidden_hint_on_arrow_keys()
    {
        await OpenAsync("/?sample=65", 1440, 900);

        var cabinet = Page.Locator("#cabinet");
        await Expect(cabinet).ToHaveAttributeAsync("role", "group");
        await Expect(cabinet).ToHaveAttributeAsync("aria-label", "Cabinet");
        await Expect(cabinet).ToHaveAttributeAsync("aria-describedby", "cabinet-hint");
        await Expect(Page.Locator("#cabinet-hint")).ToHaveTextAsync("Use the arrow keys to move between games and press Enter to open one.");
    }

    [Fact]
    public async Task In_a_big_cabinet_End_brings_the_last_box_into_view_and_it_is_still_one_tab_stop()
    {
        await OpenAsync("/?sample=400", 1440, 900);

        await Expect(Page.Locator(Boxes + "[tabindex=\"0\"]")).ToHaveCountAsync(1);
        await Page.Locator(Boxes).First.FocusAsync();
        var before = await Page.EvaluateAsync<double>("window.scrollY");

        await Page.Keyboard.PressAsync("End");

        await Expect(Page.Locator(Boxes).Last).ToBeFocusedAsync();
        await Expect(Page.Locator(Boxes).Last).ToBeInViewportAsync();
        (await Page.EvaluateAsync<double>("window.scrollY")).Should().BeGreaterThan(before);
        var last = await FocusedAsync();
        last.Top.Should().BeGreaterThanOrEqualTo(0);
        last.Bottom.Should().BeLessThanOrEqualTo(900);
        await Expect(Page.Locator(Boxes + "[tabindex=\"0\"]")).ToHaveCountAsync(1);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task At_the_end_of_a_shelf_the_arrow_key_does_nothing()
    {
        await OpenAsync("/?sample=65", 1440, 900);

        await Page.Locator(Boxes + "[data-shelf]").First.FocusAsync();
        var shelf = (await FocusedAsync()).Shelf;
        var previous = await FocusedAsync();

        for (var step = 0; step < 200; step++)
        {
            await Page.Keyboard.PressAsync("ArrowRight");
            var current = await FocusedAsync();
            if (current.EntryId == previous.EntryId && current.Kind == previous.Kind)
            {
                break;
            }

            current.Shelf.Should().Be(shelf);
            previous = current;
        }

        await Page.Keyboard.PressAsync("ArrowRight");
        var after = await FocusedAsync();
        after.EntryId.Should().Be(previous.EntryId);
        after.Kind.Should().Be(previous.Kind);
        await SaveScreenshotAsync("keyboard-focus-desktop");
    }

    [Fact]
    public async Task On_a_phone_Down_reaches_a_box_on_the_next_shelf()
    {
        await OpenAsync("/?sample=65", 390, 800);

        await Page.Locator(Boxes).Nth(await IndexOfBoxWithBoxBelowAsync()).FocusAsync();
        var start = await FocusedAsync();

        await Page.Keyboard.PressAsync("ArrowDown");
        var next = await FocusedAsync();

        next.IsBox.Should().BeTrue();
        next.Shelf.Should().NotBe(start.Shelf);
        next.Top.Should().BeGreaterThanOrEqualTo(start.Bottom - 2);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_focused_more_marker_holds_the_tab_stop_and_its_base_box_does_not()
    {
        await OpenAsync("/?sample=400", 1440, 900);

        var marker = Page.Locator(Boxes + "[data-kind=\"moreMarker\"]").First;
        var entryId = await marker.GetAttributeAsync("data-entry-id");
        await marker.FocusAsync();

        await Expect(marker).ToHaveAttributeAsync("tabindex", "0");
        await Expect(Page.Locator($"{Boxes}[data-entry-id=\"{entryId}\"]:not([data-kind=\"moreMarker\"])")).ToHaveAttributeAsync("tabindex", "-1");
        await Expect(Page.Locator(Boxes + "[tabindex=\"0\"]")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task An_empty_cabinet_has_no_tab_stop_and_Tab_goes_straight_to_the_footer_credit()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        await OpenWithoutBoxesAsync("/?sample=0");

        await Expect(Page.Locator(Boxes)).ToHaveCountAsync(0);

        await Page.Locator(LastLinkBeforeCabinet).FocusAsync();
        await Page.Keyboard.PressAsync("Tab");

        await Expect(Page.Locator(".bgg-credit")).ToBeFocusedAsync();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_single_box_is_the_one_tab_stop_and_every_arrow_stays_on_it()
    {
        await OpenAsync("/?sample=1", 1440, 900);

        var only = Page.Locator(Boxes);
        await Expect(only).ToHaveCountAsync(1);
        await Expect(only).ToHaveAttributeAsync("tabindex", "0");

        await only.FocusAsync();
        foreach (var key in new[] { "ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "Home", "End" })
        {
            await Page.Keyboard.PressAsync(key);
            await Expect(only).ToBeFocusedAsync();
        }
    }

    [Fact]
    public async Task After_the_cabinet_is_drawn_again_the_box_that_held_the_tab_stop_still_holds_it()
    {
        await OpenAsync("/?sample=65", 1440, 900);
        var onDesktop = await BoxKeysAsync();

        await ChangeProfileAsync(390, 800, "phone");
        var onPhone = await BoxKeysAsync();
        await ChangeProfileAsync(1440, 900, "desktop");

        var shared = onDesktop.Intersect(onPhone).Skip(4).First().Split('|');
        var box = Page.Locator($"{Boxes}[data-entry-id=\"{shared[0]}\"][data-kind=\"{shared[1]}\"]");
        await box.FocusAsync();
        await Expect(box).ToHaveAttributeAsync("tabindex", "0");

        await ChangeProfileAsync(390, 800, "phone");

        await Expect(box).ToHaveAttributeAsync("tabindex", "0");
        await Expect(Page.Locator(Boxes + "[tabindex=\"0\"]")).ToHaveCountAsync(1);
    }

    [Theory]
    [InlineData("/?sample=5", 1440, 900)]
    [InlineData("/?sample=5", 390, 800)]
    [InlineData("/?sample=12", 1440, 900)]
    [InlineData("/?sample=12", 390, 800)]
    [InlineData("/?sample=65", 1440, 900)]
    [InlineData("/?sample=65", 390, 800)]
    [InlineData("/?sample=400", 1440, 900)]
    [InlineData("/?sample=400", 390, 800)]
    [InlineData("/?sample=edge", 1440, 900)]
    [InlineData("/?sample=edge", 390, 800)]
    public async Task Every_box_can_be_reached_from_the_first_one_with_the_arrow_keys(string path, int width, int height)
    {
        await OpenAsync(path, width, height);

        var unreachable = await Page.EvaluateAsync<int>(
            "(async () => { const { nextBox } = await import('/js/keys.js'); const rects = [...document.querySelectorAll('#cabinet .placement')].map((b) => { const r = b.getBoundingClientRect(); return { left: r.left, top: r.top, right: r.right, bottom: r.bottom, shelf: b.dataset.shelf }; }); const seen = new Set([0]); const queue = [0]; while (queue.length > 0) { const at = queue.shift(); for (const key of ['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown']) { const to = nextBox(rects, at, key); if (!seen.has(to)) { seen.add(to); queue.push(to); } } } return rects.length - seen.size; })()");

        unreachable.Should().Be(0);
    }

    private async Task OpenWithoutBoxesAsync(string path)
    {
        var drawn = Page.WaitForResponseAsync(response => response.Url.Contains("/cabinet/layout", StringComparison.Ordinal));
        await Page.GotoAsync($"http://127.0.0.1:{PublicPort}{path}");
        await drawn;
        await Expect(Page.Locator("#cabinet .cabinet-loading")).ToHaveCountAsync(0);
    }

    private async Task ChangeProfileAsync(int width, int height, string profile)
    {
        var drawn = Page.WaitForResponseAsync(response => response.Url.Contains($"/cabinet/layout?profile={profile}", StringComparison.Ordinal));
        await Page.SetViewportSizeAsync(width, height);
        await drawn;
        await Expect(Page.Locator("#cabinet .cabinet-loading")).ToHaveCountAsync(0);
        await Expect(Page.Locator(Boxes).First).ToBeAttachedAsync();
    }

    private async Task<IReadOnlyList<string>> BoxKeysAsync() => await Page.EvaluateAsync<string[]>(
        "[...document.querySelectorAll('#cabinet .placement')].map((b) => b.dataset.entryId + '|' + b.dataset.kind)");

    private async Task OpenAsync(string path, int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await GotoCabinetAsync(path);
    }

    private async Task<FocusedBox> FocusedAsync() => await Page.EvaluateAsync<FocusedBox>(FocusedBoxScript);

    /// <summary>Gives the document-order index of the first box that has another box below it on screen.</summary>
    private async Task<int> IndexOfBoxWithBoxBelowAsync() => await Page.EvaluateAsync<int>(
        "(() => { const rects = [...document.querySelectorAll('#cabinet .placement')].map((b) => b.getBoundingClientRect()); return rects.findIndex((a) => rects.some((b) => b.top >= a.bottom)); })()");
}

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

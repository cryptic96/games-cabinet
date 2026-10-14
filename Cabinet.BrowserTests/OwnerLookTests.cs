using System.Net;
using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser the look panel of the owner's settings page: a preview that draws a real cabinet section from a sample
/// layout, a trial that every choice changes and nothing else, the publish that makes the trial the look every visitor sees, and the
/// way back to the current look.
/// </summary>
[Trait("Category", "Browser")]
public sealed class OwnerLookTests : CabinetPageTest
{
    private const string SettingsPath = "/owner/settings";
    private const string Preview = ".look-preview";
    private const string SavedText = "Saved for everyone. Open pages follow within a few seconds.";

    private readonly TemporaryDirectory _storage = new();

    [Fact]
    public async Task Trying_a_wood_changes_only_the_preview_and_Use_for_everyone_makes_it_the_look_visitors_see()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithDataAsync(null);
        var posts = CountLookPosts();
        await GotoOwnerAsync(SettingsPath);

        var preview = Page.Locator(Preview);
        await Expect(preview).ToHaveAttributeAsync("data-design", "classic");
        await Expect(preview).ToHaveAttributeAsync("data-wood", "brown");
        await Expect(preview).ToHaveAttributeAsync("data-backs", "wood");
        await Expect(preview).ToHaveAttributeAsync("data-lights", "off");
        await Expect(preview).ToHaveAttributeAsync("data-depth", "shaded");
        await Expect(preview).ToHaveAttributeAsync("aria-hidden", "true");
        (await preview.EvaluateAsync<bool>("element => element.inert")).Should().BeTrue();
        await Expect(preview.Locator(".section")).ToHaveCountAsync(1);
        await Expect(preview.Locator(".cubby")).ToHaveCountAsync(6);

        await Page.Locator("input[name=wood][value=oak]").CheckAsync();

        await Expect(preview).ToHaveAttributeAsync("data-wood", "oak");
        posts().Should().Be(0);
        (await PublicAttributeAsync("data-wood")).Should().Be("brown");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Use for everyone" }).ClickAsync();

        await Expect(Page.Locator("#finish-status")).ToContainTextAsync(SavedText);
        posts().Should().Be(1);
        (await PublicAttributeAsync("data-wood")).Should().Be("oak");
        await SaveScreenshotAsync("owner-look-1440");
        ExpectCleanPage();
    }

    [Fact]
    public async Task The_panel_opens_with_the_current_look_checked_and_both_buttons_disabled_until_a_choice_is_tried()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithDataAsync(null);
        await GotoOwnerAsync(SettingsPath);

        var publish = Page.GetByRole(AriaRole.Button, new() { Name = "Use for everyone" });
        var discard = Page.GetByRole(AriaRole.Button, new() { Name = "Back to current" });
        await Expect(publish).ToHaveAttributeAsync("aria-disabled", "true");
        await Expect(discard).ToHaveAttributeAsync("aria-disabled", "true");
        await Expect(Page.Locator("#look-trying")).ToBeHiddenAsync();
        await Expect(Page.Locator("#look-no-script")).ToBeHiddenAsync();
        await Expect(Page.Locator("input[name=design][value=classic]")).ToBeCheckedAsync();
        await Expect(Page.Locator("input[name=wood][value=brown]")).ToBeCheckedAsync();
        await Expect(Page.Locator("input[name=lights][value=off]")).ToBeCheckedAsync();
        await Expect(Page.Locator("input[name=depth][value=shaded]")).ToBeCheckedAsync();

        await Page.Locator("input[name=depth][value=deep]").CheckAsync();

        await Expect(publish).Not.ToHaveAttributeAsync("aria-disabled", "true");
        await Expect(discard).Not.ToHaveAttributeAsync("aria-disabled", "true");
        await Expect(Page.Locator("#look-trying")).ToHaveTextAsync("Trying: Classic · Warm brown · Wood backs · Lights: Off · Depth: Deep");
        await Expect(Page.Locator("#look-current")).ToHaveTextAsync("Visitors see: Classic · Warm brown · Wood backs · Lights: Off · Depth: Shaded");
        await Expect(Page.Locator("#finish-status")).ToHaveTextAsync("Trying a new look. Visitors still see the current one.");

        await discard.ClickAsync();

        await Expect(Page.Locator("#finish-status")).ToHaveTextAsync("Back to the current look.");
        await Expect(Page.Locator("input[name=depth][value=shaded]")).ToBeCheckedAsync();
        await Expect(Page.Locator(Preview)).ToHaveAttributeAsync("data-depth", "shaded");
        await Expect(Page.Locator("input[name=design][value=classic]")).ToBeFocusedAsync();
        await Expect(publish).ToHaveAttributeAsync("aria-disabled", "true");
        ExpectCleanPage();
    }

    [Fact]
    public async Task The_summary_the_script_builds_equals_the_one_the_server_wrote_for_the_current_look()
    {
        var look = new CabinetLook("library", "mahogany", "painted", "evenings", "deep");
        await StartWithDataAsync(OwnerData.Empty with { Look = look });
        var served = await Page.Context.APIRequest.GetAsync($"http://127.0.0.1:{OwnerPort}{SettingsPath}");
        var html = await served.TextAsync();
        var serverLine = WebUtility.HtmlDecode(Regex.Match(html, "id=\"look-current\">([^<]*)</p>").Groups[1].Value.Trim());

        await GotoOwnerAsync(SettingsPath);

        var built = await Page.EvaluateAsync<string>(
            """
            async () => {
              const format = await import('/js/owner-format.js');
              const preview = document.getElementById('look-preview');
              const look = Object.fromEntries(format.LOOK_FIELDS.map((field) => [field, preview.getAttribute('data-' + field)]));
              return format.fill(format.OWNER_TEXT.visitorsSee, { summary: format.lookSummary(look) });
            }
            """);

        serverLine.Should().Be("Visitors see: Library · Mahogany · Painted backs · Lights: Evenings only · Depth: Deep");
        built.Should().Be(serverLine);
        await Expect(Page.Locator("#look-current")).ToHaveTextAsync(serverLine);
        await Expect(Page.Locator(Preview)).ToHaveAttributeAsync("data-lights", "evenings");
    }

    [Fact]
    public async Task When_the_sample_cannot_load_the_trial_wall_says_so_and_publishing_still_works()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithDataAsync(null);
        await Page.RouteAsync("**/js/owner-look-sample.json*", route => route.FulfillAsync(new() { Status = 500, Body = string.Empty }));
        await GotoOwnerAsync(SettingsPath);

        await Expect(Page.Locator(Preview)).ToContainTextAsync("The preview couldn't load. You can still choose a look and press Use for everyone.");
        await Expect(Page.Locator($"{Preview} .section")).ToHaveCountAsync(0);

        await Page.Locator("input[name=wood][value=oak]").CheckAsync();
        await Expect(Page.Locator(Preview)).ToHaveAttributeAsync("data-wood", "oak");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Use for everyone" }).ClickAsync();

        await Expect(Page.Locator("#finish-status")).ToContainTextAsync(SavedText);
        (await PublicAttributeAsync("data-wood")).Should().Be("oak");
    }

    [Fact]
    public async Task Before_the_sample_arrives_the_preview_keeps_its_size_so_nothing_below_it_moves()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await StartWithDataAsync(null);
        var release = new TaskCompletionSource();
        await Page.RouteAsync("**/js/owner-look-sample.json*", async route =>
        {
            await release.Task;
            await route.ContinueAsync();
        });
        await GotoOwnerAsync(SettingsPath);

        var waitingPreview = await Page.Locator(Preview).BoundingBoxAsync();
        var waitingForm = await Page.Locator("#look-form").BoundingBoxAsync();
        await Expect(Page.Locator($"{Preview} .section[data-standin]")).ToHaveCountAsync(1);

        release.SetResult();
        await Expect(Page.Locator($"{Preview} .cubby")).ToHaveCountAsync(6);

        var drawnPreview = await Page.Locator(Preview).BoundingBoxAsync();
        var drawnForm = await Page.Locator("#look-form").BoundingBoxAsync();

        drawnPreview!.Height.Should().BeApproximately((float)waitingPreview!.Height, 1f);
        drawnForm!.Y.Should().BeApproximately((float)waitingForm!.Y, 1f);
    }

    [Theory]
    [InlineData(390, 844)]
    [InlineData(320, 640)]
    [InlineData(1440, 900)]
    public async Task The_section_lies_entirely_inside_the_preview_wrapper(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartWithDataAsync(null);
        await GotoOwnerAsync(SettingsPath);
        await Expect(Page.Locator($"{Preview} .cubby")).ToHaveCountAsync(6);

        var wrapper = await Page.Locator(Preview).BoundingBoxAsync();
        var section = await Page.Locator($"{Preview} .section").BoundingBoxAsync();

        section!.X.Should().BeGreaterThanOrEqualTo(wrapper!.X);
        (section.X + section.Width).Should().BeLessThanOrEqualTo(wrapper.X + wrapper.Width + 0.5f);
        section.Y.Should().BeGreaterThanOrEqualTo(wrapper.Y);
        (section.Y + section.Height).Should().BeLessThanOrEqualTo(wrapper.Y + wrapper.Height + 0.5f);
        var overflow = await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
        overflow.Should().BeFalse();
        await SaveScreenshotAsync($"owner-look-{width}");
    }

    [Fact]
    public async Task A_failed_save_keeps_the_trial_and_says_nothing_changed()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithDataAsync(null);
        await GotoOwnerAsync(SettingsPath);
        await Page.RouteAsync("**/owner/settings?handler=Look*", route => route.FulfillAsync(new() { Status = 500, Body = string.Empty }));

        await Page.Locator("input[name=wood][value=oak]").CheckAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Use for everyone" }).ClickAsync();

        await Expect(Page.Locator("#finish-status")).ToHaveTextAsync("Couldn't save that. Nothing changed. Try again.");
        await Expect(Page.Locator(Preview)).ToHaveAttributeAsync("data-wood", "oak");
        await Expect(Page.Locator("input[name=wood][value=oak]")).ToBeCheckedAsync();
        await Expect(Page.Locator("#look-current")).ToContainTextAsync("Warm brown");
        (await PublicAttributeAsync("data-wood")).Should().Be("brown");
    }

    [Fact]
    public async Task Undo_after_publishing_puts_the_earlier_look_back_for_everyone()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithDataAsync(null);
        await GotoOwnerAsync(SettingsPath);

        await Page.Locator("input[name=wood][value=oak]").CheckAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Use for everyone" }).ClickAsync();
        await Expect(Page.Locator("#finish-status")).ToContainTextAsync(SavedText);
        (await PublicAttributeAsync("data-wood")).Should().Be("oak");
        await Expect(Page.Locator("#look-publish")).ToBeFocusedAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Undo" }).ClickAsync();

        await Expect(Page.Locator("#finish-status")).ToHaveTextAsync("Put back the earlier look.");
        await Expect(Page.Locator("input[name=wood][value=brown]")).ToBeCheckedAsync();
        await Expect(Page.Locator(Preview)).ToHaveAttributeAsync("data-wood", "brown");
        await Expect(Page.Locator("#look-current")).ToContainTextAsync("Warm brown");
        (await PublicAttributeAsync("data-wood")).Should().Be("brown");
        ExpectCleanPage();
    }

    [Fact]
    public async Task The_design_and_wood_groups_are_tiles_that_each_carry_a_swatch_of_their_own_choice_in_the_trial_look()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithDataAsync(null);
        await GotoOwnerAsync(SettingsPath);

        var designs = Page.Locator("fieldset.look-choices", new() { Has = Page.Locator("legend", new() { HasTextString = "Design" }) });
        var woods = Page.Locator("fieldset.look-choices", new() { Has = Page.Locator("legend", new() { HasTextString = "Wood" }) });
        (await designs.Locator(".pic-label").AllTextContentsAsync()).Should().Equal("Classic", "Modern", "Display", "On legs", "Library", "Plywood", "Plywood on legs");
        (await woods.Locator(".pic-label").AllTextContentsAsync()).Should().Equal("Warm brown", "Walnut", "Oak", "Birch", "Mahogany");
        (await designs.Locator(".pic-sub").AllTextContentsAsync()).Should().Equal(
            "Moulded top, arched plinth on feet.",
            "Slab top, straight recessed base.",
            "Thick slab top, dark base that seems to float.",
            "Apron with splayed, tapered legs.",
            "Crown moulding, brass line on every shelf.",
            "Striped ply edges, rounded corners.",
            "Ply edges on two splayed legs.");
        (await woods.Locator(".pic-sub").AllTextContentsAsync()).Should().Equal(
            "Today's wood and wall.",
            "Dark and rich.",
            "Pale gold, open grain.",
            "Pale and clean, light wall.",
            "Deep red-brown.");

        var designValues = await designs.Locator(".look-swatch").EvaluateAllAsync<string[]>("swatches => swatches.map(swatch => swatch.dataset.design + '/' + swatch.dataset.wood)");
        designValues.Should().Equal("classic/brown", "modern/brown", "display/brown", "legs/brown", "library/brown", "plywood/brown", "plywood-legs/brown");
        var woodValues = await woods.Locator(".look-swatch").EvaluateAllAsync<string[]>("swatches => swatches.map(swatch => swatch.dataset.wood + '/' + swatch.dataset.design)");
        woodValues.Should().Equal("brown/classic", "walnut/classic", "oak/classic", "birch/classic", "mahogany/classic");
        (await Page.Locator(".look-swatch").EvaluateAllAsync<bool>("swatches => swatches.every(swatch => swatch.getAttribute('aria-hidden') === 'true' && swatch.dataset.lights === 'off')")).Should().BeTrue();
        await ExpectEverySwatchDrawnAsync();
        await SaveScreenshotAsync("owner-look-tiles-1440");
    }

    [Fact]
    public async Task Without_script_the_tiles_still_draw_and_the_note_about_the_preview_shows()
    {
        await StartWithDataAsync(null);
        await using var plain = await Browser.NewContextAsync(new()
        {
            JavaScriptEnabled = false,
            ExtraHTTPHeaders = new Dictionary<string, string> { ["Accept-Encoding"] = "identity" },
        });
        var page = await plain.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);

        await page.GotoAsync($"http://127.0.0.1:{OwnerPort}{SettingsPath}");

        await Expect(page.Locator(".look-tile")).ToHaveCountAsync(12);
        await Expect(page.GetByText("The preview needs scripts. You can still choose a look and press Use for everyone.")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Use for everyone" })).ToBeVisibleAsync();
        var drawn = await page.Locator(".look-swatch .placement").EvaluateAllAsync<string[]>(
            "boxes => boxes.map(box => { const r = box.getBoundingClientRect(); return `${Math.round(r.width)}x${Math.round(r.height)} ${getComputedStyle(box).backgroundColor}`; })");
        drawn.Should().OnlyContain(box => !box.StartsWith("0x", StringComparison.Ordinal) && !box.Contains("rgba(0, 0, 0, 0)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Checking_a_wood_redraws_every_design_tile_in_it_and_checking_a_design_redraws_every_wood_tile_on_it()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithDataAsync(null);
        var posts = CountLookPosts();
        await GotoOwnerAsync(SettingsPath);

        await Page.Locator("input[name=wood][value=oak]").CheckAsync();

        (await Page.Locator(".look-swatch-design").EvaluateAllAsync<string[]>("swatches => swatches.map(swatch => swatch.dataset.wood)")).Should().OnlyContain(wood => wood == "oak");
        await Expect(Page.Locator(Preview)).ToHaveAttributeAsync("data-wood", "oak");
        (await Page.Locator(".look-swatch-wood").EvaluateAllAsync<string[]>("swatches => swatches.map(swatch => swatch.dataset.wood)")).Should().Equal("brown", "walnut", "oak", "birch", "mahogany");

        await Page.Locator("input[name=design][value=library]").CheckAsync();

        (await Page.Locator(".look-swatch-wood").EvaluateAllAsync<string[]>("swatches => swatches.map(swatch => swatch.dataset.design)")).Should().OnlyContain(design => design == "library");
        await Expect(Page.Locator(Preview)).ToHaveAttributeAsync("data-design", "library");
        (await Page.Locator(".look-swatch-design").EvaluateAllAsync<string[]>("swatches => swatches.map(swatch => swatch.dataset.design)"))
            .Should().Equal("classic", "modern", "display", "legs", "library", "plywood", "plywood-legs");

        await Page.Locator("input[name=backs]").CheckAsync();
        await Page.Locator("input[name=depth][value=deep]").CheckAsync();

        (await Page.Locator(".look-swatch").EvaluateAllAsync<string[]>("swatches => swatches.map(swatch => swatch.dataset.backs + '/' + swatch.dataset.depth)")).Should().OnlyContain(pair => pair == "painted/deep");
        (await Page.Locator(".look-swatch").EvaluateAllAsync<string[]>("swatches => swatches.map(swatch => swatch.dataset.lights)")).Should().OnlyContain(lights => lights == "off");
        posts().Should().Be(0);
        await SaveScreenshotAsync("owner-look-tiles-library-oak");
    }

    [Fact]
    public async Task The_preview_lights_follow_the_trial_and_Evening_matters_only_with_Evenings_only()
    {
        await Page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
        await OpenSettingsAsync(1440, 900);
        var light = Page.Locator($"{Preview} .section-light");
        var evening = Page.Locator("#look-evening input[name=preview-time][value=day]");

        await Expect(light).ToBeHiddenAsync();
        await Expect(evening).ToBeDisabledAsync();
        await Expect(Page.Locator("#look-evening-hint")).ToHaveTextAsync("Only matters with Evenings only.");

        await Page.Locator("input[name=lights][value=on]").CheckAsync();

        await Expect(light).ToHaveCSSAsync("opacity", "1");
        (await light.EvaluateAsync<string>("element => getComputedStyle(element).transitionDuration")).Should().Be("0s");
        await Expect(evening).ToBeDisabledAsync();

        await Page.Locator("input[name=lights][value=evenings]").CheckAsync();

        await Expect(evening).ToBeEnabledAsync();
        await Expect(Page.Locator("#look-evening-hint")).ToHaveTextAsync("Only changes this preview.");
        await Expect(light).ToHaveCSSAsync("opacity", "0");
        await Expect(Page.Locator(Preview)).Not.ToHaveAttributeAsync("data-lit", new Regex(".*"));

        await Page.Locator("input[name=preview-time][value=evening]").CheckAsync();

        await Expect(light).ToHaveCSSAsync("opacity", "1");
        await Expect(Page.Locator(Preview)).ToHaveAttributeAsync("data-evening", string.Empty);
        (await Page.EvaluateAsync<bool>("() => document.documentElement.hasAttribute('data-evening') || document.documentElement.hasAttribute('data-lit')")).Should().BeFalse();
        (await PublicAttributeAsync("data-evening")).Should().BeNull();
        (await PublicAttributeAsync("data-lights")).Should().Be("off");

        await Page.Locator("input[name=preview-time][value=day]").CheckAsync();

        await Expect(light).ToHaveCSSAsync("opacity", "0");
        await Expect(Page.Locator(Preview)).Not.ToHaveAttributeAsync("data-evening", new Regex(".*"));

        await Page.Locator("input[name=lights][value=off]").CheckAsync();

        await Expect(light).ToBeHiddenAsync();
        await Expect(evening).ToBeDisabledAsync();
        ExpectCleanPage();
    }

    [Fact]
    public async Task With_motion_the_preview_lamp_fades_in_over_the_lamp_fade()
    {
        await OpenSettingsAsync(1440, 900);
        var light = Page.Locator($"{Preview} .section-light");

        await Page.Locator("input[name=lights][value=on]").CheckAsync();

        (await light.EvaluateAsync<string>("element => getComputedStyle(element).transitionDuration")).Should().Be("2s");
        await Expect(light).ToHaveCSSAsync("opacity", "1", new() { Timeout = 10000 });
    }

    [Fact]
    public async Task Beside_the_preview_the_five_woods_share_one_row_and_each_wood_tile_leaves_about_a_quarter_of_its_width_as_wall()
    {
        await OpenSettingsAsync(1440, 900);

        var rows = await Page.Locator(".look-swatch-wood").EvaluateAllAsync<double[]>("swatches => swatches.map(swatch => Math.round(swatch.getBoundingClientRect().top))");
        rows.Distinct().Should().HaveCount(1);
        var walls = await Page.Locator(".look-swatch-wood").EvaluateAllAsync<double[]>(
            "swatches => swatches.map(swatch => (swatch.querySelector('.section-body').getBoundingClientRect().left - swatch.getBoundingClientRect().left) / swatch.getBoundingClientRect().width)");
        walls.Should().OnlyContain(share => share > 0.22 && share < 0.32);
    }

    [Fact]
    public async Task Plywood_tiles_keep_their_ply_stripes_at_tile_size()
    {
        await OpenSettingsAsync(1440, 900);

        var read = await Page.Locator(".look-swatch-design[data-design^=plywood] .section-top").EvaluateAllAsync<string[]>(
            "tops => tops.map(top => getComputedStyle(top).getPropertyValue('--ply-g').trim() + '|' + getComputedStyle(top).getPropertyValue('--ply-p').trim())");

        read.Should().HaveCount(2);
        read.Should().OnlyContain(pair => pair.StartsWith("0px|", StringComparison.Ordinal));
        read.Should().OnlyContain(pair => pair.Contains("max(4px", StringComparison.Ordinal));
    }

    [Fact]
    public async Task On_a_phone_the_preview_stays_in_view_and_never_crops_the_section_for_any_design()
    {
        await OpenSettingsAsync(390, 844);
        var preview = Page.Locator(Preview);

        (await preview.EvaluateAsync<string>("element => getComputedStyle(element).position")).Should().Be("sticky");
        var shadow = await preview.EvaluateAsync<string>("element => getComputedStyle(element).boxShadow");
        shadow.Should().Contain("rgba(120, 90, 60, 0.12) 0px 1px 0px 16px");

        var controlsBefore = await Page.Locator("#look-form").BoundingBoxAsync();
        await Page.EvaluateAsync("() => window.scrollTo(0, document.querySelector('#look-form').getBoundingClientRect().top + window.scrollY - 300)");
        await Page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
        var pinned = await preview.BoundingBoxAsync();
        var controlsAfter = await Page.Locator("#look-form").BoundingBoxAsync();

        pinned!.Y.Should().BeInRange(-1f, 1f);
        controlsAfter!.Y.Should().BeLessThan(controlsBefore!.Y);
        await Page.EvaluateAsync("() => window.scrollTo(0, 0)");

        foreach (var design in new[] { "classic", "modern", "display", "legs", "library", "plywood", "plywood-legs" })
        {
            await Page.Locator($"input[name=design][value={design}]").CheckAsync();
            await Expect(preview).ToHaveAttributeAsync("data-design", design);

            var wrapper = await preview.BoundingBoxAsync();
            var section = await Page.Locator($"{Preview} .section").BoundingBoxAsync();

            section!.X.Should().BeGreaterThanOrEqualTo(wrapper!.X, design);
            (section.X + section.Width).Should().BeLessThanOrEqualTo(wrapper.X + wrapper.Width + 0.5f, design);
            section.Y.Should().BeGreaterThanOrEqualTo(wrapper.Y, design);
            (section.Y + section.Height).Should().BeLessThanOrEqualTo(wrapper.Y + wrapper.Height + 0.5f, design);
        }

        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth")).Should().BeFalse();
        await SaveScreenshotAsync("owner-look-390-tiles");
    }

    [Fact]
    public async Task On_a_phone_the_design_and_wood_rows_scroll_sideways_with_the_next_tile_peeking_and_arrowing_keeps_the_checked_tile_in_view()
    {
        await OpenSettingsAsync(390, 844);
        var rows = Page.Locator(".look-tiles");

        (await rows.EvaluateAllAsync<bool>("rows => rows.every(row => row.scrollWidth > row.clientWidth)")).Should().BeTrue();
        (await rows.EvaluateAllAsync<bool>(
            """
            rows => rows.every(row => {
              const edge = row.getBoundingClientRect().right;
              return [...row.children].some(tile => { const r = tile.getBoundingClientRect(); return r.left < edge - 8 && r.right > edge + 8; });
            })
            """)).Should().BeTrue();
        (await rows.EvaluateAllAsync<string[]>("rows => rows.map(row => getComputedStyle(row).scrollSnapType + '|' + getComputedStyle(row).overflowX)")).Should().OnlyContain(style => style == "x|auto" || style == "x proximity|auto");

        await Page.Locator("input[name=design]:checked").FocusAsync();
        for (var press = 0; press < 5; press++)
        {
            await Page.Keyboard.PressAsync("ArrowRight");
        }

        await Expect(Page.Locator("input[name=design][value=plywood]")).ToBeCheckedAsync();
        await Expect(Page.Locator(Preview)).ToHaveAttributeAsync("data-design", "plywood");
        await Page.EvaluateAsync("() => new Promise(resolve => setTimeout(resolve, 400))");
        (await Page.EvaluateAsync<bool>(
            """
            () => {
              const tile = document.querySelector('input[name=design]:checked').closest('.look-tile');
              const row = tile.parentElement.getBoundingClientRect();
              const box = tile.getBoundingClientRect();
              return box.left >= row.left - 1 && box.right <= row.right + 1;
            }
            """)).Should().BeTrue();
        (await Page.EvaluateAsync<bool>("() => document.querySelector('.look-tiles').scrollLeft > 0")).Should().BeTrue();

        var heights = await Page.Locator(".look-choice, .look-tile, .look-actions .owner-btn").EvaluateAllAsync<double[]>("controls => controls.map(control => control.getBoundingClientRect().height)");
        heights.Should().OnlyContain(height => height >= 43.5);
    }

    [Fact]
    public async Task At_the_narrowest_width_tile_names_wrap_whole_and_no_text_is_cut_off()
    {
        await OpenSettingsAsync(320, 640);

        var measured = await Page.EvaluateAsync<string[]>(
            """
            () => [...document.querySelectorAll('.look-tile .pic-label')].map(label => {
              const lineHeight = parseFloat(getComputedStyle(label).lineHeight);
              return label.textContent.trim() + '=' + Math.round(label.getBoundingClientRect().height / lineHeight);
            })
            """);
        var lines = measured.ToDictionary(entry => entry[..entry.LastIndexOf('=')], entry => int.Parse(entry[(entry.LastIndexOf('=') + 1)..], System.Globalization.CultureInfo.InvariantCulture));

        lines["Plywood on legs"].Should().Be(2);
        lines["Mahogany"].Should().Be(1);
        lines["Warm brown"].Should().BeLessThanOrEqualTo(2);
        var cutOff = await Page.EvaluateAsync<string[]>(
            "() => [...document.querySelectorAll('.pic-label, .pic-sub, .owner-hint, .look-line, .look-choice-name, #finish-status')].filter(element => element.scrollWidth > element.clientWidth).map(element => element.textContent.trim())");
        cutOff.Should().BeEmpty();
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth")).Should().BeFalse();

        await Page.Locator("input[name=lights][value=evenings]").CheckAsync();
        await Page.Locator("input[name=depth][value=deep]").CheckAsync();

        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth")).Should().BeFalse();
        await SaveScreenshotAsync("owner-look-320-tiles");
    }

    [Fact]
    public async Task In_a_short_landscape_window_the_preview_is_not_sticky()
    {
        await OpenSettingsAsync(844, 390);

        (await Page.Locator(Preview).EvaluateAsync<string>("element => getComputedStyle(element).position")).Should().Be("static");
    }

    private async Task OpenSettingsAsync(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartWithDataAsync(null);
        await GotoOwnerAsync(SettingsPath);
        await Expect(Page.Locator($"{Preview} .cubby")).ToHaveCountAsync(6);
    }

    private async Task ExpectEverySwatchDrawnAsync()
    {
        var drawn = await Page.Locator(".look-swatch").EvaluateAllAsync<bool>(
            """
            swatches => swatches.every(swatch => {
              const box = swatch.querySelector('.placement').getBoundingClientRect();
              const area = swatch.getBoundingClientRect();
              return area.width > 40 && area.height > 40 && box.width > 4 && box.height > 4
                && getComputedStyle(swatch).backgroundColor !== 'rgba(0, 0, 0, 0)';
            })
            """);

        drawn.Should().BeTrue();
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private Task StartWithDataAsync(OwnerData? data)
    {
        if (data is not null)
        {
            OwnerDataSeed.Write(_storage.FullPath, data);
        }

        return StartAsync(SyncHarness.CreateFactory(
            new ScriptedBggHandler(),
            SyncHarness.NewClock(),
            new Dictionary<string, string?> { ["Storage:Directory"] = _storage.FullPath }));
    }

    private Func<int> CountLookPosts()
    {
        var seen = 0;

        Page.Request += (_, request) =>
        {
            if (request.Method == "POST" && request.Url.Contains("handler=Look", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref seen);
            }
        };

        return () => Volatile.Read(ref seen);
    }

    private async Task<string?> PublicAttributeAsync(string name)
    {
        var other = await Page.Context.NewPageAsync();

        try
        {
            await other.GotoAsync($"http://127.0.0.1:{PublicPort}/");

            return await other.Locator("html").GetAttributeAsync(name);
        }
        finally
        {
            await other.CloseAsync();
        }
    }

    private void ExpectCleanPage()
    {
        ConsoleErrors.Should().BeEmpty();
        RequestHosts.Should().OnlyContain(host => host == $"127.0.0.1:{OwnerPort}");
    }
}

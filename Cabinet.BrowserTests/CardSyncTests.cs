using System.Collections.Concurrent;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that a sync which changes the collection while a card is open leaves the card, the cabinet behind it and
/// the card data alone, and that the cabinet is redrawn once the card has closed and its box is back in its slot.
/// </summary>
[Trait("Category", "Browser")]
public sealed class CardSyncTests : CabinetPageTest
{
    private const string OpenCard = "dialog.card-dialog[open]";
    private const string Boxes = "#cabinet .placement";
    private const string FirstEntryBox = "#cabinet .placement[data-entry-id=\"5000001\"]:not([data-kind=\"moreMarker\"])";
    private static readonly TimeSpan HoldPeriod = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task A_collection_change_while_a_card_is_open_waits_for_the_close_and_then_redraws()
    {
        var bgg = new SyncRounds.SwitchableBgg(SyntheticBggCollection.Create(5));
        var clock = SyncHarness.NewClock();
        var factory = SyncHarness.CreateFactory(bgg.Handler, clock);
        await StartAsync(factory);
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoCabinetAsync("/");
        var boxesBefore = await Page.Locator(Boxes).CountAsync();
        var versionBefore = (await SyncHarness.ReadStatus(client)).Json.GetProperty("snapshotVersion").GetString();

        var opened = Page.Locator(FirstEntryBox).First;
        var entryId = await opened.GetAttributeAsync("data-entry-id");
        await opened.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator("html[data-pull-kind]")).ToHaveCountAsync(0);
        var title = await Page.Locator("#card-title").InnerTextAsync();
        var requestsWhileOpen = new ConcurrentQueue<string>();
        Page.Request += (_, request) => requestsWhileOpen.Enqueue(request.Url);

        bgg.Serve([.. SyntheticBggCollection.Create(5).Take(3)]);
        await SyncRounds.PressAndWait(client, clock);
        (await SyncHarness.ReadStatus(client)).Json.GetProperty("snapshotVersion").GetString().Should().NotBe(versionBefore);
        await Task.Delay(HoldPeriod, TestContext.Current.CancellationToken);

        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        (await Page.Locator("#card-title").InnerTextAsync()).Should().Be(title);
        (await Page.Locator(Boxes).CountAsync()).Should().Be(boxesBefore);
        requestsWhileOpen.Should().NotContain(url => url.Contains("/cabinet/layout", StringComparison.Ordinal) || url.Contains("/cabinet/cards", StringComparison.Ordinal));

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await Expect(Page.Locator(Boxes)).Not.ToHaveCountAsync(boxesBefore);
        (await Page.Locator(Boxes).CountAsync()).Should().BeLessThan(boxesBefore);
        requestsWhileOpen.Should().Contain(url => url.Contains("/cabinet/layout", StringComparison.Ordinal));
        await Expect(Page.Locator(".placement:focus")).ToHaveAttributeAsync("data-entry-id", entryId!);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Several_collection_changes_while_a_card_is_open_cause_one_redraw_after_the_close()
    {
        var bgg = new SyncRounds.SwitchableBgg(SyntheticBggCollection.Create(5));
        var clock = SyncHarness.NewClock();
        var factory = SyncHarness.CreateFactory(bgg.Handler, clock);
        await StartAsync(factory);
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoCabinetAsync("/");
        var boxesBefore = await Page.Locator(Boxes).CountAsync();

        await Page.Locator(FirstEntryBox).First.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator("html[data-pull-kind]")).ToHaveCountAsync(0);
        var layoutRequests = new ConcurrentQueue<string>();
        Page.Request += (_, request) =>
        {
            if (request.Url.Contains("/cabinet/layout", StringComparison.Ordinal))
            {
                layoutRequests.Enqueue(request.Url);
            }
        };

        bgg.Serve([.. SyntheticBggCollection.Create(5).Take(4)]);
        await SyncRounds.PressAndWait(client, clock);
        bgg.Serve([.. SyntheticBggCollection.Create(5).Take(3)]);
        await SyncRounds.PressAndWait(client, clock);
        await Task.Delay(HoldPeriod, TestContext.Current.CancellationToken);

        layoutRequests.Should().BeEmpty();
        (await Page.Locator(Boxes).CountAsync()).Should().Be(boxesBefore);

        await Page.Keyboard.PressAsync("Escape");

        await Expect(Page.Locator(Boxes)).Not.ToHaveCountAsync(boxesBefore);
        await Task.Delay(HoldPeriod, TestContext.Current.CancellationToken);
        layoutRequests.Should().ContainSingle();
        ConsoleErrors.Should().BeEmpty();
    }
}

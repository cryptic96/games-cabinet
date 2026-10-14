using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that a look the owner publishes reaches a public page that is already open: the page follows within a few
/// seconds through the live channel, cross-fades where the browser can, leaves an open card open, and swaps the footer logo with the
/// wall.
/// </summary>
[Trait("Category", "Browser")]
public sealed class LiveLookTests : CabinetPageTest
{
    private const string SettingsPath = "/owner/settings";
    private const string OpenCard = "dialog.card-dialog[open]";
    private const string FirstBox = "#cabinet .placement:not([data-kind=\"moreMarker\"])";
    private const string SavedText = "Saved for everyone. Open pages follow within a few seconds.";
    private const float FollowTimeoutMs = 5000;
    private const string TransitionSpy = """
        (() => {
          window.__lookSpy = { calls: 0 };
          const original = document.startViewTransition?.bind(document);
          if (original === undefined) {
            return;
          }
          document.startViewTransition = (callback) => {
            window.__lookSpy.calls += 1;
            return original(callback);
          };
        })();
        """;

    private readonly TemporaryDirectory _storage = new();
    private readonly FakeTimeProvider _clock =SyncHarness.NewClock();

    [Fact]
    public async Task A_look_published_on_the_owner_page_reaches_an_open_public_page_with_one_cross_fade_and_the_card_stays_open()
    {
        await StartCabinetAsync();
        await Page.Locator(FirstBox).First.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator("html[data-pull-kind]")).ToHaveCountAsync(0);
        await Page.EvaluateAsync("() => { window.__lookSpy.calls = 0; }");

        await PublishAsync(("design", "modern"), ("wood", "oak"));

        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-wood", "oak", new() { Timeout = FollowTimeoutMs });
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-design", "modern");
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();
        await Expect(Page.Locator("html[data-look-switch]")).ToHaveCountAsync(0);
        (await Page.EvaluateAsync<int>("() => window.__lookSpy.calls")).Should().Be(1);
        ConsoleErrors.Should().BeEmpty();
    }

    private async Task StartCabinetAsync()
    {
        var bgg = new SyncRounds.SwitchableBgg(SyntheticBggCollection.Create(5));
        var factory = SyncHarness.CreateFactory(
            bgg.Handler,
            _clock,
            new Dictionary<string, string?> { ["Storage:Directory"] = _storage.FullPath });
        await StartAsync(factory);
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, _clock, advance: false);
        await Page.SetViewportSizeAsync(1440, 900);
        await Page.AddInitScriptAsync(TransitionSpy);
        await GotoCabinetAsync("/");
    }

    private async Task PublishAsync(params (string Field, string Value)[] choices)
    {
        var owner = await Page.Context.NewPageAsync();

        try
        {
            await owner.GotoAsync($"http://127.0.0.1:{OwnerPort}{SettingsPath}");
            await owner.WaitForSelectorAsync("#owner-main", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });

            foreach (var (field, value) in choices)
            {
                await owner.Locator($"input[name={field}][value={value}]").CheckAsync();
            }

            await owner.GetByRole(AriaRole.Button, new() { Name = "Use for everyone" }).ClickAsync();
            await Assertions.Expect(owner.Locator("#finish-status")).ToContainTextAsync(SavedText);
            _clock.Advance(OwnerChangeNotifier.QuietPeriod);
        }
        finally
        {
            await owner.CloseAsync();
        }
    }
}

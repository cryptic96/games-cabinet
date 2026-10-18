using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that typing another title of a game lights that game once the names have arrived, that the names are
/// read once and only after the search field is first used, that nothing breaks when they cannot be read, and that typing never
/// asks anything beyond the site itself.
/// </summary>
[Trait("Category", "Browser")]
public sealed partial class FilterAlternateTitleTests : CabinetPageTest
{
    private const string Boxes = "#cabinet .placement";
    private const string NamesRoute = "/cabinet/search-names";
    private const string PanelCount = "#filter-panel .filter-count";
    private const string CardsAddress = "/cabinet/cards?profile=desktop&sample=65";
    private const string NamesAddress = "/cabinet/search-names?sample=65";
    private const string DrawnEntryIds = "() => [...document.querySelectorAll('#cabinet .placement:not([data-kind=\"moreMarker\"])')].map((box) => box.dataset.entryId)";
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task An_other_title_lights_its_game_once_the_names_arrive()
    {
        var names = new ConcurrentQueue<string>();
        await OpenAsync("/?sample=65", names);
        var (card, title) = await PickGameWithAnOtherTitleAsync();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await Page.RouteAsync("**" + NamesRoute + "**", async route =>
        {
            await gate.Task;
            await route.ContinueAsync();
        });
        names.Should().BeEmpty();
        var box = Page.Locator($"{Boxes}[data-entry-id=\"{card.EntryId}\"]:not([data-kind=\"moreMarker\"])").First;

        await Page.Locator("#filter-q").FocusAsync();
        await Expect(Page.Locator("#filter-q")).ToBeFocusedAsync();
        await Page.Keyboard.TypeAsync(Plain(title));

        await Expect(Page.Locator("#filter-q")).ToHaveValueAsync(Plain(title));
        await Expect(box).ToHaveAttributeAsync("data-dim", string.Empty);
        (await Page.Locator(".cabinet-message, [role=progressbar], .spinner").CountAsync()).Should().Be(0);
        gate.SetResult();

        await Expect(box).Not.ToHaveAttributeAsync("data-dim", string.Empty);
        await Expect(Page.Locator(".filter-bar")).ToHaveAttributeAsync("data-active", string.Empty);
        (await Page.Locator(Boxes + "[data-dim]").CountAsync()).Should().BeGreaterThan(0);
        names.Should().ContainSingle();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_names_are_asked_for_once_after_the_first_use_and_not_at_load()
    {
        var names = new ConcurrentQueue<string>();
        await OpenAsync("/?sample=65", names);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        names.Should().BeEmpty();

        await Page.Locator("#filter-q").FocusAsync();
        await Expect(Page.Locator("#filter-q")).ToBeFocusedAsync();
        await WaitForCountAsync(names, 1);
        await Page.Keyboard.TypeAsync("abc");
        await Page.Locator("#filter-q").BlurAsync();
        await Page.Locator("#filter-q").FocusAsync();
        await Task.Delay(500, TestContext.Current.CancellationToken);

        names.Should().ContainSingle();
        names.Single().Should().Contain("sample=65");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_title_fragment_lights_in_the_same_pass_as_the_keystroke()
    {
        var names = new ConcurrentQueue<string>();
        await OpenAsync("/?sample=65", names);
        var cards = await LoadCardsAsync();
        var baseGame = cards.First(card => !card.IsExpansion);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await Page.RouteAsync("**" + NamesRoute + "**", async route =>
        {
            await gate.Task;
            await route.ContinueAsync();
        });

        await Page.Locator("#filter-q").FocusAsync();
        await Page.Keyboard.TypeAsync(baseGame.Title);

        var box = Page.Locator($"{Boxes}[data-entry-id=\"{baseGame.EntryId}\"]:not([data-kind=\"moreMarker\"])").First;
        await Expect(box).Not.ToHaveAttributeAsync("data-dim", string.Empty);
        await Expect(Page.Locator(PanelCount)).ToHaveTextAsync(CountPattern());
        gate.SetResult();
    }

    [Fact]
    public async Task A_failing_names_request_leaves_title_search_working()
    {
        var names = new ConcurrentQueue<string>();
        await StartAndRouteAsync(names, abortNames: true);
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        await OpenFilterPanelAsync();
        var cards = await LoadCardsAsync();
        var baseGame = cards.First(card => !card.IsExpansion);

        await Page.Locator("#filter-q").FocusAsync();
        await Page.Keyboard.TypeAsync(baseGame.Title);

        var box = Page.Locator($"{Boxes}[data-entry-id=\"{baseGame.EntryId}\"]:not([data-kind=\"moreMarker\"])").First;
        await Expect(box).Not.ToHaveAttributeAsync("data-dim", string.Empty);
        await Expect(Page.Locator(PanelCount)).ToHaveTextAsync(CountPattern());
        await Page.Keyboard.TypeAsync(" zzz");
        await Expect(Page.Locator(PanelCount)).ToHaveTextAsync("No games match");
        await Task.Delay(500, TestContext.Current.CancellationToken);

        (await Page.Locator(".cabinet-message").CountAsync()).Should().Be(0);
        names.Should().ContainSingle();
        ConsoleErrors.Where(error => !error.Contains("Failed to load resource", StringComparison.Ordinal)).Should().BeEmpty();
    }

    [Fact]
    public async Task Typing_never_reaches_beyond_the_site()
    {
        var names = new ConcurrentQueue<string>();
        var requests = new ConcurrentQueue<string>();
        await OpenAsync("/?sample=65", names);
        Page.Request += (_, request) => requests.Enqueue(request.Url);

        await Page.Locator("#filter-q").FocusAsync();
        await WaitForCountAsync(names, 1);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        var afterTheNames = requests.Count;
        await Page.Keyboard.TypeAsync("de stille groeve");
        await Task.Delay(500, TestContext.Current.CancellationToken);

        requests.Should().HaveCount(afterTheNames);
        requests.Where(url => url.Contains(NamesRoute, StringComparison.Ordinal)).Should().ContainSingle();
        requests.Should().OnlyContain(url => url.StartsWith($"http://127.0.0.1:{PublicPort}/", StringComparison.Ordinal));
        RequestHosts.Should().OnlyContain(host => host == $"127.0.0.1:{PublicPort}");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_changed_collection_asks_for_the_names_again_on_the_next_use()
    {
        var bgg = new SyncRounds.SwitchableBgg(SyntheticBggCollection.Create(5));
        var clock = SyncHarness.NewClock();
        var factory = SyncHarness.CreateFactory(bgg.Handler, clock);
        var names = new ConcurrentQueue<string>();
        var layouts = new ConcurrentQueue<string>();
        await StartAsync(factory);
        Page.Request += (_, request) => Record(request.Url, names, layouts);
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        await Page.SetViewportSizeAsync(DockWidths.Push, 900);
        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        await OpenFilterPanelAsync();
        await Page.Locator("#filter-q").FocusAsync();
        await WaitForCountAsync(names, 1);
        await Page.Locator("#filter-q").BlurAsync();
        var layoutsBefore = layouts.Count;
        var boxesBefore = await Page.Locator(Boxes).CountAsync();

        bgg.Serve([.. SyntheticBggCollection.Create(5).Take(3)]);
        await SyncRounds.PressAndWait(client, clock);
        await WaitForCountAsync(layouts, layoutsBefore + 1);
        await Expect(Page.Locator(Boxes)).Not.ToHaveCountAsync(boxesBefore);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        names.Should().ContainSingle();

        await Page.Locator("#filter-q").FocusAsync();
        await Page.Keyboard.TypeAsync("a");

        await WaitForCountAsync(names, 2);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        names.Should().HaveCount(2);
    }

    private static void Record(string url, ConcurrentQueue<string> names, ConcurrentQueue<string> layouts)
    {
        if (url.Contains(NamesRoute, StringComparison.Ordinal))
        {
            names.Enqueue(url);
        }
        else if (url.Contains("/cabinet/layout", StringComparison.Ordinal))
        {
            layouts.Enqueue(url);
        }
    }

    private static string Plain(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var kept = decomposed.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark);

        return new string([.. kept]).Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    private static async Task WaitForCountAsync(ConcurrentQueue<string> queue, int expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);

        while (queue.Count < expected)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Saw {queue.Count} of {expected} requests.");
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }

    [GeneratedRegex(@"^\d+ of \d+ games?$")]
    private static partial Regex CountPattern();

    /// <summary>
    /// Opens the cabinet at the width where the filter panel stands beside it, and opens the panel with its three-line button, which
    /// leaves focus on the panel's head and so does not count as a use of the search field.
    /// </summary>
    private async Task OpenAsync(string path, ConcurrentQueue<string> names)
    {
        await StartAndRouteAsync(names, abortNames: false);
        await GotoCabinetAsync(path);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        await OpenFilterPanelAsync();
    }

    private async Task StartAndRouteAsync(ConcurrentQueue<string> names, bool abortNames)
    {
        await Page.SetViewportSizeAsync(DockWidths.Push, 900);
        await StartAsync(PrototypeOn);
        Page.Request += (_, request) => Record(request.Url, names, new ConcurrentQueue<string>());

        if (abortNames)
        {
            await Page.RouteAsync("**" + NamesRoute + "**", route => route.AbortAsync());
        }
    }

    private async Task<(CardData Card, string Title)> PickGameWithAnOtherTitleAsync()
    {
        var cards = await LoadCardsAsync();
        var response = await Page.APIRequest.GetAsync($"http://127.0.0.1:{PublicPort}{NamesAddress}");
        using var document = JsonDocument.Parse(await response.TextAsync());
        var json = document.RootElement.GetProperty("names");
        var drawn = (await Page.EvaluateAsync<string[]>(DrawnEntryIds)).ToHashSet(StringComparer.Ordinal);
        var owned = cards.Where(card => drawn.Contains(card.EntryId.ToString(CultureInfo.InvariantCulture))).ToList();

        foreach (var card in owned)
        {
            if (json.TryGetProperty(card.GameId.ToString(CultureInfo.InvariantCulture), out var list) && list.GetArrayLength() > 0)
            {
                var title = list[0].GetString()!;

                if (!owned.Any(other => Plain(other.Title).Contains(Plain(title), StringComparison.Ordinal)))
                {
                    return (card, title);
                }
            }
        }

        throw new InvalidOperationException("No drawn sample game lists another title.");
    }

    private async Task<IReadOnlyList<CardData>> LoadCardsAsync()
    {
        var json = await Page.EvaluateAsync<JsonElement>("async address => (await (await fetch(address)).json()).cards", CardsAddress);

        return JsonSerializer.Deserialize<List<CardData>>(json.GetRawText(), WebOptions)!;
    }

    private sealed record CardData(long EntryId, int GameId, string Title, bool IsExpansion);
}

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the list behind the cabinet says where each game is stored, holds only the matching entries while a
/// filter is on with the same number the bar shows, and in the by-location view lists the games in one group under a heading for every
/// location. The list never disagrees with the cabinet, even when the card data or the by-location payload cannot be had.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterListTests : CabinetPageTest
{
    private readonly TemporaryDirectory _storage = new();

    private const string Heading = "#games-list-title";
    private const string TopEntries = ".games-list-items > li";
    private const string GroupTitles = ".games-list-groups > h3";
    private const string GroupEntries = ".games-list-groups > ul > li";
    private const string Count = ".filter-count";
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex CountText = new(@"^(\d+) of (\d+) games?$");
    private static readonly Regex Phrase = new(", (stored in .+|unknown location)$");

    [Fact]
    public async Task The_list_shrinks_to_the_matches_with_the_bars_number()
    {
        await OpenSampleAsync(1440, 900);
        var cards = await LoadCardsAsync();
        var fragment = cards.First(card => !card.IsExpansion).Title;

        await Expect(Page.Locator(Heading)).ToHaveTextAsync("All games");
        var before = await EntryTextsAsync();
        before.Should().NotBeEmpty();
        before.Should().OnlyContain(text => Phrase.IsMatch(text), "the sample owner has locations");
        var total = await Page.Locator(TopEntries).CountAsync();

        await Page.Locator("#filter-q").FillAsync(fragment);

        await Expect(Page.Locator(Count)).ToHaveTextAsync(CountText);
        var counted = CountText.Match(await Page.Locator(Count).InnerTextAsync());
        var lit = int.Parse(counted.Groups[1].Value, CultureInfo.InvariantCulture);
        counted.Groups[2].Value.Should().Be(total.ToString(CultureInfo.InvariantCulture));
        await Expect(Page.Locator(Heading)).ToHaveTextAsync(counted.Value);
        await Expect(Page.Locator(TopEntries)).ToHaveCountAsync(lit);
        lit.Should().BeGreaterThan(0).And.BeLessThan(total);

        await Page.Locator(".filter-clear").ClickAsync();

        await Expect(Page.Locator(Heading)).ToHaveTextAsync("All games");
        await Expect(Page.Locator(TopEntries)).ToHaveCountAsync(total);

        await Page.Locator("#filter-q").FillAsync("zzzqqqxxx");

        await Expect(Page.Locator(Heading)).ToHaveTextAsync("No games match");
        await Expect(Page.Locator(TopEntries)).ToHaveCountAsync(0);
        await Page.Locator(".skip-link").FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Page.Locator(Heading)).ToBeFocusedAsync();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Without_owner_locations_no_entry_mentions_a_place()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        var host = await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(8));
        host.Cards.Should().NotBeEmpty();
        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.Locator(TopEntries).First).ToBeAttachedAsync();
        var texts = await EntryTextsAsync();

        texts.Should().NotBeEmpty();
        texts.Should().OnlyContain(text => !text.Contains("stored in", StringComparison.Ordinal) && !text.Contains("unknown location", StringComparison.Ordinal));
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Failed_card_data_keeps_the_layout_entries_and_filters_by_search_only()
    {
        await StartAsync(PrototypeOn);
        await StubNamesAsync();
        await FailCardDataRequestsAsync();
        await Page.SetViewportSizeAsync(1440, 900);

        await GotoCabinetAsync("/?sample=65");
        await Expect(Page.Locator(".filter-note")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 20000 });
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var drawn = await Page.Locator("#cabinet .placement:not([data-kind=\"moreMarker\"])").EvaluateAllAsync<string[][]>("(boxes) => boxes.map((box) => [box.dataset.entryId, box.getAttribute('aria-label')])");
        var labels = drawn.GroupBy(pair => pair[0]).ToDictionary(group => group.Key, group => group.First()[1]);
        var entries = await Page.Locator(".games-list-items button").EvaluateAllAsync<string[][]>("(buttons) => buttons.map((button) => [button.dataset.entryId, button.textContent])");

        entries.Should().HaveCount(labels.Count, "every drawn game is one entry");
        entries.Should().OnlyContain(pair => labels[pair[0]] == pair[1], "each entry carries the words its box carries");
        entries.Should().OnlyContain(pair => !Phrase.IsMatch(pair[1]));
        var fragment = entries.Select(pair => pair[1]).First(text => !text.Contains(',', StringComparison.Ordinal));

        await Page.Locator("#filter-q").FillAsync(fragment);

        await Expect(Page.Locator(Count)).ToHaveTextAsync(CountText);
        var counted = CountText.Match(await Page.Locator(Count).InnerTextAsync());
        var lit = int.Parse(counted.Groups[1].Value, CultureInfo.InvariantCulture);
        await Expect(Page.Locator(Heading)).ToHaveTextAsync(counted.Value);
        await Expect(Page.Locator(TopEntries)).ToHaveCountAsync(lit);
        lit.Should().BeGreaterThan(0).And.BeLessThan(entries.Length);
        (await Page.Locator(".filter-group input").EvaluateAllAsync<bool[]>("(inputs) => inputs.map((input) => input.disabled)")).Should().OnlyContain(disabled => disabled);
        (await Page.Locator("#filter-loc").IsDisabledAsync()).Should().BeTrue();
        ConsoleErrors.Should().OnlyContain(error => error.Contains("Failed to load resource", StringComparison.Ordinal), "only the requests made to fail are logged");
    }

    [Fact]
    public async Task By_location_groups_the_list_and_follows_the_filters()
    {
        await OpenSampleAsync(1440, 900);
        var document = await LoadDocumentAsync();
        var groups = ExpectedGroups(document);
        var apart = FindExpansionStoredApart(document);
        apart.Should().NotBeNull("the sample holds an expansion stored away from every base game it expands");
        groups.Should().HaveCountGreaterThan(1);

        await ChooseViewAsync(1);

        await Expect(Page.Locator(GroupTitles)).ToHaveTextAsync(groups.Select(group => group.Heading).ToArray());
        await Expect(Page.Locator(Heading)).ToHaveTextAsync("All games");
        var shown = await ReadGroupsAsync();

        shown.Select(group => group.Entries.Count).Should().Equal(groups.Select(group => group.Total));
        groups[^1].Heading.Should().StartWith("Unknown location");
        var place = document.Locations!.First(candidate => candidate.Id == apart!.LocationId);
        var home = shown.Single(group => group.Title.StartsWith(place.Name + ", ", StringComparison.Ordinal));
        var line = home.Entries.Single(entry => entry.Id == apart!.EntryId.ToString(CultureInfo.InvariantCulture));
        line.Text.Should().Contain("expansion for " + apart!.Bases[0].Title);

        const int players = 4;
        await Page.Locator($"fieldset[data-filter=\"players\"] input[value=\"{players.ToString(CultureInfo.InvariantCulture)}\"]").ClickAsync();

        await Expect(Page.Locator(Count)).ToHaveTextAsync(CountText);
        var counted = CountText.Match(await Page.Locator(Count).InnerTextAsync());
        var lit = int.Parse(counted.Groups[1].Value, CultureInfo.InvariantCulture);
        await Expect(Page.Locator(Heading)).ToHaveTextAsync(counted.Value);
        await Expect(Page.Locator(GroupEntries)).ToHaveCountAsync(lit);
        var filtered = await ReadGroupsAsync();

        filtered.Should().NotBeEmpty();
        filtered.Count.Should().BeLessThan(groups.Count);
        filtered.Sum(group => group.Entries.Count).Should().Be(lit);
        filtered.Should().OnlyContain(group => group.Title.EndsWith(group.Entries.Count == 1 ? ", 1 game" : $", {group.Entries.Count} games", StringComparison.Ordinal));

        await Page.Locator(".filter-clear").ClickAsync();

        await Expect(Page.Locator(Heading)).ToHaveTextAsync("All games");
        await Expect(Page.Locator(GroupTitles)).ToHaveTextAsync(groups.Select(group => group.Heading).ToArray());
        await Expect(Page.Locator("input[name=\"filter-view\"][value=\"location\"]")).ToBeCheckedAsync();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Many_locations_each_get_a_list_group()
    {
        FilterFixture.WriteManyLocations(_storage.FullPath, 14);
        await Page.SetViewportSizeAsync(1280, 900);
        await OwnerHost.StoredAsync(factory => StartAsync(factory), _storage.FullPath);
        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var names = Enumerable.Range(0, 14).Select(index => $"Cabinet {(char)('A' + index)}").ToArray();

        await ExpectViewSwitchAvailableAsync();
        await ChooseViewAsync(1);

        await Expect(Page.Locator(GroupTitles)).ToHaveTextAsync(names.Select(name => $"{name}, 2 games").ToArray());
        (await Page.Locator(GroupTitles).EvaluateAllAsync<string[]>("(titles) => titles.map((title) => title.nextElementSibling?.tagName ?? '')")).Should().OnlyContain(tag => tag == "UL");
        (await Page.Locator(GroupTitles).EvaluateAllAsync<int[]>("(titles) => titles.map((title) => title.nextElementSibling.querySelectorAll(':scope > li').length)")).Should().OnlyContain(count => count > 0);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Switching_back_restores_the_single_list()
    {
        await OpenSampleAsync(1440, 900);
        var total = await Page.Locator(TopEntries).CountAsync();
        await ChooseViewAsync(1);
        await Expect(Page.Locator(GroupTitles).First).ToBeAttachedAsync();
        await Expect(Page.Locator(".games-list-items")).ToBeHiddenAsync();

        await ChooseViewAsync(0);

        await Expect(Page.Locator(GroupTitles)).ToHaveCountAsync(0);
        await Expect(Page.Locator(".games-list-items")).Not.ToBeHiddenAsync();
        await Expect(Page.Locator(TopEntries)).ToHaveCountAsync(total);

        await ChooseViewAsync(1);
        await Expect(Page.Locator(GroupTitles).First).ToBeAttachedAsync();
        await Page.Locator(".skip-link").FocusAsync();
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#games-list-start")).ToBeFocusedAsync();
        var panel = await Page.Locator(".games-list").BoundingBoxAsync();
        panel!.Width.Should().BeGreaterThan(100);
        var rows = await Page.Locator(".games-list-groups button").First.BoundingBoxAsync();
        rows!.Height.Should().BeGreaterThanOrEqualTo(44);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_long_location_name_wraps_inside_the_panel()
    {
        await Page.SetViewportSizeAsync(390, 844);
        var host = await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(6));
        var longName = string.Concat(Enumerable.Repeat("Lengthy", 8));
        var places = await host.SeedAsync(longName, "Shelf B");
        var games = host.Cards.DistinctBy(card => card.GameId).Take(2).ToList();
        await host.PlaceAsync(games[0], places[0]);
        await host.PlaceAsync(games[1], places[1]);
        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await ExpectViewSwitchAvailableAsync();
        await ChooseViewAsync(1);
        await Expect(Page.Locator(GroupTitles).First).ToContainTextAsync(longName);

        await Page.Locator(".skip-link").FocusAsync();
        await Page.Keyboard.PressAsync("Enter");

        await Expect(Page.Locator("#games-list-start")).ToBeFocusedAsync();
        var title = await Page.Locator(GroupTitles).First.BoundingBoxAsync();
        var panel = await Page.Locator(".games-list").BoundingBoxAsync();
        title!.Width.Should().BeLessThanOrEqualTo(panel!.Width);
        title.Height.Should().BeGreaterThan(56, "the long name wraps over more than one line");
        (await Page.Locator(".games-list").EvaluateAsync<bool>("(panel) => panel.scrollWidth <= panel.clientWidth")).Should().BeTrue();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_failed_location_view_leaves_the_plain_list()
    {
        await StartAsync(PrototypeOn);
        await StubNamesAsync();
        await Page.RouteAsync("**/cabinet/layout-by-location**", route => route.AbortAsync());
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        var total = await Page.Locator(TopEntries).CountAsync();

        await ChooseViewAsync(1);

        await Expect(Page.Locator("input[name=\"filter-view\"][value=\"all\"]")).ToBeCheckedAsync();
        await Expect(Page.Locator("#cabinet section.location-group")).ToHaveCountAsync(0);
        await Expect(Page.Locator(Heading)).ToHaveTextAsync("All games");
        await Expect(Page.Locator(TopEntries)).ToHaveCountAsync(total);
        await Expect(Page.Locator(GroupTitles)).ToHaveCountAsync(0);

        await Page.Locator("fieldset[data-filter=\"players\"] input[value=\"4\"]").ClickAsync();

        await Expect(Page.Locator(Count)).ToHaveTextAsync(CountText);
        var counted = CountText.Match(await Page.Locator(Count).InnerTextAsync());
        await Expect(Page.Locator(Heading)).ToHaveTextAsync(counted.Value);
        await Expect(Page.Locator(TopEntries)).ToHaveCountAsync(int.Parse(counted.Groups[1].Value, CultureInfo.InvariantCulture));
        await Expect(Page.Locator(GroupTitles)).ToHaveCountAsync(0);
        ConsoleErrors.Should().OnlyContain(error => error.Contains("Failed to load resource", StringComparison.Ordinal), "only the request made to fail is logged");
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task StubNamesAsync() =>
        await Page.RouteAsync("**/cabinet/search-names**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "application/json",
            Body = "{\"names\":{}}",
        }));

    private async Task OpenSampleAsync(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await StubNamesAsync();
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
    }

    private async Task<List<string>> EntryTextsAsync() =>
        [.. await Page.Locator(".games-list-items button").EvaluateAllAsync<string[]>("(buttons) => buttons.map((button) => button.textContent)")];

    private async Task<List<ShownGroup>> ReadGroupsAsync() =>
        await ReadAsync<List<ShownGroup>>("""
            () => [...document.querySelectorAll('.games-list-groups > h3')].map((title) => ({
              title: title.textContent,
              entries: [...title.nextElementSibling.querySelectorAll(':scope > li')].map((item) => ({ id: item.querySelector('button').dataset.entryId, text: item.querySelector('button').textContent })),
            }))
            """);

    private async Task<T> ReadAsync<T>(string script)
    {
        var json = await Page.EvaluateAsync<JsonElement>(script);

        return JsonSerializer.Deserialize<T>(json.GetRawText(), WebOptions)!;
    }

    private async Task<List<ViewCard>> LoadCardsAsync() => (await LoadDocumentAsync()).Cards;

    private async Task<ViewDocument> LoadDocumentAsync()
    {
        var json = await Page.EvaluateAsync<JsonElement>("async address => await (await fetch(address)).json()", "/cabinet/cards?profile=desktop&sample=65");

        return JsonSerializer.Deserialize<ViewDocument>(json.GetRawText(), WebOptions)!;
    }

    private static ViewCard? FindExpansionStoredApart(ViewDocument document)
    {
        var byEntry = document.Cards.ToDictionary(card => card.EntryId);

        return document.Cards.FirstOrDefault(card =>
            card.IsExpansion
            && card.LocationId is not null
            && card.Bases.Count > 0
            && card.Bases[0].EntryId is not null
            && card.Bases.Where(link => link.EntryId is not null && byEntry.ContainsKey(link.EntryId.Value)).All(link => byEntry[link.EntryId!.Value].LocationId != card.LocationId));
    }

    private static List<ExpectedGroup> ExpectedGroups(ViewDocument document)
    {
        var groups = new List<ExpectedGroup>();

        foreach (var place in (document.Locations ?? []).Where(place => place.Games > 0))
        {
            groups.Add(new ExpectedGroup(place.Name, TopLevel(document.Cards.Where(card => card.LocationId == place.Id).ToList()), false));
        }

        var unplaced = document.Cards.Where(card => card.LocationId is null).ToList();

        if (unplaced.Count > 0)
        {
            groups.Add(new ExpectedGroup("Unknown location", TopLevel(unplaced), true));
        }

        return groups;
    }

    private static int TopLevel(List<ViewCard> cards)
    {
        var inGroup = cards.Select(card => card.EntryId).ToHashSet();
        var nested = cards
            .Where(card => !card.IsExpansion)
            .SelectMany(card => card.Expansions)
            .Where(link => link.EntryId is not null && inGroup.Contains(link.EntryId.Value))
            .Select(link => link.EntryId!.Value)
            .ToHashSet();

        return cards.Count(card => !nested.Contains(card.EntryId));
    }

    private sealed record ViewLink(long? EntryId, string? Title);

    private sealed record ViewCard(long EntryId, string Title, bool IsExpansion, string? LocationId, List<ViewLink> Expansions, List<ViewLink> Bases);

    private sealed record ViewPlace(string Id, string Name, int Games);

    private sealed record ViewDocument(List<ViewCard> Cards, List<ViewPlace>? Locations = null);

    private sealed record ExpectedGroup(string Name, int Total, bool Unknown)
    {
        public string Heading => $"{Name}, {(Total == 1 ? "1 game" : Total.ToString(CultureInfo.InvariantCulture) + " games")}";
    }

    private sealed record ShownEntry(string Id, string Text);

    private sealed record ShownGroup(string Title, List<ShownEntry> Entries);
}

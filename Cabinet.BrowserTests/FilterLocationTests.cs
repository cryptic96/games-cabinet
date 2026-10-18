using System.Globalization;
using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the location choice puts every box that is not stored in the chosen place into shadow where it
/// stands: each box is judged by its own place, so an expansion kept apart from its base lights while the base dims; the list holds
/// the owner's places that hold a game in the owner's order and ends with the unknown choice; the choice combines with the players
/// chips; two copies of one game each answer for their own place and show it on their card; and with no owner places the control
/// is not there, even in the open panel. The tests run at the width where the open filter panel stands beside the cabinet, so the
/// panel is opened first and the boxes stay reachable while it is open.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterLocationTests : CabinetPageTest
{
    private const string Boxes = "#cabinet .placement";
    private const string Dimmed = "#cabinet .placement[data-dim]";
    private const string Select = "#filter-loc";
    private const string OpenCard = "dialog.card-dialog[open]";
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private readonly TemporaryDirectory _storage = new();

    [Fact]
    public async Task Choosing_a_place_lights_exactly_what_stands_there()
    {
        await StartAsync(PrototypeOn);
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var document = await LoadDocumentAsync("/cabinet/cards?profile=desktop&sample=65");
        var offered = (document.Locations ?? []).Where(place => place.Games > 0).ToList();
        var all = await BoxIdsAsync(Boxes);
        await OpenFilterPanelAsync();

        await Expect(Page.Locator(Select)).ToBeVisibleAsync();
        await Expect(Page.Locator("label[for=\"filter-loc\"]")).ToHaveTextAsync("Location");
        var expectedNames = new List<string> { "Any location" };
        expectedNames.AddRange(offered.Select(place => place.Name));
        if (document.Cards.Any(card => card.LocationId is null))
        {
            expectedNames.Add("Unknown location");
        }

        offered.Should().NotBeEmpty();
        (await OptionTextsAsync()).Should().Equal(expectedNames);
        (await Page.Locator($"{Select} option").EvaluateAllAsync<string[]>("(options) => options.map((option) => option.dir)")).Should().OnlyContain(direction => direction == "auto");
        var rectanglesBefore = await Page.EvaluateAsync<string[]>(BoxRectangles);

        var apart = FindExpansionKeptApart(document.Cards);
        apart.Should().NotBeNull("the sample holds an expansion stored in a different place from its base");

        foreach (var place in offered)
        {
            await Page.Locator(Select).SelectOptionAsync(place.Id);

            var lit = document.Cards.Where(card => card.LocationId == place.Id).Select(card => Id(card)).ToHashSet();
            await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(all.Count(id => !lit.Contains(id)));
            (await BoxIdsAsync(Dimmed)).Should().BeEquivalentTo(all.Where(id => !lit.Contains(id)), place.Name);
        }

        await Page.Locator(Select).SelectOptionAsync(apart!.Expansion.LocationId!);
        var dimmed = await BoxIdsAsync(Dimmed);
        dimmed.Should().NotContain(Id(apart.Expansion));
        dimmed.Should().Contain(Id(apart.Base));
        (await Page.EvaluateAsync<string[]>(BoxRectangles)).Should().Equal(rectanglesBefore);

        await Page.Locator(Select).SelectOptionAsync(string.Empty);
        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(0);
        await SaveScreenshotAsync("filter-location-sample");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Unknown_lights_only_unplaced_boxes_and_location_ands_with_players()
    {
        await StartAsync(PrototypeOn);
        await Page.SetViewportSizeAsync(1440, 900);
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var document = await LoadDocumentAsync("/cabinet/cards?profile=desktop&sample=65");
        var cards = await LoadFamilyCardsAsync("/cabinet/cards?profile=desktop&sample=65");
        var all = await BoxIdsAsync(Boxes);
        var unplaced = document.Cards.Where(card => card.LocationId is null).Select(card => Id(card)).ToHashSet();

        unplaced.Should().NotBeEmpty();
        unplaced.Count.Should().BeLessThan(all.Count);
        await OpenFilterPanelAsync();

        await Page.Locator(Select).SelectOptionAsync("unknown");

        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(all.Count(id => !unplaced.Contains(id)));
        (await BoxIdsAsync(Dimmed)).Should().BeEquivalentTo(all.Where(id => !unplaced.Contains(id)));

        var chosen = Enumerable.Range(1, 6).First(count => FilterBarTests.ExpectedLit(cards, count, null).Intersect(unplaced).Any()
            && !FilterBarTests.ExpectedLit(cards, count, null).Intersect(unplaced).SequenceEqual(unplaced));
        var both = FilterBarTests.ExpectedLit(cards, chosen, null).Intersect(unplaced).ToHashSet();

        await Page.Locator($"fieldset[data-filter=\"players\"] input[value=\"{chosen.ToString(CultureInfo.InvariantCulture)}\"]").ClickAsync();

        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(all.Count(id => !both.Contains(id)));
        (await BoxIdsAsync(Dimmed)).Should().BeEquivalentTo(all.Where(id => !both.Contains(id)));

        await Page.Locator(".filter-clear").ClickAsync();

        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(0);
        (await Page.Locator(Select).InputValueAsync()).Should().BeEmpty();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Without_owner_locations_there_is_no_location_control()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        var host = await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(5));
        host.Cards.Should().NotBeEmpty();

        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await OpenFilterPanelAsync();

        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        await Expect(Page.Locator(Select)).ToBeHiddenAsync();
        await Expect(Page.Locator(".filter-bar select:visible, .filter-bar label[for=\"filter-loc\"]:visible")).ToHaveCountAsync(0);
        (await Page.Locator(Select).EvaluateAsync<bool>("(select) => select.hidden")).Should().BeTrue();
        (await LoadDocumentAsync("/cabinet/cards?profile=desktop")).Locations.Should().BeNullOrEmpty();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_control_appears_with_the_first_owner_location_and_leaves_with_the_last()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        var host = await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(5));
        var ids = await host.SeedAsync("Cabinet A");
        await host.PlaceAsync(host.Cards[0], ids[0]);

        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await OpenFilterPanelAsync();

        await Expect(Page.Locator(Select)).ToBeVisibleAsync();
        (await OptionTextsAsync()).Should().Equal("Any location", "Cabinet A", "Unknown location");

        await host.DeleteLocationAsync(ids[0]);
        await Page.ReloadAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await OpenFilterPanelAsync();

        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        await Expect(Page.Locator(Select)).ToBeHiddenAsync();
    }

    [Fact]
    public async Task Two_copies_in_two_places_each_show_their_own_place()
    {
        FilterFixture.Write(_storage.FullPath);
        await Page.SetViewportSizeAsync(1440, 900);
        await OwnerHost.StoredAsync(factory => StartAsync(factory), _storage.FullPath);
        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var all = await BoxIdsAsync(Boxes);
        var copyOne = Entry(FilterFixture.QuarryEntries[0]);
        var copyTwo = Entry(FilterFixture.QuarryEntries[1]);
        var copyThree = Entry(FilterFixture.QuarryEntries[2]);
        var cabinetA = new[] { copyOne, copyThree, Entry(FilterFixture.HarbourEntry), Entry(FilterFixture.TallyEntry), Entry(FilterFixture.TallyExpansionEntry) };
        var shelfB = new[] { copyTwo, Entry(FilterFixture.HarbourExpansionEntry), Entry(FilterFixture.KettleEntry) };
        await OpenFilterPanelAsync();

        await Expect(Page.Locator(Select)).ToBeVisibleAsync();
        (await OptionTextsAsync()).Should().Equal("Any location", "Cabinet A", "Shelf B", "Unknown location");

        await Page.Locator(Select).SelectOptionAsync(FilterFixture.CabinetAId);

        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(all.Count(id => !cabinetA.Contains(id)));
        (await BoxIdsAsync(Dimmed)).Should().BeEquivalentTo(all.Where(id => !cabinetA.Contains(id)));

        await Page.Locator(Select).SelectOptionAsync(FilterFixture.ShelfBId);

        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(all.Count(id => !shelfB.Contains(id)));
        (await BoxIdsAsync(Dimmed)).Should().BeEquivalentTo(all.Where(id => !shelfB.Contains(id)));
        (await BoxIdsAsync(Dimmed)).Should().Contain(Entry(FilterFixture.HarbourEntry), "the base stays in Cabinet A while its expansion is on Shelf B");
        (await BoxIdsAsync(Dimmed)).Should().NotContain(Entry(FilterFixture.HarbourExpansionEntry));

        await Page.Locator(Select).SelectOptionAsync(string.Empty);
        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(0);

        await AssertStoredInAsync(copyOne, "Cabinet A");
        await AssertStoredInAsync(copyTwo, "Shelf B");
        await AssertStoredInAsync(copyThree, "Cabinet A");
        await AssertStoredInAsync(Entry(FilterFixture.TallyExpansionEntry), "Cabinet A");
        await AssertStoredInAsync(Entry(FilterFixture.MysteryEntry), null);
        await AssertStoredInAsync(Entry(FilterFixture.MeadowEntry), null);
        ConsoleErrors.Should().BeEmpty();
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private const string BoxRectangles = "() => [...document.querySelectorAll('#cabinet .placement')].map((box) => { const r = box.getBoundingClientRect(); return [box.dataset.entryId, box.dataset.kind, r.left, r.top, r.width, r.height].join(':'); })";

    private static string Entry(long entryId) => entryId.ToString(CultureInfo.InvariantCulture);

    private static string Id(PlaceCard card) => Entry(card.EntryId);

    private static ExpansionApart? FindExpansionKeptApart(IReadOnlyList<PlaceCard> cards)
    {
        var byEntry = cards.ToDictionary(card => card.EntryId);

        foreach (var card in cards.Where(card => card.IsExpansion && card.LocationId is not null))
        {
            foreach (var link in card.Bases.Where(link => link.EntryId is not null && byEntry.ContainsKey(link.EntryId.Value)))
            {
                var baseCard = byEntry[link.EntryId!.Value];

                if (baseCard.LocationId is not null && baseCard.LocationId != card.LocationId)
                {
                    return new ExpansionApart(baseCard, card);
                }
            }
        }

        return null;
    }

    private async Task AssertStoredInAsync(string entryId, string? place)
    {
        await Page.Locator($"{Boxes}[data-entry-id=\"{entryId}\"]").First.ClickAsync();
        await Expect(Page.Locator(OpenCard)).ToBeVisibleAsync();

        if (place is null)
        {
            await Expect(Page.Locator($"{OpenCard} .card-where")).ToHaveCountAsync(0);
        }
        else
        {
            await Expect(Page.Locator($"{OpenCard} .card-where dt")).ToHaveTextAsync("Stored in");
            await Expect(Page.Locator($"{OpenCard} .card-where dd")).ToHaveTextAsync(place);
        }

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0);
        await WaitUntilTheCardStepIsGoneAsync();
    }

    private async Task<List<string>> BoxIdsAsync(string selector) =>
        [.. await Page.Locator(selector).EvaluateAllAsync<string[]>("(boxes) => boxes.map((box) => box.dataset.entryId)")];

    private async Task<string[]> OptionTextsAsync() =>
        await Page.Locator($"{Select} option").EvaluateAllAsync<string[]>("(options) => options.map((option) => option.textContent)");

    private async Task<PlaceDocument> LoadDocumentAsync(string address)
    {
        var json = await Page.EvaluateAsync<JsonElement>("async address => await (await fetch(address)).json()", address);

        return JsonSerializer.Deserialize<PlaceDocument>(json.GetRawText(), WebOptions)!;
    }

    private async Task<IReadOnlyList<FilterBarTests.CardData>> LoadFamilyCardsAsync(string address)
    {
        var json = await Page.EvaluateAsync<JsonElement>("async address => (await (await fetch(address)).json()).cards", address);

        return JsonSerializer.Deserialize<List<FilterBarTests.CardData>>(json.GetRawText(), WebOptions)!;
    }

    private sealed record PlaceLink(long? EntryId);

    private sealed record PlaceCard(long EntryId, bool IsExpansion, string? LocationId, string? Location, List<PlaceLink> Bases);

    private sealed record PlaceRef(string Id, string Name, int Games);

    private sealed record PlaceDocument(List<PlaceCard> Cards, List<PlaceRef>? Locations = null);

    private sealed record ExpansionApart(PlaceCard Base, PlaceCard Expansion);
}

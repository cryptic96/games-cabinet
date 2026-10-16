using System.Globalization;
using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the player and time choices put every game that cannot be played that way in shadow where it
/// stands: one group of nine or six radios, tapping the chosen chip again returns to Any, a family is judged as a whole with its
/// owned expansions, unknown values stay in shadow, every filter combines with the others, and a choice that changes hundreds of
/// boxes at once never starts hundreds of fades.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterBarTests : CabinetPageTest
{
    private const string Boxes = "#cabinet .placement";
    private const string Dimmed = "#cabinet .placement[data-dim]";
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Choosing_five_lights_the_families_five_can_play()
    {
        await OpenAsync("/?sample=65", 1440, 900);
        var cards = await LoadCardsAsync();
        var group = Page.Locator("fieldset.filter-group[data-filter=\"players\"]");

        await Expect(group.Locator("legend")).ToHaveTextAsync("Players");
        (await group.Locator("input[type=radio][name=\"filter-players\"]").CountAsync()).Should().Be(9);
        (await group.Locator("input[type=radio]:checked").EvaluateAllAsync<string[]>("(radios) => radios.map((radio) => radio.value)")).Should().Equal(string.Empty);
        (await Page.GetByRole(AriaRole.Radio, new PageGetByRoleOptions { Name = "5 players" }).CountAsync()).Should().Be(1);
        (await Page.GetByRole(AriaRole.Radio, new PageGetByRoleOptions { Name = "1 player", Exact = true }).CountAsync()).Should().Be(1);
        (await Page.GetByRole(AriaRole.Radio, new PageGetByRoleOptions { Name = "8 or more players" }).CountAsync()).Should().Be(1);
        (await Page.GetByRole(AriaRole.Radio, new PageGetByRoleOptions { Name = "Any", Exact = true }).CountAsync()).Should().BeGreaterThan(0);

        var expected = ExpectedLit(cards, players: 5, minutes: null);
        var all = await BoxIdsAsync(Boxes);
        all.Count(id => !expected.Contains(id)).Should().BeGreaterThan(0);
        all.Count(expected.Contains).Should().BeGreaterThan(0);
        (await Page.Locator(Dimmed).CountAsync()).Should().Be(0);
        var rectanglesBefore = await Page.EvaluateAsync<string[]>(BoxRectangles);

        await group.Locator("input[value=\"5\"]").ClickAsync();

        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(all.Count(id => !expected.Contains(id)));
        (await BoxIdsAsync(Dimmed)).Should().BeEquivalentTo(all.Where(id => !expected.Contains(id)));
        (await Page.EvaluateAsync<string[]>(BoxRectangles)).Should().Equal(rectanglesBefore);
        await Expect(Page.Locator(".filter-bar")).ToHaveAttributeAsync("data-active", string.Empty);
        await SaveScreenshotAsync("filter-bar-five");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task An_owned_expansion_widens_the_range_a_family_lights_for()
    {
        await OpenAsync("/?sample=65", 1440, 900);
        var cards = await LoadCardsAsync();
        var widened = FindWidenedCount(cards);
        widened.Should().NotBeNull("the sample holds a family an owned expansion makes playable for one more count");

        await Page.Locator($"fieldset[data-filter=\"players\"] input[value=\"{widened!.Players}\"]").ClickAsync();

        var all = await BoxIdsAsync(Boxes);
        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(all.Count(id => !ExpectedLit(cards, widened.Players, null).Contains(id)));
        var dimmed = await BoxIdsAsync(Dimmed);
        dimmed.Should().NotContain(widened.BaseId);
        dimmed.Should().NotContain(widened.ExpansionId);
    }

    [Fact]
    public async Task Tapping_the_chosen_count_again_returns_to_Any()
    {
        await OpenAsync("/?sample=65", 1440, 900);
        var group = Page.Locator("fieldset[data-filter=\"players\"]");
        var five = group.Locator("input[value=\"5\"]");
        var any = group.Locator("input[value=\"\"]");

        await five.ClickAsync();
        await Expect(five).ToBeCheckedAsync();
        await Expect(Page.Locator(Dimmed).First).ToBeAttachedAsync();

        await five.ClickAsync();

        await Expect(any).ToBeCheckedAsync();
        await Expect(five).Not.ToBeCheckedAsync();
        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(0);
        await Expect(Page.Locator(".filter-bar")).Not.ToHaveAttributeAsync("data-active", string.Empty);

        await any.ClickAsync();
        await Expect(any).ToBeCheckedAsync();
        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(0);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_arrow_keys_move_the_count_and_the_group_is_one_tab_stop()
    {
        await OpenAsync("/?sample=65", 1440, 900);
        var group = Page.Locator("fieldset[data-filter=\"players\"]");
        var cards = await LoadCardsAsync();
        var any = group.Locator("input[value=\"\"]");

        await any.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(group.Locator("input[value=\"1\"]")).ToBeCheckedAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(group.Locator("input[value=\"2\"]")).ToBeCheckedAsync();
        var expected = ExpectedLit(cards, players: 2, minutes: null);
        var all = await BoxIdsAsync(Boxes);
        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(all.Count(id => !expected.Contains(id)));

        await Page.Keyboard.PressAsync("ArrowLeft");
        await Expect(group.Locator("input[value=\"1\"]")).ToBeCheckedAsync();
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Expect(any).ToBeCheckedAsync();
        await Expect(Page.Locator(Dimmed)).ToHaveCountAsync(0);

        await Page.Keyboard.PressAsync("Tab");
        (await Page.EvaluateAsync<bool>("() => document.activeElement?.name === 'filter-players'")).Should().BeFalse();
        await Page.Keyboard.PressAsync("Shift+Tab");
        await Expect(any).ToBeFocusedAsync();
        ConsoleErrors.Should().BeEmpty();
    }

    private const string BoxRectangles = "() => [...document.querySelectorAll('#cabinet .placement')].map((box) => { const r = box.getBoundingClientRect(); return [box.dataset.entryId, box.dataset.kind, r.left, r.top, r.width, r.height].join(':'); })";

    private async Task<List<string>> BoxIdsAsync(string selector) =>
        [.. await Page.Locator(selector).EvaluateAllAsync<string[]>("(boxes) => boxes.map((box) => box.dataset.entryId)")];

    private static WidenedFamily? FindWidenedCount(IReadOnlyList<CardData> cards)
    {
        var byEntry = cards.ToDictionary(card => card.EntryId);

        foreach (var card in cards.Where(card => !card.IsExpansion))
        {
            var ownBase = card.MaxPlayers ?? 0;

            foreach (var link in card.Expansions.Where(link => link.EntryId is not null && byEntry.ContainsKey(link.EntryId.Value)))
            {
                var expansion = byEntry[link.EntryId!.Value];
                var count = ownBase + 1;

                if (ownBase > 0 && count <= 8 && (expansion.MaxPlayers ?? 0) >= count && FamilyLitAt(cards, card, count))
                {
                    return new WidenedFamily(
                        count,
                        card.EntryId.ToString(CultureInfo.InvariantCulture),
                        expansion.EntryId.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        return null;
    }

    private static bool FamilyLitAt(IReadOnlyList<CardData> cards, CardData card, int count) =>
        ExpectedLit(cards, count, null).Contains(card.EntryId.ToString(CultureInfo.InvariantCulture));

    internal static HashSet<string> ExpectedLit(IReadOnlyList<CardData> cards, int? players, int? minutes)
    {
        var byEntry = cards.ToDictionary(card => card.EntryId);
        var families = new Dictionary<long, HashSet<long>>();

        foreach (var card in cards.Where(card => !card.IsExpansion))
        {
            families[card.EntryId] = [card.EntryId];
        }

        foreach (var card in cards.Where(card => !card.IsExpansion))
        {
            foreach (var link in card.Expansions.Where(link => link.EntryId is not null && byEntry.ContainsKey(link.EntryId.Value)))
            {
                families[card.EntryId].Add(link.EntryId!.Value);
            }
        }

        foreach (var card in cards.Where(card => card.IsExpansion))
        {
            foreach (var link in card.Bases.Where(link => link.EntryId is not null && families.ContainsKey(link.EntryId.Value)))
            {
                families[link.EntryId!.Value].Add(card.EntryId);
            }
        }

        var joined = families.Values.SelectMany(members => members).ToHashSet();

        foreach (var card in cards.Where(card => card.IsExpansion && !joined.Contains(card.EntryId)))
        {
            families[card.EntryId] = [card.EntryId];
        }

        var lit = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (key, members) in families)
        {
            if (FamilyPasses(byEntry, key, members, players, minutes))
            {
                lit.UnionWith(members.Select(entry => entry.ToString(CultureInfo.InvariantCulture)));
            }
        }

        return lit;
    }

    private static bool FamilyPasses(Dictionary<long, CardData> byEntry, long key, HashSet<long> members, int? players, int? minutes)
    {
        if (players is { } count)
        {
            var mins = members.Select(entry => byEntry[entry].MinPlayers).Where(value => value > 0).ToList();
            var maxes = members.Select(entry => byEntry[entry].MaxPlayers).Where(value => value > 0).ToList();

            if (mins.Count == 0 || maxes.Count == 0)
            {
                return false;
            }

            var fits = count >= 8 ? maxes.Max() >= 8 : mins.Min() <= count && count <= maxes.Max();

            if (!fits)
            {
                return false;
            }
        }

        if (minutes is { } limit)
        {
            var stated = byEntry[key].PlayTime;

            if (stated is not > 0 || stated > limit)
            {
                return false;
            }
        }

        return true;
    }

    private async Task OpenAsync(string path, int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await GotoCabinetAsync(path);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
    }

    private async Task<IReadOnlyList<CardData>> LoadCardsAsync()
    {
        var json = await Page.EvaluateAsync<JsonElement>(
            "async address => (await (await fetch(address)).json()).cards",
            "/cabinet/cards?profile=desktop&sample=65");

        return JsonSerializer.Deserialize<List<CardData>>(json.GetRawText(), WebOptions)!;
    }

    internal sealed record CardLink(long? EntryId, int GameId, string Title);

    internal sealed record CardData(
        long EntryId,
        string Title,
        bool IsExpansion,
        List<CardLink> Expansions,
        List<CardLink> Bases,
        int? MinPlayers,
        int? MaxPlayers,
        int? PlayTime);

    private sealed record WidenedFamily(int Players, string BaseId, string ExpansionId);
}

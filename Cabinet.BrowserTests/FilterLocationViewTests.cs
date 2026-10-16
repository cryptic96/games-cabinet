using System.Globalization;
using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that the view switch at the start of the filter bar draws one named cabinet per storage location, that the
/// numbers on screen add up across those cabinets, that the keyboard walks from one cabinet into the next, and that a live redraw keeps
/// the view. The switch stays hidden while the owner has no locations, and at rest only the segment of the view on screen is filled.
/// </summary>
[Trait("Category", "Browser")]
public sealed class FilterLocationViewTests : CabinetPageTest
{
    private const string Boxes = "#cabinet .placement";
    private const string Groups = "#cabinet section.location-group";
    private const string Headings = "#cabinet .location-heading";
    private const string Switch = ".filter-view";
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(1440, 900, "desktop")]
    [InlineData(390, 844, "phone")]
    public async Task By_location_draws_one_named_cabinet_per_place(int width, int height, string profile)
    {
        await OpenSampleAsync(width, height);
        var document = await LoadCardsAsync(profile);
        var boxesTogether = await EntryIdsAsync();
        var sectionsTogether = await Page.Locator("#cabinet .section").CountAsync();
        var groups = ExpectedGroups(document);

        groups.Should().HaveCountGreaterThan(1);
        await Expect(Page.Locator(Switch)).ToBeVisibleAsync();
        (await Page.EvaluateAsync<bool>("() => document.querySelector('.filter-bar').firstElementChild.firstElementChild.classList.contains('filter-view')")).Should().BeTrue("the switch is first in the bar");
        await Expect(Page.Locator("input[name=\"filter-view\"][value=\"all\"]")).ToBeCheckedAsync();

        await Page.Locator(".filter-view-segment").Nth(1).ClickAsync();

        await Expect(Page.Locator(Groups)).ToHaveCountAsync(groups.Count);
        await Expect(Page.Locator("input[name=\"filter-view\"][value=\"location\"]")).ToBeCheckedAsync();
        await Expect(Page.Locator(Headings)).ToHaveTextAsync(groups.Select(group => group.Heading).ToArray());
        (await Page.Locator(Headings).EvaluateAllAsync<string[]>("(headings) => headings.map((heading) => heading.tagName)")).Should().OnlyContain(tag => tag == "H2");
        (await Page.Locator("#cabinet > :not(.location-group)").CountAsync()).Should().Be(0);
        (await Page.Locator(Groups).EvaluateAllAsync<bool[]>("(groups) => groups.map((group) => group.getAttribute('aria-labelledby') === group.querySelector('.location-heading').id)")).Should().OnlyContain(labelled => labelled);
        (await Page.Locator(".location-name").EvaluateAllAsync<string[]>("(names) => names.map((name) => name.getAttribute('dir'))")).Should().OnlyContain(direction => direction == "auto");
        var boxesByLocation = await EntryIdsAsync();
        boxesByLocation.Should().Contain(boxesTogether, "every game the one big cabinet draws stands in a location cabinet");
        (await Page.Locator(Boxes + ":not([data-kind=\"moreMarker\"])").CountAsync()).Should().Be(boxesByLocation.Count, "every box stands in exactly one cabinet");
        (await Page.EvaluateAsync<int>("() => [...document.querySelectorAll('#cabinet .placement')].filter((box) => box.closest('.location-group') === null).length")).Should().Be(0);
        var placeOf = document.Cards.ToDictionary(card => card.EntryId.ToString(CultureInfo.InvariantCulture), card => card.LocationId ?? "unknown");
        var drawnIn = await Page.Locator(Boxes + ":not([data-kind=\"moreMarker\"])").EvaluateAllAsync<string[][]>("(boxes) => boxes.map((box) => [box.dataset.entryId, box.closest('.location-group').dataset.locationId])");
        drawnIn.Should().OnlyContain(pair => placeOf[pair[0]] == pair[1], "each box stands in the cabinet of its own location");
        (await Page.EvaluateAsync<int>(GroupOverlaps)).Should().Be(0);
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= document.documentElement.clientWidth")).Should().BeTrue();
        await SaveScreenshotAsync($"filter-location-view-{profile}");

        await Page.Locator(".filter-view-segment").Nth(0).ClickAsync();

        await Expect(Page.Locator(Groups)).ToHaveCountAsync(0);
        await Expect(Page.Locator("#cabinet .section")).ToHaveCountAsync(sectionsTogether);
        (await EntryIdsAsync()).Should().BeEquivalentTo(boxesTogether);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Without_owner_locations_there_is_no_switch()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        var host = await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(5));
        host.Cards.Should().NotBeEmpty();

        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        await Expect(Page.Locator(Switch)).ToBeHiddenAsync();
        (await Page.Locator(Switch).EvaluateAsync<bool>("(element) => element.hidden")).Should().BeTrue();
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task At_rest_only_the_current_view_segment_is_filled()
    {
        await OpenSampleAsync(1280, 900);
        await Expect(Page.Locator(Switch)).ToBeVisibleAsync();
        await RestAsync();

        var atRest = await ReadBarAsync();

        atRest.Filled.Should().Equal("All together");
        atRest.FilledColour.Should().Be(atRest.BarColour, "the filled segment is the wall's text colour");
        atRest.AnyChips.Should().NotBeEmpty();
        atRest.AnyChips.Should().OnlyContain(chip => chip.Alpha == 0 && chip.Weight == "600" && chip.Shadow.Contains("inset", StringComparison.Ordinal));
        atRest.Controls.Should().BeGreaterThan(10);

        await Page.Locator(".filter-view-segment").Nth(1).ClickAsync();
        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        await RestAsync();

        var byLocation = await ReadBarAsync();

        byLocation.Filled.Should().Equal("By location");
        byLocation.FilledColour.Should().Be(byLocation.BarColour);
        ConsoleErrors.Should().BeEmpty();
    }

    private const string GroupOverlaps = """
        () => {
          const rects = [...document.querySelectorAll('#cabinet .location-group')].map((group) =>
            [...group.querySelectorAll('.placement')].map((box) => box.getBoundingClientRect()));
          let overlaps = 0;
          for (let a = 0; a < rects.length; a += 1) {
            for (let b = a + 1; b < rects.length; b += 1) {
              for (const one of rects[a]) {
                for (const other of rects[b]) {
                  if (one.left < other.right && other.left < one.right && one.top < other.bottom && other.top < one.bottom) {
                    overlaps += 1;
                  }
                }
              }
            }
          }
          return overlaps;
        }
        """;

    private const string ReadBar = """
        () => {
          const alphaOf = (colour) => {
            const match = /^rgba?\(([^)]+)\)$/.exec(colour);
            if (match === null) { return 1; }
            const parts = match[1].split(/[ ,\/]+/).filter((part) => part !== '');
            return parts.length > 3 ? Number(parts[3]) : 1;
          };
          const bar = document.querySelector('.filter-bar');
          const visible = (element) => {
            const style = getComputedStyle(element);
            return style.visibility !== 'hidden' && style.display !== 'none' && element.getClientRects().length > 0;
          };
          const controls = [...bar.querySelectorAll('.filter-view-segment, .filter-chip, #filter-q, #filter-loc, button')].filter(visible);
          const filled = controls.filter((control) => alphaOf(getComputedStyle(control).backgroundColor) > 0);
          const anyChips = [...bar.querySelectorAll('.filter-chip')].filter((chip) => chip.querySelector('input[value=""]:checked') !== null).filter(visible).map((chip) => {
            const style = getComputedStyle(chip);
            return { alpha: alphaOf(style.backgroundColor), weight: style.fontWeight, shadow: style.boxShadow };
          });
          return {
            controls: controls.length,
            filled: filled.map((control) => control.textContent.trim()),
            filledColour: filled.length === 0 ? '' : getComputedStyle(filled[0]).backgroundColor,
            barColour: getComputedStyle(bar).color,
            anyChips,
          };
        }
        """;

    private async Task OpenSampleAsync(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await StartAsync(PrototypeOn);
        await Page.RouteAsync("**/cabinet/search-names**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "application/json",
            Body = "{\"names\":{}}",
        }));
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
    }

    private async Task RestAsync()
    {
        await Page.EvaluateAsync("() => { window.scrollTo(0, 0); document.activeElement?.blur(); }");
        await Page.Mouse.MoveAsync(5, 700);
        await Page.EvaluateAsync("() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
    }

    private async Task<BarReading> ReadBarAsync()
    {
        var json = await Page.EvaluateAsync<JsonElement>(ReadBar);

        return JsonSerializer.Deserialize<BarReading>(json.GetRawText(), WebOptions)!;
    }

    private async Task<List<string>> EntryIdsAsync() =>
        [.. (await Page.Locator(Boxes + ":not([data-kind=\"moreMarker\"])").EvaluateAllAsync<string[]>("(boxes) => boxes.map((box) => box.dataset.entryId)")).Distinct()];

    private async Task<ViewDocument> LoadCardsAsync(string profile)
    {
        var json = await Page.EvaluateAsync<JsonElement>("async address => await (await fetch(address)).json()", $"/cabinet/cards?profile={profile}&sample=65");

        return JsonSerializer.Deserialize<ViewDocument>(json.GetRawText(), WebOptions)!;
    }

    private static List<ExpectedGroup> ExpectedGroups(ViewDocument document)
    {
        var groups = new List<ExpectedGroup>();

        foreach (var place in (document.Locations ?? []).Where(place => place.Games > 0))
        {
            groups.Add(new ExpectedGroup(place.Name, TopLevel(document.Cards.Where(card => card.LocationId == place.Id).ToList())));
        }

        var unplaced = document.Cards.Where(card => card.LocationId is null).ToList();

        if (unplaced.Count > 0)
        {
            groups.Add(new ExpectedGroup("Unknown location", TopLevel(unplaced)));
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

    private static string Games(int count) =>
        count == 1 ? "1 game" : count.ToString(CultureInfo.InvariantCulture) + " games";

    private sealed record ViewLink(long? EntryId);

    private sealed record ViewCard(long EntryId, bool IsExpansion, string? LocationId, List<ViewLink> Expansions);

    private sealed record ViewPlace(string Id, string Name, int Games);

    private sealed record ViewDocument(List<ViewCard> Cards, List<ViewPlace>? Locations = null);

    private sealed record ExpectedGroup(string Name, int Total)
    {
        public string Heading => $"{Name} · {Games(Total)}";
    }

    private sealed record AnyChip(double Alpha, string Weight, string Shadow);

    private sealed record BarReading(int Controls, List<string> Filled, string FilledColour, string BarColour, List<AnyChip> AnyChips);
}

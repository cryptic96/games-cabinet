using System.Globalization;
using System.Text.Json;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.FakeBgg;
using Cabinet.IntegrationTests.Infrastructure;
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
    private readonly TemporaryDirectory _storage = new();

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
        await ExpectViewSwitchAvailableAsync();

        if (width >= 1024)
        {
            (await Page.EvaluateAsync<bool>("() => document.querySelector('.filter-bar').firstElementChild.firstElementChild.classList.contains('filter-view')")).Should().BeTrue("the switch is first in the bar");
        }

        await Expect(Page.Locator("input[name=\"filter-view\"][value=\"all\"]")).ToBeCheckedAsync();

        await ChooseViewAsync(1);

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

        await ChooseViewAsync(0);

        await Expect(Page.Locator(Groups)).ToHaveCountAsync(0);
        await Expect(Page.Locator("#cabinet .section")).ToHaveCountAsync(sectionsTogether);
        (await EntryIdsAsync()).Should().BeEquivalentTo(boxesTogether);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_count_adds_up_across_the_location_cabinets()
    {
        await OpenSampleAsync(1440, 900);
        var document = await LoadCardsAsync("desktop");
        var cards = await LoadFamilyCardsAsync("desktop");
        var groups = ExpectedGroups(document);
        await Page.EvaluateAsync(RecordFiltered);

        await ChooseViewAsync(1);

        await Expect(Page.Locator(Groups)).ToHaveCountAsync(groups.Count);
        await Page.WaitForFunctionAsync("() => window.__filtered !== undefined && window.__filtered.view === 'location'");
        var resting = await ReadAsync<Filtered>("() => window.__filtered");
        var headingTotal = (await HeadingNumbersAsync()).Sum(numbers => numbers.Total);

        resting.Active.Should().BeFalse();
        resting.Groups.Should().Be(groups.Count);
        resting.HasLocations.Should().BeTrue();
        resting.Total.Should().Be(headingTotal, "the count with no filter is the sum of the headings");
        resting.Total.Should().Be(groups.Sum(group => group.Total));
        resting.Lit.Should().Be(resting.Total);

        const int players = 4;
        var litIds = FilterBarTests.ExpectedLit(cards, players, null);
        var expectedLit = LitEntries(document, litIds);
        expectedLit.Should().BeGreaterThan(0).And.BeLessThan(resting.Total);

        await Page.Locator($"fieldset[data-filter=\"players\"] input[value=\"{players.ToString(CultureInfo.InvariantCulture)}\"]").ClickAsync();

        await Expect(Page.Locator(".filter-count")).ToHaveTextAsync($"{expectedLit} of {resting.Total} games");
        var filtering = await ReadAsync<Filtered>("() => window.__filtered");
        filtering.Active.Should().BeTrue();
        filtering.View.Should().Be("location");
        filtering.Lit.Should().Be(expectedLit);
        filtering.Total.Should().Be(resting.Total);
        filtering.Groups.Should().Be(groups.Count);

        await Page.Locator(".filter-clear").ClickAsync();

        await Expect(Page.Locator(".filter-count")).ToHaveTextAsync(string.Empty);
        (await ReadAsync<Filtered>("() => window.__filtered")).Total.Should().Be(headingTotal);
        ConsoleErrors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(390, 844)]
    public async Task Arrow_down_crosses_into_the_next_location(int width, int height)
    {
        await OpenSampleAsync(width, height);
        await ChooseViewAsync(1);
        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        await Page.WaitForFunctionAsync("() => document.querySelectorAll('#cabinet .location-group').length > 1");

        var index = await Page.EvaluateAsync<int>(LowestBoxOfFirstGroup);
        var box = Page.Locator(Groups).First.Locator(".placement").Nth(index);
        await box.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");

        var landed = await ReadAsync<Landing>(Landed);
        landed.Group.Should().Be(1, "down from the last shelf of the first location continues in the next location");
        landed.Shelf.Should().EndWith(":0", "it lands on the first shelf of a section");
        landed.Section.Should().BeGreaterThan(landed.From, "the sections are numbered across the cabinets");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Headings_count_the_matches_while_filtering()
    {
        await OpenSampleAsync(1440, 900);
        var document = await LoadCardsAsync("desktop");
        var cards = await LoadFamilyCardsAsync("desktop");
        var groups = ExpectedGroups(document);
        var players = Enumerable.Range(1, 8).First(count =>
        {
            var perGroup = LitByGroup(document, FilterBarTests.ExpectedLit(cards, count, null));

            return perGroup.Any(lit => lit == 0) && perGroup.Any(lit => lit > 0);
        });
        var litPerGroup = LitByGroup(document, FilterBarTests.ExpectedLit(cards, players, null));
        await ChooseViewAsync(1);
        await Expect(Page.Locator(Groups)).ToHaveCountAsync(groups.Count);
        var mutedColour = await ColourOfAsync("--room-muted");
        var textColour = await ColourOfAsync("--room-text");
        groups.Should().Contain(group => group.Total == 1, "the sample holds a cabinet with a single game");

        await Page.Locator($"fieldset[data-filter=\"players\"] input[value=\"{players.ToString(CultureInfo.InvariantCulture)}\"]").ClickAsync();

        var expected = groups.Select((group, index) => $"{group.Name} · {litPerGroup[index].ToString(CultureInfo.InvariantCulture)} of {Games(group.Total)}").ToArray();
        await Expect(Page.Locator(Headings)).ToHaveTextAsync(expected);
        var names = await Page.Locator(".location-name").EvaluateAllAsync<string[]>("(names) => names.map((name) => getComputedStyle(name).color)");

        for (var index = 0; index < groups.Count; index++)
        {
            names[index].Should().Be(litPerGroup[index] == 0 ? mutedColour : textColour, groups[index].Name);
        }

        (await Page.Locator(Headings + "[data-no-match]").CountAsync()).Should().Be(litPerGroup.Count(lit => lit == 0));

        await Page.Locator(".filter-clear").ClickAsync();

        await Expect(Page.Locator(Headings)).ToHaveTextAsync(groups.Select(group => group.Heading).ToArray());
        await Expect(Page.Locator(Headings + "[data-no-match]")).ToHaveCountAsync(0);
        ConsoleErrors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(1280, 900)]
    [InlineData(1920, 1000)]
    [InlineData(390, 844)]
    public async Task Each_location_keeps_its_own_rows(int width, int height)
    {
        await OpenSampleAsync(width, height);
        await ChooseViewAsync(1);
        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        await Page.WaitForFunctionAsync("() => document.querySelectorAll('#cabinet .location-group').length > 1");

        var rows = await ReadAsync<List<GroupRows>>(MeasureGroups);

        rows.Should().HaveCountGreaterThan(1);

        foreach (var group in rows)
        {
            group.HeadingWidth.Should().BeApproximately(group.Width, 1, "the heading spans its group");
            group.HeadingLeft.Should().BeApproximately(group.Left, 1);
            group.SectionTops.Should().OnlyContain(top => top >= group.HeadingBottom - 1, "the heading stands above the sections");
        }

        for (var index = 1; index < rows.Count; index++)
        {
            rows[index].HeadingTop.Should().BeGreaterThanOrEqualTo(rows[index - 1].SectionsBottom - 1, "a group never shares a row with another group");
        }

        if (width < 640)
        {
            rows.Should().OnlyContain(group => group.SectionLefts.Distinct().Count() == 1, "a phone shows one column");
        }

        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= document.documentElement.clientWidth")).Should().BeTrue();
        ConsoleErrors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(1280)]
    [InlineData(390)]
    public async Task A_long_location_name_wraps_and_never_widens_the_page(int width)
    {
        await Page.SetViewportSizeAsync(width, 900);
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

        await Expect(Page.Locator(Headings).First).ToHaveTextAsync(new System.Text.RegularExpressions.Regex("^" + longName + @" · \d+ games?$"));
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= document.documentElement.clientWidth")).Should().BeTrue();
        var heading = await Page.Locator(Headings).First.BoundingBoxAsync();
        heading!.Width.Should().BeLessThanOrEqualTo(width);

        if (width < 640)
        {
            heading.Height.Should().BeGreaterThan(60, "the name wraps over more than one line");
        }

        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task A_live_redraw_keeps_the_location_view()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        var host = await OwnerHost.SyncAsync(factory => StartAsync(factory), SyntheticBggCollection.Create(120));
        var places = await host.SeedAsync("Cabinet A", "Shelf B");
        var games = host.Cards.DistinctBy(card => card.GameId).Take(3).ToList();
        await host.PlaceAsync(games[0], places[1]);
        await host.PlaceAsync(games[1], places[1]);
        await host.PlaceAsync(games[2], places[0]);
        await Page.RouteAsync("**/cabinet/search-names**", route => route.FulfillAsync(new RouteFulfillOptions { Status = 200, ContentType = "application/json", Body = "{\"names\":{}}" }));
        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await ExpectViewSwitchAvailableAsync();
        await ChooseViewAsync(1);
        await Expect(Page.Locator(Groups)).ToHaveCountAsync(3);
        await Expect(Page.Locator(Headings).Nth(0)).ToContainTextAsync("Cabinet A · ");
        await Expect(Page.Locator(Headings).Nth(1)).ToContainTextAsync("Shelf B · ");
        var headingsBefore = await Page.Locator(Headings).AllInnerTextsAsync();
        await Page.EvaluateAsync("() => { window.scrollTo(0, 400); window.__first = document.querySelector('#cabinet .location-group'); }");
        await Page.WaitForFunctionAsync("() => window.scrollY > 300");
        var scrolledBefore = await Page.EvaluateAsync<double>("() => window.scrollY");

        await host.PlaceAsync(games[0], places[0]);
        host.TellOpenPages();

        await Expect(Page.Locator(Headings).Nth(0)).Not.ToHaveTextAsync(headingsBefore[0], new LocatorAssertionsToHaveTextOptions { Timeout = 20000 });
        await Expect(Page.Locator(Headings).Nth(1)).Not.ToHaveTextAsync(headingsBefore[1]);
        (await Page.EvaluateAsync<bool>("() => window.__first.isConnected")).Should().BeFalse("the groups were drawn again");
        await Expect(Page.Locator(Groups)).ToHaveCountAsync(3);
        await Expect(Page.Locator("input[name=\"filter-view\"][value=\"location\"]")).ToBeCheckedAsync();
        var scrolledAfter = await Page.EvaluateAsync<double>("() => window.scrollY");
        Math.Abs(scrolledAfter - scrolledBefore).Should().BeLessThan(900);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Many_locations_stay_navigable()
    {
        FilterFixture.WriteManyLocations(_storage.FullPath, 14);
        await Page.SetViewportSizeAsync(1280, 900);
        await OwnerHost.StoredAsync(factory => StartAsync(factory), _storage.FullPath);
        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var names = Enumerable.Range(0, 14).Select(index => $"Cabinet {(char)('A' + index)}").ToArray();

        await ExpectViewSwitchAvailableAsync();
        await Expect(Page.Locator("#filter-loc option")).ToHaveTextAsync(["Any location", .. names]);
        await AssertNoSidewaysScrollAsync(1280);
        await AssertNoSidewaysScrollAsync(390);

        await Page.SetViewportSizeAsync(1280, 900);
        await ChooseViewAsync(1);

        await Expect(Page.Locator(Groups)).ToHaveCountAsync(14);
        await Expect(Page.Locator(Headings)).ToHaveTextAsync(names.Select(name => $"{name} · 2 games").ToArray());
        (await Page.Locator(Groups + " > h2.location-heading").CountAsync()).Should().Be(14);
        await AssertNoSidewaysScrollAsync(1280);
        await AssertNoSidewaysScrollAsync(390);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Locations_with_nothing_placed_show_only_Unknown_location()
    {
        FilterFixture.Write(_storage.FullPath, placeCopies: false);
        await Page.SetViewportSizeAsync(1440, 900);
        await OwnerHost.StoredAsync(factory => StartAsync(factory), _storage.FullPath);
        await GotoCabinetAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var json = await Page.EvaluateAsync<JsonElement>("async address => await (await fetch(address)).json()", "/cabinet/cards?profile=desktop");
        var document = JsonSerializer.Deserialize<ViewDocument>(json.GetRawText(), WebOptions)!;
        var every = await EntryIdsAsync();

        await ExpectViewSwitchAvailableAsync();
        await Expect(Page.Locator("#filter-loc option")).ToHaveTextAsync(["Any location", "Unknown location"]);

        await ChooseViewAsync(1);

        await Expect(Page.Locator(Groups)).ToHaveCountAsync(1);
        await Expect(Page.Locator(Headings)).ToHaveTextAsync($"Unknown location · {Games(TopLevel(document.Cards))}");
        (await Page.Locator(Groups).Locator(".placement:not([data-kind=\"moreMarker\"])").EvaluateAllAsync<string[]>("(boxes) => boxes.map((box) => box.dataset.entryId)")).Distinct().Should().BeEquivalentTo(every);
        (await Page.Locator(Boxes).CountAsync()).Should().Be(await Page.Locator(Groups).Locator(".placement").CountAsync());
        ConsoleErrors.Should().BeEmpty();
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _storage.Dispose();
        GC.SuppressFinalize(this);
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
        await ExpectViewSwitchAvailableAsync();
        await RestAsync();

        var atRest = await ReadBarAsync();

        atRest.Filled.Should().Equal("All together");
        atRest.FilledColour.Should().Be(atRest.BarColour, "the filled segment is the wall's text colour");
        atRest.AnyChips.Should().NotBeEmpty();
        atRest.AnyChips.Should().OnlyContain(chip => chip.Alpha == 0 && chip.Weight == "600" && chip.Shadow.Contains("inset", StringComparison.Ordinal));
        atRest.Controls.Should().BeGreaterThan(10);

        await ChooseViewAsync(1);
        await Expect(Page.Locator(Groups).First).ToBeAttachedAsync();
        await RestAsync();

        var byLocation = await ReadBarAsync();

        byLocation.Filled.Should().Equal("By location");
        byLocation.FilledColour.Should().Be(byLocation.BarColour);
        ConsoleErrors.Should().BeEmpty();
    }

    private const string RecordFiltered = """
        () => {
          document.addEventListener('cabinet:filtered', (event) => {
            const entries = event.detail.entries;
            window.__filtered = {
              view: event.detail.view,
              active: event.detail.active,
              lit: event.detail.count.lit,
              total: event.detail.count.total,
              groups: Array.isArray(entries) ? 0 : entries.groups.length,
              hasLocations: event.detail.hasLocations,
            };
          });
        }
        """;

    private const string LowestBoxOfFirstGroup = """
        () => {
          const boxes = [...document.querySelector('#cabinet .location-group').querySelectorAll('.placement')];
          let best = 0;
          boxes.forEach((box, index) => {
            if (box.getBoundingClientRect().bottom > boxes[best].getBoundingClientRect().bottom) { best = index; }
          });
          return best;
        }
        """;

    private const string Landed = """
        () => {
          const box = document.activeElement;
          const groups = [...document.querySelectorAll('#cabinet .location-group')];
          const first = groups[0].querySelectorAll('.placement');
          return {
            group: groups.indexOf(box.closest('.location-group')),
            shelf: box.dataset.shelf,
            section: Number.parseInt(box.dataset.shelf, 10),
            from: Math.max(...[...first].map((candidate) => Number.parseInt(candidate.dataset.shelf, 10))),
          };
        }
        """;

    private const string MeasureGroups = """
        () => [...document.querySelectorAll('#cabinet .location-group')].map((group) => {
          const box = group.getBoundingClientRect();
          const heading = group.querySelector('.location-heading').getBoundingClientRect();
          const sections = [...group.querySelectorAll('.section')].map((section) => section.getBoundingClientRect());
          return {
            left: box.left,
            width: box.width,
            headingLeft: heading.left,
            headingWidth: heading.width,
            headingTop: heading.top,
            headingBottom: heading.bottom,
            sectionTops: sections.map((rect) => rect.top),
            sectionLefts: sections.map((rect) => Math.round(rect.left)),
            sectionsBottom: sections.length === 0 ? heading.bottom : Math.max(...sections.map((rect) => rect.bottom)),
          };
        })
        """;

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

    private async Task<BarReading> ReadBarAsync() => await ReadAsync<BarReading>(ReadBar);

    private async Task<T> ReadAsync<T>(string script)
    {
        var json = await Page.EvaluateAsync<JsonElement>(script);

        return JsonSerializer.Deserialize<T>(json.GetRawText(), WebOptions)!;
    }

    private async Task<List<string>> EntryIdsAsync() =>
        [.. (await Page.Locator(Boxes + ":not([data-kind=\"moreMarker\"])").EvaluateAllAsync<string[]>("(boxes) => boxes.map((box) => box.dataset.entryId)")).Distinct()];

    private async Task<ViewDocument> LoadCardsAsync(string profile)
    {
        var json = await Page.EvaluateAsync<JsonElement>("async address => await (await fetch(address)).json()", $"/cabinet/cards?profile={profile}&sample=65");

        return JsonSerializer.Deserialize<ViewDocument>(json.GetRawText(), WebOptions)!;
    }

    private async Task<IReadOnlyList<FilterBarTests.CardData>> LoadFamilyCardsAsync(string profile)
    {
        var json = await Page.EvaluateAsync<JsonElement>("async address => (await (await fetch(address)).json()).cards", $"/cabinet/cards?profile={profile}&sample=65");

        return JsonSerializer.Deserialize<List<FilterBarTests.CardData>>(json.GetRawText(), WebOptions)!;
    }

    private async Task<List<HeadingNumbers>> HeadingNumbersAsync()
    {
        var texts = await Page.Locator(Headings).EvaluateAllAsync<string[]>("(headings) => headings.map((heading) => heading.textContent)");

        return [.. texts.Select(text =>
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, @"· (?:(\d+) of )?(\d+) games?$");
            match.Success.Should().BeTrue(text);

            return new HeadingNumbers(match.Groups[1].Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : -1, int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
        })];
    }

    private static int LitEntries(ViewDocument document, HashSet<string> lit) => LitByGroup(document, lit).Sum();

    private static List<int> LitByGroup(ViewDocument document, HashSet<string> lit)
    {
        var counts = new List<int>();
        var places = (document.Locations ?? []).Where(place => place.Games > 0).Select(place => place.Id).Cast<string?>().ToList();

        if (document.Cards.Any(card => card.LocationId is null))
        {
            places.Add(null);
        }

        foreach (var place in places)
        {
            var cards = document.Cards.Where(card => card.LocationId == place).ToList();
            var inGroup = cards.Select(card => card.EntryId).ToHashSet();
            var childrenOf = cards
                .Where(card => !card.IsExpansion)
                .ToDictionary(card => card.EntryId, card => card.Expansions.Where(link => link.EntryId is not null && inGroup.Contains(link.EntryId.Value)).Select(link => link.EntryId!.Value).ToList());
            var nested = childrenOf.Values.SelectMany(children => children).ToHashSet();

            counts.Add(cards
                .Where(card => !nested.Contains(card.EntryId))
                .Count(card => lit.Contains(Id(card.EntryId)) || (childrenOf.TryGetValue(card.EntryId, out var children) && children.Any(child => lit.Contains(Id(child))))));
        }

        return counts;
    }

    private static string Id(long entryId) => entryId.ToString(CultureInfo.InvariantCulture);

    private async Task AssertNoSidewaysScrollAsync(int width)
    {
        await Page.SetViewportSizeAsync(width, 900);
        await Page.EvaluateAsync("() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= document.documentElement.clientWidth")).Should().BeTrue($"no sideways scroll at {width} px");
    }

    private async Task<string> ColourOfAsync(string token) =>
        await Page.EvaluateAsync<string>("(name) => { const probe = document.createElement('span'); probe.style.color = `var(${name})`; document.body.append(probe); const colour = getComputedStyle(probe).color; probe.remove(); return colour; }", token);

    private static int TopLevel(IEnumerable<ViewCard> cards) => TopLevel(cards.ToList());

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

    private sealed record Filtered(string? View, bool Active, int Lit, int Total, int Groups, bool HasLocations);

    private sealed record Landing(int Group, string Shelf, int Section, int From);

    private sealed record HeadingNumbers(int Lit, int Total);

    private sealed record GroupRows(double Left, double Width, double HeadingLeft, double HeadingWidth, double HeadingTop, double HeadingBottom, List<double> SectionTops, List<int> SectionLefts, double SectionsBottom);

    private sealed record AnyChip(double Alpha, string Weight, string Shadow);

    private sealed record BarReading(int Controls, List<string> Filled, string FilledColour, string BarColour, List<AnyChip> AnyChips);
}

using System.Globalization;
using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that every control of the filter bar and of its sheet shows the wall's focus ring when it is reached by
/// the keyboard: a solid ring two pixels wide that stands two pixels outside the control, in exactly the ring colour of the wall, so
/// the ring always lies on the surface its contrast was measured on. A focused choice inside a group of choices is lifted above its
/// neighbours so none of them covers its ring.
/// </summary>
[Trait("Category", "Browser")]
public sealed partial class FilterFocusRingTests : CabinetPageTest, IDisposable
{
    private const string PlayersGroup = "fieldset[data-filter=\"players\"]";
    private const string TimeGroup = "fieldset[data-filter=\"time\"]";
    private const string ChipHost = ".filter-chip";
    private const string ViewHost = ".filter-view-segment";
    private const string OpenSheet = "dialog.filter-dialog[open]";
    private const int ViewportHeightOfAPhone = 844;
    private const string RingWidth = "2px";
    private const string RingOffset = "2px";
    private const string SolidStyle = "solid";

    private const string FocusAndRead = """
        (args) => {
          const target = document.querySelector(args.target);
          if (target === null) {
            return ['missing'];
          }
          target.focus();
          const host = args.host === '' ? target : target.closest(args.host);
          const style = getComputedStyle(host);
          return [
            host.matches(':focus-visible') || target.matches(':focus-visible') ? 'yes' : 'no',
            style.outlineColor,
            style.outlineStyle,
            style.outlineWidth,
            style.outlineOffset,
          ];
        }
        """;

    private const string FocusAndReadLift = """
        (args) => {
          const target = document.querySelector(args.target);
          target.focus();
          const host = target.closest('.filter-chip');
          const read = (element) => { const style = getComputedStyle(element); return [style.zIndex, style.position]; };
          return [read(host), ...[...host.parentElement.querySelectorAll('.filter-chip')].filter((chip) => chip !== host).map(read)];
        }
        """;

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private readonly TemporaryDirectory _storage = new();

    /// <summary>A dark wall and a light wall, by day.</summary>
    public static TheoryData<string> Walls { get; } = ["brown", "birch"];

    /// <summary>
    /// Each control of the full bar at a desktop width, of the slim bar at a phone width and of the open sheet draws the wall's ring,
    /// two pixels wide, two pixels outside itself, when the keyboard focuses it.
    /// </summary>
    /// <param name="wood">The wood whose wall is drawn.</param>
    [Theory]
    [MemberData(nameof(Walls))]
    public async Task Every_control_draws_the_wall_ring_outside_itself(string wood)
    {
        await StartWithLookAsync(wood);
        var problems = new List<string>();

        await OpenAsync(1440, 900);
        var ring = await RootColourAsync("--room-ring");
        await ChooseSomethingToClearAsync();
        problems.AddRange(await CheckAsync(wood, ring, "the search field", "#filter-q", string.Empty));
        problems.AddRange(await CheckAsync(wood, ring, "the location select", "#filter-loc", string.Empty));
        problems.AddRange(await CheckAsync(wood, ring, "a players chip", $"{PlayersGroup} input[value=\"4\"]", ChipHost));
        problems.AddRange(await CheckAsync(wood, ring, "a time chip", $"{TimeGroup} input:not([value=\"\"])", ChipHost));
        problems.AddRange(await CheckAsync(wood, ring, "a view toggle segment", "input[name=\"filter-view\"][value=\"location\"]", ViewHost));
        problems.AddRange(await CheckAsync(wood, ring, "the text Clear", ".filter-bar .filter-clear", string.Empty));
        await SaveScreenshotAsync($"filter-ring-full-bar-{wood}");

        await OpenAsync(390, ViewportHeightOfAPhone);
        await ChooseSomethingToClearAsync();
        problems.AddRange(await CheckAsync(wood, ring, "the Filters button", ".filter-bar .filter-open", string.Empty));
        problems.AddRange(await CheckAsync(wood, ring, "the icon Clear", ".filter-bar .filter-clear-icon", string.Empty));
        await SaveScreenshotAsync($"filter-ring-slim-bar-{wood}");

        await Page.Locator(".filter-bar .filter-open").ClickAsync();
        await Expect(Page.Locator(OpenSheet)).ToBeVisibleAsync();
        problems.AddRange(await CheckAsync(wood, ring, "the sheet's Clear", ".filter-sheet-actions .filter-sheet-clear", string.Empty));
        problems.AddRange(await CheckAsync(wood, ring, "the sheet's primary button", ".filter-done", string.Empty));
        await SaveScreenshotAsync($"filter-ring-sheet-{wood}");

        problems.Should().BeEmpty($"{wood}: every control wears the wall's ring outside itself");
        ConsoleErrors.Should().BeEmpty();
    }

    /// <summary>
    /// A focused choice in the bar's group of choices stands above every neighbour, so a neighbour never covers its ring.
    /// </summary>
    [Fact]
    public async Task A_focused_chip_in_a_segmented_group_is_lifted()
    {
        await StartWithLookAsync("brown");
        await OpenAsync(1440, 900);
        await Page.Keyboard.PressAsync("Shift");

        var read = await Page.EvaluateAsync<string[][]>(FocusAndReadLift, new { target = $"{PlayersGroup} input[value=\"4\"]" });
        var focused = read[0];
        var neighbours = read.Skip(1).ToList();

        neighbours.Should().NotBeEmpty("the group holds more than the focused chip");
        focused[1].Should().NotBe("static", "a lifted chip is positioned so its stacking level counts");
        var lift = ZIndex(focused[0]);
        lift.Should().BeGreaterThanOrEqualTo(1, "a focused chip is lifted");
        neighbours.Should().OnlyContain(neighbour => ZIndex(neighbour[0]) < lift, "no neighbour stands at or above the focused chip, so none covers its ring");
        ConsoleErrors.Should().BeEmpty();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private static int ZIndex(string computed) => computed == "auto" ? 0 : int.Parse(computed, CultureInfo.InvariantCulture);

    private static RgbColour ParseColour(string computed)
    {
        var match = ComputedColour().Match(computed);

        match.Success.Should().BeTrue($"'{computed}' is a computed rgb colour");

        return new RgbColour(
            byte.Parse(match.Groups["r"].Value, CultureInfo.InvariantCulture),
            byte.Parse(match.Groups["g"].Value, CultureInfo.InvariantCulture),
            byte.Parse(match.Groups["b"].Value, CultureInfo.InvariantCulture));
    }

    [GeneratedRegex(@"^rgba?\((?<r>\d+), (?<g>\d+), (?<b>\d+)(, [0-9.]+)?\)$")]
    private static partial Regex ComputedColour();

    private async Task StartWithLookAsync(string wood)
    {
        OwnerDataSeed.Write(_storage.FullPath, OwnerData.Empty with { Revision = 1, Look = new CabinetLook("classic", wood, "wood", "off", "shaded") });
        await StartAsync(new Dictionary<string, string?>(PrototypeOn) { ["Storage:Directory"] = _storage.FullPath });
        await Page.RouteAsync("**/cabinet/search-names**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "application/json",
            Body = "{\"names\":{}}",
        }));
    }

    private async Task OpenAsync(int width, int height)
    {
        await Page.SetViewportSizeAsync(width, height);
        await GotoCabinetAsync("/?sample=65");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator(".filter-bar")).ToBeVisibleAsync();
        await Page.WaitForFunctionAsync("() => { const toggle = document.querySelector('.filter-view'); return toggle !== null && !toggle.hidden; }");
    }

    private async Task ChooseSomethingToClearAsync()
    {
        await Page.Locator("#filter-q").FillAsync("a");
        await Page.WaitForSelectorAsync(".filter-bar[data-active]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
    }

    private async Task<RgbColour> RootColourAsync(string property)
    {
        var colour = await Page.EvaluateAsync<string>(
            """
            (name) => {
              const probe = document.createElement('span');
              probe.style.color = 'var(' + name + ')';
              document.body.append(probe);
              const resolved = getComputedStyle(probe).color;
              probe.remove();
              return resolved;
            }
            """,
            property);

        return ParseColour(colour);
    }

    private async Task<List<string>> CheckAsync(string wood, RgbColour ring, string control, string target, string host)
    {
        var where = $"{wood}, {control} ({target})";
        var problems = new List<string>();

        await Page.Keyboard.PressAsync("Shift");
        var read = await Page.EvaluateAsync<string[]>(FocusAndRead, new { target, host });

        if (read[0] == "missing")
        {
            problems.Add($"{where}: the control is not on the page");

            return problems;
        }

        if (read[0] != "yes")
        {
            problems.Add($"{where}: a keyboard focus does not show its ring");
        }

        if (ParseColour(read[1]) != ring)
        {
            problems.Add($"{where}: the ring is {read[1]}, not the wall's ring colour");
        }

        if (read[2] != SolidStyle)
        {
            problems.Add($"{where}: the ring is {read[2]}, not solid");
        }

        if (read[3] != RingWidth)
        {
            problems.Add($"{where}: the ring is {read[3]} wide, not {RingWidth}");
        }

        if (read[4] != RingOffset)
        {
            problems.Add($"{where}: the ring stands {read[4]} from the control, not {RingOffset} outside it");
        }

        return problems;
    }
}

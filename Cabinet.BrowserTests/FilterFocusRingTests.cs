using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that every control of the filter dock, the slim bar and the sheet shows the wall's focus ring when it is reached by
/// the keyboard: a solid ring two pixels wide that stands two pixels outside the control, in exactly the ring colour of the wall, so
/// the ring always lies on the surface its contrast was measured on. That covers the desktop dock folded to its rail, open beside the
/// cabinet and open over it. No ring is cut by the edge of the rail, the panel, the sheet or the screen, and a control whose
/// neighbour reaches under its ring is lifted above that neighbour.
/// </summary>
[Trait("Category", "Browser")]
public sealed partial class FilterFocusRingTests : CabinetPageTest, IDisposable
{
    private const string PlayersGroup = "fieldset[data-filter=\"players\"]";
    private const string TimeGroup = "fieldset[data-filter=\"time\"]";
    private const string ChipHost = ".filter-chip";
    private const string ViewHost = ".filter-view-segment";
    private const string OpenSheet = "dialog.filter-dialog[open]";
    private const string RailClear = ".filter-rail .filter-rail-clear";
    private const string PhoneFilters = ".filter-bar .filter-open";
    private const int ViewportHeightOfAPhone = 844;
    private const int DesktopHeight = 900;
    private const string RingWidth = "2px";
    private const string RingOffset = "2px";
    private const string SolidStyle = "solid";

    private const string FocusAndRead = """
        (args) => {
          const target = document.querySelector(args.target);
          if (target === null) {
            return { missing: true };
          }
          target.focus();
          const host = args.host === '' ? target : target.closest(args.host);
          const style = getComputedStyle(host);
          const box = host.getBoundingClientRect();
          const reach = Number.parseFloat(style.outlineWidth) + Number.parseFloat(style.outlineOffset);
          const ring = { left: box.left - reach, top: box.top - reach, right: box.right + reach, bottom: box.bottom + reach };
          const name = (element) => element.id !== '' ? '#' + element.id : element.tagName.toLowerCase() + (element.className ? '.' + String(element.className).trim().split(/\s+/).join('.') : '');
          const inside = (outer) => ring.left >= outer.left - 0.5 && ring.top >= outer.top - 0.5 && ring.right <= outer.right + 0.5 && ring.bottom <= outer.bottom + 0.5;
          const clippedBy = [];
          if (!inside({ left: 0, top: 0, right: document.documentElement.clientWidth, bottom: document.documentElement.clientHeight })) {
            clippedBy.push('the viewport');
          }
          let fixed = getComputedStyle(host).position === 'fixed';
          for (let parent = host.parentElement; parent !== null && parent !== document.body && !fixed; parent = parent.parentElement) {
            const own = getComputedStyle(parent);
            if (own.overflowX !== 'visible' || own.overflowY !== 'visible') {
              const outer = parent.getBoundingClientRect();
              const left = outer.left + parent.clientLeft;
              const top = outer.top + parent.clientTop;
              if (!inside({ left, top, right: left + parent.clientWidth, bottom: top + parent.clientHeight })) {
                clippedBy.push(name(parent));
              }
            }
            fixed = own.position === 'fixed';
          }
          const level = (element) => {
            const own = getComputedStyle(element);
            return own.position === 'static' || own.zIndex === 'auto' ? 0 : Number.parseInt(own.zIndex, 10);
          };
          const covered = [];
          const middle = Number.parseFloat(style.outlineOffset) + Number.parseFloat(style.outlineWidth) / 2;
          const points = [];
          for (const share of [0.25, 0.5, 0.75]) {
            const x = box.left + box.width * share;
            const y = box.top + box.height * share;
            points.push([x, box.top - middle], [x, box.bottom + middle], [box.left - middle, y], [box.right + middle, y]);
          }
          for (const [x, y] of points) {
            const hit = document.elementFromPoint(x, y);
            if (hit === null || hit.contains(host) || host.contains(hit)) {
              continue;
            }
            let theirs = hit;
            while (theirs.parentElement !== null && !theirs.parentElement.contains(host)) {
              theirs = theirs.parentElement;
            }
            let mine = host;
            while (mine.parentElement !== null && mine.parentElement !== theirs.parentElement) {
              mine = mine.parentElement;
            }
            if (level(mine) <= level(theirs)) {
              covered.push(name(theirs));
            }
          }
          return {
            missing: false,
            visible: host.matches(':focus-visible') || target.matches(':focus-visible'),
            colour: style.outlineColor,
            style: style.outlineStyle,
            width: style.outlineWidth,
            offset: style.outlineOffset,
            clippedBy,
            coveredBy: [...new Set(covered)],
          };
        }
        """;

    private const string PanelInPlace = """
        () => {
          const panel = document.querySelector('#filter-panel');
          return panel.getAnimations().length === 0 && Math.abs(panel.getBoundingClientRect().left) < 0.5;
        }
        """;

    private const string ReadSymbol = """
        (selector) => {
          const button = document.querySelector(selector);
          const symbol = button.querySelector('svg use[href$="#i-menu"]')?.closest('svg') ?? null;
          if (symbol === null) {
            return false;
          }
          const outer = button.getBoundingClientRect();
          const inner = symbol.getBoundingClientRect();
          return inner.width > 0 && inner.left >= outer.left && inner.right <= outer.right && inner.top >= outer.top && inner.bottom <= outer.bottom;
        }
        """;

    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private readonly TemporaryDirectory _storage = new();

    /// <summary>A dark wall and a light wall, by day.</summary>
    public static TheoryData<string> Walls { get; } = ["brown", "birch"];

    /// <summary>
    /// Each control of the desktop dock, of the slim bar at a phone width and of the open sheet draws the wall's ring, two pixels wide,
    /// two pixels outside itself, when the keyboard focuses it: the folded rail's three-line button, its four group buttons and its
    /// Clear; the pushed panel's head button, Clear, search field, chips, view segment and select; the head button, the search field
    /// and a chip of the panel that lies over the cabinet; and the phone's Filters button with its symbol, the icon Clear and the
    /// sheet's buttons. No ring is cut by the padding or the edge of the rail, the panel, the sheet or the screen, and no neighbour
    /// stands over it.
    /// </summary>
    /// <param name="wood">The wood whose wall is drawn.</param>
    [Theory]
    [MemberData(nameof(Walls))]
    public async Task Every_control_draws_the_wall_ring_outside_itself(string wood)
    {
        await StartWithLookAsync(wood);
        var problems = new List<string>();

        await OpenAsync(DockWidths.Push, DesktopHeight);
        var ring = await RootColourAsync("--room-ring");
        await ChooseSomethingToClearAsync();
        await Expect(Page.Locator(RailClear)).ToBeVisibleAsync();
        problems.AddRange(await CheckAsync(wood, ring, "the rail's three-line button", ".filter-rail .filter-menu", string.Empty));

        foreach (var group in new[] { "search", "players", "time", "location" })
        {
            problems.AddRange(await CheckAsync(wood, ring, $"the rail's {group} button", $".filter-rail-button[data-group=\"{group}\"]", string.Empty));
        }

        problems.AddRange(await CheckAsync(wood, ring, "the rail's Clear", RailClear, string.Empty));
        await SaveScreenshotAsync($"filter-ring-rail-{wood}");

        await OpenFilterPanelAsync();
        (await DockStateAsync()).Should().Be("push");
        problems.AddRange(await CheckAsync(wood, ring, "the panel head's three-line button", "#filter-panel .filter-menu", string.Empty));
        problems.AddRange(await CheckAsync(wood, ring, "the panel's Clear", "#filter-panel .filter-panel-head .filter-clear", string.Empty));
        problems.AddRange(await CheckAsync(wood, ring, "the pushed search field", "#filter-q", string.Empty));
        problems.AddRange(await CheckAsync(wood, ring, "a pushed players chip", $"{PlayersGroup} input[value=\"4\"]", ChipHost));
        problems.AddRange(await CheckAsync(wood, ring, "a pushed time chip", $"{TimeGroup} input:not([value=\"\"])", ChipHost));
        problems.AddRange(await CheckAsync(wood, ring, "a pushed view segment", "input[name=\"filter-view\"][value=\"location\"]", ViewHost));
        problems.AddRange(await CheckAsync(wood, ring, "the pushed location select", "#filter-loc", string.Empty));
        await SaveScreenshotAsync($"filter-ring-push-{wood}");
        await FoldFilterPanelAsync();

        await OpenAsync(DockWidths.Over, DesktopHeight);
        await OpenFilterPanelAsync();
        (await DockStateAsync()).Should().Be("over");
        await Page.WaitForFunctionAsync(PanelInPlace);
        problems.AddRange(await CheckAsync(wood, ring, "the fly-over head's three-line button", "#filter-panel .filter-menu", string.Empty));
        problems.AddRange(await CheckAsync(wood, ring, "the fly-over search field", "#filter-q", string.Empty));
        problems.AddRange(await CheckAsync(wood, ring, "a fly-over players chip", $"{PlayersGroup} input[value=\"4\"]", ChipHost));
        await SaveScreenshotAsync($"filter-ring-over-{wood}");

        await OpenAsync(DockWidths.Phone, ViewportHeightOfAPhone);
        await ChooseSomethingToClearAsync();
        (await Page.EvaluateAsync<bool>(ReadSymbol, PhoneFilters)).Should().BeTrue($"{wood}: the Filters button holds its three-line symbol inside its edge, so the ring goes round both");
        problems.AddRange(await CheckAsync(wood, ring, "the Filters button", PhoneFilters, string.Empty));
        problems.AddRange(await CheckAsync(wood, ring, "the icon Clear", ".filter-bar .filter-clear-icon", string.Empty));
        await SaveScreenshotAsync($"filter-ring-slim-bar-{wood}");

        await Page.Locator(PhoneFilters).ClickAsync();
        await Expect(Page.Locator(OpenSheet)).ToBeVisibleAsync();
        problems.AddRange(await CheckAsync(wood, ring, "the sheet's Clear", ".filter-sheet-actions .filter-sheet-clear", string.Empty));
        problems.AddRange(await CheckAsync(wood, ring, "the sheet's primary button", ".filter-done", string.Empty));
        await SaveScreenshotAsync($"filter-ring-sheet-{wood}");

        problems.Should().BeEmpty($"{wood}: every control wears the wall's ring outside itself");
        ConsoleErrors.Should().BeEmpty();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

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
        var docked = await IsDockWidthAsync();

        if (docked)
        {
            await OpenFilterPanelAsync();
        }

        await Page.Locator("#filter-q").FillAsync("a");
        await Page.WaitForSelectorAsync(".filter-bar[data-active]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });

        if (docked)
        {
            await FoldFilterPanelAsync();
        }
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
        var element = await Page.EvaluateAsync<JsonElement>(FocusAndRead, new { target, host });
        var read = JsonSerializer.Deserialize<FocusReading>(element.GetRawText(), WebOptions)!;

        if (read.Missing)
        {
            problems.Add($"{where}: the control is not on the page");

            return problems;
        }

        if (!read.Visible)
        {
            problems.Add($"{where}: a keyboard focus does not show its ring");
        }

        if (ParseColour(read.Colour) != ring)
        {
            problems.Add($"{where}: the ring is {read.Colour}, not the wall's ring colour");
        }

        if (read.Style != SolidStyle)
        {
            problems.Add($"{where}: the ring is {read.Style}, not solid");
        }

        if (read.Width != RingWidth)
        {
            problems.Add($"{where}: the ring is {read.Width} wide, not {RingWidth}");
        }

        if (read.Offset != RingOffset)
        {
            problems.Add($"{where}: the ring stands {read.Offset} from the control, not {RingOffset} outside it");
        }

        if (read.ClippedBy.Count > 0)
        {
            problems.Add($"{where}: the ring is cut by {string.Join(", ", read.ClippedBy)}");
        }

        if (read.CoveredBy.Count > 0)
        {
            problems.Add($"{where}: the ring lies under {string.Join(", ", read.CoveredBy)}, which is not below the focused control");
        }

        return problems;
    }

    /// <summary>What a focused control shows: its ring, and what would cut it or stand over it.</summary>
    /// <param name="Missing">True when the control is not on the page.</param>
    /// <param name="Visible">True when the browser treats the focus as a keyboard focus.</param>
    /// <param name="Colour">The computed ring colour.</param>
    /// <param name="Style">The computed ring style.</param>
    /// <param name="Width">The computed ring width.</param>
    /// <param name="Offset">The computed ring offset.</param>
    /// <param name="ClippedBy">The scrolling or clipping boxes, and the screen, whose visible area does not hold the whole ring.</param>
    /// <param name="CoveredBy">The neighbours that reach under the ring and are not stacked below the focused control.</param>
    private sealed record FocusReading(
        bool Missing,
        bool Visible,
        string Colour,
        string Style,
        string Width,
        string Offset,
        IReadOnlyList<string> ClippedBy,
        IReadOnlyList<string> CoveredBy);
}

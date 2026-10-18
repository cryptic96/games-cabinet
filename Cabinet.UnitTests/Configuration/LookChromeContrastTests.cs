using System.Globalization;
using System.Text.RegularExpressions;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Owner;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.UnitTests.Configuration;

/// <summary>
/// Pins the legibility of the public page's chrome on every wall: the text, the muted text and the lines on the wall under the
/// brightest part of the light falloff, the attribution logo the server picks for the wall, the focus ring on the wall and on every
/// surface of the furniture, and the tones of the backs and the kick. The colours are read from the shipped style sheets and worked
/// out the way the browser works them out, so a change to a token that breaks legibility fails here.
/// </summary>
[Trait("Category", "Configuration")]
public partial class LookChromeContrastTests
{
    private const double TextContrast = 4.5;
    private const double GraphicContrast = 3.0;
    private const double LogoLuminanceRule = 0.27;
    private const double DarkWallBelow = 0.12;
    private const double LightWallAbove = 0.45;
    private const double DarkBackAtMost = 0.017;
    private const double MidBackLowest = 0.22;
    private const double MidBackHighest = 0.29;
    private const double KickNeverBlackBelow = 0.012;
    private const double KickOverFrontAtLeast = 1.4;
    private const string NavyLogoColour = "#3f3a60";
    private const string WhiteLogoColour = "#ffffff";

    private static readonly Lazy<StyleSheetTokens> Sheets = new(() =>
        StyleSheetTokens.FromFiles(RepositoryPaths.ServiceDirectory(), "site.css", "cabinet-wood.css"));

    /// <summary>The five woods the owner can pick.</summary>
    public static TheoryData<string> Woods { get; } = [.. CabinetLook.Woods];

    /// <summary>Every wood in the day and in the evening.</summary>
    public static TheoryData<string, string> WoodsByHour { get; } = BuildWoodsByHour();

    [Theory]
    [MemberData(nameof(Woods))]
    public void Text_and_muted_text_read_and_lines_show_on_the_wall_under_the_brightest_falloff(string wood)
    {
        var tokens = Sheets.Value.ForWood(wood);
        var wall = BrightestWall(tokens);

        SpineColour.ContrastRatio(StyleSheetTokens.Colour(tokens, "--room-text"), wall).Should().BeGreaterThanOrEqualTo(TextContrast, "the text reads on the wall");
        SpineColour.ContrastRatio(StyleSheetTokens.Colour(tokens, "--room-muted"), wall).Should().BeGreaterThanOrEqualTo(TextContrast, "the muted text reads on the wall");
        SpineColour.ContrastRatio(StyleSheetTokens.Colour(tokens, "--room-line"), wall).Should().BeGreaterThanOrEqualTo(GraphicContrast, "the lines show on the wall");
    }

    [Theory]
    [MemberData(nameof(Woods))]
    public void The_label_on_an_accent_filled_control_reads_on_the_accent(string wood)
    {
        var tokens = Sheets.Value.ForWood(wood);

        SpineColour.ContrastRatio(StyleSheetTokens.Colour(tokens, "--room-on-accent"), StyleSheetTokens.Colour(tokens, "--room-accent"))
            .Should().BeGreaterThanOrEqualTo(TextContrast);
    }

    [Theory]
    [MemberData(nameof(Woods))]
    public void The_wall_is_clearly_dark_or_clearly_light_with_and_without_the_falloff(string wood)
    {
        var tokens = Sheets.Value.ForWood(wood);
        var plain = StyleSheetTokens.Colour(tokens, "--room");

        foreach (var wall in new[] { plain, BrightestWall(tokens) })
        {
            var luminance = StyleSheetTokens.Luminance(wall);

            (luminance < DarkWallBelow || luminance > LightWallAbove).Should().BeTrue($"{wood}'s wall has luminance {luminance:0.000}, which is neither clearly dark nor clearly light");
        }
    }

    [Theory]
    [MemberData(nameof(Woods))]
    public void The_logo_the_server_picks_for_a_wall_is_the_one_that_reads_on_it(string wood)
    {
        var tokens = Sheets.Value.ForWood(wood);
        var wall = BrightestWall(tokens);
        var lightWall = StyleSheetTokens.Luminance(StyleSheetTokens.Colour(tokens, "--room")) > LogoLuminanceRule;
        var logo = StyleSheetTokens.Colour(StyleSheetTokens.Parse($"x {{ --logo: {(lightWall ? NavyLogoColour : WhiteLogoColour)}; }}").Block("x"), "--logo");

        CabinetLook.HasLightWall(wood).Should().Be(lightWall);
        SpineColour.ContrastRatio(logo, wall).Should().BeGreaterThanOrEqualTo(TextContrast);
    }

    [Fact]
    public void The_dark_logo_file_is_drawn_in_the_navy_this_test_assumes()
    {
        var logo = File.ReadAllText(Path.Combine(RepositoryPaths.ServiceDirectory(), "wwwroot", "img", "powered-by-bgg-dark.svg"));

        logo.Should().Contain(NavyLogoColour.ToUpperInvariant());
    }

    [Theory]
    [MemberData(nameof(Woods))]
    public void The_focus_ring_of_the_page_shows_on_the_wall_and_the_ring_of_a_box_shows_on_every_wood_surface(string wood)
    {
        var tokens = Sheets.Value.ForWood(wood);
        var wall = BrightestWall(tokens);

        SpineColour.ContrastRatio(StyleSheetTokens.Colour(tokens, "--room-ring"), wall).Should().BeGreaterThanOrEqualTo(GraphicContrast, "the page's own ring shows on the wall");

        var boxRing = StyleSheetTokens.Colour(tokens, "--wood-light");
        var halo = HaloOrNull(tokens);
        var surfaces = new Dictionary<string, RgbColour>
        {
            ["frame and shelf"] = StyleSheetTokens.Colour(tokens, "--wood"),
            ["plain back"] = StyleSheetTokens.Colour(tokens, "--back"),
            ["painted back"] = StyleSheetTokens.Colour(tokens, "--back-painted"),
        };

        foreach (var (name, surface) in surfaces)
        {
            var throughAccent = SpineColour.ContrastRatio(boxRing, surface) >= GraphicContrast;
            var throughHalo = halo is { } ink
                && SpineColour.ContrastRatio(ink, surface) >= GraphicContrast
                && SpineColour.ContrastRatio(boxRing, ink) >= GraphicContrast;

            (throughAccent || throughHalo).Should().BeTrue($"the ring of a box on {wood}'s {name} reaches {GraphicContrast}:1 through the accent or the ink halo");
        }
    }

    /// <summary>
    /// The mark of a box that is open or has just been found with Show on shelf is the furniture ring, so it has to read against the
    /// frame, the shelf and both backs of every wood, by day and in the evening. The ring colour is the one the shipped rule draws.
    /// </summary>
    /// <param name="wood">The wood whose furniture the box stands on.</param>
    /// <param name="hour">The light state of the page, which never changes a furniture token.</param>
    [Theory]
    [MemberData(nameof(WoodsByHour))]
    public void The_mark_of_an_open_or_found_box_is_the_furniture_ring_and_shows_on_every_wood_surface_by_day_and_in_the_evening(string wood, string hour)
    {
        var rule = MarkRule().Match(File.ReadAllText(Path.Combine(RepositoryPaths.ServiceDirectory(), "wwwroot", "css", "cabinet.css")));

        rule.Success.Should().BeTrue("the open and found box share one outline rule that draws the furniture ring in 3px");

        var tokens = Sheets.Value.ForWood(wood);
        var mark = StyleSheetTokens.Colour(tokens, "--wood-light");
        var halo = HaloOrNull(tokens);
        var surfaces = new Dictionary<string, RgbColour>
        {
            ["frame and shelf"] = StyleSheetTokens.Colour(tokens, "--wood"),
            ["plain back"] = StyleSheetTokens.Colour(tokens, "--back"),
            ["painted back"] = StyleSheetTokens.Colour(tokens, "--back-painted"),
        };

        foreach (var (name, surface) in surfaces)
        {
            var throughAccent = SpineColour.ContrastRatio(mark, surface) >= GraphicContrast;
            var throughHalo = halo is { } ink
                && SpineColour.ContrastRatio(ink, surface) >= GraphicContrast
                && SpineColour.ContrastRatio(mark, ink) >= GraphicContrast;

            (throughAccent || throughHalo).Should().BeTrue($"the mark of an open or found box on {wood}'s {name} by {hour} reaches {GraphicContrast}:1 through the accent or the ink halo");
        }
    }

    [Theory]
    [MemberData(nameof(Woods))]
    public void Every_back_sits_in_a_safe_luminance_zone(string wood)
    {
        var tokens = Sheets.Value.ForWood(wood);

        foreach (var name in new[] { "--back", "--back-painted" })
        {
            var luminance = StyleSheetTokens.Luminance(StyleSheetTokens.Colour(tokens, name));

            (luminance <= DarkBackAtMost || (luminance >= MidBackLowest && luminance <= MidBackHighest))
                .Should().BeTrue($"{wood}'s {name} has luminance {luminance:0.0000}, which is in the zone where boxes can match the back");
        }
    }

    [Theory]
    [MemberData(nameof(Woods))]
    public void The_kick_is_darker_than_the_wall_and_on_a_light_wall_also_darker_than_the_front_but_never_black(string wood)
    {
        var tokens = Sheets.Value.ForWood(wood);
        var kick = StyleSheetTokens.Colour(tokens, "--recess");
        var wall = StyleSheetTokens.Colour(tokens, "--room");
        var front = StyleSheetTokens.Colour(tokens, "--wood");

        StyleSheetTokens.Luminance(kick).Should().BeLessThan(StyleSheetTokens.Luminance(wall));

        if (CabinetLook.HasLightWall(wood))
        {
            SpineColour.ContrastRatio(front, kick).Should().BeGreaterThanOrEqualTo(KickOverFrontAtLeast);
            SpineColour.ContrastRatio(wall, kick).Should().BeGreaterThanOrEqualTo(TextContrast);
            StyleSheetTokens.Luminance(kick).Should().BeGreaterThanOrEqualTo(KickNeverBlackBelow);
        }
    }

    /// <summary>
    /// The filter bar and its sheet read on every wall by day and in the evening: the control text, the muted text, the borders, the
    /// filled choice, the text on the hover wash, the focus ring and the quiet bar under the choice that means any. A stuck bar and the
    /// sheet stand on the flat wall; a bar still in the page stands on the wall under the brightest part of the falloff.
    /// </summary>
    /// <param name="wood">The wood whose wall is drawn.</param>
    /// <param name="hour">The light state of the page, which never changes a wall token.</param>
    [Theory]
    [MemberData(nameof(WoodsByHour))]
    public void The_filter_bar_and_the_sheet_read_on_every_wall_by_day_and_in_the_evening(string wood, string hour)
    {
        var tokens = Sheets.Value.ForWood(wood);
        var flat = StyleSheetTokens.Colour(tokens, "--room");
        var walls = new Dictionary<string, RgbColour>
        {
            ["the stuck bar and the sheet on the flat wall"] = flat,
            ["the bar in the page flow under the brightest falloff"] = BrightestWall(tokens),
        };
        var text = StyleSheetTokens.Colour(tokens, "--room-text");
        var muted = StyleSheetTokens.Colour(tokens, "--room-muted");
        var line = StyleSheetTokens.Colour(tokens, "--room-line");
        var ring = StyleSheetTokens.Colour(tokens, "--room-ring");

        foreach (var (surface, wall) in walls)
        {
            var label = $"{wood} by {hour}, {surface}";
            var wash = WashOver(tokens, wall);

            SpineColour.ContrastRatio(text, wall).Should().BeGreaterThanOrEqualTo(TextContrast, $"{label}: control text, legends, the count and a location name");
            SpineColour.ContrastRatio(muted, wall).Should().BeGreaterThanOrEqualTo(TextContrast, $"{label}: the hint, the placeholder, a heading count and disabled text");
            SpineColour.ContrastRatio(line, wall).Should().BeGreaterThanOrEqualTo(GraphicContrast, $"{label}: control borders and the segmented outline");
            SpineColour.ContrastRatio(text, wash).Should().BeGreaterThanOrEqualTo(TextContrast, $"{label}: control text on the hover wash");
            SpineColour.ContrastRatio(ring, wall).Should().BeGreaterThanOrEqualTo(GraphicContrast, $"{label}: the focus ring drawn outside a control");
            SpineColour.ContrastRatio(text, wall).Should().BeGreaterThanOrEqualTo(GraphicContrast, $"{label}: the quiet bar under the choice that means any");
        }

        SpineColour.ContrastRatio(flat, text).Should().BeGreaterThanOrEqualTo(TextContrast, $"{wood} by {hour}: the label on a filled choice, a highlighted Clear and the primary sheet button");
    }

    /// <summary>
    /// The desktop filter dock reads on every wall by day and in the evening. The rail and the panel open beside the cabinet stand on
    /// the wall under the brightest part of the falloff; the panel that lies over the cabinet stands on the flat wall colour it is
    /// filled with. Every pair the dock draws is measured on the surface it is drawn on: the symbols, the filled rail button, the
    /// phone's filled count, both counts, the muted rest count and "of 52" (never on a wash or a fill), the heading, legends and
    /// controls, the hover wash, the outlined and dashed borders with the muted symbol of a button that is off, and the focus ring.
    /// </summary>
    /// <param name="wood">The wood whose wall is drawn.</param>
    /// <param name="hour">The light state of the page, which never changes a wall token.</param>
    [Theory]
    [MemberData(nameof(WoodsByHour))]
    public void The_rail_and_the_panel_read_on_every_wall_by_day_and_in_the_evening(string wood, string hour)
    {
        var tokens = Sheets.Value.ForWood(wood);
        var docked = BrightestWall(tokens);
        var room = StyleSheetTokens.Colour(tokens, "--room");
        var flyOver = room;
        var text = StyleSheetTokens.Colour(tokens, "--room-text");
        var muted = StyleSheetTokens.Colour(tokens, "--room-muted");
        var line = StyleSheetTokens.Colour(tokens, "--room-line");
        var ring = StyleSheetTokens.Colour(tokens, "--room-ring");
        var label = $"{wood} by {hour}";
        var pairs = new List<ContrastPair>
        {
            new("rail symbols and the three-line symbol on the docked wall", text, docked, GraphicContrast),
            new("a filled rail button's symbol on its fill", room, text, GraphicContrast),
            new("the phone's filled count on its pill", room, text, TextContrast),
            new("the rail's count number and the panel's count while filtering on the docked wall", text, docked, TextContrast),
            new("the panel's count while filtering on the fly-over fill", text, flyOver, TextContrast),
            new("the rail's \"of 52\" and the panel's rest count on the docked wall", muted, docked, TextContrast),
            new("the panel's rest count on the fly-over fill", muted, flyOver, TextContrast),
            new("the panel heading, legends and controls on the docked wall", text, docked, TextContrast),
            new("the panel heading, legends and controls on the fly-over fill", text, flyOver, TextContrast),
            new("a hovered rail button on the wash over the docked wall", text, WashOver(tokens, docked), TextContrast),
            new("a hovered panel head button on the wash over the fly-over fill", text, WashOver(tokens, flyOver), TextContrast),
            new("the three-line and rail Clear borders on the docked wall", line, docked, GraphicContrast),
            new("the three-line and panel Clear borders on the fly-over fill", line, flyOver, GraphicContrast),
            new("the dashed border of a button that is off on the docked wall", line, docked, GraphicContrast),
            new("the muted symbol of a button that is off on the docked wall", muted, docked, TextContrast),
            new("the focus ring on the docked wall", ring, docked, GraphicContrast),
            new("the focus ring on the fly-over fill", ring, flyOver, GraphicContrast),
        };

        foreach (var pair in pairs)
        {
            SpineColour.ContrastRatio(pair.Foreground, pair.Background).Should().BeGreaterThanOrEqualTo(pair.Minimum, $"{label}: {pair.Name}");
        }
    }

    [Theory]
    [MemberData(nameof(Woods))]
    public void The_evening_changes_no_wall_token(string wood)
    {
        var modifiers = StyleSheetTokens.FromFiles(RepositoryPaths.ServiceDirectory(), "cabinet-modifiers.css");

        foreach (var selector in new[] { "[data-lights=\"on\"]", "[data-lights=\"evenings\"]", "[data-lit]", ":root" })
        {
            modifiers.Block(selector).Keys.Should().NotContain(name => name.StartsWith("--room", StringComparison.Ordinal), $"{wood}: the light state leaves the wall tokens of {selector} alone");
        }
    }

    private static RgbColour WashOver(IReadOnlyDictionary<string, string> tokens, RgbColour wall)
    {
        var wash = WashPattern().Match(StyleSheetTokens.Resolve(tokens, "--room-wash"));

        wash.Success.Should().BeTrue("the hover wash is the wall's text colour at a share over nothing");

        var share = double.Parse(wash.Groups["share"].Value, CultureInfo.InvariantCulture) / 100.0;

        return StyleSheetTokens.Over(StyleSheetTokens.Colour(tokens, "--room-text"), wall, share);
    }

    [GeneratedRegex(@"\.placement\[data-open\],\s*\.placement\[data-found\]\s*\{[^}]*outline:\s*3px solid var\(--wood-light\);")]
    private static partial Regex MarkRule();

    [GeneratedRegex(@"^color-mix\(in srgb, #[0-9a-fA-F]{6} (?<share>[0-9.]+)%, transparent\)$")]
    private static partial Regex WashPattern();

    private static TheoryData<string, string> BuildWoodsByHour()
    {
        var data = new TheoryData<string, string>();

        foreach (var wood in CabinetLook.Woods)
        {
            foreach (var hour in new[] { "day", "evening" })
            {
                data.Add(wood, hour);
            }
        }

        return data;
    }

    private static RgbColour BrightestWall(IReadOnlyDictionary<string, string> tokens)
    {
        var wall = StyleSheetTokens.Colour(tokens, "--room");
        var alpha = StyleSheetTokens.Number(tokens, "--room-lit-alpha");

        return alpha <= 0 ? wall : StyleSheetTokens.Over(StyleSheetTokens.Channels(tokens, "--falloff-rgb"), wall, alpha);
    }

    private static RgbColour? HaloOrNull(IReadOnlyDictionary<string, string> tokens) =>
        StyleSheetTokens.Resolve(tokens, "--ring-halo") == "transparent" ? null : StyleSheetTokens.Colour(tokens, "--ring-halo");

    /// <summary>One colour drawn on one surface, with the contrast it must reach there.</summary>
    /// <param name="Name">What is drawn and where, for the failure message.</param>
    /// <param name="Foreground">The colour that is drawn.</param>
    /// <param name="Background">The surface it is drawn on.</param>
    /// <param name="Minimum">The lowest contrast ratio allowed.</param>
    private sealed record ContrastPair(string Name, RgbColour Foreground, RgbColour Background, double Minimum);
}

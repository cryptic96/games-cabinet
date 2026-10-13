using System.Globalization;
using System.Text.RegularExpressions;
using Cabinet.Domain.Layout;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.UnitTests.Configuration;

/// <summary>
/// Pins the legibility of the owner tools' colours and of the corner tag on boxes that are not placed. The colours are read from the
/// shipped style sheets and blended the way the browser blends them, so a change to a token that breaks legibility fails here.
/// </summary>
[Trait("Category", "Configuration")]
public partial class OwnerContrastTests
{
    private const double TextContrast = 4.5;
    private const double GraphicContrast = 3.0;

    [Fact]
    public void Ink_and_the_label_colour_read_on_the_paper()
    {
        var tokens = ReadTokens();
        var paper = Colour(tokens, "--paper");

        SpineColour.ContrastRatio(Colour(tokens, "--ink"), paper).Should().BeGreaterThanOrEqualTo(TextContrast);
        SpineColour.ContrastRatio(Colour(tokens, "--paper-label"), paper).Should().BeGreaterThanOrEqualTo(TextContrast);
    }

    [Fact]
    public void Ink_and_the_label_colour_read_on_the_hover_tint_over_the_paper()
    {
        var tokens = ReadTokens();
        var tinted = TintOver(tokens, "--hover-tint", Colour(tokens, "--paper"));

        SpineColour.ContrastRatio(Colour(tokens, "--ink"), tinted).Should().BeGreaterThanOrEqualTo(TextContrast);
        SpineColour.ContrastRatio(Colour(tokens, "--paper-label"), tinted).Should().BeGreaterThanOrEqualTo(TextContrast);
    }

    [Fact]
    public void The_paper_reads_on_the_destructive_colour()
    {
        var tokens = ReadTokens();

        SpineColour.ContrastRatio(Colour(tokens, "--paper"), Colour(tokens, "--danger")).Should().BeGreaterThanOrEqualTo(TextContrast);
    }

    [Fact]
    public void The_destructive_colour_reads_on_the_paper_and_on_the_hover_tint()
    {
        var tokens = ReadTokens();
        var paper = Colour(tokens, "--paper");

        SpineColour.ContrastRatio(Colour(tokens, "--danger"), paper).Should().BeGreaterThanOrEqualTo(TextContrast);
        SpineColour.ContrastRatio(Colour(tokens, "--danger"), TintOver(tokens, "--hover-tint", paper)).Should().BeGreaterThanOrEqualTo(TextContrast);
    }

    [Fact]
    public void The_wall_text_reads_on_the_mid_and_dark_wood()
    {
        var tokens = ReadTokens();
        var wall = Colour(tokens, "--wall-text");

        SpineColour.ContrastRatio(wall, Colour(tokens, "--wood-mid")).Should().BeGreaterThanOrEqualTo(TextContrast);
        SpineColour.ContrastRatio(wall, Colour(tokens, "--wood-dark")).Should().BeGreaterThanOrEqualTo(TextContrast);
    }

    [Fact]
    public void Ink_reads_on_the_wall_text_colour()
    {
        var tokens = ReadTokens();

        SpineColour.ContrastRatio(Colour(tokens, "--ink"), Colour(tokens, "--wall-text")).Should().BeGreaterThanOrEqualTo(TextContrast);
    }

    [Fact]
    public void The_bar_fill_is_the_mid_wood_the_contrast_checks_assume()
    {
        var stylesheet = File.ReadAllText(StylesheetPath("owner.css"));

        stylesheet.Should().Contain("--owner-bar-bg: var(--wood-mid);");
    }

    [Fact]
    public void For_every_palette_background_at_least_one_of_the_tags_two_tones_reaches_three_to_one()
    {
        var tokens = ReadTokens();
        var paper = Colour(tokens, "--paper");
        var ink = Colour(tokens, "--ink");

        foreach (var tone in SpinePalette.Tones)
        {
            var background = Hex(tone.Background);
            var best = Math.Max(SpineColour.ContrastRatio(paper, background), SpineColour.ContrastRatio(ink, background));

            best.Should().BeGreaterThanOrEqualTo(GraphicContrast, $"the tag must show on {tone.Background}");
        }
    }

    [Fact]
    public void The_tag_is_painted_in_the_paper_and_ink_tokens_the_contrast_checks_assume()
    {
        var stylesheet = File.ReadAllText(StylesheetPath("owner.css"));
        var tag = TagRule().Match(stylesheet);

        tag.Success.Should().BeTrue("the owner style sheet draws the tag");
        tag.Groups["body"].Value.Should().Contain("var(--paper)").And.Contain("var(--ink)");
    }

    private static string StylesheetPath(string name) => Path.Combine(RepositoryPaths.ServiceDirectory(), "wwwroot", "css", name);

    private static Dictionary<string, string> ReadTokens()
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var name in new[] { "site.css", "owner.css" })
        {
            var stylesheet = File.ReadAllText(StylesheetPath(name));
            var root = RootBlock().Match(stylesheet);

            root.Success.Should().BeTrue($"{name} declares its tokens on the root element");

            foreach (Match declaration in TokenDeclaration().Matches(root.Groups["body"].Value))
            {
                tokens[declaration.Groups["name"].Value] = declaration.Groups["value"].Value.Trim();
            }
        }

        return tokens;
    }

    private static RgbColour Colour(Dictionary<string, string> tokens, string name)
    {
        tokens.Should().ContainKey(name);

        return Hex(tokens[name]);
    }

    private static RgbColour Hex(string value)
    {
        var match = HexColour().Match(value);

        match.Success.Should().BeTrue($"{value} is a hexadecimal colour");

        return new RgbColour(Channel(match, 1), Channel(match, 2), Channel(match, 3));
    }

    private static RgbColour TintOver(Dictionary<string, string> tokens, string name, RgbColour surface)
    {
        tokens.Should().ContainKey(name);

        var match = RgbaColour().Match(tokens[name]);

        match.Success.Should().BeTrue($"{name} is written as rgb(r g b / alpha)");

        var alpha = double.Parse(match.Groups["a"].Value, CultureInfo.InvariantCulture);
        var tint = new RgbColour(
            byte.Parse(match.Groups["r"].Value, CultureInfo.InvariantCulture),
            byte.Parse(match.Groups["g"].Value, CultureInfo.InvariantCulture),
            byte.Parse(match.Groups["b"].Value, CultureInfo.InvariantCulture));

        return new RgbColour(Mix(tint.R, surface.R, alpha), Mix(tint.G, surface.G, alpha), Mix(tint.B, surface.B, alpha));
    }

    private static byte Mix(byte top, byte bottom, double topShare) =>
        (byte)Math.Round((top * topShare) + (bottom * (1 - topShare)), MidpointRounding.AwayFromZero);

    private static byte Channel(Match match, int group) => byte.Parse(match.Groups[group].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    [GeneratedRegex(@":root\s*\{(?<body>[^}]*)\}")]
    private static partial Regex RootBlock();

    [GeneratedRegex(@"(?<name>--[a-z-]+):\s*(?<value>[^;]+);")]
    private static partial Regex TokenDeclaration();

    [GeneratedRegex(@"^#([0-9a-fA-F]{2})([0-9a-fA-F]{2})([0-9a-fA-F]{2})$")]
    private static partial Regex HexColour();

    [GeneratedRegex(@"^rgb\((?<r>\d+) (?<g>\d+) (?<b>\d+) / (?<a>[0-9.]+)\)$")]
    private static partial Regex RgbaColour();

    [GeneratedRegex(@"\.placement-tag\s*\{(?<body>[^}]*)\}")]
    private static partial Regex TagRule();
}

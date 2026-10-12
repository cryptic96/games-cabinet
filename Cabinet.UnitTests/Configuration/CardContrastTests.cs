using System.Globalization;
using System.Text.RegularExpressions;
using Cabinet.Domain.Layout;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.UnitTests.Configuration;

/// <summary>
/// Pins the legibility of the paper card and the language toggle: the card's text colours must read on the paper and on the faint
/// ruling drawn over it, and the toggle's colours must read on the dark wood. The colours are read from the shipped style sheet,
/// so a change to a token that breaks legibility fails here.
/// </summary>
[Trait("Category", "Configuration")]
public partial class CardContrastTests
{
    private const double MinimumContrast = 4.5;
    private const double MutedShare = 0.7;

    [Fact]
    public void Ink_and_the_label_colour_read_on_the_paper()
    {
        var tokens = ReadTokens();

        SpineColour.ContrastRatio(Colour(tokens, "--ink"), Colour(tokens, "--paper")).Should().BeGreaterThanOrEqualTo(MinimumContrast);
        SpineColour.ContrastRatio(Colour(tokens, "--paper-label"), Colour(tokens, "--paper")).Should().BeGreaterThanOrEqualTo(MinimumContrast);
    }

    [Fact]
    public void Ink_and_the_label_colour_read_on_the_ruling_drawn_over_the_paper()
    {
        var tokens = ReadTokens();
        var ruling = RulingOverPaper(tokens);

        SpineColour.ContrastRatio(Colour(tokens, "--ink"), ruling).Should().BeGreaterThanOrEqualTo(MinimumContrast);
        SpineColour.ContrastRatio(Colour(tokens, "--paper-label"), ruling).Should().BeGreaterThanOrEqualTo(MinimumContrast);
    }

    [Fact]
    public void The_toggle_colours_read_on_the_dark_wood()
    {
        var tokens = ReadTokens();
        var wood = Colour(tokens, "--wood-dark");
        var wall = Colour(tokens, "--wall-text");
        var muted = Mix(wall, wood, MutedShare);

        SpineColour.ContrastRatio(muted, wood).Should().BeGreaterThanOrEqualTo(MinimumContrast);
        SpineColour.ContrastRatio(wall, wood).Should().BeGreaterThanOrEqualTo(MinimumContrast);
    }

    [Fact]
    public void The_muted_chrome_token_is_the_mix_the_contrast_check_assumes()
    {
        var stylesheet = File.ReadAllText(Path.Combine(RepositoryPaths.ServiceDirectory(), "wwwroot", "css", "site.css"));

        stylesheet.Should().Contain("--chrome-muted: color-mix(in srgb, var(--wall-text) 70%, var(--wood-dark));");
    }

    private static Dictionary<string, string> ReadTokens()
    {
        var stylesheet = File.ReadAllText(Path.Combine(RepositoryPaths.ServiceDirectory(), "wwwroot", "css", "site.css"));
        var root = RootBlock().Match(stylesheet);

        root.Success.Should().BeTrue("the style sheet declares its tokens on the root element");

        return TokenDeclaration().Matches(root.Groups["body"].Value).ToDictionary(match => match.Groups["name"].Value, match => match.Groups["value"].Value.Trim());
    }

    private static RgbColour Colour(Dictionary<string, string> tokens, string name)
    {
        tokens.Should().ContainKey(name);

        var match = HexColour().Match(tokens[name]);

        match.Success.Should().BeTrue($"{name} is a hexadecimal colour");

        return new RgbColour(Channel(match, 1), Channel(match, 2), Channel(match, 3));
    }

    private static RgbColour RulingOverPaper(Dictionary<string, string> tokens)
    {
        tokens.Should().ContainKey("--paper-rule");

        var match = RgbaColour().Match(tokens["--paper-rule"]);

        match.Success.Should().BeTrue("the ruling is written as rgb(r g b / alpha)");

        var alpha = double.Parse(match.Groups["a"].Value, CultureInfo.InvariantCulture);
        var line = new RgbColour(byte.Parse(match.Groups["r"].Value, CultureInfo.InvariantCulture), byte.Parse(match.Groups["g"].Value, CultureInfo.InvariantCulture), byte.Parse(match.Groups["b"].Value, CultureInfo.InvariantCulture));

        return Mix(line, Colour(tokens, "--paper"), alpha);
    }

    private static RgbColour Mix(RgbColour top, RgbColour bottom, double topShare) =>
        new(Mix(top.R, bottom.R, topShare), Mix(top.G, bottom.G, topShare), Mix(top.B, bottom.B, topShare));

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
}

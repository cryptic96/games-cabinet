using System.Globalization;
using System.Text;
using Cabinet.Domain.Layout;

namespace Cabinet.UnitTests.Configuration;

/// <summary>
/// Reads the custom properties a style sheet declares in each selector block, so a test can work out the colours the browser
/// ends up with from the shipped files instead of repeating their values. Comments are dropped, blocks inside at-rules such as
/// layers and container queries are found too, and a value is resolved by following its <c>var()</c> references within the
/// merged blocks and by working out the <c>color-mix()</c> calls that mix in sRGB or in OKLCH with a grey.
/// </summary>
public sealed class StyleSheetTokens
{
    private const double ByteMax = 255.0;
    private const double PercentDivisor = 100.0;
    private const int MaxReferenceDepth = 32;

    private readonly List<(IReadOnlyList<string> Selectors, IReadOnlyDictionary<string, string> Declarations)> _blocks;

    private StyleSheetTokens(List<(IReadOnlyList<string> Selectors, IReadOnlyDictionary<string, string> Declarations)> blocks) => _blocks = blocks;

    /// <summary>Reads the given style sheets of the web service, in order, as one set of blocks.</summary>
    /// <param name="serviceDirectory">The folder of the web service project.</param>
    /// <param name="fileNames">The names of the files under <c>wwwroot/css</c>.</param>
    public static StyleSheetTokens FromFiles(string serviceDirectory, params string[] fileNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceDirectory);
        ArgumentNullException.ThrowIfNull(fileNames);

        var combined = new StringBuilder();

        foreach (var fileName in fileNames)
        {
            combined.AppendLine(File.ReadAllText(Path.Combine(serviceDirectory, "wwwroot", "css", fileName)));
        }

        return Parse(combined.ToString());
    }

    /// <summary>Reads style sheet text as a list of selector blocks with their custom property declarations.</summary>
    /// <param name="stylesheet">The style sheet text.</param>
    public static StyleSheetTokens Parse(string stylesheet)
    {
        ArgumentNullException.ThrowIfNull(stylesheet);

        var blocks = new List<(IReadOnlyList<string>, IReadOnlyDictionary<string, string>)>();
        Collect(StripComments(stylesheet), blocks);

        return new StyleSheetTokens(blocks);
    }

    /// <summary>The custom properties of every block whose selector list names exactly the given selector, later blocks overriding earlier ones.</summary>
    /// <param name="selector">One selector, such as <c>:root</c> or <c>[data-wood="oak"]</c>.</param>
    public IReadOnlyDictionary<string, string> Block(string selector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);

        var merged = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (selectors, declarations) in _blocks.Where(block => block.Selectors.Contains(selector, StringComparer.Ordinal)))
        {
            foreach (var (name, value) in declarations)
            {
                merged[name] = value;
            }
        }

        return merged;
    }

    /// <summary>The custom properties an element has when the root block applies and then the block of the given wood.</summary>
    /// <param name="wood">The wood, as it is stored.</param>
    public IReadOnlyDictionary<string, string> ForWood(string wood)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wood);

        var merged = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var selector in new[] { ":root", "[data-wood]", $"[data-wood=\"{wood}\"]" })
        {
            foreach (var (name, value) in Block(selector))
            {
                merged[name] = value;
            }
        }

        return merged;
    }

    /// <summary>Gives the value of a token with every <c>var()</c> reference replaced, as the browser would compute it on the element.</summary>
    /// <param name="tokens">The custom properties of the element.</param>
    /// <param name="name">The custom property name, including the two dashes.</param>
    public static string Resolve(IReadOnlyDictionary<string, string> tokens, string name)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!tokens.TryGetValue(name, out var value))
        {
            throw new KeyNotFoundException($"{name} is not declared.");
        }

        return Substitute(tokens, value, 0);
    }

    /// <summary>Works out a token as an sRGB colour, following references and colour mixes.</summary>
    /// <param name="tokens">The custom properties of the element.</param>
    /// <param name="name">The custom property name, including the two dashes.</param>
    public static RgbColour Colour(IReadOnlyDictionary<string, string> tokens, string name) =>
        ToRgb(Evaluate(Resolve(tokens, name)));

    /// <summary>Works out a token that holds one number, such as an alpha or a strength.</summary>
    /// <param name="tokens">The custom properties of the element.</param>
    /// <param name="name">The custom property name, including the two dashes.</param>
    public static double Number(IReadOnlyDictionary<string, string> tokens, string name) =>
        double.Parse(Resolve(tokens, name), CultureInfo.InvariantCulture);

    /// <summary>Works out a token that holds three space-separated channels, such as <c>255 244 228</c>.</summary>
    /// <param name="tokens">The custom properties of the element.</param>
    /// <param name="name">The custom property name, including the two dashes.</param>
    public static RgbColour Channels(IReadOnlyDictionary<string, string> tokens, string name)
    {
        var parts = Resolve(tokens, name).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return new RgbColour(
            byte.Parse(parts[0], CultureInfo.InvariantCulture),
            byte.Parse(parts[1], CultureInfo.InvariantCulture),
            byte.Parse(parts[2], CultureInfo.InvariantCulture));
    }

    /// <summary>Lays a colour over another on their gamma-encoded channels, as the browser blends a translucent paint.</summary>
    /// <param name="top">The colour painted on top.</param>
    /// <param name="bottom">The colour underneath.</param>
    /// <param name="topShare">The opacity of the top colour, from 0 to 1.</param>
    public static RgbColour Over(RgbColour top, RgbColour bottom, double topShare) =>
        ToRgb(Blend(ToChannels(top), ToChannels(bottom), topShare));

    /// <summary>The relative luminance of a colour as WCAG defines it, from 0 for black to 1 for white.</summary>
    /// <param name="colour">The colour.</param>
    public static double Luminance(RgbColour colour) =>
        (SpineColour.ContrastRatio(colour, SpineColour.Black) * 0.05) - 0.05;

    private static string Substitute(IReadOnlyDictionary<string, string> tokens, string value, int depth)
    {
        if (depth > MaxReferenceDepth)
        {
            throw new InvalidOperationException("A custom property refers to itself.");
        }

        var result = new StringBuilder();
        var index = 0;

        while (index < value.Length)
        {
            var open = value.IndexOf("var(", index, StringComparison.Ordinal);

            if (open < 0)
            {
                result.Append(value, index, value.Length - index);
                break;
            }

            result.Append(value, index, open - index);
            var close = MatchingParenthesis(value, open + 3);
            var inner = value[(open + 4)..close];
            var comma = TopLevelComma(inner);
            var referenced = (comma < 0 ? inner : inner[..comma]).Trim();
            var fallback = comma < 0 ? null : inner[(comma + 1)..].Trim();

            if (tokens.TryGetValue(referenced, out var found))
            {
                result.Append(Substitute(tokens, found, depth + 1));
            }
            else if (fallback is not null)
            {
                result.Append(Substitute(tokens, fallback, depth + 1));
            }
            else
            {
                throw new KeyNotFoundException($"{referenced} is not declared.");
            }

            index = close + 1;
        }

        return result.ToString().Trim();
    }

    private static double[] Evaluate(string value)
    {
        if (value.StartsWith('#'))
        {
            return ParseHex(value);
        }

        if (value.StartsWith("color-mix(", StringComparison.Ordinal))
        {
            return EvaluateMix(value[10..^1]);
        }

        if (value == "black")
        {
            return [0, 0, 0];
        }

        if (value == "white")
        {
            return [ByteMax, ByteMax, ByteMax];
        }

        throw new NotSupportedException($"'{value}' is not a colour this reader understands.");
    }

    private static double[] EvaluateMix(string arguments)
    {
        var parts = SplitTopLevel(arguments);
        var space = parts[0].Trim();
        var (first, firstShare) = ParseStop(parts[1]);
        var (second, secondShare) = ParseStop(parts[2]);
        var firstPercent = firstShare ?? (secondShare is { } given ? PercentDivisor - given : PercentDivisor / 2);
        var secondPercent = secondShare ?? (firstShare is { } own ? PercentDivisor - own : PercentDivisor / 2);
        var share = firstPercent / (firstPercent + secondPercent);

        var one = Evaluate(first);
        var two = Evaluate(second);

        return space switch
        {
            "in srgb" => Blend(one, two, share),
            "in oklch" => MixInOklch(one, two, share),
            _ => throw new NotSupportedException($"'{space}' is not a colour space this reader mixes in."),
        };
    }

    private static (string Colour, double? Share) ParseStop(string stop)
    {
        var text = stop.Trim();
        var lastSpace = text.LastIndexOf(' ');

        if (lastSpace > 0 && text.EndsWith('%'))
        {
            var percent = text[(lastSpace + 1)..^1];

            if (double.TryParse(percent, NumberStyles.Float, CultureInfo.InvariantCulture, out var share))
            {
                return (text[..lastSpace].Trim(), share);
            }
        }

        return (text, null);
    }

    private static double[] Blend(double[] top, double[] bottom, double topShare) =>
    [
        (top[0] * topShare) + (bottom[0] * (1 - topShare)),
        (top[1] * topShare) + (bottom[1] * (1 - topShare)),
        (top[2] * topShare) + (bottom[2] * (1 - topShare)),
    ];

    private static double[] MixInOklch(double[] first, double[] second, double firstShare)
    {
        var one = ToOklch(first);
        var two = ToOklch(second);
        var secondShare = 1 - firstShare;
        var lightness = (one.L * firstShare) + (two.L * secondShare);
        var chroma = (one.C * firstShare) + (two.C * secondShare);
        var hue = one.C < 1e-4 ? two.H : one.H;

        return FromOklch(lightness, chroma, hue);
    }

    private static (double L, double C, double H) ToOklch(double[] channels)
    {
        var r = ToLinear(channels[0] / ByteMax);
        var g = ToLinear(channels[1] / ByteMax);
        var b = ToLinear(channels[2] / ByteMax);
        var l = Math.Cbrt((0.4122214708 * r) + (0.5363325363 * g) + (0.0514459929 * b));
        var m = Math.Cbrt((0.2119034982 * r) + (0.6806995451 * g) + (0.1073969566 * b));
        var s = Math.Cbrt((0.0883024619 * r) + (0.2817188376 * g) + (0.6299787005 * b));
        var lightness = (0.2104542553 * l) + (0.7936177850 * m) - (0.0040720468 * s);
        var a = (1.9779984951 * l) - (2.4285922050 * m) + (0.4505937099 * s);
        var bb = (0.0259040371 * l) + (0.7827717662 * m) - (0.8086757660 * s);

        return (lightness, Math.Sqrt((a * a) + (bb * bb)), Math.Atan2(bb, a));
    }

    private static double[] FromOklch(double lightness, double chroma, double hue)
    {
        var a = chroma * Math.Cos(hue);
        var b = chroma * Math.Sin(hue);
        var l = Cube(lightness + (0.3963377774 * a) + (0.2158037573 * b));
        var m = Cube(lightness - (0.1055613458 * a) - (0.0638541728 * b));
        var s = Cube(lightness - (0.0894841775 * a) - (1.2914855480 * b));
        var r = (4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s);
        var g = (-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s);
        var blue = (-0.0041960863 * l) - (0.7034186147 * m) + (1.7076147010 * s);

        return [FromLinear(r) * ByteMax, FromLinear(g) * ByteMax, FromLinear(blue) * ByteMax];
    }

    private static double Cube(double value) => value * value * value;

    private static double ToLinear(double value) =>
        value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);

    private static double FromLinear(double value)
    {
        var clamped = Math.Clamp(value, 0, 1);

        return clamped <= 0.0031308 ? clamped * 12.92 : (1.055 * Math.Pow(clamped, 1 / 2.4)) - 0.055;
    }

    private static double[] ParseHex(string value)
    {
        var digits = value[1..];

        if (digits.Length == 3)
        {
            digits = string.Concat(digits.Select(digit => new string(digit, 2)));
        }

        return
        [
            byte.Parse(digits.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(digits.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(digits.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        ];
    }

    private static double[] ToChannels(RgbColour colour) => [colour.R, colour.G, colour.B];

    private static RgbColour ToRgb(double[] channels) => new(
        (byte)Math.Clamp(Math.Round(channels[0], MidpointRounding.AwayFromZero), 0, ByteMax),
        (byte)Math.Clamp(Math.Round(channels[1], MidpointRounding.AwayFromZero), 0, ByteMax),
        (byte)Math.Clamp(Math.Round(channels[2], MidpointRounding.AwayFromZero), 0, ByteMax));

    private static string StripComments(string stylesheet)
    {
        var result = new StringBuilder();
        var index = 0;

        while (index < stylesheet.Length)
        {
            var open = stylesheet.IndexOf("/*", index, StringComparison.Ordinal);

            if (open < 0)
            {
                result.Append(stylesheet, index, stylesheet.Length - index);
                break;
            }

            result.Append(stylesheet, index, open - index);
            var close = stylesheet.IndexOf("*/", open + 2, StringComparison.Ordinal);
            index = close < 0 ? stylesheet.Length : close + 2;
        }

        return result.ToString();
    }

    private static void Collect(string text, List<(IReadOnlyList<string>, IReadOnlyDictionary<string, string>)> blocks)
    {
        var index = 0;

        while (index < text.Length)
        {
            var open = text.IndexOf('{', index);
            var semicolon = text.IndexOf(';', index);

            if (open < 0)
            {
                return;
            }

            if (semicolon >= 0 && semicolon < open)
            {
                index = semicolon + 1;
                continue;
            }

            var prelude = text[index..open].Trim();
            var close = MatchingBrace(text, open);
            var body = text[(open + 1)..close];
            index = close + 1;

            if (prelude.StartsWith('@') && body.Contains('{', StringComparison.Ordinal))
            {
                Collect(body, blocks);
                continue;
            }

            var selectors = prelude.Split(',').Select(selector => string.Join(' ', selector.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))).ToList();
            blocks.Add((selectors, ParseDeclarations(body)));
        }
    }

    private static Dictionary<string, string> ParseDeclarations(string body)
    {
        var declarations = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var declaration in SplitTopLevel(body, ';'))
        {
            var colon = declaration.IndexOf(':', StringComparison.Ordinal);

            if (colon <= 0)
            {
                continue;
            }

            var name = declaration[..colon].Trim();

            if (name.StartsWith("--", StringComparison.Ordinal))
            {
                declarations[name] = string.Join(' ', declaration[(colon + 1)..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            }
        }

        return declarations;
    }

    private static int MatchingBrace(string text, int open)
    {
        var depth = 0;

        for (var index = open; index < text.Length; index++)
        {
            depth += text[index] == '{' ? 1 : text[index] == '}' ? -1 : 0;

            if (depth == 0)
            {
                return index;
            }
        }

        throw new FormatException("A block in the style sheet is not closed.");
    }

    private static int MatchingParenthesis(string text, int open)
    {
        var depth = 0;

        for (var index = open; index < text.Length; index++)
        {
            depth += text[index] == '(' ? 1 : text[index] == ')' ? -1 : 0;

            if (depth == 0)
            {
                return index;
            }
        }

        throw new FormatException("A parenthesis in a value is not closed.");
    }

    private static int TopLevelComma(string text)
    {
        var depth = 0;

        for (var index = 0; index < text.Length; index++)
        {
            depth += text[index] == '(' ? 1 : text[index] == ')' ? -1 : 0;

            if (text[index] == ',' && depth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static List<string> SplitTopLevel(string text, char separator = ',')
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;

        for (var index = 0; index < text.Length; index++)
        {
            depth += text[index] == '(' ? 1 : text[index] == ')' ? -1 : 0;

            if (text[index] == separator && depth == 0)
            {
                parts.Add(text[start..index]);
                start = index + 1;
            }
        }

        parts.Add(text[start..]);

        return parts;
    }
}

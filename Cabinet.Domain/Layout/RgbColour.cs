using System.Globalization;

namespace Cabinet.Domain.Layout;

/// <summary>An opaque colour in the sRGB colour space with one byte per channel.</summary>
/// <param name="R">The red channel, 0 to 255.</param>
/// <param name="G">The green channel, 0 to 255.</param>
/// <param name="B">The blue channel, 0 to 255.</param>
public readonly record struct RgbColour(byte R, byte G, byte B)
{
    private const int HexLength = 7;
    private const int ChannelDigits = 2;

    /// <summary>The colour as lowercase six-digit hexadecimal text such as <c>#bd1e28</c>.</summary>
    public string ToHex() => string.Create(CultureInfo.InvariantCulture, $"#{R:x2}{G:x2}{B:x2}");

    /// <summary>
    /// Reads a colour written exactly as <c>#</c> followed by six lowercase hexadecimal digits. Anything else, including
    /// uppercase digits, three-digit shorthand, colour names and null, is refused.
    /// </summary>
    /// <param name="text">The text to read.</param>
    /// <param name="colour">The colour when the text is well formed, otherwise black.</param>
    /// <returns>Whether the text was well formed.</returns>
    public static bool TryParseHex(string? text, out RgbColour colour)
    {
        colour = default;

        if (text is null || text.Length != HexLength || text[0] != '#')
        {
            return false;
        }

        for (var index = 1; index < text.Length; index++)
        {
            if (!IsLowerHexDigit(text[index]))
            {
                return false;
            }
        }

        colour = new RgbColour(Channel(text, 0), Channel(text, 1), Channel(text, 2));

        return true;
    }

    private static bool IsLowerHexDigit(char character) => character is (>= '0' and <= '9') or (>= 'a' and <= 'f');

    private static byte Channel(string text, int index) =>
        byte.Parse(text.AsSpan(1 + (index * ChannelDigits), ChannelDigits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
}

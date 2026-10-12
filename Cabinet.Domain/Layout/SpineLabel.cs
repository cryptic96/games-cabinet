using System.Globalization;

namespace Cabinet.Domain.Layout;

/// <summary>
/// Shortens a title for the text drawn on a spine or a flat box. Only the text on the box is shortened; the full title
/// always travels with the placement so it can still be read in full. Lengths are counted in text elements, so an emoji,
/// a surrogate pair or a letter with a combining mark is never split.
/// </summary>
public static class SpineLabel
{
    /// <summary>The fewest text elements a label may be shortened to, however small the room on the box.</summary>
    public const int MinTextElements = 3;

    /// <summary>
    /// The fewest text elements a shortened label may show before its ellipsis. Below that the label is left empty so the
    /// box shows only its colour and rules instead of a scrap of text; the full title stays in the name and tooltip.
    /// </summary>
    public const int MinVisibleLabelChars = 5;

    private const string Ellipsis = "…";
    private const string ColonSeparator = ": ";
    private const string DashSeparator = " - ";

    private static readonly char[] Whitespace = [' ', '\t', '\n', '\r'];
    private static readonly char[] Separators = [',', ';', ':', '-', '\u2013', '\u2014', '&', ' '];

    /// <summary>The short joining words, English and Dutch, that never end a shortened label on their own.</summary>
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "of", "and", "or", "in", "on", "at", "to", "for", "with", "from", "by", "vs",
        "de", "het", "een", "van", "en",
    };

    /// <summary>
    /// Trims the title and cuts it at its first colon or spaced dash when something stands before that point. When the
    /// remaining text is longer than the budget it keeps the first budget-minus-one text elements, drops any stop words
    /// and separators the cut leaves dangling at its end, and appends an ellipsis. A title made only of stop words keeps
    /// its plain cut. The budget is raised to <see cref="MinTextElements"/> when it is lower. A shortened label that
    /// would show fewer than <see cref="MinVisibleLabelChars"/> text elements before its ellipsis is left empty, and a
    /// title that fits is never shortened or hidden. A blank title gives an empty label.
    /// </summary>
    /// <param name="title">The full title.</param>
    /// <param name="maxTextElements">The most text elements the box has room for.</param>
    public static string Shorten(string title, int maxTextElements)
    {
        ArgumentNullException.ThrowIfNull(title);

        var text = CutAtSeparator(title.Trim());
        var budget = Math.Max(maxTextElements, MinTextElements);
        var length = new StringInfo(text).LengthInTextElements;

        if (length <= budget)
        {
            return text;
        }

        var kept = DropDanglingStopWords(new StringInfo(text).SubstringByTextElements(0, budget - 1));

        return new StringInfo(kept).LengthInTextElements < MinVisibleLabelChars ? string.Empty : kept + Ellipsis;
    }

    private static string DropDanglingStopWords(string cut)
    {
        var kept = cut.TrimEnd();

        while (true)
        {
            var wordStart = kept.LastIndexOfAny(Whitespace) + 1;
            var word = kept[wordStart..].Trim(Separators);

            if (wordStart == 0 || !StopWords.Contains(word))
            {
                break;
            }

            kept = kept[..wordStart].TrimEnd();
        }

        kept = kept.TrimEnd(Separators).TrimEnd();

        return StopWords.Contains(kept.Trim(Separators)) || kept.Length == 0 ? cut : kept;
    }

    private static string CutAtSeparator(string text)
    {
        var colon = text.IndexOf(ColonSeparator, StringComparison.Ordinal);
        var dash = text.IndexOf(DashSeparator, StringComparison.Ordinal);
        var cut = (colon, dash) switch
        {
            (< 0, < 0) => -1,
            (< 0, _) => dash,
            (_, < 0) => colon,
            _ => Math.Min(colon, dash),
        };

        if (cut <= 0)
        {
            return text;
        }

        var before = text[..cut].TrimEnd();

        return before.Length == 0 ? text : before;
    }
}

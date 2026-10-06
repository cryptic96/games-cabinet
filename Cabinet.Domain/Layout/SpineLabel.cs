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

    private const string Ellipsis = "…";
    private const string ColonSeparator = ": ";
    private const string DashSeparator = " - ";

    /// <summary>
    /// Trims the title and cuts it at its first colon or spaced dash when something stands before that point. When the
    /// remaining text is longer than the budget it keeps the first budget-minus-one text elements and appends an ellipsis.
    /// The budget is raised to <see cref="MinTextElements"/> when it is lower. A blank title gives an empty label.
    /// </summary>
    /// <param name="title">The full title.</param>
    /// <param name="maxTextElements">The most text elements the box has room for.</param>
    public static string Shorten(string title, int maxTextElements)
    {
        ArgumentNullException.ThrowIfNull(title);

        var text = CutAtSeparator(title.Trim());
        var budget = Math.Max(maxTextElements, MinTextElements);
        var length = new StringInfo(text).LengthInTextElements;

        return length <= budget
            ? text
            : new StringInfo(text).SubstringByTextElements(0, budget - 1) + Ellipsis;
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

using System.Globalization;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using SkiaSharp;

namespace Cabinet.Service.Review;

/// <summary>
/// Draws the review sheet: pages of eight games, one row each, with both candidate pictures, the verdict, the choice, the
/// result as the cabinet draws it, the spine and the size source. Text is drawn with the typefaces the caller loads, because
/// a machine with no fonts would otherwise draw nothing at all.
/// </summary>
public static class ReviewSheet
{
    /// <summary>How many games one page holds.</summary>
    public const int RowsPerPage = 8;

    /// <summary>The width of a page in pixels.</summary>
    public const int PageWidthPx = 1600;

    /// <summary>The height of the pictures, the result and the spine in pixels.</summary>
    public const int ArtHeightPx = 160;

    /// <summary>The width of the spine strip in pixels.</summary>
    public const int SpineWidthPx = 40;

    /// <summary>The thickness of the outline around the chosen candidate in pixels.</summary>
    public const int OutlinePx = 3;

    private const int Margin = 22;
    private const int HeaderHeight = 92;
    private const int FirstRulesBaseline = 62;
    private const int RulesLineHeight = 20;
    private const string RuleSeparator = "|";
    private const string RuleJoint = "   ";
    private const int ColumnHeaderHeight = 36;
    private const int RowPadding = 10;
    private const int CellPadding = 8;
    private const int RowHeight = ArtHeightPx + (2 * RowPadding);
    private const int PositionWidth = 56;
    private const int TitleWidth = 300;
    private const int CandidateWidth = 210;
    private const int VerdictWidth = 150;
    private const int ChosenWidth = 230;
    private const int ResultWidth = 210;
    private const int SpineCellWidth = 70;
    private const int SizeWidth = 120;
    private const int PageBottomMargin = 16;
    private const int PngQuality = 100;
    private const float BodyTextSize = 17f;
    private const float HeaderTextSize = 21f;
    private const float SmallTextSize = 14f;
    private const float PercentFactor = 100f;
    private const float QuarterTurn = 90f;
    private const string Ellipsis = "…";
    private const string MissingWord = "none";

    private static readonly SKColor PageColour = new(0xf2, 0xf0, 0xec);
    private static readonly SKColor RowColour = new(0xff, 0xff, 0xff);
    private static readonly SKColor RowEdgeColour = new(0xd8, 0xd4, 0xcc);
    private static readonly SKColor InkColour = new(0x22, 0x20, 0x1c);
    private static readonly SKColor MutedColour = new(0x6a, 0x66, 0x5e);
    private static readonly SKColor ChosenOutlineColour = new(0xd9, 0xb9, 0x8a);
    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    /// <summary>
    /// Draws every page. A page holds <see cref="RowsPerPage"/> rows and the last page holds the rest; with no rows there is
    /// one page with only its header. A picture that cannot be read is drawn as missing.
    /// </summary>
    /// <param name="rows">The rows, in collection order.</param>
    /// <param name="rules">The rules in use, stated in each page header.</param>
    /// <param name="typeface">The typeface of the body text.</param>
    /// <param name="boldTypeface">The typeface of the headings.</param>
    /// <returns>One PNG file per page.</returns>
    public static IReadOnlyList<byte[]> Draw(
        IReadOnlyList<ReviewRow> rows,
        ArtRules rules,
        SKTypeface typeface,
        SKTypeface boldTypeface)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(typeface);
        ArgumentNullException.ThrowIfNull(boldTypeface);

        var pageCount = Math.Max(1, (rows.Count + RowsPerPage - 1) / RowsPerPage);
        var pages = new List<byte[]>(pageCount);

        using var body = new SKFont(typeface, BodyTextSize);
        using var small = new SKFont(typeface, SmallTextSize);
        using var heading = new SKFont(boldTypeface, BodyTextSize);
        using var title = new SKFont(boldTypeface, HeaderTextSize);

        for (var page = 0; page < pageCount; page++)
        {
            var pageRows = rows.Skip(page * RowsPerPage).Take(RowsPerPage).ToList();
            var fonts = new Fonts(body, small, heading, title);
            pages.Add(DrawPage(pageRows, rows.Count, page + 1, pageCount, rules, fonts));
        }

        return pages;
    }

    /// <summary>The sentence in each page header that states the rules in use.</summary>
    /// <param name="rules">The rules in use.</param>
    public static string RulesText(ArtRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var thresholds = rules.Thresholds;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"flat: fill at least {Percent(thresholds.FlatMinFill)}% and emptiest corner at most {Percent(thresholds.FlatMaxCorner)}%   |   3D shot: fill at most {Percent(thresholds.ThreeDMaxFill)}% and second corner at least {Percent(thresholds.ThreeDMinCorner)}%   |   shape margin {rules.ShapeMarginPercent}%   |   landscape covers make landscape boxes: {(rules.OrientFromCover ? "yes" : "no")}   |   unsure pictures wider by more than {rules.UnsureLandscapeMarginPercent}% turn boxes");
    }

    /// <summary>
    /// Breaks the rules sentence into lines that each measure at most the given width. A line break falls at a rule
    /// separator when it can, and at a space inside a rule that is itself too wide. Joining the lines with a space gives the
    /// sentence back, apart from the spacing around the separators.
    /// </summary>
    /// <param name="rules">The rules in use.</param>
    /// <param name="font">The font the lines are drawn with.</param>
    /// <param name="maxWidth">The widest a line may measure.</param>
    public static IReadOnlyList<string> RulesLines(ArtRules rules, SKFont font, float maxWidth)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(font);

        var parts = RulesText(rules).Split(RuleSeparator, StringSplitOptions.TrimEntries);
        var lines = new List<string>();
        var current = string.Empty;

        for (var index = 0; index < parts.Length; index++)
        {
            var part = index < parts.Length - 1 ? parts[index] + RuleJoint + RuleSeparator : parts[index];
            var joined = current.Length == 0 ? part : current + RuleJoint + part;

            if (font.MeasureText(joined) <= maxWidth)
            {
                current = joined;

                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(current);
                current = string.Empty;
            }

            current = WrapWide(part, font, maxWidth, lines);
        }

        if (current.Length > 0)
        {
            lines.Add(current);
        }

        return lines;
    }

    /// <summary>The height of the page header in pixels when the rules sentence takes the given number of lines.</summary>
    /// <param name="rulesLineCount">The number of lines the rules sentence takes, at least one.</param>
    public static int HeaderHeightPx(int rulesLineCount) => HeaderHeight + (Math.Max(1, rulesLineCount) - 1) * RulesLineHeight;

    /// <summary>The baseline in pixels of one line of the rules sentence.</summary>
    /// <param name="lineIndex">The zero-based line number.</param>
    public static int RulesBaselinePx(int lineIndex) => FirstRulesBaseline + (lineIndex * RulesLineHeight);

    /// <summary>The height of a page in pixels for the given number of rows and rules lines.</summary>
    /// <param name="rowCount">The number of game rows on the page.</param>
    /// <param name="rulesLineCount">The number of lines the rules sentence takes.</param>
    public static int PageHeightPx(int rowCount, int rulesLineCount) =>
        HeaderHeightPx(rulesLineCount) + ColumnHeaderHeight + (rowCount * RowHeight) + PageBottomMargin;

    /// <summary>The widest a line of the rules sentence may measure on a page, in pixels.</summary>
    public const int RulesWidthPx = PageWidthPx - (2 * Margin);

    private static string WrapWide(string part, SKFont font, float maxWidth, List<string> lines)
    {
        var current = string.Empty;

        foreach (var word in part.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var joined = current.Length == 0 ? word : current + " " + word;

            if (current.Length > 0 && font.MeasureText(joined) > maxWidth)
            {
                lines.Add(current);
                current = word;

                continue;
            }

            current = joined;
        }

        return current;
    }

    private static string Percent(double share) => Math.Round(share * PercentFactor).ToString(CultureInfo.InvariantCulture);

    private static byte[] DrawPage(
        IReadOnlyList<ReviewRow> pageRows,
        int totalRows,
        int pageNumber,
        int pageCount,
        ArtRules rules,
        Fonts fonts)
    {
        var rulesLines = RulesLines(rules, fonts.Small, RulesWidthPx);
        var headerHeight = HeaderHeightPx(rulesLines.Count);
        var height = PageHeightPx(pageRows.Count, rulesLines.Count);

        using var bitmap = new SKBitmap(new SKImageInfo(PageWidthPx, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(PageColour);
            DrawHeader(canvas, pageRows, totalRows, pageNumber, pageCount, rulesLines, headerHeight, fonts);

            var top = headerHeight + ColumnHeaderHeight;

            foreach (var row in pageRows)
            {
                DrawRow(canvas, row, top, fonts);
                top += RowHeight;
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, PngQuality);

        return png.ToArray();
    }

    private static void DrawHeader(
        SKCanvas canvas,
        IReadOnlyList<ReviewRow> pageRows,
        int totalRows,
        int pageNumber,
        int pageCount,
        IReadOnlyList<string> rulesLines,
        int headerHeight,
        Fonts fonts)
    {
        var range = pageRows.Count == 0
            ? "no games"
            : string.Create(CultureInfo.InvariantCulture, $"games {pageRows[0].Position} to {pageRows[^1].Position} of {totalRows}");

        using var ink = Fill(InkColour);
        using var muted = Fill(MutedColour);

        canvas.DrawText(
            string.Create(CultureInfo.InvariantCulture, $"Review sheet, page {pageNumber} of {pageCount}, {range}"),
            Margin,
            34,
            SKTextAlign.Left,
            fonts.Title,
            ink);

        for (var line = 0; line < rulesLines.Count; line++)
        {
            canvas.DrawText(rulesLines[line], Margin, RulesBaselinePx(line), SKTextAlign.Left, fonts.Small, muted);
        }

        var x = Margin;
        var baseline = headerHeight + 24;
        foreach (var (name, width) in Columns())
        {
            canvas.DrawText(name, x + CellPadding, baseline, SKTextAlign.Left, fonts.Heading, ink);
            x += width;
        }
    }

    private static IEnumerable<(string Name, int Width)> Columns()
    {
        yield return ("No.", PositionWidth);
        yield return ("Title", TitleWidth);
        yield return ("A: owned version", CandidateWidth);
        yield return ("B: main image", CandidateWidth);
        yield return ("Verdict of A", VerdictWidth);
        yield return ("Chosen", ChosenWidth);
        yield return ("Result", ResultWidth);
        yield return ("Spine", SpineCellWidth);
        yield return ("Size source", SizeWidth);
    }

    private static void DrawRow(SKCanvas canvas, ReviewRow row, int top, Fonts fonts)
    {
        using var background = Fill(RowColour);
        using var edge = new SKPaint { Color = RowEdgeColour, IsStroke = true, StrokeWidth = 1, IsAntialias = false };
        var rowRect = new SKRect(Margin, top, PageWidthPx - Margin, top + RowHeight - 2);
        canvas.DrawRect(rowRect, background);
        canvas.DrawRect(rowRect, edge);

        using var ink = Fill(InkColour);
        var baseline = top + (RowHeight / 2f) + 6;
        var x = Margin;

        canvas.DrawText(row.Position.ToString(CultureInfo.InvariantCulture), x + CellPadding, baseline, SKTextAlign.Left, fonts.Heading, ink);
        x += PositionWidth;

        canvas.DrawText(Ellipsised(row.Title, fonts.Body, TitleWidth - (2 * CellPadding)), x + CellPadding, baseline, SKTextAlign.Left, fonts.Body, ink);
        x += TitleWidth;

        DrawCandidate(canvas, row.CandidateAPath, x, top, row.Pick == ArtPick.VersionImage, fonts);
        x += CandidateWidth;

        DrawCandidate(canvas, row.CandidateBPath, x, top, row.Pick == ArtPick.MainImage, fonts);
        x += CandidateWidth;

        canvas.DrawText(row.Verdict, x + CellPadding, baseline - 10, SKTextAlign.Left, fonts.Heading, ink);
        if (row.ScoreText is not null)
        {
            using var muted = Fill(MutedColour);
            canvas.DrawText($"score {row.ScoreText}", x + CellPadding, baseline + 16, SKTextAlign.Left, fonts.Small, muted);
        }

        x += VerdictWidth;

        canvas.DrawText(row.ChosenText, x + CellPadding, baseline, SKTextAlign.Left, fonts.Body, ink);
        x += ChosenWidth;

        DrawResult(canvas, row, x + CellPadding, top + RowPadding, fonts);
        x += ResultWidth;

        DrawSpine(canvas, row, x + CellPadding, top + RowPadding, fonts);
        x += SpineCellWidth;

        canvas.DrawText(row.SizeSource, x + CellPadding, baseline, SKTextAlign.Left, fonts.Body, ink);
    }

    private static void DrawCandidate(SKCanvas canvas, string? path, int cellLeft, int rowTop, bool chosen, Fonts fonts)
    {
        var left = cellLeft + CellPadding;
        var top = rowTop + RowPadding;
        var available = new SKSize(CandidateWidth - (2 * CellPadding), ArtHeightPx);
        using var bitmap = path is null ? null : Decode(path);

        if (bitmap is null)
        {
            using var muted = Fill(MutedColour);
            canvas.DrawText(MissingWord, left, top + (ArtHeightPx / 2f) + 6, SKTextAlign.Left, fonts.Body, muted);

            return;
        }

        var target = FitInside(bitmap.Width, bitmap.Height, available);
        var rect = SKRect.Create(left, top, target.Width, target.Height);
        canvas.DrawBitmap(bitmap, rect, Sampling);

        if (chosen)
        {
            using var outline = new SKPaint { Color = ChosenOutlineColour, IsStroke = true, StrokeWidth = OutlinePx, IsAntialias = false };
            var outer = SKRect.Create(rect.Left - (OutlinePx / 2f), rect.Top - (OutlinePx / 2f), rect.Width + OutlinePx, rect.Height + OutlinePx);
            canvas.DrawRect(outer, outline);
        }
    }

    private static void DrawResult(SKCanvas canvas, ReviewRow row, int left, int top, Fonts fonts)
    {
        var width = Math.Min(
            ResultWidth - (2 * CellPadding),
            (int)Math.Round((double)ArtHeightPx * row.Box.WidthMm / Math.Max(1, row.Box.HeightMm), MidpointRounding.AwayFromZero));
        var front = SKRect.Create(left, top, Math.Max(1, width), ArtHeightPx);
        using var bitmap = row.ResultArtPath is null ? null : Decode(row.ResultArtPath);

        if (bitmap is null)
        {
            using var generated = Fill(Colour(row.SpineBackground, SKColors.Gray));
            canvas.DrawRect(front, generated);
            using var generatedText = Fill(Colour(row.SpineText, SKColors.White));
            canvas.DrawText("generated", front.Left + 6, front.MidY, SKTextAlign.Left, fonts.Small, generatedText);

            return;
        }

        if (ArtFitting.Fit((int)front.Width, (int)front.Height, bitmap.Width, bitmap.Height) == ArtFit.Exact)
        {
            canvas.DrawBitmap(bitmap, front, Sampling);

            return;
        }

        FillBars(canvas, front, row.Edges, bitmap);
        var art = FitInside(bitmap.Width, bitmap.Height, new SKSize(front.Width, front.Height));
        var rect = SKRect.Create(front.Left + ((front.Width - art.Width) / 2f), front.Top + ((front.Height - art.Height) / 2f), art.Width, art.Height);
        canvas.DrawBitmap(bitmap, rect, Sampling);
    }

    private static void FillBars(SKCanvas canvas, SKRect front, ArtEdges? edges, SKBitmap bitmap)
    {
        var art = FitInside(bitmap.Width, bitmap.Height, new SKSize(front.Width, front.Height));
        var barX = (front.Width - art.Width) / 2f;
        var barY = (front.Height - art.Height) / 2f;

        using var top = Fill(Colour(edges?.Top, SKColors.Black));
        using var bottom = Fill(Colour(edges?.Bottom, SKColors.Black));
        using var left = Fill(Colour(edges?.Left, SKColors.Black));
        using var right = Fill(Colour(edges?.Right, SKColors.Black));

        canvas.DrawRect(new SKRect(front.Left, front.Top, front.Right, front.Top + barY), top);
        canvas.DrawRect(new SKRect(front.Left, front.Bottom - barY, front.Right, front.Bottom), bottom);
        canvas.DrawRect(new SKRect(front.Left, front.Top, front.Left + barX, front.Bottom), left);
        canvas.DrawRect(new SKRect(front.Right - barX, front.Top, front.Right, front.Bottom), right);
    }

    private static void DrawSpine(SKCanvas canvas, ReviewRow row, int left, int top, Fonts fonts)
    {
        using var background = Fill(Colour(row.SpineBackground, SKColors.Gray));
        using var text = Fill(Colour(row.SpineText, SKColors.White));
        var strip = SKRect.Create(left, top, SpineWidthPx, ArtHeightPx);
        canvas.DrawRect(strip, background);

        canvas.Save();
        canvas.ClipRect(strip);
        canvas.Translate(strip.MidX, strip.Top + 6);
        canvas.RotateDegrees(QuarterTurn);
        canvas.DrawText(
            Ellipsised(row.Title, fonts.Small, ArtHeightPx - 12),
            0,
            fonts.Small.Size / 3f,
            SKTextAlign.Left,
            fonts.Small,
            text);
        canvas.Restore();
    }

    private static string Ellipsised(string text, SKFont font, float maxWidth)
    {
        if (font.MeasureText(text) <= maxWidth)
        {
            return text;
        }

        var length = text.Length;

        while (length > 0 && font.MeasureText(text[..length].TrimEnd() + Ellipsis) > maxWidth)
        {
            length--;
        }

        return text[..length].TrimEnd() + Ellipsis;
    }

    private static SKSize FitInside(int width, int height, SKSize available)
    {
        var scale = Math.Min(available.Width / width, available.Height / height);

        return new SKSize(Math.Max(1f, width * scale), Math.Max(1f, height * scale));
    }

    private static SKBitmap? Decode(string path)
    {
        try
        {
            return SKBitmap.Decode(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static SKColor Colour(string? hex, SKColor fallback) =>
        hex is not null && SKColor.TryParse(hex, out var colour) ? colour : fallback;

    private static SKPaint Fill(SKColor colour) => new() { Color = colour, IsAntialias = true };

    private sealed record Fonts(SKFont Body, SKFont Small, SKFont Heading, SKFont Title);
}

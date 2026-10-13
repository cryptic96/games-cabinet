using System.Globalization;
using Cabinet.Domain.Collection;
using Cabinet.Repository.Images;
using Cabinet.Repository.Storage;
using Cabinet.Service.Layout;
using SkiaSharp;

namespace Cabinet.Service.Review;

/// <summary>
/// The operator mode that builds the review sheet from the stored collection and the stored pictures with the settings the
/// deployed service reads. It is reached only from the command line of the service executable, has no route and prints
/// counts only, never a title or a path of a picture.
/// </summary>
public static class ReviewSheetCommand
{
    /// <summary>Exit code: the pages were written.</summary>
    public const int Written = 0;

    /// <summary>Exit code: the arguments were not understood.</summary>
    public const int Usage = 2;

    /// <summary>Exit code: there is no stored collection in the state directory.</summary>
    public const int NoCollection = 3;

    /// <summary>Exit code: no usable font could be loaded.</summary>
    public const int NoFont = 4;

    /// <summary>Exit code: the output directory could not be written.</summary>
    public const int OutputNotWritable = 5;

    private const string UsageText =
        "usage: review-sheet --state <state directory> --out <output directory> [--font <regular font file>] [--bold-font <bold font file>]";

    private const string NoFontText = "no usable font: install fonts-dejavu-core or pass --font";
    private const string StateOption = "--state";
    private const string OutOption = "--out";
    private const string FontOption = "--font";
    private const string BoldFontOption = "--bold-font";

    /// <summary>The regular and bold font files tried in order when no font is given: the places the DejaVu fonts install to.</summary>
    public static IReadOnlyList<(string Regular, string Bold)> FontSearchPaths { get; } =
    [
        ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"),
        ("/usr/share/fonts/TTF/DejaVuSans.ttf", "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf"),
    ];

    /// <summary>
    /// Builds the sheet and writes its pages as <c>review-01.png</c>, <c>review-02.png</c> and so on.
    /// </summary>
    /// <param name="args">The arguments after the command word.</param>
    /// <param name="output">Receives one line of counts on success.</param>
    /// <param name="error">Receives the reason when nothing is written.</param>
    /// <returns>An exit code: 0 written, 2 usage, 3 no stored collection, 4 no usable font, 5 output directory not writable.</returns>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!TryParse(args, out var options))
        {
            error.WriteLine(UsageText);

            return Usage;
        }

        ArtRules rules;

        try
        {
            rules = ArtSettings.FromConfiguration(ReadConfiguration());
        }
        catch (InvalidOperationException exception)
        {
            error.WriteLine(exception.Message);

            return Usage;
        }

        var loaded = SnapshotStore.LoadReadOnly(options.State);

        if (loaded.Snapshot is not { } snapshot)
        {
            error.WriteLine(loaded.Reason);

            return NoCollection;
        }

        if (!TryLoadFonts(options, out var typeface, out var boldTypeface))
        {
            error.WriteLine(NoFontText);

            return NoFont;
        }

        using (typeface)
        using (boldTypeface)
        {
            var rows = ReviewSheetModel.Build(snapshot, rules, Path.Combine(options.State, ArtCache.DirectoryName));
            var pages = ReviewSheet.Draw(rows, rules, typeface!, boldTypeface!);

            if (!TryWrite(options.Out, pages))
            {
                error.WriteLine("the output directory could not be written");

                return OutputNotWritable;
            }

            output.WriteLine(Counts(rows, pages.Count));

            return Written;
        }
    }

    private static bool TryParse(string[] args, out Options options)
    {
        string? state = null;
        string? outDirectory = null;
        string? font = null;
        string? boldFont = null;
        options = default!;

        for (var index = 0; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
            {
                return false;
            }

            switch (args[index])
            {
                case StateOption:
                    state = args[index + 1];
                    break;
                case OutOption:
                    outDirectory = args[index + 1];
                    break;
                case FontOption:
                    font = args[index + 1];
                    break;
                case BoldFontOption:
                    boldFont = args[index + 1];
                    break;
                default:
                    return false;
            }
        }

        if (state is null || outDirectory is null)
        {
            return false;
        }

        options = new Options(state, outDirectory, font, boldFont);

        return true;
    }

    private static IConfiguration ReadConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true);

        if (!string.IsNullOrWhiteSpace(environment))
        {
            builder.AddJsonFile($"appsettings.{environment}.json", optional: true);
        }

        return builder.AddEnvironmentVariables().Build();
    }

    private static bool TryLoadFonts(Options options, out SKTypeface? typeface, out SKTypeface? boldTypeface)
    {
        typeface = null;
        boldTypeface = null;

        if (options.Font is null && options.BoldFont is null)
        {
            return TryLoadInstalledPair(out typeface, out boldTypeface);
        }

        var regular = options.Font is null ? LoadInstalledRegular() : LoadFont(options.Font);
        var bold = options.BoldFont is null ? regular : LoadFont(options.BoldFont);

        return Complete(regular, bold, out typeface, out boldTypeface);
    }

    private static bool TryLoadInstalledPair(out SKTypeface? typeface, out SKTypeface? boldTypeface)
    {
        foreach (var (regularPath, boldPath) in FontSearchPaths)
        {
            var regular = LoadFont(regularPath);

            if (regular is null)
            {
                continue;
            }

            var bold = File.Exists(boldPath) ? LoadFont(boldPath) ?? regular : regular;

            return Complete(regular, bold, out typeface, out boldTypeface);
        }

        typeface = null;
        boldTypeface = null;

        return false;
    }

    private static SKTypeface? LoadInstalledRegular()
    {
        foreach (var (regularPath, _) in FontSearchPaths)
        {
            if (LoadFont(regularPath) is { } regular)
            {
                return regular;
            }
        }

        return null;
    }

    private static bool Complete(SKTypeface? regular, SKTypeface? bold, out SKTypeface? typeface, out SKTypeface? boldTypeface)
    {
        if (regular is not null && bold is not null)
        {
            typeface = regular;
            boldTypeface = bold;

            return true;
        }

        if (!ReferenceEquals(regular, bold))
        {
            bold?.Dispose();
        }

        regular?.Dispose();

        typeface = null;
        boldTypeface = null;

        return false;
    }

    private static SKTypeface? LoadFont(string path)
    {
        try
        {
            return File.Exists(path) ? SKTypeface.FromFile(path) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static bool TryWrite(string directory, IReadOnlyList<byte[]> pages)
    {
        try
        {
            Directory.CreateDirectory(directory);

            for (var index = 0; index < pages.Count; index++)
            {
                File.WriteAllBytes(
                    Path.Combine(directory, string.Create(CultureInfo.InvariantCulture, $"review-{index + 1:00}.png")),
                    pages[index]);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static string Counts(IReadOnlyList<ReviewRow> rows, int pages)
    {
        var verdicts = ReviewSheetModel.CountVerdicts(rows);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"review sheet: {pages} pages, {rows.Count} games; verdicts flat {verdicts.Flat}, 3D shot {verdicts.ThreeD}, 3D shot cut-out {verdicts.CutOut}, 3D shot tight crop {verdicts.TightCrop}, unsure {verdicts.Unsure}, no verdict {verdicts.NoVerdict}; chosen version image {rows.Count(row => row.Pick == ArtPick.VersionImage)}, main image {rows.Count(row => row.Pick == ArtPick.MainImage)}, generated cover {rows.Count(row => row.Pick == ArtPick.GeneratedCover)}");
    }

    private sealed record Options(string State, string Out, string? Font, string? BoldFont);
}

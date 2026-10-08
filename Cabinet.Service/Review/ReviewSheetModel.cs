using System.Globalization;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;

namespace Cabinet.Service.Review;

/// <summary>One game's line of the review sheet, in the plain words the operator reads.</summary>
/// <param name="Position">The place in collection order, counted from one.</param>
/// <param name="Title">The title, or a placeholder when the source gave none.</param>
/// <param name="CandidateAPath">The stored file of the owned edition's picture, or null when there is none to show.</param>
/// <param name="CandidateBPath">The stored file of the game's main picture, or null when there is none to show.</param>
/// <param name="Verdict">The verdict of the owned edition's picture as a word.</param>
/// <param name="ScoreText">The detector's score with two decimals, or null when there is no verdict.</param>
/// <param name="Pick">Which picture the cabinet draws.</param>
/// <param name="ChosenText">The choice in words.</param>
/// <param name="Box">The box front as the cabinet draws it.</param>
/// <param name="ResultArtPath">The stored file of the chosen picture, or null when the generated cover is drawn.</param>
/// <param name="Edges">The edge colours of the chosen picture, or null when it has none.</param>
/// <param name="SpineBackground">The spine background as a hexadecimal colour.</param>
/// <param name="SpineText">The spine text colour as a hexadecimal colour.</param>
/// <param name="SizeSource">Where the box size came from, as a word.</param>
public sealed record ReviewRow(
    int Position,
    string Title,
    string? CandidateAPath,
    string? CandidateBPath,
    string Verdict,
    string? ScoreText,
    ArtPick Pick,
    string ChosenText,
    BoxDimensions Box,
    string? ResultArtPath,
    ArtEdges? Edges,
    string SpineBackground,
    string SpineText,
    string SizeSource);

/// <summary>Builds the rows of the review sheet from the stored collection with the decisions the cabinet makes.</summary>
public static class ReviewSheetModel
{
    /// <summary>The title shown for a game whose title is blank.</summary>
    public const string UntitledTitle = "Untitled game";

    private const int PreferredCandidateWidth = 240;
    private const string ScoreFormat = "0.00";

    /// <summary>
    /// One row per mapped item in collection order. A picture is shown only when its record is usable and its stored file
    /// is in the art directory; anything else reads as missing.
    /// </summary>
    /// <param name="snapshot">The stored collection.</param>
    /// <param name="rules">The rules the deployed cabinet uses.</param>
    /// <param name="artDirectory">The directory holding the stored pictures.</param>
    public static IReadOnlyList<ReviewRow> Build(CollectionSnapshot snapshot, ArtRules rules, string artDirectory)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentException.ThrowIfNullOrWhiteSpace(artDirectory);

        var traces = SnapshotMapper.Explain(snapshot, rules);
        var rows = new List<ReviewRow>(traces.Count);

        for (var index = 0; index < traces.Count; index++)
        {
            rows.Add(RowOf(index + 1, traces[index], artDirectory));
        }

        return rows;
    }

    private static ReviewRow RowOf(int position, MappedItemTrace trace, string artDirectory)
    {
        var candidateA = PathOf(trace.VersionImage, artDirectory);
        var candidateB = PathOf(trace.MainImage, artDirectory);
        var tone = trace.Mapped.Colour ?? SpinePalette.Tones[SpinePalette.ToneFor(trace.Mapped.BggId)];

        return new ReviewRow(
            position,
            string.IsNullOrWhiteSpace(trace.Item.Title) ? UntitledTitle : trace.Item.Title,
            candidateA,
            candidateB,
            trace.VersionVerdict is { } verdict ? VerdictWord(verdict, trace.VersionImage?.Features?.CutOut == true) : "no verdict",
            trace.VersionScore?.ToString(ScoreFormat, CultureInfo.InvariantCulture),
            trace.Pick,
            ChosenWords(trace.Pick),
            trace.Shape.Box,
            trace.Pick switch
            {
                ArtPick.VersionImage => candidateA,
                ArtPick.MainImage => candidateB,
                _ => null,
            },
            trace.Mapped.Art?.Edges,
            tone.Background,
            tone.Text,
            SourceWord(trace.Shape.Source));
    }

    private static string? PathOf(ImageRecord? record, string artDirectory)
    {
        if (record?.Files is not { Count: > 0 } files)
        {
            return null;
        }

        var ordered = files.OrderBy(file => file.Width).ToList();
        var file = ordered.FirstOrDefault(candidate => candidate.Width >= PreferredCandidateWidth) ?? ordered[^1];

        if (Path.GetFileName(file.Name) != file.Name)
        {
            return null;
        }

        var path = Path.Combine(artDirectory, file.Name);

        return File.Exists(path) ? path : null;
    }

    private static string VerdictWord(ArtVerdict verdict, bool cutOut) => verdict switch
    {
        ArtVerdict.Flat => "flat",
        ArtVerdict.ThreeD when cutOut => "3D shot, cut-out",
        ArtVerdict.ThreeD => "3D shot",
        _ => "unsure",
    };

    private static string ChosenWords(ArtPick pick) => pick switch
    {
        ArtPick.VersionImage => "chosen: version image",
        ArtPick.MainImage => "chosen: main image",
        _ => "generated cover",
    };

    private static string SourceWord(BoxSource source) => source switch
    {
        BoxSource.RealSize => "real size",
        BoxSource.CoverShape => "cover shape",
        BoxSource.Estimate => "estimate",
        _ => "default",
    };
}

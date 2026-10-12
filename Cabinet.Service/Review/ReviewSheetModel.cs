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
/// <param name="Kind">The verdict kind of the owned edition's picture, or null when there is no verdict.</param>
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
    ArtVerdictKind? Kind,
    string? ScoreText,
    ArtPick Pick,
    string ChosenText,
    BoxDimensions Box,
    string? ResultArtPath,
    ArtEdges? Edges,
    string SpineBackground,
    string SpineText,
    string SizeSource);

/// <summary>How many games fall under each verdict kind, and how many have no verdict.</summary>
/// <param name="Flat">Games whose owned edition's picture reads flat.</param>
/// <param name="Unsure">Games whose picture reads unsure.</param>
/// <param name="ThreeD">Games whose picture reads as a plain 3D shot.</param>
/// <param name="CutOut">Games whose picture reads as a cut-out.</param>
/// <param name="TightCrop">Games whose picture reads as a tight crop.</param>
/// <param name="NoVerdict">Games with no usable picture of the owned edition.</param>
public sealed record VerdictCounts(int Flat, int Unsure, int ThreeD, int CutOut, int TightCrop, int NoVerdict)
{
    /// <summary>The number of games counted, which is the number of games on the sheet.</summary>
    public int Total => Flat + Unsure + ThreeD + CutOut + TightCrop + NoVerdict;
}

/// <summary>Builds the rows of the review sheet from the stored collection with the decisions the cabinet makes.</summary>
public static class ReviewSheetModel
{
    /// <summary>The title shown for a game whose title is blank.</summary>
    public const string UntitledTitle = "Untitled game";

    private const string NoVerdictWord = "no verdict";
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

    /// <summary>Counts the rows under each verdict kind; every row lands in exactly one count.</summary>
    /// <param name="rows">The rows of the sheet.</param>
    public static VerdictCounts CountVerdicts(IReadOnlyList<ReviewRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return new VerdictCounts(
            rows.Count(row => row.Kind == ArtVerdictKind.Flat),
            rows.Count(row => row.Kind == ArtVerdictKind.Unsure),
            rows.Count(row => row.Kind == ArtVerdictKind.ThreeD),
            rows.Count(row => row.Kind == ArtVerdictKind.CutOut),
            rows.Count(row => row.Kind == ArtVerdictKind.TightCrop),
            rows.Count(row => row.Kind is null));
    }

    private static ReviewRow RowOf(int position, MappedItemTrace trace, string artDirectory)
    {
        var candidateA = PathOf(trace.VersionImage, artDirectory);
        ArtVerdictKind? kind = trace.VersionVerdict is { } versionVerdict
            ? ArtVerdictKinds.Of(versionVerdict, trace.VersionImage?.Features)
            : null;
        var candidateB = PathOf(trace.MainImage, artDirectory);
        var tone = trace.Mapped.Colour ?? SpinePalette.Tones[SpinePalette.ToneFor(trace.Mapped.BggId)];

        return new ReviewRow(
            position,
            string.IsNullOrWhiteSpace(trace.Item.Title) ? UntitledTitle : trace.Item.Title,
            candidateA,
            candidateB,
            kind is { } known ? VerdictWord(known) : NoVerdictWord,
            kind,
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

    private static string VerdictWord(ArtVerdictKind kind) => kind switch
    {
        ArtVerdictKind.Flat => "flat",
        ArtVerdictKind.CutOut => "3D shot, cut-out",
        ArtVerdictKind.TightCrop => "3D shot, tight crop",
        ArtVerdictKind.ThreeD => "3D shot",
        _ => "unsure",
    };

    private static string ChosenWords(ArtPick pick) => pick switch
    {
        ArtPick.VersionImage => "chosen: version image",
        ArtPick.MainImage => "chosen: main image",
        ArtPick.OwnerLink => "chosen: owner link",
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

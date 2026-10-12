using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Repository.Images;

namespace Cabinet.Service.Sync;

/// <summary>
/// Builds the record of a picture that was fetched, resized and measured. The sync and the owner's linked pictures both
/// store their pictures through it, so the files, the measurements, the colour pair, the edge colours and the version of the
/// analysis are worked out the same way wherever a picture came from.
/// </summary>
public static class ArtRecords
{
    /// <summary>Builds the stored record of a processed picture, with its files widest first.</summary>
    /// <param name="sourceUrl">The address the picture was fetched from.</param>
    /// <param name="attemptedAt">The moment the picture was fetched.</param>
    /// <param name="done">What the processor made of the picture: the encoded sizes and what was measured.</param>
    public static ImageRecord FromProcessed(string sourceUrl, DateTimeOffset attemptedAt, ArtProcessing.Done done)
    {
        ArgumentNullException.ThrowIfNull(sourceUrl);
        ArgumentNullException.ThrowIfNull(done);

        return new ImageRecord(
            sourceUrl,
            ImageStatus.Ok,
            attemptedAt,
            [.. done.Variants.OrderByDescending(variant => variant.Width).Select(variant => new ArtFile(variant.Width, variant.Height, variant.Name))],
            done.Facts.Features,
            done.Facts.Main.ToHex(),
            SpineColour.PairFor(done.Facts.Main),
            new ArtEdges(done.Facts.Top.ToHex(), done.Facts.Right.ToHex(), done.Facts.Bottom.ToHex(), done.Facts.Left.ToHex()),
            ArtProcessor.AnalysisVersion);
    }
}

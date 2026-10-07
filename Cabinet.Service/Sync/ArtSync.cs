using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Repository.Images;

namespace Cabinet.Service.Sync;

/// <summary>How the picture step of a sync run ended.</summary>
/// <param name="Snapshot">The collection with every image record the step wrote.</param>
/// <param name="Stored">How many pictures were fetched and stored.</param>
/// <param name="Failed">How many could not be fetched.</param>
/// <param name="Refused">How many were refused by a rule.</param>
/// <param name="Undecodable">How many could not be read as pictures.</param>
/// <param name="Waiting">How many are still due, because the run ran out of downloads or time.</param>
public sealed record ArtSyncResult(CollectionSnapshot Snapshot, int Stored, int Failed, int Refused, int Undecodable, int Waiting);

/// <summary>
/// The picture step of a sync run. It walks the collection, fetches the pictures that are due one at a time on the
/// picture pacer, resizes and stores them, measures each one once, and records how each attempt ended and what was
/// measured. Every game offers two pictures, the one of its owned edition and its main picture, each distinct address once. It starts at most a fixed number of
/// downloads per run and none after the deadline, so a large collection fills in over several runs and a slow host cannot
/// hold the run. A picture that goes wrong is only recorded; it never fails the run. It logs counts only, never an
/// address, a title or an identifier.
/// </summary>
/// <param name="sourceFactory">Creates the picture source for each run, so a pooled connection never outlives its handler's lifetime.</param>
/// <param name="cache">Where the resized pictures are stored.</param>
/// <param name="options">The picture rules.</param>
/// <param name="time">The clock the deadline and the retry rule are measured on.</param>
/// <param name="logger">Receives the counts at the end of the step.</param>
public sealed class ArtSync(
    Func<IArtSource> sourceFactory,
    ArtCache cache,
    ImageOptions options,
    TimeProvider time,
    ILogger<ArtSync> logger)
{
    private const int CommitEvery = 10;

    /// <summary>Fetches the pictures that are due, within the per-run limit and the deadline.</summary>
    /// <param name="snapshot">The stored collection with the image records known so far.</param>
    /// <param name="deadline">The moment after which no further download starts.</param>
    /// <param name="commit">Stores a snapshot and shows it; called after every ten changed records and once at the end.</param>
    /// <param name="cancellationToken">Cancels the step.</param>
    public async Task<ArtSyncResult> RunAsync(
        CollectionSnapshot snapshot,
        DateTimeOffset deadline,
        Func<CollectionSnapshot, Task> commit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(commit);

        var images = new Dictionary<string, ImageRecord>(snapshot.Images ?? new Dictionary<string, ImageRecord>(), StringComparer.Ordinal);
        var now = time.GetUtcNow();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var counts = new Counts();
        var attempts = 0;
        var waiting = 0;
        var uncommitted = 0;
        var current = snapshot;
        IArtSource? source = null;

        foreach (var item in snapshot.Items.OrderBy(item => item.CollectionId).ThenBy(item => item.GameId))
        {
            foreach (var address in Candidates(item, snapshot))
            {
                if (!seen.Add(address) || !IsDue(address, images, now))
                {
                    continue;
                }

                if (attempts >= options.MaxDownloadsPerRun || time.GetUtcNow() >= deadline)
                {
                    waiting++;

                    continue;
                }

                source ??= sourceFactory();
                attempts++;
                images[address] = await FetchAsync(source, address, counts, cancellationToken);
                uncommitted++;

                if (uncommitted >= CommitEvery)
                {
                    current = snapshot with { Images = new Dictionary<string, ImageRecord>(images, StringComparer.Ordinal) };
                    await commit(current);
                    uncommitted = 0;
                }
            }
        }

        if (uncommitted > 0)
        {
            current = snapshot with { Images = new Dictionary<string, ImageRecord>(images, StringComparer.Ordinal) };
            await commit(current);
        }

        logger.LogInformation(
            "Box pictures: {Stored} stored, {Failed} failed, {Refused} refused, {Undecodable} undecodable, {Waiting} still waiting.",
            counts.Stored,
            counts.Failed,
            counts.Refused,
            counts.Undecodable,
            waiting);

        return new ArtSyncResult(current, counts.Stored, counts.Failed, counts.Refused, counts.Undecodable, waiting);
    }

    /// <summary>
    /// The addresses an item offers, in the order they are tried: the picture of its owned edition, then its main picture,
    /// which is the one its details name, or the one the collection gave for the item until its details arrive. The same
    /// address is offered once.
    /// </summary>
    private static IEnumerable<string> Candidates(SnapshotItem item, CollectionSnapshot snapshot)
    {
        var version = item.VersionImageUrl;
        var main = SnapshotMapper.MainPictureUrl(item, snapshot);

        if (version is not null)
        {
            yield return version;
        }

        if (main is not null && main != version)
        {
            yield return main;
        }
    }

    private bool IsDue(string address, Dictionary<string, ImageRecord> images, DateTimeOffset now)
    {
        if (!images.TryGetValue(address, out var record))
        {
            return true;
        }

        if (record.Status != ImageStatus.Ok)
        {
            return record.AttemptedAtUtc + options.RetryFailedAfter <= now;
        }

        return record.Files is not { Count: > 0 } files
            || files.Any(file => !cache.Has(file.Name))
            || record.Features is null
            || record.AnalysisVersion != ArtProcessor.AnalysisVersion;
    }

    private async Task<ImageRecord> FetchAsync(IArtSource source, string address, Counts counts, CancellationToken cancellationToken)
    {
        var attemptedAt = time.GetUtcNow();

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            counts.Refused++;

            return new ImageRecord(address, ImageStatus.Refused, attemptedAt);
        }

        switch (await source.DownloadAsync(uri, cancellationToken))
        {
            case ArtDownload.Turned:
                counts.Refused++;

                return new ImageRecord(address, ImageStatus.Refused, attemptedAt);

            case ArtDownload.Missed:
                counts.Failed++;

                return new ImageRecord(address, ImageStatus.Failed, attemptedAt);

            case ArtDownload.Fetched fetched:
                return Store(address, fetched.Bytes, attemptedAt, counts);

            default:
                counts.Failed++;

                return new ImageRecord(address, ImageStatus.Failed, attemptedAt);
        }
    }

    private ImageRecord Store(string address, byte[] bytes, DateTimeOffset attemptedAt, Counts counts)
    {
        switch (ArtProcessor.Process(bytes, options.Limits))
        {
            case ArtProcessing.Turned:
                counts.Refused++;

                return new ImageRecord(address, ImageStatus.Refused, attemptedAt);

            case ArtProcessing.Done done:
                try
                {
                    foreach (var variant in done.Variants)
                    {
                        cache.Write(variant);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    counts.Failed++;

                    return new ImageRecord(address, ImageStatus.Failed, attemptedAt);
                }

                counts.Stored++;

                return new ImageRecord(
                    address,
                    ImageStatus.Ok,
                    attemptedAt,
                    [.. done.Variants.OrderByDescending(variant => variant.Width).Select(variant => new ArtFile(variant.Width, variant.Height, variant.Name))],
                    done.Facts.Features,
                    done.Facts.Main.ToHex(),
                    SpineColour.PairFor(done.Facts.Main),
                    new ArtEdges(done.Facts.Top.ToHex(), done.Facts.Right.ToHex(), done.Facts.Bottom.ToHex(), done.Facts.Left.ToHex()),
                    ArtProcessor.AnalysisVersion);

            default:
                counts.Undecodable++;

                return new ImageRecord(address, ImageStatus.Undecodable, attemptedAt);
        }
    }

    private sealed class Counts
    {
        public int Stored { get; set; }

        public int Failed { get; set; }

        public int Refused { get; set; }

        public int Undecodable { get; set; }
    }
}

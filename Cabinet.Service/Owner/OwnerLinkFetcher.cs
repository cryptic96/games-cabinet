using Cabinet.Domain.Collection;
using Cabinet.Repository.Images;
using Cabinet.Service.Sync;

namespace Cabinet.Service.Owner;

/// <summary>A picture the owner linked, fetched and processed, ready to be stored.</summary>
/// <param name="Url">The canonical address it was fetched from.</param>
/// <param name="Variants">The resized pictures, which are written to the art directory before the record is stored.</param>
/// <param name="Record">The record of the picture, built the way the sync builds its own.</param>
public sealed record ProcessedLink(string Url, IReadOnlyList<EncodedArt> Variants, ImageRecord Record);

/// <summary>How a check of an owner's picture address ended.</summary>
public abstract record LinkCheck
{
    /// <summary>The address gave a picture that can be used.</summary>
    /// <param name="Link">The processed picture.</param>
    public sealed record Ready(ProcessedLink Link) : LinkCheck;

    /// <summary>The address did not give a usable picture.</summary>
    /// <param name="Reason">One category word that says why: address, host, redirect, type, size, pixels, status, unavailable, timeout, unreadable or failed.</param>
    public sealed record Failed(string Reason) : LinkCheck;
}

/// <summary>
/// Fetches a picture the owner linked and processes it into the same kind of record the sync stores. It uses the picture
/// source the sync uses, which sends no credentials of any kind, goes only to an address the source policy allows, follows
/// a redirect at most a few times, refuses a body that is too big or not a picture and waits its turn on the picture pacer,
/// so the owner's pictures and the sync's pictures never run at the same time. The picture is checked against the same byte,
/// pixel and time limits. Every way a check can fail is one category word; only cancelling is passed on. It logs nothing, so
/// no address ever reaches a log.
/// </summary>
/// <param name="sourceFactory">Creates the picture source for each check, so a pooled connection never outlives its handler's lifetime.</param>
/// <param name="options">The picture rules.</param>
/// <param name="time">The clock the record is stamped with.</param>
public sealed class OwnerLinkFetcher(Func<IArtSource> sourceFactory, ImageOptions options, TimeProvider time)
{
    /// <summary>Fetches and processes the picture at an address the owner entered.</summary>
    /// <param name="text">The address as the owner typed it, or null.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    public async Task<LinkCheck> CheckAsync(string? text, CancellationToken cancellationToken)
    {
        var address = ArtUrl.Canonical(text);

        if (address is null)
        {
            return new LinkCheck.Failed("address");
        }

        try
        {
            return await FetchAsync(address, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new LinkCheck.Failed("failed");
        }
    }

    private async Task<LinkCheck> FetchAsync(string address, CancellationToken cancellationToken)
    {
        var attemptedAt = time.GetUtcNow();

        switch (await sourceFactory().DownloadAsync(new Uri(address), cancellationToken))
        {
            case ArtDownload.Turned turned:
                return new LinkCheck.Failed(turned.Reason);

            case ArtDownload.Missed missed:
                return new LinkCheck.Failed(missed.Reason);

            case ArtDownload.Fetched fetched:
                return Process(address, fetched.Bytes, attemptedAt);

            default:
                return new LinkCheck.Failed("failed");
        }
    }

    private LinkCheck Process(string address, byte[] bytes, DateTimeOffset attemptedAt) =>
        ArtProcessor.Process(bytes, options.Limits) switch
        {
            ArtProcessing.Turned turned => new LinkCheck.Failed(turned.Reason),
            ArtProcessing.Done done => new LinkCheck.Ready(
                new ProcessedLink(address, done.Variants, ArtRecords.FromProcessed(address, attemptedAt, done))),
            _ => new LinkCheck.Failed("unreadable"),
        };
}

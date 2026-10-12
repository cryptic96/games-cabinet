using Cabinet.Domain.Collection;
using Cabinet.Repository.Images;
using Cabinet.Service.Sync;

namespace Cabinet.Service.Owner;

/// <summary>A picture the owner linked, fetched and processed, ready to be stored.</summary>
/// <param name="Url">The canonical address it was fetched from.</param>
/// <param name="Variants">The resized pictures, which are written to the art directory before the record is stored.</param>
/// <param name="Record">The record of the picture, built the way the sync builds its own.</param>
public sealed record ProcessedLink(string Url, IReadOnlyList<EncodedArt> Variants, ImageRecord Record);

/// <summary>Why an address the owner entered did not give a usable picture. Each value is one reason the page can name exactly.</summary>
public enum LinkProblem
{
    /// <summary>Nothing was entered.</summary>
    Blank,

    /// <summary>The text is not a web address: it is not an address at all, not http or https, or too long.</summary>
    NotAWebAddress,

    /// <summary>The address is a page of the game site, not a picture; no page of the game site is ever fetched.</summary>
    PageLink,

    /// <summary>The address is not an https picture address on the picture host, on its default port and without user information.</summary>
    OtherHost,

    /// <summary>The picture host did not give the picture: it answered with an error, was unreachable or did not answer in time.</summary>
    FetchFailed,

    /// <summary>The picture host answered that there is nothing at the address.</summary>
    NotFound,

    /// <summary>What came back is not a picture: the type is wrong or the bytes do not decode.</summary>
    NotAPicture,

    /// <summary>The picture is bigger in bytes than the limit.</summary>
    TooLarge,

    /// <summary>The picture holds more pixels than the limit.</summary>
    TooManyPixels,

    /// <summary>The picture is far longer than it is wide, or the other way round, which no box is.</summary>
    ExtremeShape,
}

/// <summary>How a check of an owner's picture address ended.</summary>
public abstract record LinkCheck
{
    /// <summary>The address gave a picture that can be used.</summary>
    /// <param name="Link">The processed picture.</param>
    public sealed record Ready(ProcessedLink Link) : LinkCheck;

    /// <summary>The address did not give a usable picture.</summary>
    /// <param name="Problem">The one reason it did not.</param>
    /// <param name="Bytes">For a picture that is too large, the size the host announced, when it announced one; otherwise null.</param>
    /// <param name="LimitBytes">For a picture that is too large, the most bytes a picture may have; otherwise null.</param>
    public sealed record Failed(LinkProblem Problem, long? Bytes = null, long? LimitBytes = null) : LinkCheck;
}

/// <summary>
/// Fetches a picture the owner linked and processes it into the same kind of record the sync stores. Before anything is
/// fetched the address is classified, in this order: blank, not a web address, a page of the game site, not on the picture
/// host. Only a direct https picture address on the allowlisted host reaches the picture source, so no page of the game site
/// is ever fetched. The source is the one the sync uses, which sends no credentials of any kind, follows a redirect at most a
/// few times, refuses a body that is too big or not a picture and waits its turn on the picture pacer, so the owner's
/// pictures and the sync's pictures never run at the same time. A check takes at most <see cref="CheckTimeout"/> on the
/// injected clock. The bytes are inspected before they are decoded, and every way a check can fail is one
/// <see cref="LinkProblem"/>; only cancelling is passed on. It logs nothing, so no address ever reaches a log.
/// </summary>
/// <param name="sourceFactory">Creates the picture source for each check, so a pooled connection never outlives its handler's lifetime.</param>
/// <param name="options">The picture rules.</param>
/// <param name="time">The clock the record is stamped with and the time limit runs on.</param>
public sealed class OwnerLinkFetcher(Func<IArtSource> sourceFactory, ImageOptions options, TimeProvider time)
{
    /// <summary>The longest one check may take, from the first request to the processed picture.</summary>
    public static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(15);

    private const string GameSiteHost = "boardgamegeek.com";
    private const int NotFoundStatus = 404;

    /// <summary>The name of the picture host a link must point at, for a message that says where pictures may come from.</summary>
    public string AllowedHost => options.AllowedHosts.Order(StringComparer.Ordinal).First();

    /// <summary>Classifies, fetches and processes the picture at an address the owner entered.</summary>
    /// <param name="text">The address as the owner typed it, or null.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    public async Task<LinkCheck> CheckAsync(string? text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new LinkCheck.Failed(LinkProblem.Blank);
        }

        if (ArtUrl.Canonical(text) is not { } address)
        {
            return new LinkCheck.Failed(LinkProblem.NotAWebAddress);
        }

        var uri = new Uri(address);

        if (IsGameSitePage(uri))
        {
            return new LinkCheck.Failed(LinkProblem.PageLink);
        }

        if (!options.Policy.Allows(uri))
        {
            return new LinkCheck.Failed(LinkProblem.OtherHost);
        }

        using var timeLimit = new CancellationTokenSource(CheckTimeout, time);
        using var checkEnds = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeLimit.Token);

        try
        {
            return await FetchAsync(address, uri, checkEnds.Token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new LinkCheck.Failed(LinkProblem.FetchFailed);
        }
    }

    private static bool IsGameSitePage(Uri uri)
    {
        var host = uri.IdnHost.TrimEnd('.');

        return host.Equals(GameSiteHost, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith($".{GameSiteHost}", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<LinkCheck> FetchAsync(string address, Uri uri, CancellationToken cancellationToken)
    {
        var attemptedAt = time.GetUtcNow();

        return await sourceFactory().DownloadAsync(uri, cancellationToken) switch
        {
            ArtDownload.Turned turned => Refused(turned),
            ArtDownload.Missed { Reason: "status", StatusCode: NotFoundStatus } => new LinkCheck.Failed(LinkProblem.NotFound),
            ArtDownload.Fetched fetched => Process(address, fetched.Bytes, attemptedAt),
            _ => new LinkCheck.Failed(LinkProblem.FetchFailed),
        };
    }

    private LinkCheck.Failed Refused(ArtDownload.Turned turned) => turned.Reason switch
    {
        "host" => new LinkCheck.Failed(LinkProblem.OtherHost),
        "type" => new LinkCheck.Failed(LinkProblem.NotAPicture),
        "size" => new LinkCheck.Failed(LinkProblem.TooLarge, turned.AnnouncedBytes, options.MaxBytes),
        _ => new LinkCheck.Failed(LinkProblem.FetchFailed),
    };

    private LinkCheck Process(string address, byte[] bytes, DateTimeOffset attemptedAt)
    {
        var inspection = ArtProcessor.Inspect(bytes, options.Limits);

        if (!inspection.Decodable)
        {
            return new LinkCheck.Failed(LinkProblem.NotAPicture);
        }

        if (inspection.TooManyPixels)
        {
            return new LinkCheck.Failed(LinkProblem.TooManyPixels);
        }

        if (inspection.ExtremeShape)
        {
            return new LinkCheck.Failed(LinkProblem.ExtremeShape);
        }

        return ArtProcessor.Process(bytes, options.Limits) switch
        {
            ArtProcessing.Done done => new LinkCheck.Ready(
                new ProcessedLink(address, done.Variants, ArtRecords.FromProcessed(address, attemptedAt, done))),
            ArtProcessing.Turned { Reason: "pixels" } => new LinkCheck.Failed(LinkProblem.TooManyPixels),
            _ => new LinkCheck.Failed(LinkProblem.NotAPicture),
        };
    }
}

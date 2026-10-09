using System.Net;
using System.Net.Http.Headers;
using Cabinet.Repository.Bgg;

namespace Cabinet.Repository.Images;

/// <summary>The most a downloaded picture may weigh and the most pixels it may hold.</summary>
/// <param name="MaxBytes">The most bytes a download may have, announced or streamed.</param>
/// <param name="MaxPixels">The most pixels, width times height, a picture may report before it is decoded.</param>
public sealed record ArtLimits(long MaxBytes, long MaxPixels);

/// <summary>How a download ended.</summary>
public abstract record ArtDownload
{
    /// <summary>The picture was fetched.</summary>
    /// <param name="Bytes">The body of the answer.</param>
    public sealed record Fetched(byte[] Bytes) : ArtDownload;

    /// <summary>The picture was not fetched because it broke a rule.</summary>
    /// <param name="Reason">One category word that says which rule: host, redirect, type or size.</param>
    public sealed record Turned(string Reason) : ArtDownload;

    /// <summary>The picture could not be fetched although no rule was broken.</summary>
    /// <param name="Reason">One category word: status, unavailable or timeout.</param>
    public sealed record Missed(string Reason) : ArtDownload;

    /// <summary>The picture was fetched.</summary>
    /// <param name="bytes">The body of the answer.</param>
    public static ArtDownload Downloaded(byte[] bytes) => new Fetched(bytes);

    /// <summary>The picture was refused by a rule.</summary>
    /// <param name="reason">One category word that says which rule.</param>
    public static ArtDownload Refused(string reason) => new Turned(reason);

    /// <summary>The picture could not be fetched.</summary>
    /// <param name="reason">One category word that says why.</param>
    public static ArtDownload Failed(string reason) => new Missed(reason);
}

/// <summary>Fetches the bytes of a picture from an address.</summary>
public interface IArtSource
{
    /// <summary>Downloads the picture, or says by a category word why it did not.</summary>
    /// <param name="uri">The address; it is checked against the source policy before anything is sent.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    Task<ArtDownload> DownloadAsync(Uri uri, CancellationToken cancellationToken);
}

/// <summary>Spaces picture downloads out, on its own schedule apart from the one the BGG API has.</summary>
public interface IImagePacer : IRequestPacer;

/// <summary>The pacer for picture downloads: a configured gap that is never shorter than <see cref="MinimumGap"/>.</summary>
public sealed class ImagePacer : IImagePacer
{
    /// <summary>The shortest gap ever kept between two picture requests.</summary>
    public static readonly TimeSpan MinimumGap = TimeSpan.FromMilliseconds(500);

    private readonly RequestPacer _inner;

    /// <summary>Creates the pacer.</summary>
    /// <param name="gap">The least time between two requests; raised to <see cref="MinimumGap"/> when it is shorter.</param>
    /// <param name="time">The clock the gap is measured with.</param>
    public ImagePacer(TimeSpan gap, TimeProvider time) => _inner = new RequestPacer(gap, time, MinimumGap);

    /// <inheritdoc />
    public Task<IDisposable> WaitTurnAsync(CancellationToken cancellationToken) => _inner.WaitTurnAsync(cancellationToken);
}

/// <summary>
/// Fetches pictures politely and safely. It sends no credentials, goes only to addresses the policy allows, follows a
/// redirect at most three times and checks every target again, refuses a body that is too big or not a picture, and waits
/// its turn on the picture pacer before each request. A connection that fails or breaks off part way, a body that cannot
/// be read and a redirect target that is not a usable address all end as a failed download, never as an exception. A
/// visitor never reaches it: only the sync calls it.
/// </summary>
public sealed class ImageDownloader : IArtSource
{
    /// <summary>The most redirects followed for one picture.</summary>
    public const int MaxRedirects = 3;

    private const int CopyBufferBytes = 81_920;

    private readonly HttpClient _http;
    private readonly ArtSourcePolicy _policy;
    private readonly ArtLimits _limits;
    private readonly IImagePacer _pacer;
    private readonly string _userAgent;

    /// <summary>Creates the downloader.</summary>
    /// <param name="http">The client; it must not add credentials and must not follow redirects itself.</param>
    /// <param name="policy">Which addresses may be fetched.</param>
    /// <param name="limits">How big a download may be.</param>
    /// <param name="pacer">Spaces the requests out.</param>
    /// <param name="bgg">The BGG options, used only for the User-Agent text.</param>
    public ImageDownloader(HttpClient http, ArtSourcePolicy policy, ArtLimits limits, IImagePacer pacer, BggOptions bgg)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(pacer);
        ArgumentNullException.ThrowIfNull(bgg);

        _http = http;
        _policy = policy;
        _limits = limits;
        _pacer = pacer;
        _userAgent = BggTransport.UserAgent(bgg);
    }

    /// <inheritdoc />
    public async Task<ArtDownload> DownloadAsync(Uri uri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);

        var target = uri;

        for (var followed = 0; ; followed++)
        {
            if (!_policy.Allows(target))
            {
                return ArtDownload.Refused(followed == 0 ? "host" : "redirect");
            }

            var hop = await FetchOnceAsync(target, cancellationToken);

            if (hop.Redirect is not { } next)
            {
                return hop.Result!;
            }

            if (followed >= MaxRedirects)
            {
                return ArtDownload.Refused("redirect");
            }

            target = next;
        }
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private async Task<(ArtDownload? Result, Uri? Redirect)> FetchOnceAsync(Uri target, CancellationToken cancellationToken)
    {
        using var lease = await _pacer.WaitTurnAsync(cancellationToken);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, target);
            request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (IsRedirect(response.StatusCode))
            {
                return response.Headers.Location is { } location && Uri.TryCreate(target, location, out var next)
                    ? (null, next)
                    : (ArtDownload.Failed("status"), null);
            }

            return (await ReadAnswerAsync(response, cancellationToken), null);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException)
        {
            return (ArtDownload.Failed("unavailable"), null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (ArtDownload.Failed("timeout"), null);
        }
    }

    private async Task<ArtDownload> ReadAnswerAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return ArtDownload.Failed("status");
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;

        if (mediaType is not null && !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return ArtDownload.Refused("type");
        }

        if (response.Content.Headers.ContentLength > _limits.MaxBytes)
        {
            return ArtDownload.Refused("size");
        }

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[CopyBufferBytes];

        while (true)
        {
            var read = await body.ReadAsync(chunk, cancellationToken);

            if (read == 0)
            {
                return ArtDownload.Downloaded(buffer.ToArray());
            }

            if (buffer.Length + read > _limits.MaxBytes)
            {
                return ArtDownload.Refused("size");
            }

            buffer.Write(chunk, 0, read);
        }
    }
}

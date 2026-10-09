using System.Net;
using System.Net.Http.Headers;
using Cabinet.FakeBgg.Testing;
using SkiaSharp;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>What one picture request carried, kept so a test can prove that no credentials went with it.</summary>
/// <param name="Uri">The full request address.</param>
/// <param name="AuthorizationScheme">The scheme of the Authorization header, or null when there was none.</param>
/// <param name="AuthorizationParameter">The credentials of the Authorization header, or null when there was none.</param>
/// <param name="UserAgent">The User-Agent header text, or null when there was none.</param>
public sealed record RecordedImageRequest(Uri Uri, string? AuthorizationScheme, string? AuthorizationParameter, string? UserAgent);

/// <summary>
/// A transport for tests that answers picture requests from a dictionary of invented bodies, by absolute address, with
/// not found for every other address, and remembers every request it was given. An address can also be made to send a
/// body that breaks off part way or one that stalls until the request is cancelled.
/// </summary>
public sealed class ScriptedImageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (byte[] Bytes, string ContentType)> _bodies = new(StringComparer.Ordinal);
    private readonly HashSet<string> _broken = new(StringComparer.Ordinal);
    private readonly HashSet<string> _stalled = new(StringComparer.Ordinal);
    private readonly List<RecordedImageRequest> _requests = [];
    private readonly TaskCompletionSource _stallReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _gate = new();

    /// <summary>Runs once for every request, after it is recorded and before it is answered; a test uses it to move the clock mid-run.</summary>
    public Action<Uri>? OnRequest { get; set; }

    /// <summary>Completes once a stalled body has been asked for its first bytes and is holding the download.</summary>
    public Task StallReached => _stallReached.Task;

    /// <summary>Every request received so far, oldest first.</summary>
    public IReadOnlyList<RecordedImageRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Makes an address answer with a body.</summary>
    /// <param name="address">The absolute address.</param>
    /// <param name="bytes">The body.</param>
    /// <param name="contentType">The content type header value.</param>
    public ScriptedImageHandler Serve(string address, byte[] bytes, string contentType = "image/png")
    {
        lock (_gate)
        {
            _bodies[address] = (bytes, contentType);
        }

        return this;
    }

    /// <summary>Makes an address answer with picture headers and the first bytes of a body, and then break off as a dropped connection does.</summary>
    /// <param name="address">The absolute address.</param>
    public ScriptedImageHandler ServeBrokenBody(string address)
    {
        lock (_gate)
        {
            _broken.Add(address);
        }

        return this;
    }

    /// <summary>Makes an address answer with picture headers and then send no body at all until the request is cancelled.</summary>
    /// <param name="address">The absolute address.</param>
    public ScriptedImageHandler ServeStalledBody(string address)
    {
        lock (_gate)
        {
            _stalled.Add(address);
        }

        return this;
    }

    /// <summary>Makes an address answer with a plain solid-colour PNG of the given size.</summary>
    /// <param name="address">The absolute address.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="fill">The colour to fill it with; a fixed reddish colour when null.</param>
    public ScriptedImageHandler ServePicture(string address, int width, int height, SKColor? fill = null) => Serve(address, Png(width, height, fill));

    /// <summary>Draws a solid-colour PNG in code, so no captured picture is ever needed.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="fill">The colour to fill it with; a fixed reddish colour when null.</param>
    public static byte[] Png(int width, int height, SKColor? fill = null)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(fill ?? new SKColor(170, 60, 40));
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("The request has no address.");
        var userAgent = request.Headers.UserAgent.ToString();
        (byte[] Bytes, string ContentType) body;
        bool found;
        bool broken;
        bool stalled;

        lock (_gate)
        {
            _requests.Add(new RecordedImageRequest(
                uri,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                userAgent.Length == 0 ? null : userAgent));
            found = _bodies.TryGetValue(uri.AbsoluteUri, out body);
            broken = _broken.Contains(uri.AbsoluteUri);
            stalled = _stalled.Contains(uri.AbsoluteUri);
        }

        OnRequest?.Invoke(uri);

        if (broken || stalled)
        {
            Stream troubled = broken ? BreakingBodyStream.DroppedConnection() : new StallingBodyStream(() => _stallReached.TrySetResult());

            return Task.FromResult(Answer(request, new StreamContent(troubled), "image/png"));
        }

        if (!found)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
        }

        return Task.FromResult(Answer(request, new ByteArrayContent(body.Bytes), body.ContentType));
    }

    private static HttpResponseMessage Answer(HttpRequestMessage request, HttpContent content, string contentType)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content,
            RequestMessage = request,
        };
        response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        return response;
    }
}

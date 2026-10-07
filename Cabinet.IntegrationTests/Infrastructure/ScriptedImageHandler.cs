using System.Net;
using System.Net.Http.Headers;
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
/// not found for every other address, and remembers every request it was given.
/// </summary>
public sealed class ScriptedImageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (byte[] Bytes, string ContentType)> _bodies = new(StringComparer.Ordinal);
    private readonly List<RecordedImageRequest> _requests = [];
    private readonly object _gate = new();

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

    /// <summary>Makes an address answer with a plain solid-colour PNG of the given size.</summary>
    /// <param name="address">The absolute address.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    public ScriptedImageHandler ServePicture(string address, int width, int height) => Serve(address, Png(width, height));

    /// <summary>Draws a solid-colour PNG in code, so no captured picture is ever needed.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    public static byte[] Png(int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(170, 60, 40));
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

        lock (_gate)
        {
            _requests.Add(new RecordedImageRequest(
                uri,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                userAgent.Length == 0 ? null : userAgent));
            found = _bodies.TryGetValue(uri.AbsoluteUri, out body);
        }

        if (!found)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
        }

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(body.Bytes),
            RequestMessage = request,
        };
        response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(body.ContentType);

        return Task.FromResult(response);
    }
}

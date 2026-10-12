using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>What the owner port answered to one request of an <see cref="OwnerSession"/>.</summary>
/// <param name="Status">The status code.</param>
/// <param name="Body">The whole body as text.</param>
/// <param name="Location">The redirect target exactly as the server wrote it, or null when it did not redirect.</param>
/// <param name="Headers">The response and content headers by name, each as the text the server wrote; null when a test built the reply by hand.</param>
public sealed record OwnerReply(HttpStatusCode Status, string Body, string? Location, IReadOnlyDictionary<string, string>? Headers = null)
{
    /// <summary>The text of a response header, or null when the answer did not carry it.</summary>
    /// <param name="name">The header name.</param>
    public string? Header(string name) => Headers is not null && Headers.TryGetValue(name, out var value) ? value : null;

    /// <summary>Reads the body as JSON.</summary>
    public JsonElement Json => JsonDocument.Parse(Body).RootElement.Clone();

    /// <summary>The text of a JSON member of the body.</summary>
    /// <param name="name">The member name.</param>
    public string JsonText(string name) => Json.GetProperty(name).GetString()!;
}

/// <summary>
/// A browser stand-in for the owner port: it keeps cookies, follows no redirect, reads the antiforgery token from the
/// <c>request-verification-token</c> meta element of a page and posts forms, or forms that ask for a JSON answer, with it.
/// </summary>
public sealed partial class OwnerSession : IDisposable
{
    private const string TokenFieldName = "__RequestVerificationToken";
    private const string TokenHeaderName = "RequestVerificationToken";
    private const string TokenSourcePath = "/owner/settings";

    private readonly HttpClient _client;
    private string? _token;

    /// <summary>Creates a session against the owner listener of a booted host.</summary>
    /// <param name="factory">The booted host.</param>
    public OwnerSession(CabinetWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = true, CookieContainer = new CookieContainer() };
        _client = new HttpClient(handler) { BaseAddress = new Uri($"http://127.0.0.1:{factory.OwnerPort}") };
    }

    /// <summary>Reads a page, and keeps the antiforgery token it carries for the posts that follow.</summary>
    /// <param name="path">The path, with any query.</param>
    public async Task<OwnerReply> GetAsync(string path)
    {
        using var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);
        var reply = await ReadAsync(response);
        var match = TokenMeta().Match(reply.Body);

        if (match.Success)
        {
            _token = WebUtility.HtmlDecode(match.Groups[1].Value);
        }

        return reply;
    }

    /// <summary>Posts a form the way a browser without scripts does: the token travels as a form field.</summary>
    /// <param name="path">The path, with the handler in its query.</param>
    /// <param name="fields">The form fields.</param>
    /// <param name="withToken">Whether to send the token; false posts a form a forged page would send.</param>
    public async Task<OwnerReply> PostFormAsync(string path, IEnumerable<KeyValuePair<string, string>> fields, bool withToken = true)
    {
        var token = withToken ? await TokenAsync() : null;
        var content = fields.Select(field => field);

        if (token is not null)
        {
            content = content.Append(new KeyValuePair<string, string>(TokenFieldName, token));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(content) };

        return await SendAsync(request);
    }

    /// <summary>Posts the fields the way a script does: the token travels in the header and the answer is asked for as JSON.</summary>
    /// <param name="path">The path, with the handler in its query.</param>
    /// <param name="fields">The form fields.</param>
    /// <param name="withToken">Whether to send the token header.</param>
    public async Task<OwnerReply> PostJsonAsync(string path, IEnumerable<KeyValuePair<string, string>> fields, bool withToken = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(fields) };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (withToken)
        {
            request.Headers.Add(TokenHeaderName, await TokenAsync());
        }

        return await SendAsync(request);
    }

    /// <summary>
    /// Posts a file the way a browser does with a multipart form: the file is one part, under a client-side name the server must
    /// never use, and the token travels as a form field, or as a header with a JSON answer asked for when the post is a script's.
    /// </summary>
    /// <param name="path">The path, with the handler in its query.</param>
    /// <param name="fieldName">The name of the file field.</param>
    /// <param name="content">The bytes of the file; null sends the form with no file part at all, as a browser does when none was chosen.</param>
    /// <param name="asScript">Whether to post as a script does: token in the header and a JSON answer asked for.</param>
    /// <param name="withToken">Whether to send the token.</param>
    /// <param name="expectContinue">Whether to ask the server to agree before the body is sent, so a refusal of a huge body is read as the answer it is.</param>
    public async Task<OwnerReply> PostFileAsync(string path, string fieldName, byte[]? content, bool asScript = false, bool withToken = true, bool expectContinue = false)
    {
        var token = withToken ? await TokenAsync() : null;
        using var form = new MultipartFormDataContent();

        if (content is not null)
        {
            var file = new ByteArrayContent(content);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            form.Add(file, fieldName, "..\\..\\client-chosen-name.json");
        }

        if (token is not null && !asScript)
        {
            form.Add(new StringContent(token), TokenFieldName);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = form };
        request.Headers.ExpectContinue = expectContinue;

        if (asScript)
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (token is not null)
            {
                request.Headers.Add(TokenHeaderName, token);
            }
        }

        return await SendAsync(request);
    }

    /// <summary>Reads the value of the first hidden form field of that name on a page; the field names its name before its value.</summary>

    /// <param name="html">The page.</param>
    /// <param name="name">The field name.</param>
    public static string FieldValue(string html, string name)
    {
        var match = Regex.Match(html, "name=\"" + Regex.Escape(name) + "\"[^>]*value=\"([^\"]*)\"");

        return match.Success
            ? WebUtility.HtmlDecode(match.Groups[1].Value)
            : throw new InvalidOperationException($"The page has no field named {name}.");
    }

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();

    private async Task<string> TokenAsync()
    {
        if (_token is null)
        {
            await GetAsync(TokenSourcePath);
        }

        return _token ?? throw new InvalidOperationException("The owner page carried no antiforgery token.");
    }

    private async Task<OwnerReply> SendAsync(HttpRequestMessage request)
    {
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        return await ReadAsync(response);
    }

    private static async Task<OwnerReply> ReadAsync(HttpResponseMessage response) => new(
        response.StatusCode,
        await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
        response.Headers.Location?.OriginalString,
        response.Headers.Concat(response.Content.Headers).ToDictionary(
            header => header.Key,
            header => string.Join(", ", header.Value),
            StringComparer.OrdinalIgnoreCase));

    [GeneratedRegex("""<meta name="request-verification-token" content="([^"]*)" />""")]
    private static partial Regex TokenMeta();
}

using System.Net;
using System.Net.Http.Headers;

namespace Cabinet.Repository.Bgg;

/// <summary>What the BGG client needs to know: where to call, who to call as, and how politely.</summary>
/// <param name="BaseUri">The base address of the XML API; it ends with a slash.</param>
/// <param name="Username">The collection owner's username, or null when not configured.</param>
/// <param name="Token">The API token, or null when not configured; it is only ever sent to the BGG API host.</param>
/// <param name="ContactUrl">An optional address that identifies the operator in the User-Agent.</param>
/// <param name="MinRequestGap">The least time between two requests; anything below the minimum is raised to it.</param>
/// <param name="IncludePrivateInfo">Whether collection requests also ask for private inventory information.</param>
/// <param name="ProductVersion">The running build's version, for the User-Agent.</param>
/// <param name="BaseUriOverrideIgnored">Whether a configured base address was left unused because it is only honoured in development.</param>
public sealed record BggOptions(
    Uri BaseUri,
    string? Username,
    string? Token,
    string? ContactUrl,
    TimeSpan MinRequestGap,
    bool IncludePrivateInfo,
    string ProductVersion,
    bool BaseUriOverrideIgnored = false)
{
    /// <summary>The only host the token may be sent to.</summary>
    public const string ApiHost = "boardgamegeek.com";

    /// <summary>The address of the XML API.</summary>
    public static readonly Uri DefaultBaseUri = new("https://boardgamegeek.com/xmlapi2/");

    /// <summary>The shortest time allowed between two requests to the API.</summary>
    public static readonly TimeSpan MinimumRequestGap = TimeSpan.FromSeconds(5);

    /// <summary>Whether both the username and the token are set, which a sync needs before it may call out.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Token);

    /// <summary>Names the type only, so a log line or an exception message can never carry the token or the username.</summary>
    public override string ToString() => nameof(BggOptions);
}

/// <summary>
/// Attaches the API token to a request only when it is an HTTPS request for the BGG API host. Any other request leaves
/// without credentials, whatever a caller put on it, so the token cannot travel to another host.
/// </summary>
/// <param name="options">The options that hold the token.</param>
public sealed class BggAuthHandler(BggOptions options) : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Headers.Authorization = null;

        if (!string.IsNullOrWhiteSpace(options.Token)
            && request.RequestUri is { Scheme: "https" } uri
            && string.Equals(uri.IdnHost, BggOptions.ApiHost, StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>The pieces of the BGG connection that do not depend on a request.</summary>
public static class BggTransport
{
    /// <summary>
    /// The innermost handler: it never follows a redirect, so a credential cannot be carried to another address, and it
    /// decompresses whatever encoding the answer uses.
    /// </summary>
    public static HttpMessageHandler CreatePrimaryHandler() =>
        new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false,
        };

    /// <summary>The User-Agent text: the product and version, and the operator's contact address when one is configured.</summary>
    /// <param name="options">The options that hold the version and the contact address.</param>
    public static string UserAgent(BggOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var product = $"GamesCabinet/{options.ProductVersion}";

        return string.IsNullOrWhiteSpace(options.ContactUrl) ? product : $"{product} (+{options.ContactUrl})";
    }
}

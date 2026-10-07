namespace Cabinet.Repository.Images;

/// <summary>Reads a picture address from the text the source gave, which is untrusted.</summary>
public static class ArtUrl
{
    /// <summary>The longest address that is kept.</summary>
    public const int MaxLength = 2048;

    private static readonly string ProtocolRelativePrefix = new('/', 2);

    /// <summary>
    /// Turns the text into the one canonical form an address is stored and compared in: the ends are trimmed, a leading
    /// double slash means https, and anything that is not an absolute http or https address of at most
    /// <see cref="MaxLength"/> characters gives null. An http address is kept so the source policy can refuse it by name.
    /// </summary>
    /// <param name="text">The address as the source gave it, or null.</param>
    public static string? Canonical(string? text)
    {
        var trimmed = text?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        var address = trimmed.StartsWith(ProtocolRelativePrefix, StringComparison.Ordinal) ? $"https:{trimmed}" : trimmed;

        if (address.Length > MaxLength
            || !Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || uri.Host.Length == 0)
        {
            return null;
        }

        return address;
    }
}

/// <summary>
/// Decides which addresses a picture may be fetched from. Only https on an exactly named host, on the default port and
/// without user information passes, so nothing the source says can steer a request to another host.
/// </summary>
/// <param name="AllowedHosts">The host names pictures may come from, compared in full and ignoring case.</param>
/// <param name="DevelopmentOrigin">An origin that is allowed exactly as it is spelled, for a local stand-in during development; otherwise null.</param>
public sealed record ArtSourcePolicy(IReadOnlySet<string> AllowedHosts, Uri? DevelopmentOrigin = null)
{
    /// <summary>Tells whether a picture may be fetched from the address.</summary>
    /// <param name="uri">The address, already absolute.</param>
    public bool Allows(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri)
        {
            return false;
        }

        if (DevelopmentOrigin is { } origin
            && string.Equals(uri.Scheme, origin.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.IdnHost, origin.IdnHost, StringComparison.OrdinalIgnoreCase)
            && uri.Port == origin.Port
            && uri.UserInfo.Length == 0)
        {
            return true;
        }

        return uri.Scheme == Uri.UriSchemeHttps
            && uri.IsDefaultPort
            && uri.UserInfo.Length == 0
            && AllowedHosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase);
    }
}

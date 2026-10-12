using System.Globalization;

namespace Cabinet.Service.Language;

/// <summary>
/// One of the two languages the site's own words come in. The visitor's functional cookie decides first, then the browser's
/// language preferences, and English is the fallback, so the page can be written in the right language from the first byte.
/// </summary>
/// <param name="Code">The lowercase language code, <c>en</c> or <c>nl</c>.</param>
/// <param name="Text">The labels of the language.</param>
public sealed record SiteLanguage(string Code, SiteText Text)
{
    /// <summary>The name of the one functional cookie that remembers the visitor's choice.</summary>
    public const string CookieName = "lang";

    private const string EnglishCode = "en";
    private const string DutchCode = "nl";
    private const string DutchCultureName = "nl-NL";
    private const int OctoberMonth = 10;
    private const string DutchOctober = "oktober";

    private static readonly object ItemsKey = new();

    /// <summary>English, the fallback.</summary>
    public static SiteLanguage English { get; } = new(EnglishCode, SiteText.English);

    /// <summary>Dutch.</summary>
    public static SiteLanguage Dutch { get; } = new(DutchCode, SiteText.Dutch);

    /// <summary>Both languages, in the order the toggle lists them.</summary>
    public static IReadOnlyList<SiteLanguage> All { get; } = [English, Dutch];

    /// <summary>
    /// The culture that writes dates and numbers for the language: the invariant culture for English and <c>nl-NL</c> for Dutch.
    /// It is looked up when asked for, so loading the type never fails on a host without culture data.
    /// </summary>
    public CultureInfo Culture => Code == DutchCode
        ? CultureInfo.GetCultureInfo(DutchCultureName)
        : CultureInfo.InvariantCulture;

    /// <summary>Finds the language for a code, ignoring case; only <c>en</c> and <c>nl</c> exist.</summary>
    /// <param name="code">The code to look up.</param>
    /// <param name="language">The language when the code is known, otherwise English.</param>
    public static bool TryGet(string? code, out SiteLanguage language)
    {
        foreach (var candidate in All)
        {
            if (string.Equals(candidate.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                language = candidate;
                return true;
            }
        }

        language = English;
        return false;
    }

    /// <summary>
    /// The language of this request: a valid cookie value wins, otherwise the first browser preference whose primary subtag is
    /// Dutch or English by quality, otherwise English. The answer is kept for the rest of the request so every part of the page
    /// agrees.
    /// </summary>
    /// <param name="context">The request to read.</param>
    public static SiteLanguage Resolve(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Items.TryGetValue(ItemsKey, out var cached) && cached is SiteLanguage known)
        {
            return known;
        }

        var resolved = FromCookie(context.Request) ?? FromAcceptLanguage(context.Request) ?? English;
        context.Items[ItemsKey] = resolved;

        return resolved;
    }

    /// <summary>
    /// Stops the start-up with a clear message when the host cannot write Dutch dates, instead of serving English month names
    /// on the Dutch page.
    /// </summary>
    public static void EnsureAvailable()
    {
        string? october = null;

        try
        {
            october = Dutch.Culture.DateTimeFormat.GetMonthName(OctoberMonth);
        }
        catch (CultureNotFoundException)
        {
        }

        if (!string.Equals(october, DutchOctober, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Dutch dates need the ICU libraries on this host, and invariant globalization must stay off.");
        }
    }

    private static SiteLanguage? FromCookie(HttpRequest request) =>
        request.Cookies.TryGetValue(CookieName, out var value) && TryGet(value, out var language) ? language : null;

    private static SiteLanguage? FromAcceptLanguage(HttpRequest request)
    {
        var preferences = request.GetTypedHeaders().AcceptLanguage;

        foreach (var preference in preferences.OrderByDescending(entry => entry.Quality ?? 1.0))
        {
            if (preference.Quality is { } quality && quality <= 0)
            {
                continue;
            }

            var tag = preference.Value.Value ?? string.Empty;
            var separator = tag.IndexOf('-', StringComparison.Ordinal);
            var primary = separator < 0 ? tag : tag[..separator];

            if (TryGet(primary, out var language))
            {
                return language;
            }
        }

        return null;
    }
}

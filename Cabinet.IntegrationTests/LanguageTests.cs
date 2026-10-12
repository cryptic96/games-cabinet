using System.Net;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Verifies the page is written in the visitor's language from the first byte, that the choice comes from the cookie before
/// the browser's preferences, and that the language never leaks into the data the page reads.
/// </summary>
public class LanguageTests
{
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    [Fact]
    public async Task A_browser_that_prefers_dutch_gets_the_dutch_page()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = PlainClient(factory);

        using var response = await Get(client, "/", acceptLanguage: "nl-NL,nl;q=0.9,en;q=0.8");
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("<html lang=\"nl\">");
        html.Should().Contain("<title>Spellenkast</title>");
        html.Should().Contain("De kast wordt gevuld.");
        html.Should().Contain("Versie ");
        response.Content.Headers.ContentLanguage.Should().ContainSingle().Which.Should().Be("nl");
        VaryOf(response).Should().Contain("Accept-Language").And.Contain("Cookie");
    }

    [Fact]
    public async Task A_request_without_a_language_preference_gets_the_english_page()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = PlainClient(factory);

        using var response = await Get(client, "/", acceptLanguage: null);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().Contain("<html lang=\"en\">");
        html.Should().Contain("Games Cabinet");
        html.Should().Contain("The cabinet is being filled.");
        response.Content.Headers.ContentLanguage.Should().ContainSingle().Which.Should().Be("en");
        VaryOf(response).Should().Contain("Accept-Language").And.Contain("Cookie");
    }

    [Theory]
    [InlineData("de, nl;q=0.5", "nl")]
    [InlineData("fr", "en")]
    [InlineData("nl-BE", "nl")]
    [InlineData("nl;q=0, en;q=0.1", "en")]
    [InlineData("*", "en")]
    public async Task The_browser_preference_picks_the_first_supported_language_by_quality(string header, string expected)
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = PlainClient(factory);

        using var response = await Get(client, "/", acceptLanguage: header);

        response.Content.Headers.ContentLanguage.Should().ContainSingle().Which.Should().Be(expected);
    }

    [Fact]
    public async Task The_layout_data_does_not_change_with_the_language()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = PlainClient(factory);

        using var plain = await Get(client, "/cabinet/layout?profile=desktop", acceptLanguage: null);
        using var dutch = await Get(client, "/cabinet/layout?profile=desktop", acceptLanguage: "nl");

        plain.StatusCode.Should().Be(HttpStatusCode.OK);
        plain.Headers.ETag.Should().NotBeNull();
        dutch.Headers.ETag.Should().Be(plain.Headers.ETag);
    }

    [Theory]
    [InlineData("/language/nl")]
    [InlineData("/language/NL")]
    public async Task Switching_to_dutch_sets_one_lowercase_cookie_and_returns_to_the_plain_page(string path)
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = PlainClient(factory);

        using var response = await Get(client, path, acceptLanguage: "en");

        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location!.OriginalString.Should().Be("/");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var cookie = response.Headers.GetValues("Set-Cookie").Should().ContainSingle().Subject.ToLowerInvariant();
        cookie.Should().StartWith("lang=nl;");
        cookie.Should().Contain("httponly").And.Contain("samesite=lax").And.Contain("path=/").And.Contain("max-age=31536000");
        cookie.Should().NotContain("secure", "the test host answers over plain http");
    }

    [Fact]
    public async Task An_unknown_language_code_answers_not_found_and_sets_nothing()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = PlainClient(factory);

        using var response = await Get(client, "/language/de", acceptLanguage: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [Fact]
    public async Task A_honoured_sample_stays_in_the_redirect_and_anything_else_does_not()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = PlainClient(factory);

        using var known = await Get(client, "/language/en?sample=65", acceptLanguage: null);
        using var hostile = await Get(client, "/language/en?sample=https://evil.example", acceptLanguage: null);
        using var unknown = await Get(client, "/language/en?sample=evil", acceptLanguage: null);

        known.Headers.Location!.OriginalString.Should().Be("/?sample=65");
        hostile.Headers.Location!.OriginalString.Should().Be("/");
        unknown.Headers.Location!.OriginalString.Should().Be("/");
    }

    [Fact]
    public async Task A_sample_is_ignored_in_the_redirect_while_the_prototype_is_off()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = PlainClient(factory);

        using var response = await Get(client, "/language/en?sample=65", acceptLanguage: null);

        response.Headers.Location!.OriginalString.Should().Be("/");
    }

    [Fact]
    public async Task The_cookie_beats_the_browser_preference()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = PlainClient(factory);

        using var response = await Get(client, "/", acceptLanguage: "en", cookie: "lang=nl");
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().Contain("<html lang=\"nl\">");
        html.Should().Contain("De kast wordt gevuld.");
    }

    [Theory]
    [InlineData("lang=xx")]
    [InlineData("lang=")]
    public async Task An_unknown_or_empty_cookie_lets_the_browser_preference_decide(string cookie)
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = PlainClient(factory);

        using var response = await Get(client, "/", acceptLanguage: "nl", cookie: cookie);

        response.Content.Headers.ContentLanguage.Should().ContainSingle().Which.Should().Be("nl");
    }

    [Fact]
    public async Task The_dutch_page_marks_nl_as_current_and_both_items_point_at_the_language_route()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = PlainClient(factory);

        using var response = await Get(client, "/", acceptLanguage: "nl");
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().Contain("<nav class=\"lang-toggle\" aria-label=\"Taal\">");
        html.Should().Contain("<a href=\"/language/en\" lang=\"en\" hreflang=\"en\" aria-label=\"EN, English\">EN</a>");
        html.Should().Contain("<a href=\"/language/nl\" lang=\"nl\" hreflang=\"nl\" aria-label=\"NL, Nederlands\" aria-current=\"true\">NL</a>");
        html.Should().NotContain("&#xB7;").And.NotContain("\u00B7");
    }

    [Fact]
    public async Task The_english_page_labels_the_toggle_language_and_marks_en_as_current()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = PlainClient(factory);

        using var response = await Get(client, "/", acceptLanguage: null);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().Contain("<nav class=\"lang-toggle\" aria-label=\"Language\">");
        html.Should().Contain("aria-label=\"EN, English\" aria-current=\"true\">EN</a>");
    }

    [Fact]
    public async Task The_toggle_keeps_the_sample_while_one_is_shown()
    {
        await using var factory = new CabinetWebApplicationFactory(PrototypeOn);
        using var client = PlainClient(factory);

        using var response = await Get(client, "/?sample=65", acceptLanguage: null);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().Contain("href=\"/language/nl?sample=65\"");
    }

    [Fact]
    public async Task The_page_itself_never_sets_a_cookie()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = PlainClient(factory);

        using var response = await Get(client, "/", acceptLanguage: "nl");

        response.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    internal static HttpClient PlainClient(CabinetWebApplicationFactory factory) =>
        new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        {
            BaseAddress = new Uri($"http://127.0.0.1:{factory.PublicPort}"),
        };

    internal static Task<HttpResponseMessage> Get(HttpClient client, string path, string? acceptLanguage, string? cookie = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (acceptLanguage is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        }

        if (cookie is not null)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string VaryOf(HttpResponseMessage response) =>
        string.Join(", ", response.Headers.Vary);
}

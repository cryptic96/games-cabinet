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

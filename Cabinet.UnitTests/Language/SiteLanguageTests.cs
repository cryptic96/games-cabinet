using Cabinet.Service.Language;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Cabinet.UnitTests.Language;

/// <summary>Verifies which language a request gets: the cookie first, then the browser's preferences by quality, then English.</summary>
public class SiteLanguageTests
{
    [Theory]
    [InlineData("nl", "en", "nl")]
    [InlineData("EN", "nl", "en")]
    [InlineData("NL", null, "nl")]
    [InlineData("xx", "nl", "nl")]
    [InlineData("", "nl", "nl")]
    [InlineData(null, "nl", "nl")]
    [InlineData("nl-BE", "en", "en")]
    public void The_cookie_decides_first_and_only_when_it_holds_a_known_code(string? cookie, string? header, string expected)
    {
        var context = ContextWith(cookie, header);

        SiteLanguage.Resolve(context).Code.Should().Be(expected);
    }

    [Theory]
    [InlineData("nl-BE", "nl")]
    [InlineData("NL-be", "nl")]
    [InlineData("en-GB,nl;q=0.9", "en")]
    [InlineData("de-DE,de;q=0.9,nl;q=0.4", "nl")]
    [InlineData("nl;q=0, en;q=0.1", "en")]
    [InlineData("nl;q=0.2, en;q=0.8", "en")]
    [InlineData("*", "en")]
    [InlineData("", "en")]
    [InlineData("fr", "en")]
    [InlineData("nl_NL", "en")]
    [InlineData(";;;,,,q=", "en")]
    [InlineData(null, "en")]
    public void The_browser_preference_picks_the_first_supported_language_by_quality(string? header, string expected)
    {
        var context = ContextWith(cookie: null, header);

        SiteLanguage.Resolve(context).Code.Should().Be(expected);
    }

    [Fact]
    public void Resolving_twice_in_one_request_returns_the_same_instance()
    {
        var context = ContextWith(cookie: null, "nl");

        var first = SiteLanguage.Resolve(context);
        context.Request.Headers.AcceptLanguage = "en";
        var second = SiteLanguage.Resolve(context);

        second.Should().BeSameAs(first);
    }

    [Theory]
    [InlineData("en", true)]
    [InlineData("NL", true)]
    [InlineData("de", false)]
    [InlineData("nl-BE", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_the_two_exact_codes_are_known(string? code, bool known)
    {
        SiteLanguage.TryGet(code, out _).Should().Be(known);
    }

    [Fact]
    public void The_cookie_name_is_the_short_plain_one()
    {
        SiteLanguage.CookieName.Should().Be("lang");
    }

    private static DefaultHttpContext ContextWith(string? cookie, string? acceptLanguage)
    {
        var context = new DefaultHttpContext();

        if (cookie is not null)
        {
            context.Request.Headers.Cookie = $"{SiteLanguage.CookieName}={cookie}";
        }

        if (acceptLanguage is not null)
        {
            context.Request.Headers.AcceptLanguage = acceptLanguage;
        }

        return context;
    }
}

using System.Net;
using System.Text.RegularExpressions;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests;

/// <summary>Verifies every HTML page carries the linked BoardGameGeek credit through the shared layout.</summary>
[Trait("Category", "Credit")]
public partial class CreditTests
{
    [Fact]
    public async Task Every_razor_page_renders_the_linked_credit()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();
        var routes = PageRoutes(factory);

        routes.Should().NotBeEmpty("the app serves at least the cabinet page");

        foreach (var route in routes)
        {
            using var response = await client.GetAsync(route, TestContext.Current.CancellationToken);
            var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.OK, $"page {route} is served");
            Credit().IsMatch(html).Should().BeTrue($"page {route} renders the credit link with its logo");
        }
    }

    [Fact]
    public async Task Footer_lists_the_version_before_the_credit()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var footer = html[html.IndexOf("<footer>", StringComparison.Ordinal)..];

        footer.IndexOf("class=\"version\"", StringComparison.Ordinal).Should().BeGreaterThan(-1);
        footer.IndexOf("class=\"version\"", StringComparison.Ordinal)
            .Should().BeLessThan(footer.IndexOf("class=\"bgg-credit\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Header_does_not_name_the_data_source()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var header = html[..html.IndexOf("</header>", StringComparison.Ordinal)];

        header.Should().NotContain("BGG");
        header.Should().NotContain("BoardGameGeek");
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public async Task Credit_is_present_whether_the_prototype_is_on_or_off(string enabled)
    {
        await using var factory = new CabinetWebApplicationFactory(new Dictionary<string, string?> { ["Prototype:Enabled"] = enabled });
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        Credit().IsMatch(html).Should().BeTrue();
    }

    [Fact]
    public async Task The_logo_is_served_from_the_own_origin_as_an_image()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var logoUrl = LogoSource().Match(html).Groups["src"].Value;

        logoUrl.Should().StartWith("/img/powered-by-bgg.");
        using var response = await client.GetAsync(logoUrl, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.ShouldHaveMediaTypeStartingWith("image/");
        body.Should().NotBeEmpty();
    }

    [Fact]
    public async Task The_logo_reserves_its_space_with_intrinsic_dimensions()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        LogoSource().Match(html).Value.Should().MatchRegex("width=\"[0-9]+\" height=\"[0-9]+\"");
    }

    [Fact]
    public async Task The_page_references_no_origin_other_than_its_own_apart_from_the_credit_link()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var references = Reference().Matches(html).Select(match => match.Groups["url"].Value)
            .Where(url => url != "https://boardgamegeek.com")
            .ToList();

        references.Should().NotBeEmpty();
        references.Should().OnlyContain(url => Regex.IsMatch(url, "^/(?!/)"));
    }

    private static IReadOnlyList<string> PageRoutes(CabinetWebApplicationFactory factory) =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<PageActionDescriptor>() is not null)
            .Select(endpoint => RouteOf(endpoint.RoutePattern))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static string RouteOf(RoutePattern pattern)
    {
        pattern.Parameters.Should().BeEmpty($"page route {pattern.RawText} must be requestable without values");
        return "/" + pattern.RawText?.TrimStart('/');
    }

    [GeneratedRegex("<a class=\"bgg-credit\" href=\"https://boardgamegeek\\.com\" rel=\"noopener\">\\s*<img [^>]*alt=\"Powered by BGG\"")]
    private static partial Regex Credit();

    [GeneratedRegex("<img [^>]*?src=\"(?<src>[^\"?]+)[^\"]*\"[^>]*alt=\"Powered by BGG\"[^>]*>")]
    private static partial Regex LogoSource();

    [GeneratedRegex("\\s(?:src|href)=\"(?<url>[^\"]*)\"")]
    private static partial Regex Reference();
}

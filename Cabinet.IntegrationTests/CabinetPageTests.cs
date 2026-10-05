using System.Net;
using System.Text.RegularExpressions;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>Verifies the page mounts the cabinet, links served fingerprinted assets and stays free of inline styles and scripts.</summary>
public partial class CabinetPageTests
{
    [Fact]
    public async Task Page_has_the_cabinet_mount()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        html.Should().Contain("id=\"cabinet\"");
    }

    [Fact]
    public async Task Page_links_a_fingerprinted_cabinet_stylesheet_that_is_served()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var hrefs = StylesheetLink().Matches(html).Select(match => match.Groups["href"].Value).ToList();
        var cabinetHref = hrefs.SingleOrDefault(href => href.StartsWith("/css/cabinet.", StringComparison.Ordinal));

        cabinetHref.Should().NotBeNull("the page links the cabinet stylesheet");
        cabinetHref.Should().MatchRegex(@"^/css/cabinet\..+\.css$");
        hrefs[0].Should().MatchRegex(@"^/css/site\..+\.css$", "the shared stylesheet stays first");

        using var stylesheet = await client.GetAsync(cabinetHref, TestContext.Current.CancellationToken);

        stylesheet.StatusCode.Should().Be(HttpStatusCode.OK);
        stylesheet.Content.Headers.ContentType?.MediaType.Should().Be("text/css");
    }

    [Fact]
    public async Task Page_links_a_fingerprinted_module_script_that_is_served()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var match = ModuleScript().Match(html);

        match.Success.Should().BeTrue("the page loads the cabinet as a module script");
        match.Groups["src"].Value.Should().MatchRegex(@"^/js/cabinet\..+\.js$");

        using var script = await client.GetAsync(match.Groups["src"].Value, TestContext.Current.CancellationToken);

        script.StatusCode.Should().Be(HttpStatusCode.OK);
        script.Content.Headers.ContentType?.MediaType.Should().Be("text/javascript");
    }

    [Theory]
    [InlineData("/js/render.js")]
    [InlineData("/js/copy.js")]
    public async Task Modules_imported_by_relative_path_are_served_at_their_original_paths(string path)
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/javascript");
    }

    [Fact]
    public async Task Page_carries_no_inline_style_attributes_and_no_inline_script_bodies()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        StyleAttribute().IsMatch(html).Should().BeFalse("inline styles are blocked by a strict content security policy");
        InlineScriptBody().IsMatch(html).Should().BeFalse("inline scripts are blocked by a strict content security policy");
    }

    [GeneratedRegex("<link[^>]*rel=\"stylesheet\"[^>]*href=\"(?<href>[^\"]+)\"")]
    private static partial Regex StylesheetLink();

    [GeneratedRegex("<script[^>]*type=\"module\"[^>]*src=\"(?<src>[^\"]+)\"")]
    private static partial Regex ModuleScript();

    [GeneratedRegex("<[a-zA-Z][^>]*\\sstyle\\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex StyleAttribute();

    [GeneratedRegex("<script(?![^>]*\\ssrc=)[^>]*>\\s*\\S", RegexOptions.IgnoreCase)]
    private static partial Regex InlineScriptBody();
}
